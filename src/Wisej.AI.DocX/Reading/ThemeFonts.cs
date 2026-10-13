///////////////////////////////////////////////////////////////////////////////
//
// (C) 2026 ICE TEA GROUP LLC - ALL RIGHTS RESERVED
//
//
//
// ALL INFORMATION CONTAINED HEREIN IS, AND REMAINS
// THE PROPERTY OF ICE TEA GROUP LLC AND ITS SUPPLIERS, IF ANY.
// THE INTELLECTUAL PROPERTY AND TECHNICAL CONCEPTS CONTAINED
// HEREIN ARE PROPRIETARY TO ICE TEA GROUP LLC AND ITS SUPPLIERS
// AND MAY BE COVERED BY U.S. AND FOREIGN PATENTS, PATENT IN PROCESS, AND
// ARE PROTECTED BY TRADE SECRET OR COPYRIGHT LAW.
//
// DISSEMINATION OF THIS INFORMATION OR REPRODUCTION OF THIS MATERIAL
// IS STRICTLY FORBIDDEN UNLESS PRIOR WRITTEN PERMISSION IS OBTAINED
// FROM ICE TEA GROUP LLC.
//
///////////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Xml.Linq;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// The major (headings) and minor (body) fonts of the document theme.
	/// </summary>
	/// <remarks>
	/// Runs refer to theme fonts with attributes such as <c>w:asciiTheme="minorHAnsi"</c>;
	/// this class resolves those references to actual font family names.
	/// </remarks>
	internal sealed class ThemeFonts
	{
		private string _majorLatin = "Calibri Light";
		private string _minorLatin = "Calibri";
		private string _majorEastAsia;
		private string _minorEastAsia;

		/// <summary>
		/// Loads the font scheme from the theme part.
		/// </summary>
		/// <param name="theme">The root element of the theme part (<c>a:theme</c>). Can be <c>null</c>.</param>
		/// <param name="settings">The root element of the settings part, used for the East Asian theme language. Can be <c>null</c>.</param>
		/// <returns>The theme fonts; Office defaults are used for anything missing.</returns>
		public static ThemeFonts Load(XElement theme, XElement settings)
		{
			var fonts = new ThemeFonts();

			var scheme = theme?.Element(Ns.A + "themeElements")?.Element(Ns.A + "fontScheme");
			if (scheme == null)
				return fonts;

			var script = GetEastAsianScript(settings?.Element(Ns.W + "themeFontLang")?.WAttr("eastAsia"));

			var major = scheme.Element(Ns.A + "majorFont");
			var minor = scheme.Element(Ns.A + "minorFont");

			fonts._majorLatin = Typeface(major?.Element(Ns.A + "latin")) ?? fonts._majorLatin;
			fonts._minorLatin = Typeface(minor?.Element(Ns.A + "latin")) ?? fonts._minorLatin;
			fonts._majorEastAsia = Typeface(major?.Element(Ns.A + "ea")) ?? ScriptTypeface(major, script);
			fonts._minorEastAsia = Typeface(minor?.Element(Ns.A + "ea")) ?? ScriptTypeface(minor, script);

			return fonts;
		}

		/// <summary>
		/// Resolves a theme font reference (<c>majorHAnsi</c>, <c>minorEastAsia</c>, ...) to a font family name.
		/// </summary>
		/// <param name="themeFont">The value of a <c>w:*Theme</c> attribute.</param>
		/// <returns>The font family name, or <c>null</c> when it cannot be resolved.</returns>
		public string Resolve(string themeFont)
		{
			if (String.IsNullOrEmpty(themeFont))
				return null;

			var major = themeFont.StartsWith("major", StringComparison.OrdinalIgnoreCase);
			var eastAsia = themeFont.EndsWith("EastAsia", StringComparison.OrdinalIgnoreCase);

			if (eastAsia)
				return major ? _majorEastAsia : _minorEastAsia;

			return major ? _majorLatin : _minorLatin;
		}

		/// <summary>
		/// Returns the non-empty <c>typeface</c> attribute of a theme font element.
		/// </summary>
		/// <param name="font">The <c>a:latin</c>, <c>a:ea</c> or <c>a:font</c> element. Can be <c>null</c>.</param>
		/// <returns>The typeface, or <c>null</c> when missing or empty.</returns>
		private static string Typeface(XElement font)
		{
			var typeface = font?.Attribute("typeface")?.Value;
			return String.IsNullOrWhiteSpace(typeface) ? null : typeface;
		}

		/// <summary>
		/// Returns the typeface the theme assigns to an East Asian script.
		/// </summary>
		/// <param name="font">The <c>a:majorFont</c> or <c>a:minorFont</c> element. Can be <c>null</c>.</param>
		/// <param name="script">The script code (<c>Jpan</c>, <c>Hans</c>, <c>Hant</c>, <c>Hang</c>). Can be <c>null</c>.</param>
		/// <returns>The typeface, or <c>null</c>.</returns>
		private static string ScriptTypeface(XElement font, string script)
		{
			if (font == null || script == null)
				return null;

			return Typeface(font.Elements(Ns.A + "font").FirstOrDefault(f => (string)f.Attribute("script") == script));
		}

		/// <summary>
		/// Maps the East Asian theme language to the script code used by the theme font scheme.
		/// </summary>
		/// <param name="language">A language tag such as <c>ja-JP</c> or <c>zh-CN</c>. Can be <c>null</c>.</param>
		/// <returns>The script code, or <c>null</c>.</returns>
		private static string GetEastAsianScript(string language)
		{
			if (String.IsNullOrEmpty(language))
				return null;

			language = language.ToLowerInvariant();

			if (language.StartsWith("ja"))
				return "Jpan";
			if (language.StartsWith("ko"))
				return "Hang";
			if (language == "zh-tw" || language == "zh-hk" || language == "zh-mo")
				return "Hant";
			if (language.StartsWith("zh"))
				return "Hans";

			return null;
		}
	}
}

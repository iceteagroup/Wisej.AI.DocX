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
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Accumulates run properties (<c>w:rPr</c>) across the layers of the style cascade.
	/// </summary>
	/// <remarks>
	/// Every property is nullable: a layer only overrides the properties it specifies.
	/// Toggle properties (bold, italic, ...) are resolved by override rather than by the
	/// exclusive-or rule of the specification: it matches what users see in practice and
	/// keeps the result predictable.
	/// </remarks>
	internal sealed class RunPropertiesBuilder
	{
		private bool? _bold;
		private bool? _italic;
		private bool? _underline;
		private bool? _strike;
		private bool? _hidden;
		private bool? _caps;
		private bool? _smallCaps;
		private string _verticalAlign;
		private string _latinFont;
		private string _latinThemeFont;
		private string _eastAsiaFont;
		private string _eastAsiaThemeFont;
		private double? _size;

		/// <summary>
		/// Gets or sets the identifier of the character style specified by the layers, or <c>null</c>.
		/// </summary>
		public string CharacterStyleId { get; set; }

		/// <summary>
		/// Creates a copy of the accumulated properties.
		/// </summary>
		/// <returns>A new builder with the same values.</returns>
		public RunPropertiesBuilder Clone()
		{
			return (RunPropertiesBuilder)MemberwiseClone();
		}

		/// <summary>
		/// Applies the properties specified by a <c>w:rPr</c> element over the accumulated ones.
		/// </summary>
		/// <param name="rPr">The run properties element. Can be <c>null</c>.</param>
		public void Apply(XElement rPr)
		{
			if (rPr == null)
				return;

			foreach (var e in rPr.Elements())
			{
				if (e.Name.Namespace != Ns.W)
					continue;

				switch (e.Name.LocalName)
				{
					case "b":
						_bold = e.OnOff();
						break;

					case "i":
						_italic = e.OnOff();
						break;

					case "u":
						_underline = e.WVal() != "none";
						break;

					case "strike":
					case "dstrike":
						_strike = e.OnOff();
						break;

					case "vanish":
						_hidden = e.OnOff();
						break;

					case "caps":
						_caps = e.OnOff();
						break;

					case "smallCaps":
						_smallCaps = e.OnOff();
						break;

					case "vertAlign":
						_verticalAlign = e.WVal();
						break;

					case "sz":
						_size = XmlExtensions.ParseDouble(e.WVal()) / 2.0 ?? _size;
						break;

					case "rFonts":
						ApplyFonts(e);
						break;

					case "rStyle":
						CharacterStyleId = e.WVal();
						break;
				}
			}
		}

		/// <summary>
		/// Produces the final run style.
		/// </summary>
		/// <param name="theme">The theme fonts used to resolve theme font references.</param>
		/// <returns>A new <see cref="RunStyle"/> with the accumulated values or their defaults.</returns>
		public RunStyle Build(ThemeFonts theme)
		{
			var latin = (_latinThemeFont != null ? theme.Resolve(_latinThemeFont) : null) ?? _latinFont ?? "Calibri";
			var eastAsia = (_eastAsiaThemeFont != null ? theme.Resolve(_eastAsiaThemeFont) : null) ?? _eastAsiaFont;

			return new RunStyle
			{
				Bold = _bold ?? false,
				Italic = _italic ?? false,
				Underline = _underline ?? false,
				Strike = _strike ?? false,
				Hidden = _hidden ?? false,
				Caps = _caps ?? false,
				SmallCaps = _smallCaps ?? false,
				Superscript = _verticalAlign == "superscript",
				Subscript = _verticalAlign == "subscript",
				FontFamily = latin,
				EastAsiaFontFamily = eastAsia,
				FontSize = _size ?? 10,
				Monospace = FontNames.IsMonospace(latin),
			};
		}

		/// <summary>
		/// Applies <c>w:rFonts</c>. A theme reference overrides an explicit font at the same level.
		/// </summary>
		/// <param name="e">The fonts element.</param>
		private void ApplyFonts(XElement e)
		{
			var asciiTheme = e.WAttr("asciiTheme") ?? e.WAttr("hAnsiTheme");
			var ascii = e.WAttr("ascii") ?? e.WAttr("hAnsi");

			if (asciiTheme != null)
			{
				_latinThemeFont = asciiTheme;
				_latinFont = ascii ?? _latinFont;
			}
			else if (ascii != null)
			{
				_latinThemeFont = null;
				_latinFont = ascii;
			}

			var eastAsiaTheme = e.WAttr("eastAsiaTheme");
			var eastAsia = e.WAttr("eastAsia");

			if (eastAsiaTheme != null)
			{
				_eastAsiaThemeFont = eastAsiaTheme;
			}
			else if (!String.IsNullOrEmpty(eastAsia))
			{
				_eastAsiaThemeFont = null;
				_eastAsiaFont = eastAsia;
			}
		}
	}
}

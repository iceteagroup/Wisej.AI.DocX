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
using System.Collections.Generic;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// The metrics of a font face needed to lay out text: line height and character advance widths.
	/// </summary>
	/// <remarks>
	/// All the values are expressed in em units (fractions of the font size), so they are
	/// multiplied by the font size in points to obtain distances in points.
	/// </remarks>
	internal sealed class FontMetrics
	{
		/// <summary>
		/// The characters stored in the built-in metric tables, in table order.
		/// </summary>
		public static readonly string TableCharacters = BuildTableCharacters();

		private readonly Dictionary<int, double> _widths;
		private readonly double _defaultWidth;

		/// <summary>
		/// Initializes a new instance of the <see cref="FontMetrics"/> class.
		/// </summary>
		/// <param name="lineHeight">The single line height, in em.</param>
		/// <param name="ascent">The ascent above the baseline, in em.</param>
		/// <param name="widths">The advance width of every mapped code point, in em.</param>
		public FontMetrics(double lineHeight, double ascent, Dictionary<int, double> widths)
		{
			LineHeight = lineHeight;
			Ascent = ascent;
			_widths = widths;

			// Unmapped characters (missing glyphs replaced by a fallback font) use the width of a lower case letter.
			_defaultWidth = widths.TryGetValue('n', out var n) && n > 0 ? n : 0.5;
		}

		/// <summary>
		/// Gets the height of a single spaced line, in em.
		/// </summary>
		public double LineHeight { get; }

		/// <summary>
		/// Gets the ascent above the baseline, in em.
		/// </summary>
		public double Ascent { get; }

		/// <summary>
		/// Returns the advance width of a character, in em.
		/// </summary>
		/// <param name="c">The character.</param>
		/// <returns>The advance width.</returns>
		public double GetAdvance(char c)
		{
			if (_widths.TryGetValue(c, out var width))
				return width;

			return IsWide(c) ? 1.0 : _defaultWidth;
		}

		/// <summary>
		/// Creates the metrics of a built-in table.
		/// </summary>
		/// <param name="lineHeight">The single line height, in em.</param>
		/// <param name="ascent">The ascent, in em.</param>
		/// <param name="encodedWidths">The widths of <see cref="TableCharacters"/> in thousandths of em, as big endian 16-bit values encoded in Base64.</param>
		/// <returns>The metrics.</returns>
		public static FontMetrics FromTable(double lineHeight, double ascent, string encodedWidths)
		{
			var bytes = Convert.FromBase64String(encodedWidths);
			var widths = new Dictionary<int, double>(TableCharacters.Length);

			for (var i = 0; i < TableCharacters.Length && i * 2 + 1 < bytes.Length; i++)
				widths[TableCharacters[i]] = ((bytes[i * 2] << 8) | bytes[i * 2 + 1]) / 1000.0;

			return new FontMetrics(lineHeight, ascent, widths);
		}

		/// <summary>
		/// Determines whether a character is a full width (East Asian) character.
		/// </summary>
		/// <param name="c">The character.</param>
		/// <returns><c>true</c> for ideographs, kana, hangul and full width forms.</returns>
		public static bool IsWide(char c)
		{
			return (c >= 'ᄀ' && c <= 'ᅟ')
				|| (c >= '⺀' && c <= '꓏')
				|| (c >= '가' && c <= '힣')
				|| (c >= '豈' && c <= '﫿')
				|| (c >= '︰' && c <= '﹏')
				|| (c >= '＀' && c <= '｠')
				|| (c >= '￠' && c <= '￦');
		}

		/// <summary>
		/// Builds the list of characters covered by the built-in tables: printable ASCII, Latin-1
		/// and the most common typographic punctuation.
		/// </summary>
		/// <returns>The characters.</returns>
		private static string BuildTableCharacters()
		{
			var chars = new List<char>();

			for (var c = ' '; c <= '~'; c++)
				chars.Add(c);

			for (var c = ' '; c <= 'ÿ'; c++)
				chars.Add(c);

			chars.AddRange("‘’“”–—•…€™");
			return new string(chars.ToArray());
		}
	}
}

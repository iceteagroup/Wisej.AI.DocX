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
using System.IO;
using System.Text;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Reads the metrics needed for text layout from TrueType and OpenType font files.
	/// </summary>
	/// <remarks>
	/// Only the tables that describe the vertical metrics and the advance widths are read:
	/// <c>head</c>, <c>hhea</c>, <c>OS/2</c>, <c>hmtx</c>, <c>cmap</c> and <c>name</c>.
	/// Font collections (<c>.ttc</c>) are supported.
	/// </remarks>
	internal static class TrueTypeReader
	{
		/// <summary>
		/// Reads the family names of every font contained in a file.
		/// </summary>
		/// <param name="path">The path of a <c>.ttf</c>, <c>.otf</c> or <c>.ttc</c> file.</param>
		/// <returns>The family name, bold and italic flags of each font; empty when the file cannot be read.</returns>
		public static List<(string Family, bool Bold, bool Italic, int Index)> ReadFamilies(string path)
		{
			var result = new List<(string, bool, bool, int)>();

			try
			{
				var data = File.ReadAllBytes(path);
				var offsets = GetFontOffsets(data);

				for (var i = 0; i < offsets.Count; i++)
				{
					var tables = ReadTableDirectory(data, offsets[i]);
					var family = ReadFamilyName(data, tables);
					if (family == null)
						continue;

					var style = ReadStyle(data, tables);
					result.Add((family, style.Bold, style.Italic, i));
				}
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is IndexOutOfRangeException || ex is ArgumentException)
			{
				// Unreadable or malformed font files are skipped.
			}

			return result;
		}

		/// <summary>
		/// Reads the layout metrics of a font.
		/// </summary>
		/// <param name="path">The path of the font file.</param>
		/// <param name="index">The index of the font in a collection, 0 for single font files.</param>
		/// <returns>The metrics, or <c>null</c> when the font cannot be read.</returns>
		public static FontMetrics ReadMetrics(string path, int index)
		{
			try
			{
				var data = File.ReadAllBytes(path);
				var offsets = GetFontOffsets(data);
				if (index < 0 || index >= offsets.Count)
					return null;

				return ReadMetrics(data, ReadTableDirectory(data, offsets[index]));
			}
			catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is IndexOutOfRangeException || ex is ArgumentException)
			{
				return null;
			}
		}

		#region Implementation

		/// <summary>
		/// Reads the metrics of a font from its tables.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="tables">The table directory of the font.</param>
		/// <returns>The metrics, or <c>null</c> when required tables are missing.</returns>
		private static FontMetrics ReadMetrics(byte[] data, Dictionary<string, int> tables)
		{
			if (!tables.TryGetValue("head", out var head) || !tables.TryGetValue("hhea", out var hhea)
				|| !tables.TryGetValue("hmtx", out var hmtx) || !tables.TryGetValue("cmap", out var cmap))
			{
				return null;
			}

			double unitsPerEm = U16(data, head + 18);
			if (unitsPerEm <= 0)
				return null;

			var hheaAscender = S16(data, hhea + 4);
			var hheaDescender = S16(data, hhea + 6);
			var hheaLineGap = S16(data, hhea + 8);
			var numberOfHMetrics = U16(data, hhea + 34);

			// Word computes single line spacing like GDI: the Windows ascent and descent plus the
			// part of the typographic line gap not already included in them.
			double ascent = hheaAscender;
			double height = hheaAscender - hheaDescender + hheaLineGap;

			if (tables.TryGetValue("OS/2", out var os2))
			{
				var winAscent = U16(data, os2 + 74);
				var winDescent = U16(data, os2 + 76);
				var external = Math.Max(0, hheaLineGap - ((winAscent + winDescent) - (hheaAscender - hheaDescender)));

				ascent = winAscent;
				height = winAscent + winDescent + external;
			}

			var glyphs = ReadCharacterMap(data, cmap);
			var widths = new Dictionary<int, double>(glyphs.Count);

			foreach (var pair in glyphs)
			{
				var glyph = pair.Value;
				var metric = glyph < numberOfHMetrics ? glyph : numberOfHMetrics - 1;
				widths[pair.Key] = U16(data, hmtx + metric * 4) / unitsPerEm;
			}

			return new FontMetrics(height / unitsPerEm, ascent / unitsPerEm, widths);
		}

		/// <summary>
		/// Returns the offsets of the fonts in a file: one for a single font, several for a collection.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <returns>The offsets of the table directories.</returns>
		private static List<int> GetFontOffsets(byte[] data)
		{
			var offsets = new List<int>();

			if (data.Length >= 12 && data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f')
			{
				var count = (int)U32(data, 8);
				for (var i = 0; i < count && 12 + i * 4 + 4 <= data.Length; i++)
					offsets.Add((int)U32(data, 12 + i * 4));
			}
			else
			{
				offsets.Add(0);
			}

			return offsets;
		}

		/// <summary>
		/// Reads the table directory of a font.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="offset">The offset of the table directory.</param>
		/// <returns>The offset of every table by tag.</returns>
		private static Dictionary<string, int> ReadTableDirectory(byte[] data, int offset)
		{
			var tables = new Dictionary<string, int>(StringComparer.Ordinal);
			var count = U16(data, offset + 4);

			for (var i = 0; i < count; i++)
			{
				var record = offset + 12 + i * 16;
				var tag = Encoding.ASCII.GetString(data, record, 4);
				tables[tag] = (int)U32(data, record + 8);
			}

			return tables;
		}

		/// <summary>
		/// Reads the Unicode character to glyph mapping (<c>cmap</c> formats 4 and 12).
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="cmap">The offset of the <c>cmap</c> table.</param>
		/// <returns>The glyph index of every mapped code point.</returns>
		private static Dictionary<int, int> ReadCharacterMap(byte[] data, int cmap)
		{
			var result = new Dictionary<int, int>();
			var count = U16(data, cmap + 2);

			int best = -1;
			var bestScore = -1;

			for (var i = 0; i < count; i++)
			{
				var record = cmap + 4 + i * 8;
				var platform = U16(data, record);
				var encoding = U16(data, record + 2);
				var subtable = cmap + (int)U32(data, record + 4);
				var format = U16(data, subtable);

				// Prefer the full Unicode table, then the BMP table, then the symbol table.
				var score = format == 12 && (platform == 3 || platform == 0) ? 3
					: format == 4 && (platform == 3 && encoding == 1 || platform == 0) ? 2
					: format == 4 && platform == 3 && encoding == 0 ? 1
					: -1;

				if (score > bestScore)
				{
					best = subtable;
					bestScore = score;
				}
			}

			if (best < 0)
				return result;

			if (U16(data, best) == 12)
				ReadFormat12(data, best, result);
			else
				ReadFormat4(data, best, result, symbol: bestScore == 1);

			return result;
		}

		/// <summary>
		/// Reads a format 4 (segment mapping to delta values) character map.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="table">The offset of the subtable.</param>
		/// <param name="result">The mapping to fill.</param>
		/// <param name="symbol">Whether the table is a symbol table (characters in U+F000 to U+F0FF).</param>
		private static void ReadFormat4(byte[] data, int table, Dictionary<int, int> result, bool symbol)
		{
			var segments = U16(data, table + 6) / 2;
			var endCodes = table + 14;
			var startCodes = endCodes + segments * 2 + 2;
			var deltas = startCodes + segments * 2;
			var rangeOffsets = deltas + segments * 2;

			for (var s = 0; s < segments; s++)
			{
				var end = U16(data, endCodes + s * 2);
				var start = U16(data, startCodes + s * 2);
				var delta = S16(data, deltas + s * 2);
				var rangeOffset = U16(data, rangeOffsets + s * 2);

				for (var c = start; c <= end && c != 0xFFFF; c++)
				{
					int glyph;
					if (rangeOffset == 0)
					{
						glyph = (c + delta) & 0xFFFF;
					}
					else
					{
						var address = rangeOffsets + s * 2 + rangeOffset + (c - start) * 2;
						glyph = U16(data, address);
						if (glyph != 0)
							glyph = (glyph + delta) & 0xFFFF;
					}

					if (glyph != 0)
					{
						result[c] = glyph;

						// Symbol fonts map their characters to U+F0xx; expose them at their ASCII codes as well.
						if (symbol && c >= 0xF000 && c <= 0xF0FF)
							result[c - 0xF000] = glyph;
					}
				}
			}
		}

		/// <summary>
		/// Reads a format 12 (segmented coverage) character map.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="table">The offset of the subtable.</param>
		/// <param name="result">The mapping to fill.</param>
		private static void ReadFormat12(byte[] data, int table, Dictionary<int, int> result)
		{
			var groups = (int)U32(data, table + 12);

			for (var g = 0; g < groups; g++)
			{
				var record = table + 16 + g * 12;
				var start = (int)U32(data, record);
				var end = (int)U32(data, record + 4);
				var glyph = (int)U32(data, record + 8);

				// Only the Basic Multilingual Plane matters for the layout.
				for (var c = start; c <= end && c <= 0xFFFF; c++)
					result[c] = glyph + (c - start);
			}
		}

		/// <summary>
		/// Reads the family name of a font from its <c>name</c> table.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="tables">The table directory.</param>
		/// <returns>The family name, or <c>null</c>.</returns>
		private static string ReadFamilyName(byte[] data, Dictionary<string, int> tables)
		{
			if (!tables.TryGetValue("name", out var name))
				return null;

			var count = U16(data, name + 2);
			var storage = name + U16(data, name + 4);
			string family = null;

			for (var i = 0; i < count; i++)
			{
				var record = name + 6 + i * 12;
				var platform = U16(data, record);
				var language = U16(data, record + 4);
				var nameId = U16(data, record + 6);
				var length = U16(data, record + 8);
				var offset = U16(data, record + 10);

				// Name 1 is the legacy family name, which is the name Word documents use.
				if (nameId != 1 || platform != 3)
					continue;

				var value = Encoding.BigEndianUnicode.GetString(data, storage + offset, length);

				// Prefer the English (United States) name.
				if (family == null || language == 0x0409)
					family = value;
			}

			return family;
		}

		/// <summary>
		/// Reads the bold and italic flags of a font from its <c>OS/2</c> or <c>head</c> table.
		/// </summary>
		/// <param name="data">The font file content.</param>
		/// <param name="tables">The table directory.</param>
		/// <returns>The style flags.</returns>
		private static (bool Bold, bool Italic) ReadStyle(byte[] data, Dictionary<string, int> tables)
		{
			if (tables.TryGetValue("OS/2", out var os2))
			{
				var selection = U16(data, os2 + 62);
				return ((selection & 0x20) != 0, (selection & 0x01) != 0);
			}

			if (tables.TryGetValue("head", out var head))
			{
				var macStyle = U16(data, head + 44);
				return ((macStyle & 1) != 0, (macStyle & 2) != 0);
			}

			return (false, false);
		}

		/// <summary>Reads a big endian unsigned 16-bit integer.</summary>
		/// <param name="data">The data.</param>
		/// <param name="offset">The offset.</param>
		/// <returns>The value.</returns>
		private static int U16(byte[] data, int offset) => (data[offset] << 8) | data[offset + 1];

		/// <summary>Reads a big endian signed 16-bit integer.</summary>
		/// <param name="data">The data.</param>
		/// <param name="offset">The offset.</param>
		/// <returns>The value.</returns>
		private static int S16(byte[] data, int offset) => (short)U16(data, offset);

		/// <summary>Reads a big endian unsigned 32-bit integer.</summary>
		/// <param name="data">The data.</param>
		/// <param name="offset">The offset.</param>
		/// <returns>The value.</returns>
		private static uint U32(byte[] data, int offset) => (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

		#endregion
	}
}

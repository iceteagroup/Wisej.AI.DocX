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
using System.Text;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Maps the characters of the legacy symbol fonts (Symbol, Wingdings) to Unicode.
	/// </summary>
	/// <remarks>
	/// Word stores symbol characters either in the private use area (U+F020 to U+F0FF) or as
	/// plain ASCII codes paired with a symbol font. Without a mapping the text would contain
	/// meaningless characters, e.g. <c>a</c> instead of <c>α</c> or U+F0B7 instead of a bullet.
	/// </remarks>
	internal static class SymbolMap
	{
		/// <summary>
		/// The Symbol font encoding (Greek letters and mathematical symbols).
		/// </summary>
		private static readonly Dictionary<int, string> Symbol = Build(
			0x22, "∀", 0x24, "∃", 0x27, "∋", 0x2A, "∗", 0x2D, "−", 0x40, "≅", 0x5C, "∴", 0x5E, "⊥", 0x60, "‾", 0x7E, "∼",
			0x41, "Α", 0x42, "Β", 0x43, "Χ", 0x44, "Δ", 0x45, "Ε", 0x46, "Φ", 0x47, "Γ", 0x48, "Η", 0x49, "Ι", 0x4A, "ϑ",
			0x4B, "Κ", 0x4C, "Λ", 0x4D, "Μ", 0x4E, "Ν", 0x4F, "Ο", 0x50, "Π", 0x51, "Θ", 0x52, "Ρ", 0x53, "Σ", 0x54, "Τ",
			0x55, "Υ", 0x56, "ς", 0x57, "Ω", 0x58, "Ξ", 0x59, "Ψ", 0x5A, "Ζ",
			0x61, "α", 0x62, "β", 0x63, "χ", 0x64, "δ", 0x65, "ε", 0x66, "φ", 0x67, "γ", 0x68, "η", 0x69, "ι", 0x6A, "ϕ",
			0x6B, "κ", 0x6C, "λ", 0x6D, "μ", 0x6E, "ν", 0x6F, "ο", 0x70, "π", 0x71, "θ", 0x72, "ρ", 0x73, "σ", 0x74, "τ",
			0x75, "υ", 0x76, "ϖ", 0x77, "ω", 0x78, "ξ", 0x79, "ψ", 0x7A, "ζ",
			0xA1, "ϒ", 0xA2, "′", 0xA3, "≤", 0xA4, "⁄", 0xA5, "∞", 0xA6, "ƒ", 0xA7, "♣", 0xA8, "♦", 0xA9, "♥", 0xAA, "♠",
			0xAB, "↔", 0xAC, "←", 0xAD, "↑", 0xAE, "→", 0xAF, "↓", 0xB0, "°", 0xB1, "±", 0xB2, "″", 0xB3, "≥", 0xB4, "×",
			0xB5, "∝", 0xB6, "∂", 0xB7, "•", 0xB8, "÷", 0xB9, "≠", 0xBA, "≡", 0xBB, "≈", 0xBC, "…", 0xC4, "⊗", 0xC5, "⊕",
			0xC6, "∅", 0xC7, "∩", 0xC8, "∪", 0xC9, "⊃", 0xCA, "⊇", 0xCB, "⊄", 0xCC, "⊂", 0xCD, "⊆", 0xCE, "∈", 0xCF, "∉",
			0xD0, "∠", 0xD1, "∇", 0xD2, "®", 0xD3, "©", 0xD4, "™", 0xD5, "∏", 0xD6, "√", 0xD7, "⋅", 0xD8, "¬", 0xD9, "∧",
			0xDA, "∨", 0xDB, "⇔", 0xDC, "⇐", 0xDD, "⇑", 0xDE, "⇒", 0xDF, "⇓", 0xE0, "◊", 0xE1, "〈", 0xE5, "∑", 0xF1, "〉",
			0xF2, "∫");

		/// <summary>
		/// The most common characters of the Wingdings fonts (bullets, check boxes, arrows).
		/// </summary>
		private static readonly Dictionary<int, string> Wingdings = Build(
			0x28, "☎", 0x2A, "✉", 0x4A, "☺", 0x4B, "😐", 0x4C, "☹", 0x6C, "●", 0x6E, "■", 0x6F, "□", 0x71, "❑", 0x72, "❒",
			0x73, "⬧", 0x74, "⧫", 0x75, "◆", 0x76, "❖", 0x77, "⬥", 0x9F, "•", 0xA1, "○", 0xA7, "▪", 0xA8, "◻", 0xD8, "➢",
			0xDF, "←", 0xE0, "→", 0xE8, "➔", 0xF0, "⇨", 0xFB, "✗", 0xFC, "✓", 0xFD, "☒", 0xFE, "☑");

		/// <summary>
		/// Converts a character of a symbol font to its Unicode equivalent.
		/// </summary>
		/// <param name="code">The character code, either in the private use area or in the 0x20-0xFF range.</param>
		/// <param name="fontFamily">The symbol font family. Can be <c>null</c>.</param>
		/// <returns>The Unicode text, or <c>null</c> when the character is not known.</returns>
		public static string Map(int code, string fontFamily)
		{
			if (code >= 0xF000 && code <= 0xF0FF)
				code -= 0xF000;

			if (code < 0x20 || code > 0xFF)
				return null;

			if (fontFamily != null && fontFamily.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase))
				return Wingdings.TryGetValue(code, out var wingding) ? wingding : null;

			if (fontFamily != null && fontFamily.StartsWith("Webdings", StringComparison.OrdinalIgnoreCase))
				return null;

			return Symbol.TryGetValue(code, out var symbol) ? symbol : null;
		}

		/// <summary>
		/// Replaces the private use area characters of a string with their Unicode equivalents,
		/// dropping those that cannot be mapped.
		/// </summary>
		/// <param name="text">The text to convert.</param>
		/// <param name="fontFamily">The symbol font the text is set in. Can be <c>null</c>.</param>
		/// <returns>The converted text.</returns>
		public static string MapText(string text, string fontFamily)
		{
			if (String.IsNullOrEmpty(text))
				return text;

			var symbolFont = FontNames.IsSymbolFont(fontFamily);
			StringBuilder sb = null;

			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				var isPrivate = c >= '' && c <= '';

				if (!isPrivate && !(symbolFont && c >= ' ' && c <= 'ÿ'))
				{
					sb?.Append(c);
					continue;
				}

				if (sb == null)
					sb = new StringBuilder(text, 0, i, text.Length);

				sb.Append(Map(c, fontFamily) ?? (isPrivate ? "" : c.ToString()));
			}

			return sb?.ToString() ?? text;
		}

		/// <summary>
		/// Builds a lookup table from alternating code and text arguments.
		/// </summary>
		/// <param name="pairs">Alternating <see cref="int"/> codes and <see cref="string"/> values.</param>
		/// <returns>The lookup table.</returns>
		private static Dictionary<int, string> Build(params object[] pairs)
		{
			var map = new Dictionary<int, string>(pairs.Length / 2);
			for (var i = 0; i < pairs.Length; i += 2)
				map[(int)pairs[i]] = (string)pairs[i + 1];

			return map;
		}
	}
}

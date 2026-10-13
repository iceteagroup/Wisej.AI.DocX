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

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Classifies font family names.
	/// </summary>
	internal static class FontNames
	{
		/// <summary>
		/// Fragments of font family names that identify monospaced (programming) fonts.
		/// </summary>
		private static readonly string[] MonospaceFragments =
		{
			"mono", "courier", "consolas", "console", "code", "typewriter", "inconsolata", "menlo", "monaco", "fixedsys",
		};

		/// <summary>
		/// Determines whether a font family is monospaced, so that text set in it is rendered as code.
		/// </summary>
		/// <param name="family">The font family name. Can be <c>null</c>.</param>
		/// <returns><c>true</c> for well known monospaced font families.</returns>
		public static bool IsMonospace(string family)
		{
			if (String.IsNullOrEmpty(family))
				return false;

			foreach (var fragment in MonospaceFragments)
			{
				if (family.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Determines whether a font family is a symbol font whose characters are mapped to the
		/// private use area (<c>Symbol</c>, <c>Wingdings</c>, <c>Webdings</c>).
		/// </summary>
		/// <param name="family">The font family name. Can be <c>null</c>.</param>
		/// <returns><c>true</c> for symbol fonts.</returns>
		public static bool IsSymbolFont(string family)
		{
			if (String.IsNullOrEmpty(family))
				return false;

			return family.StartsWith("Symbol", StringComparison.OrdinalIgnoreCase)
				|| family.StartsWith("Wingdings", StringComparison.OrdinalIgnoreCase)
				|| family.StartsWith("Webdings", StringComparison.OrdinalIgnoreCase);
		}
	}
}

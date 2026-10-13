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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Wisej.AI.DocX.Reading;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Finds the metrics of the fonts used by a document.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The lookup order is: the built-in tables of the most common Office fonts, the fonts installed on
	/// the machine (when enabled), the metric-compatible substitutes (Liberation, Carlito, Helvetica...)
	/// and finally a generic font of the same class (serif, sans serif, monospaced, East Asian).
	/// </para>
	/// <para>
	/// Font files are indexed once per process and their metrics are cached, so the cost of using
	/// system fonts is paid only the first time a font is needed.
	/// </para>
	/// </remarks>
	internal sealed partial class FontCatalog
	{
		/// <summary>Metric-compatible substitutes of fonts that may not be installed.</summary>
		private static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["Helvetica"] = "Arial",
			["Helvetica Neue"] = "Arial",
			["Liberation Sans"] = "Arial",
			["Arimo"] = "Arial",
			["Nimbus Sans"] = "Arial",
			["Times"] = "Times New Roman",
			["Liberation Serif"] = "Times New Roman",
			["Tinos"] = "Times New Roman",
			["Nimbus Roman"] = "Times New Roman",
			["Courier"] = "Courier New",
			["Liberation Mono"] = "Courier New",
			["Cousine"] = "Courier New",
			["Carlito"] = "Calibri",
			["Caladea"] = "Cambria",
			["Calibri Body"] = "Calibri",
			["Cambria Math"] = "Cambria",
			["Segoe UI Semibold"] = "Segoe UI",
			["MS PMincho"] = "MS Mincho",
			["MS PGothic"] = "MS Gothic",
			["ＭＳ 明朝"] = "MS Mincho",
			["ＭＳ ゴシック"] = "MS Gothic",
			["宋体"] = "SimSun",
			["新細明體"] = "PMingLiU",
			["微软雅黑"] = "Microsoft YaHei",
			["游明朝"] = "Yu Mincho",
			["游ゴシック"] = "Yu Gothic",
			["맑은 고딕"] = "Malgun Gothic",
		};

		/// <summary>Fragments of family names that identify serif fonts.</summary>
		private static readonly string[] SerifFragments =
		{
			"times", "roman", "serif", "garamond", "georgia", "cambria", "book", "palatino", "century", "minion",
			"baskerville", "bodoni", "caslon", "didot", "constantia", "rockwell", "goudy", "perpetua", "centaur",
		};

		/// <summary>Fragments of family names that identify East Asian fonts.</summary>
		private static readonly string[] EastAsianFragments =
		{
			"mincho", "gothic", "sim", "song", "hei", "ming", "kai", "yahei", "meiryo", "malgun", "gulim", "batang",
			"dotum", "gungsuh", "fangsong", "dengxian", "jhenghei", "yu ", "ms p",
		};

		private static readonly ConcurrentDictionary<string, FontMetrics> Cache = new ConcurrentDictionary<string, FontMetrics>(StringComparer.OrdinalIgnoreCase);
		private static readonly object IndexLock = new object();
		private static Dictionary<string, List<(string Path, int Index, bool Bold, bool Italic)>> _systemFonts;

		private readonly bool _useSystemFonts;

		/// <summary>
		/// Initializes a new instance of the <see cref="FontCatalog"/> class.
		/// </summary>
		/// <param name="useSystemFonts">Whether the fonts installed on the machine are used.</param>
		public FontCatalog(bool useSystemFonts)
		{
			_useSystemFonts = useSystemFonts;
		}

		/// <summary>
		/// Returns the metrics of a font face.
		/// </summary>
		/// <param name="family">The font family name. Can be <c>null</c>.</param>
		/// <param name="bold">Whether the bold face is requested.</param>
		/// <returns>The metrics of the font, or of the best substitute.</returns>
		public FontMetrics Get(string family, bool bold)
		{
			family = Normalize(family);
			var key = (_useSystemFonts ? "s|" : "b|") + family + (bold ? "|b" : "|r");

			return Cache.GetOrAdd(key, _ => Resolve(family, bold));
		}

		#region Implementation

		/// <summary>
		/// Resolves the metrics of a font face, following the lookup order.
		/// </summary>
		/// <param name="family">The normalized family name.</param>
		/// <param name="bold">Whether the bold face is requested.</param>
		/// <returns>The metrics.</returns>
		private FontMetrics Resolve(string family, bool bold)
		{
			var metrics = GetBuiltIn(family, bold);

			if (metrics == null && _useSystemFonts)
				metrics = GetSystemFont(family, bold);

			if (metrics == null && Aliases.TryGetValue(family, out var alias))
				metrics = GetBuiltIn(alias, bold) ?? (_useSystemFonts ? GetSystemFont(alias, bold) : null);

			return metrics ?? GetBuiltIn(GetGenericSubstitute(family), bold) ?? GetBuiltIn("Arial", bold);
		}

		/// <summary>
		/// Returns the built-in metrics of a font face, falling back to the regular face when the bold one is missing.
		/// </summary>
		/// <param name="family">The family name.</param>
		/// <param name="bold">Whether the bold face is requested.</param>
		/// <returns>The metrics, or <c>null</c> when the family is not built in.</returns>
		private static FontMetrics GetBuiltIn(string family, bool bold)
		{
			if (bold && BuiltInTables.TryGetValue(family + "|b", out var boldTable))
				return boldTable.Value;

			return BuiltInTables.TryGetValue(family + "|r", out var table) ? table.Value : null;
		}

		/// <summary>
		/// Returns the metrics of an installed font.
		/// </summary>
		/// <param name="family">The family name.</param>
		/// <param name="bold">Whether the bold face is requested.</param>
		/// <returns>The metrics, or <c>null</c> when the font is not installed.</returns>
		private static FontMetrics GetSystemFont(string family, bool bold)
		{
			var index = GetSystemFontIndex();
			if (!index.TryGetValue(family, out var faces))
				return null;

			var face = faces.FirstOrDefault(f => f.Bold == bold && !f.Italic);
			if (face.Path == null)
				face = faces.FirstOrDefault(f => !f.Italic);
			if (face.Path == null)
				face = faces[0];

			return TrueTypeReader.ReadMetrics(face.Path, face.Index);
		}

		/// <summary>
		/// Returns the index of the installed fonts by family name, building it on first use.
		/// </summary>
		/// <returns>The faces of every installed family.</returns>
		private static Dictionary<string, List<(string Path, int Index, bool Bold, bool Italic)>> GetSystemFontIndex()
		{
			lock (IndexLock)
			{
				if (_systemFonts != null)
					return _systemFonts;

				var index = new Dictionary<string, List<(string, int, bool, bool)>>(StringComparer.OrdinalIgnoreCase);

				foreach (var directory in GetFontDirectories())
				{
					IEnumerable<string> files;
					try
					{
						files = Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
							.Where(f => f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
								|| f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)
								|| f.EndsWith(".ttc", StringComparison.OrdinalIgnoreCase))
							.ToList();
					}
					catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
					{
						continue;
					}

					foreach (var file in files)
					{
						foreach (var font in TrueTypeReader.ReadFamilies(file))
						{
							if (!index.TryGetValue(font.Family, out var faces))
								index[font.Family] = faces = new List<(string, int, bool, bool)>();

							faces.Add((file, font.Index, font.Bold, font.Italic));
						}
					}
				}

				_systemFonts = index;
				return index;
			}
		}

		/// <summary>
		/// Returns the directories that contain fonts on Windows, Linux and macOS.
		/// </summary>
		/// <returns>The existing font directories.</returns>
		private static IEnumerable<string> GetFontDirectories()
		{
			var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

			var candidates = new[]
			{
				Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
				Path.Combine(localAppData, "Microsoft", "Windows", "Fonts"),

				// Office downloads its cloud fonts (Aptos, ...) here.
				Path.Combine(localAppData, "Microsoft", "FontCache", "4", "CloudFonts"),
				"/usr/share/fonts",
				"/usr/local/share/fonts",
				Path.Combine(home, ".fonts"),
				Path.Combine(home, ".local", "share", "fonts"),
				"/Library/Fonts",
				"/System/Library/Fonts",
				Path.Combine(home, "Library", "Fonts"),
			};

			return candidates.Where(d => !String.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct(StringComparer.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Returns a built-in font of the same class as an unknown font.
		/// </summary>
		/// <param name="family">The family name.</param>
		/// <returns>The name of a built-in font.</returns>
		private static string GetGenericSubstitute(string family)
		{
			var name = family.ToLowerInvariant();

			if (FontNames.IsMonospace(family))
				return "Courier New";

			if (EastAsianFragments.Any(name.Contains) || family.Any(FontMetrics.IsWide))
				return "MS Mincho";

			if (SerifFragments.Any(name.Contains) && !name.Contains("sans"))
				return "Times New Roman";

			return "Arial";
		}

		/// <summary>
		/// Normalizes a family name: trims it and removes the qualifiers Word adds in the user interface.
		/// </summary>
		/// <param name="family">The family name. Can be <c>null</c>.</param>
		/// <returns>The normalized name; <c>Calibri</c> when the name is empty.</returns>
		private static string Normalize(string family)
		{
			if (String.IsNullOrWhiteSpace(family))
				return "Calibri";

			family = family.Trim();

			foreach (var suffix in new[] { " (Body)", " (Headings)", " (Body CS)", " (Headings CS)" })
			{
				if (family.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
					family = family.Substring(0, family.Length - suffix.Length);
			}

			return family;
		}

		#endregion
	}
}

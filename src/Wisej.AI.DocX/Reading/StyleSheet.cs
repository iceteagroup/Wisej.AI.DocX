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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Resolves the effective paragraph and run formatting from the style definitions part.
	/// </summary>
	/// <remarks>
	/// The cascade applied, from the weakest to the strongest layer, is:
	/// document defaults, table style, numbering level (paragraphs only), paragraph style
	/// (following <c>w:basedOn</c> from the root), character style and direct formatting.
	/// The merged style layers are cached, so resolving the formatting of a run only costs
	/// the application of its direct properties.
	/// </remarks>
	internal sealed class StyleSheet
	{
		/// <summary>Matches the built-in heading style names (<c>heading 1</c> to <c>heading 9</c>).</summary>
		private static readonly Regex HeadingName = new Regex(@"^heading\s*([1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>Matches the built-in table of contents style names (<c>toc 1</c> to <c>toc 9</c>).</summary>
		private static readonly Regex TocName = new Regex(@"^(toc\s*[1-9]|table of figures)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private readonly Dictionary<string, StyleDefinition> _styles = new Dictionary<string, StyleDefinition>(StringComparer.Ordinal);
		private readonly Dictionary<string, ParagraphPropertiesBuilder> _paragraphCache = new Dictionary<string, ParagraphPropertiesBuilder>(StringComparer.Ordinal);
		private readonly Dictionary<string, RunPropertiesBuilder> _runCache = new Dictionary<string, RunPropertiesBuilder>(StringComparer.Ordinal);
		private readonly ThemeFonts _theme;

		private XElement _defaultParagraphProperties;
		private XElement _defaultRunProperties;
		private string _defaultParagraphStyleId;

		/// <summary>
		/// Initializes a new instance of the <see cref="StyleSheet"/> class.
		/// </summary>
		/// <param name="styles">The root element of the styles part (<c>w:styles</c>). Can be <c>null</c>.</param>
		/// <param name="theme">The theme fonts.</param>
		public StyleSheet(XElement styles, ThemeFonts theme)
		{
			_theme = theme;

			if (styles != null)
				Load(styles);
		}

		/// <summary>
		/// Gets the theme fonts.
		/// </summary>
		public ThemeFonts Theme => _theme;

		#region Resolution

		/// <summary>
		/// Resolves the effective formatting of a paragraph.
		/// </summary>
		/// <param name="pPr">The direct paragraph properties (<c>w:pPr</c>). Can be <c>null</c>.</param>
		/// <param name="tableStyleId">The style of the table containing the paragraph, or <c>null</c>.</param>
		/// <param name="numbering">The numbering definitions, used to apply the indentation of list levels. Can be <c>null</c>.</param>
		/// <param name="numberingId">Returns the effective numbering instance, or <c>null</c>.</param>
		/// <param name="numberingLevel">Returns the effective numbering level.</param>
		/// <returns>The resolved paragraph properties.</returns>
		public ParagraphProperties ResolveParagraph(
			XElement pPr,
			string tableStyleId,
			NumberingDefinitions numbering,
			out int? numberingId,
			out int numberingLevel)
		{
			var styleId = GetParagraphStyleId(pPr);
			var builder = GetParagraphStyleLayers(styleId, tableStyleId).Clone();

			// The numbering referenced by the style or by the paragraph inserts its level properties
			// between the style and the direct formatting.
			var direct = new ParagraphPropertiesBuilder();
			direct.Apply(pPr);

			numberingId = direct.NumberingId ?? builder.NumberingId;
			numberingLevel = direct.NumberingLevel ?? builder.NumberingLevel ?? 0;

			if (numberingId > 0 && numbering != null)
				builder.Apply(numbering.GetLevelProperties(numberingId.Value, numberingLevel), includeNumbering: false);

			builder.Apply(pPr);

			var properties = builder.Build();
			properties.StyleId = styleId;
			properties.StyleName = GetStyleName(styleId);
			properties.MarkStyle = ResolveRun(pPr?.Element(Ns.W + "rPr"), styleId, tableStyleId);

			ClassifyParagraph(properties, builder.OutlineLevel);
			return properties;
		}

		/// <summary>
		/// Resolves the effective formatting of a run.
		/// </summary>
		/// <param name="rPr">The direct run properties (<c>w:rPr</c>). Can be <c>null</c>.</param>
		/// <param name="paragraphStyleId">The style of the containing paragraph, or <c>null</c> for the default style.</param>
		/// <param name="tableStyleId">The style of the containing table, or <c>null</c>.</param>
		/// <returns>The resolved run style.</returns>
		public RunStyle ResolveRun(XElement rPr, string paragraphStyleId, string tableStyleId)
		{
			var characterStyleId = rPr?.WChildVal("rStyle");
			var builder = GetRunStyleLayers(paragraphStyleId ?? _defaultParagraphStyleId, tableStyleId, characterStyleId).Clone();

			builder.Apply(rPr);
			return builder.Build(_theme);
		}

		/// <summary>
		/// Returns the display name of a style, or <c>null</c> when the style does not exist.
		/// </summary>
		/// <param name="styleId">The style identifier. Can be <c>null</c>.</param>
		/// <returns>The style name.</returns>
		public string GetStyleName(string styleId)
		{
			return styleId != null && _styles.TryGetValue(styleId, out var style) ? style.Name : null;
		}

		/// <summary>
		/// Returns the numbering instance and level set by a numbering style (<c>w:numStyleLink</c> targets).
		/// </summary>
		/// <param name="styleId">The identifier of the numbering style.</param>
		/// <param name="numberingId">Returns the numbering instance referenced by the style.</param>
		/// <returns><c>true</c> when the style exists and references a numbering instance.</returns>
		public bool TryGetStyleNumbering(string styleId, out int numberingId)
		{
			numberingId = 0;

			if (styleId == null || !_styles.TryGetValue(styleId, out var style))
				return false;

			var builder = new ParagraphPropertiesBuilder();
			builder.Apply(style.ParagraphProperties);

			numberingId = builder.NumberingId ?? 0;
			return numberingId > 0;
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Loads the style definitions and the document defaults.
		/// </summary>
		/// <param name="styles">The root element of the styles part.</param>
		private void Load(XElement styles)
		{
			var defaults = styles.Element(Ns.W + "docDefaults");
			_defaultParagraphProperties = defaults?.Element(Ns.W + "pPrDefault")?.Element(Ns.W + "pPr");
			_defaultRunProperties = defaults?.Element(Ns.W + "rPrDefault")?.Element(Ns.W + "rPr");

			foreach (var e in styles.Elements(Ns.W + "style"))
			{
				var id = e.WAttr("styleId");
				if (String.IsNullOrEmpty(id) || _styles.ContainsKey(id))
					continue;

				var style = new StyleDefinition
				{
					Id = id,
					Type = e.WAttr("type") ?? "paragraph",
					Name = e.WChildVal("name") ?? id,
					BasedOn = e.WChildVal("basedOn"),
					ParagraphProperties = e.Element(Ns.W + "pPr"),
					RunProperties = e.Element(Ns.W + "rPr"),
				};

				_styles[id] = style;

				if (style.Type == "paragraph" && XmlExtensions.ParseOnOff(e.WAttr("default"), false))
					_defaultParagraphStyleId = id;
			}
		}

		/// <summary>
		/// Returns the paragraph style of a paragraph, falling back to the default paragraph style.
		/// </summary>
		/// <param name="pPr">The direct paragraph properties. Can be <c>null</c>.</param>
		/// <returns>The style identifier, or <c>null</c> when the document has no default style.</returns>
		private string GetParagraphStyleId(XElement pPr)
		{
			var styleId = pPr?.WChildVal("pStyle");
			return styleId != null && _styles.ContainsKey(styleId) ? styleId : _defaultParagraphStyleId;
		}

		/// <summary>
		/// Returns the cached merge of the document defaults, the table style and the paragraph style chain.
		/// </summary>
		/// <param name="styleId">The paragraph style. Can be <c>null</c>.</param>
		/// <param name="tableStyleId">The table style. Can be <c>null</c>.</param>
		/// <returns>The merged layers; callers must clone the result before modifying it.</returns>
		private ParagraphPropertiesBuilder GetParagraphStyleLayers(string styleId, string tableStyleId)
		{
			var key = styleId + "|" + tableStyleId;
			if (_paragraphCache.TryGetValue(key, out var cached))
				return cached;

			var builder = new ParagraphPropertiesBuilder();
			builder.Apply(_defaultParagraphProperties);

			foreach (var style in GetChain(tableStyleId))
				builder.Apply(style.ParagraphProperties);

			foreach (var style in GetChain(styleId))
				builder.Apply(style.ParagraphProperties);

			_paragraphCache[key] = builder;
			return builder;
		}

		/// <summary>
		/// Returns the cached merge of the document defaults, the table style, the paragraph style chain
		/// and the character style chain.
		/// </summary>
		/// <param name="paragraphStyleId">The paragraph style. Can be <c>null</c>.</param>
		/// <param name="tableStyleId">The table style. Can be <c>null</c>.</param>
		/// <param name="characterStyleId">The character style. Can be <c>null</c>.</param>
		/// <returns>The merged layers; callers must clone the result before modifying it.</returns>
		private RunPropertiesBuilder GetRunStyleLayers(string paragraphStyleId, string tableStyleId, string characterStyleId)
		{
			var key = paragraphStyleId + "|" + tableStyleId + "|" + characterStyleId;
			if (_runCache.TryGetValue(key, out var cached))
				return cached;

			var builder = new RunPropertiesBuilder();
			builder.Apply(_defaultRunProperties);

			foreach (var style in GetChain(tableStyleId))
				builder.Apply(style.RunProperties);

			foreach (var style in GetChain(paragraphStyleId))
				builder.Apply(style.RunProperties);

			foreach (var style in GetChain(characterStyleId))
				builder.Apply(style.RunProperties);

			_runCache[key] = builder;
			return builder;
		}

		/// <summary>
		/// Returns the inheritance chain of a style, from the root ancestor to the style itself.
		/// </summary>
		/// <param name="styleId">The style identifier. Can be <c>null</c>.</param>
		/// <returns>The styles of the chain; empty when the style does not exist.</returns>
		private List<StyleDefinition> GetChain(string styleId)
		{
			var chain = new List<StyleDefinition>();

			// The depth limit protects against circular w:basedOn references.
			while (styleId != null && chain.Count < 32 && _styles.TryGetValue(styleId, out var style))
			{
				if (chain.Contains(style))
					break;

				chain.Insert(0, style);
				styleId = style.BasedOn;
			}

			return chain;
		}

		/// <summary>
		/// Determines the semantic role of a paragraph from its style names and outline level.
		/// </summary>
		/// <param name="properties">The paragraph properties to update.</param>
		/// <param name="outlineLevel">The resolved outline level, or <c>null</c>.</param>
		private void ClassifyParagraph(ParagraphProperties properties, int? outlineLevel)
		{
			var name = properties.StyleName?.Trim().ToLowerInvariant() ?? "";

			switch (name)
			{
				case "title":
					properties.Kind = ParagraphKind.Title;
					return;

				case "subtitle":
					properties.Kind = ParagraphKind.Subtitle;
					return;

				case "quote":
				case "intense quote":
				case "block text":
					properties.Kind = ParagraphKind.Quote;
					return;

				case "caption":
					properties.Kind = ParagraphKind.Caption;
					return;

				case "toc heading":
					properties.Kind = ParagraphKind.Heading;
					properties.HeadingLevel = 1;
					return;
			}

			if (TocName.IsMatch(name))
			{
				properties.Kind = ParagraphKind.TableOfContents;
				return;
			}

			if (outlineLevel >= 0 && outlineLevel <= 8)
			{
				properties.Kind = ParagraphKind.Heading;
				properties.HeadingLevel = outlineLevel.Value + 1;
				return;
			}

			// Styles derived from the built-in headings are headings even when the outline level was removed.
			foreach (var style in GetChain(properties.StyleId))
			{
				var match = HeadingName.Match(style.Name);
				if (match.Success && outlineLevel == null)
				{
					properties.Kind = ParagraphKind.Heading;
					properties.HeadingLevel = match.Groups[1].Value[0] - '0';
				}
			}
		}

		#endregion

		#region StyleDefinition

		/// <summary>
		/// A style definition (<c>w:style</c>).
		/// </summary>
		private sealed class StyleDefinition
		{
			/// <summary>Gets or sets the style identifier.</summary>
			public string Id { get; set; }

			/// <summary>Gets or sets the style type: <c>paragraph</c>, <c>character</c>, <c>table</c> or <c>numbering</c>.</summary>
			public string Type { get; set; }

			/// <summary>Gets or sets the display name.</summary>
			public string Name { get; set; }

			/// <summary>Gets or sets the identifier of the parent style, or <c>null</c>.</summary>
			public string BasedOn { get; set; }

			/// <summary>Gets or sets the paragraph properties of the style, or <c>null</c>.</summary>
			public XElement ParagraphProperties { get; set; }

			/// <summary>Gets or sets the run properties of the style, or <c>null</c>.</summary>
			public XElement RunProperties { get; set; }
		}

		#endregion
	}
}

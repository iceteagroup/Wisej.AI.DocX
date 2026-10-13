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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// Renders the document as GitHub flavored Markdown.
	/// </summary>
	/// <remarks>
	/// The output favors what language models read best: ATX headings, nested lists that keep the
	/// original numbering, pipe tables with merged cells resolved (HTML tables when a table nests other
	/// tables), image references with their alternative text, LaTeX equations, footnotes and charts
	/// expanded into data tables. Escaping is limited to the characters that would change the meaning
	/// of the Markdown, so the text stays readable.
	/// </remarks>
	internal sealed class MarkdownRenderer : DocumentRenderer
	{
		/// <summary>Matches line starts that Markdown would read as a heading, a quote, a bullet or a rule.</summary>
		private static readonly Regex BlockMarker = new Regex(@"^(\s*)(#{1,6}(?=\s|$)|>|[+\-](?=\s|$)|[-=]+\s*$)", RegexOptions.CultureInvariant);

		/// <summary>Matches line starts that Markdown would read as an ordered list item.</summary>
		private static readonly Regex OrderedMarker = new Regex(@"^(\s*\d{1,9})([.)])(?=\s|$)", RegexOptions.CultureInvariant);

		/// <summary>The emphasis markers, in nesting order.</summary>
		private static readonly Emphasis[] EmphasisOrder = { Emphasis.Bold, Emphasis.Italic, Emphasis.Strike, Emphasis.Superscript, Emphasis.Subscript };

		/// <summary>
		/// Initializes a new instance of the <see cref="MarkdownRenderer"/> class.
		/// </summary>
		/// <param name="model">The paginated document model.</param>
		/// <param name="options">The extraction options.</param>
		public MarkdownRenderer(DocxModel model, DocxExtractorOptions options)
			: base(model, options, "```")
		{
		}

		/// <inheritdoc/>
		protected override string DefaultPageHeaderFormat => "<!-- Page {0} -->";

		#region Paragraphs

		/// <inheritdoc/>
		protected override void WriteParagraph(ParagraphBlock paragraph, List<Span> spans, bool isContinuation)
		{
			var properties = paragraph.Properties;

			// Paragraphs set entirely in a code font become fenced code blocks.
			if (Options.IncludeFormatting && IsCode(spans) && properties.Kind == ParagraphKind.Normal && paragraph.List == null)
			{
				Pages.AppendBlock(String.Concat(spans.Select(s => s.Text)).TrimEnd(), OutputBlockKind.Code);
				return;
			}

			var content = FormatSpans(spans, InlineMode.Block).Trim();
			if (content.Length == 0)
				return;

			switch (properties.Kind)
			{
				case ParagraphKind.Title:
				case ParagraphKind.Heading:
					var level = properties.Kind == ParagraphKind.Title ? 1 : Math.Min(6, Math.Max(1, properties.HeadingLevel));
					var label = paragraph.List != null && !paragraph.List.IsBullet && !isContinuation ? paragraph.List.Text + " " : "";
					Pages.AppendBlock(new string('#', level) + " " + label + Collapse(content), OutputBlockKind.Paragraph);
					return;

				case ParagraphKind.Quote:
					Pages.AppendBlock(PrefixLines(EscapeLineStarts(content), "> ", "> "), OutputBlockKind.Paragraph);
					return;

				case ParagraphKind.TableOfContents:
					Pages.AppendBlock(Collapse(content), OutputBlockKind.ListItem);
					return;
			}

			if (paragraph.List != null)
			{
				var indent = new string(' ', paragraph.List.Level * 3);
				var marker = paragraph.List.IsBullet ? "-" : paragraph.List.Text;
				var hanging = indent + new string(' ', marker.Length + 1);
				var first = isContinuation ? hanging : indent + marker + " ";

				Pages.AppendBlock(PrefixLines(content, first, hanging), OutputBlockKind.ListItem);
				return;
			}

			Pages.AppendBlock(EscapeLineStarts(content), OutputBlockKind.Paragraph);
		}

		/// <summary>
		/// Determines whether a paragraph is entirely set in a code font.
		/// </summary>
		/// <param name="spans">The spans of the paragraph.</param>
		/// <returns><c>true</c> when every piece of text is code.</returns>
		private static bool IsCode(List<Span> spans)
		{
			var text = spans.Where(s => !String.IsNullOrWhiteSpace(s.Text)).ToList();
			return text.Count > 0 && text.All(s => s.Code && !s.IsMarkup && s.Link == null);
		}

		#endregion

		#region Spans

		/// <inheritdoc/>
		protected override string FormatSpans(List<Span> spans, InlineMode mode)
		{
			var sb = new StringBuilder();
			var index = 0;

			while (index < spans.Count)
			{
				// Tracked changes enclose links, which enclose emphasis.
				var revision = spans[index].Revision;
				var end = index;
				while (end < spans.Count && spans[end].Revision == revision)
					end++;

				var group = FormatLinks(spans, index, end, mode);

				if (revision == RevisionKind.Inserted)
					sb.Append("{++").Append(group).Append("++}");
				else if (revision == RevisionKind.Deleted)
					sb.Append("{--").Append(group).Append("--}");
				else
					sb.Append(group);

				index = end;
			}

			return sb.ToString();
		}

		/// <summary>
		/// Formats a range of spans, grouping the spans that share a hyperlink.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="start">The first span of the range.</param>
		/// <param name="end">The end of the range, exclusive.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <returns>The formatted text.</returns>
		private string FormatLinks(List<Span> spans, int start, int end, InlineMode mode)
		{
			var sb = new StringBuilder();
			var index = start;

			while (index < end)
			{
				var link = spans[index].Link;
				var groupEnd = index;
				while (groupEnd < end && spans[groupEnd].Link == link)
					groupEnd++;

				var text = FormatEmphasis(spans, index, groupEnd, mode);

				if (link != null && text.Trim().Length > 0)
				{
					var lead = text.Substring(0, text.Length - text.TrimStart().Length);
					var trail = text.Substring(text.TrimEnd().Length);
					sb.Append(lead).Append('[').Append(text.Trim()).Append("](").Append(FormatUrl(link)).Append(')').Append(trail);
				}
				else
				{
					sb.Append(text);
				}

				index = groupEnd;
			}

			return sb.ToString();
		}

		/// <summary>
		/// Formats a range of spans with emphasis markers, nesting them correctly and keeping white space outside the markers.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="start">The first span of the range.</param>
		/// <param name="end">The end of the range, exclusive.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <returns>The formatted text.</returns>
		private static string FormatEmphasis(List<Span> spans, int start, int end, InlineMode mode)
		{
			var sb = new StringBuilder();
			var open = new List<Emphasis>();
			var pendingSpace = "";

			for (var i = start; i < end; i++)
			{
				var span = spans[i];

				if (span.IsMarkup)
				{
					CloseEmphasis(sb, open, 0);
					sb.Append(pendingSpace).Append(span.Text);
					pendingSpace = "";
					continue;
				}

				var text = span.Code ? CodeSpan(span.Text) : Escape(span.Text, mode);
				var core = text.Trim();

				if (core.Length == 0)
				{
					pendingSpace += text;
					continue;
				}

				var lead = text.Substring(0, text.Length - text.TrimStart().Length);
				var trail = text.Substring(text.TrimEnd().Length);
				var wanted = GetEmphasis(span);

				// Close every marker that is not wanted, together with the markers nested inside it.
				var firstUnwanted = open.FindIndex(e => !wanted.Contains(e));
				if (firstUnwanted >= 0)
					CloseEmphasis(sb, open, firstUnwanted);

				sb.Append(pendingSpace).Append(lead);

				foreach (var emphasis in EmphasisOrder)
				{
					if (wanted.Contains(emphasis) && !open.Contains(emphasis))
					{
						sb.Append(OpenMarker(emphasis));
						open.Add(emphasis);
					}
				}

				sb.Append(core);
				pendingSpace = trail;
			}

			CloseEmphasis(sb, open, 0);
			sb.Append(pendingSpace);

			return sb.ToString();
		}

		/// <summary>
		/// Closes the open emphasis markers from the innermost down to <paramref name="index"/>.
		/// </summary>
		/// <param name="sb">The output.</param>
		/// <param name="open">The open markers, outermost first.</param>
		/// <param name="index">The index of the outermost marker to close.</param>
		private static void CloseEmphasis(StringBuilder sb, List<Emphasis> open, int index)
		{
			for (var i = open.Count - 1; i >= index; i--)
			{
				sb.Append(CloseMarker(open[i]));
				open.RemoveAt(i);
			}
		}

		/// <summary>
		/// Returns the emphasis of a span.
		/// </summary>
		/// <param name="span">The span.</param>
		/// <returns>The emphasis markers the span needs.</returns>
		private static List<Emphasis> GetEmphasis(Span span)
		{
			var result = new List<Emphasis>(2);

			if (span.Bold)
				result.Add(Emphasis.Bold);
			if (span.Italic)
				result.Add(Emphasis.Italic);
			if (span.Strike)
				result.Add(Emphasis.Strike);
			if (span.Superscript)
				result.Add(Emphasis.Superscript);
			if (span.Subscript)
				result.Add(Emphasis.Subscript);

			return result;
		}

		/// <summary>
		/// Returns the opening marker of an emphasis.
		/// </summary>
		/// <param name="emphasis">The emphasis.</param>
		/// <returns>The marker.</returns>
		private static string OpenMarker(Emphasis emphasis)
		{
			switch (emphasis)
			{
				case Emphasis.Bold: return "**";
				case Emphasis.Italic: return "*";
				case Emphasis.Strike: return "~~";
				case Emphasis.Superscript: return "<sup>";
				default: return "<sub>";
			}
		}

		/// <summary>
		/// Returns the closing marker of an emphasis.
		/// </summary>
		/// <param name="emphasis">The emphasis.</param>
		/// <returns>The marker.</returns>
		private static string CloseMarker(Emphasis emphasis)
		{
			switch (emphasis)
			{
				case Emphasis.Superscript: return "</sup>";
				case Emphasis.Subscript: return "</sub>";
				default: return OpenMarker(emphasis);
			}
		}

		/// <inheritdoc/>
		protected override Span FormatLineBreak(InlineMode mode, Inline inline)
		{
			switch (mode)
			{
				case InlineMode.Block:
					return Markup("\n", inline);

				case InlineMode.Cell:
					return Markup("<br>", inline);

				default:
					return Markup(" ", inline);
			}
		}

		#endregion

		#region Tables

		/// <inheritdoc/>
		protected override void WriteTable(TableGrid grid, List<TableGrid.GridRow> headerRows, List<TableGrid.GridRow> bodyRows, List<Action> deferred)
		{
			if (bodyRows.Count == 0 && headerRows.Count == 0)
				return;

			var html = Options.Tables == TableFormat.Html || (Options.Tables == TableFormat.Auto && grid.HasNestedTables);
			var text = html ? FormatHtmlTable(grid, headerRows, bodyRows, deferred) : FormatPipeTable(grid, headerRows, bodyRows, deferred);

			Pages.AppendBlock(text, OutputBlockKind.Paragraph);
		}

		/// <summary>
		/// Formats rows as a pipe table.
		/// </summary>
		/// <param name="grid">The table grid.</param>
		/// <param name="headerRows">The declared header rows.</param>
		/// <param name="bodyRows">The rows to write.</param>
		/// <param name="deferred">Receives content written after the table.</param>
		/// <returns>The pipe table.</returns>
		private string FormatPipeTable(TableGrid grid, List<TableGrid.GridRow> headerRows, List<TableGrid.GridRow> bodyRows, List<Action> deferred)
		{
			var cache = new Dictionary<TableCell, string>();
			string Text(TableGrid.Slot slot)
			{
				if (slot == null || (!slot.IsOrigin && Options.MergedCells == MergedCellMode.Empty))
					return "";

				if (!cache.TryGetValue(slot.Cell, out var text))
					cache[slot.Cell] = text = RenderCellText(slot.Cell, deferred);

				return text;
			}

			// Pipe tables need a header: the declared header rows, or else the first row of the table,
			// which is repeated on the following pages to keep the column names with the data.
			var header = headerRows;
			var body = bodyRows;
			if (header.Count == 0)
			{
				header = new List<TableGrid.GridRow> { grid.Rows[0] };
				if (body.Count > 0 && body[0] == grid.Rows[0])
					body = body.Skip(1).ToList();
			}

			var sb = new StringBuilder();
			var columns = grid.ColumnCount;

			// Multiple header rows (a group heading spanning columns above the column names) are combined.
			var names = new string[columns];
			for (var c = 0; c < columns; c++)
			{
				var parts = header.Select(r => Text(r.Slots[c])).Where(t => t.Length > 0).Distinct().ToList();
				names[c] = String.Join(" / ", parts);
			}

			sb.Append("| ").Append(String.Join(" | ", names)).Append(" |\n");
			sb.Append('|').Append(String.Concat(Enumerable.Repeat(" --- |", columns)));

			foreach (var row in body)
				sb.Append("\n| ").Append(String.Join(" | ", row.Slots.Select(Text))).Append(" |");

			return sb.ToString();
		}

		/// <summary>
		/// Formats rows as an HTML table, with spans for merged cells and nested tables.
		/// </summary>
		/// <param name="grid">The table grid.</param>
		/// <param name="headerRows">The declared header rows.</param>
		/// <param name="bodyRows">The rows to write.</param>
		/// <param name="deferred">Receives content written after the table.</param>
		/// <returns>The HTML table.</returns>
		private string FormatHtmlTable(TableGrid grid, List<TableGrid.GridRow> headerRows, List<TableGrid.GridRow> bodyRows, List<Action> deferred)
		{
			var sb = new StringBuilder("<table>");
			var rows = headerRows.Concat(bodyRows).ToList();
			var written = new HashSet<TableCell>();

			for (var r = 0; r < rows.Count; r++)
			{
				var row = rows[r];
				var tag = r < headerRows.Count ? "th" : "td";
				sb.Append("\n<tr>");

				for (var c = 0; c < row.Slots.Length; c++)
				{
					var slot = row.Slots[c];

					// Slots covered by a span are skipped, unless the merged cell started on a previous page.
					if (slot != null && (slot.IsHorizontalContinuation || (slot.IsVerticalContinuation && written.Contains(slot.Cell))))
						continue;

					if (slot != null)
						written.Add(slot.Cell);

					var colspan = 1;
					while (c + colspan < row.Slots.Length && row.Slots[c + colspan] != null && slot != null && row.Slots[c + colspan].Cell == slot.Cell && row.Slots[c + colspan].IsHorizontalContinuation)
						colspan++;

					var rowspan = 1;
					while (slot != null && r + rowspan < rows.Count && rows[r + rowspan].Slots[c]?.Cell == slot.Cell && rows[r + rowspan].Slots[c].IsVerticalContinuation)
						rowspan++;

					sb.Append('<').Append(tag);
					if (colspan > 1)
						sb.Append(" colspan=\"").Append(colspan).Append('"');
					if (rowspan > 1)
						sb.Append(" rowspan=\"").Append(rowspan).Append('"');
					sb.Append('>');

					if (slot != null)
						sb.Append(RenderCellHtml(slot.Cell, deferred));

					sb.Append("</").Append(tag).Append('>');
				}

				sb.Append("</tr>");
			}

			sb.Append("\n</table>");
			return sb.ToString();
		}

		/// <inheritdoc/>
		protected override string RenderCellText(TableCell cell, List<Action> deferred)
		{
			var parts = new List<string>();

			foreach (var block in cell.Blocks)
			{
				switch (block)
				{
					case ParagraphBlock paragraph:
						var text = FormatSpans(BuildSpans(paragraph, paragraph.Inlines.Where(i => !(i is PageStartInline)), InlineMode.Cell, deferred), InlineMode.Cell).Trim();
						if (text.Length > 0)
							parts.Add(paragraph.List != null ? (paragraph.List.IsBullet ? "• " : paragraph.List.Text + " ") + text : text);
						break;

					case TableBlock nested:
						foreach (var row in nested.Rows)
						{
							var cells = row.Cells.Select(c => RenderCellText(c, deferred)).Where(t => t.Length > 0);
							var line = String.Join(" / ", cells);
							if (line.Length > 0)
								parts.Add(line);
						}
						break;

					case RawTextBlock raw:
						parts.Add(Escape(raw.Text, InlineMode.Cell).Replace("\n", "<br>"));
						break;
				}
			}

			return String.Join("<br>", parts);
		}

		/// <summary>
		/// Returns the content of a table cell for an HTML table, with nested tables as HTML.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="deferred">Receives content written after the table.</param>
		/// <returns>The cell content.</returns>
		private string RenderCellHtml(TableCell cell, List<Action> deferred)
		{
			var parts = new List<string>();

			foreach (var block in cell.Blocks)
			{
				if (block is TableBlock nested)
				{
					var grid = new TableGrid(nested);
					if (grid.Rows.Count > 0)
					{
						var headerRows = grid.Rows.Take(grid.HeaderRowCount).ToList();
						parts.Add(FormatHtmlTable(grid, headerRows, grid.Rows.Skip(headerRows.Count).ToList(), deferred));
					}
				}
				else
				{
					var single = new TableCell();
					single.Blocks.Add(block);

					var text = RenderCellText(single, deferred);
					if (text.Length > 0)
						parts.Add(text.Replace("\\|", "|"));
				}
			}

			return String.Join("<br>", parts);
		}

		#endregion

		#region Elements

		/// <inheritdoc/>
		protected override string FormatImage(DocxImage image)
		{
			var alt = (image.AltText ?? image.Title ?? "").Replace('\n', ' ').Replace("[", "(").Replace("]", ")");
			return $"![{alt}]({image.FileName})";
		}

		/// <inheritdoc/>
		protected override string FormatImageDescription(DocxImage image)
		{
			return PrefixLines($"**{image.FileName}:** " + image.Description.Trim(), "> ", "> ");
		}

		/// <inheritdoc/>
		protected override string FormatInlineImageDescription(DocxImage image)
		{
			return "(" + Collapse(image.Description) + ")";
		}

		/// <inheritdoc/>
		protected override string FormatMath(string latex, bool display)
		{
			return display ? "$$" + latex + "$$" : "$" + latex + "$";
		}

		/// <inheritdoc/>
		protected override string FormatNoteReference(string label)
		{
			return "[^" + label + "]";
		}

		/// <inheritdoc/>
		protected override string FormatNoteDefinition(string label, string text)
		{
			return "[^" + label + "]: " + text;
		}

		/// <inheritdoc/>
		protected override string FormatComment(Comment comment, string text)
		{
			var sb = new StringBuilder("> **Comment**");

			if (comment.Author != null)
				sb.Append(" by ").Append(comment.Author);

			if (comment.Date != null && comment.Date.Length >= 10)
				sb.Append(" (").Append(comment.Date.Substring(0, 10)).Append(')');

			if (comment.AnchorText != null)
				sb.Append(" on “").Append(Collapse(comment.AnchorText)).Append('”');

			return sb.Append(": ").Append(text).ToString();
		}

		/// <inheritdoc/>
		protected override string FormatHeaderFooter(bool isHeader, string text)
		{
			return (isHeader ? "**Header:** " : "**Footer:** ") + text;
		}

		/// <inheritdoc/>
		protected override string FormatChart(ChartData chart)
		{
			var sb = new StringBuilder();
			sb.Append("**Chart").Append(chart.Title != null ? ": " + Escape(chart.Title, InlineMode.Flat) : "").Append("**");

			var details = new List<string>();
			if (chart.ChartTypes.Count > 0)
				details.Add(String.Join(" + ", chart.ChartTypes) + " chart");
			if (chart.CategoryAxisTitle != null)
				details.Add("x axis: " + chart.CategoryAxisTitle);
			if (chart.ValueAxisTitle != null)
				details.Add("y axis: " + chart.ValueAxisTitle);
			if (details.Count > 0)
				sb.Append(" (").Append(String.Join("; ", details)).Append(')');

			sb.Append("\n\n| ").Append(Cell(chart.CategoryLabel));
			foreach (var series in chart.Series)
				sb.Append(" | ").Append(Cell(series.Name));
			sb.Append(" |\n|").Append(String.Concat(Enumerable.Repeat(" --- |", chart.Series.Count + 1)));

			for (var i = 0; i < chart.Categories.Count; i++)
			{
				sb.Append("\n| ").Append(Cell(chart.Categories[i]));
				foreach (var series in chart.Series)
					sb.Append(" | ").Append(Cell(i < series.Values.Count ? series.Values[i] : ""));
				sb.Append(" |");
			}

			return sb.ToString();
		}

		/// <inheritdoc/>
		protected override string FormatSmartArt(List<SmartArtNode> nodes)
		{
			var sb = new StringBuilder("**Diagram:**");

			void Write(List<SmartArtNode> level, int depth)
			{
				foreach (var node in level)
				{
					sb.Append('\n').Append(new string(' ', depth * 2)).Append("- ").Append(Escape(node.Text, InlineMode.Flat));
					Write(node.Children, depth + 1);
				}
			}

			Write(nodes, 0);
			return sb.ToString();
		}

		#endregion

		#region Escaping

		/// <summary>
		/// Escapes the characters that Markdown would interpret.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="mode">Where the text is written.</param>
		/// <returns>The escaped text.</returns>
		private static string Escape(string text, InlineMode mode)
		{
			if (String.IsNullOrEmpty(text))
				return text;

			var sb = new StringBuilder(text.Length + 8);

			for (var i = 0; i < text.Length; i++)
			{
				var c = text[i];
				var previous = i > 0 ? text[i - 1] : ' ';
				var next = i + 1 < text.Length ? text[i + 1] : ' ';

				switch (c)
				{
					case '\\':
					case '*':
					case '`':
						sb.Append('\\').Append(c);
						break;

					case '_':
						// An underscore inside a word (snake_case) cannot start emphasis.
						if (Char.IsLetterOrDigit(previous) && Char.IsLetterOrDigit(next))
							sb.Append(c);
						else
							sb.Append("\\_");
						break;

					case '~':
						sb.Append(next == '~' || previous == '~' ? "\\~" : "~");
						break;

					case '<':
						sb.Append(Char.IsLetter(next) || next == '/' || next == '!' || next == '?' ? "&lt;" : "<");
						break;

					case '|':
						sb.Append(mode == InlineMode.Cell ? "\\|" : "|");
						break;

					case '\r':
						break;

					case '\n':
						sb.Append(mode == InlineMode.Block ? "\n" : mode == InlineMode.Cell ? "<br>" : " ");
						break;

					default:
						sb.Append(c);
						break;
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Escapes the start of every line that Markdown would read as a heading, a quote or a list item.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The escaped text.</returns>
		private static string EscapeLineStarts(string text)
		{
			var lines = text.Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				lines[i] = BlockMarker.Replace(lines[i], @"$1\$2", 1);
				lines[i] = OrderedMarker.Replace(lines[i], @"$1\$2", 1);
			}

			return String.Join("\n", lines);
		}

		/// <summary>
		/// Formats a code span, choosing a fence longer than any backtick run in the text.
		/// </summary>
		/// <param name="text">The code.</param>
		/// <returns>The code span.</returns>
		private static string CodeSpan(string text)
		{
			var core = text.Trim();
			if (core.Length == 0)
				return text;

			var longest = 0;
			var run = 0;
			foreach (var c in core)
			{
				run = c == '`' ? run + 1 : 0;
				longest = Math.Max(longest, run);
			}

			var fence = new string('`', longest + 1);
			var padding = core.StartsWith("`") || core.EndsWith("`") ? " " : "";
			var lead = text.Substring(0, text.Length - text.TrimStart().Length);
			var trail = text.Substring(text.TrimEnd().Length);

			return lead + fence + padding + core + padding + fence + trail;
		}

		/// <summary>
		/// Formats the target of a link, encoding the characters that would end it.
		/// </summary>
		/// <param name="url">The target.</param>
		/// <returns>The formatted target.</returns>
		private static string FormatUrl(string url)
		{
			return url.Trim().Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29");
		}

		/// <summary>
		/// Escapes text for a pipe table cell.
		/// </summary>
		/// <param name="text">The text. Can be <c>null</c>.</param>
		/// <returns>The escaped text.</returns>
		private static string Cell(string text)
		{
			return Escape(Collapse(text ?? ""), InlineMode.Cell);
		}

		/// <summary>
		/// Collapses line breaks into spaces.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The text on a single line.</returns>
		private static string Collapse(string text)
		{
			return Regex.Replace(text, @"\s*\n\s*", " ").Trim();
		}

		/// <summary>
		/// Prefixes every line of a text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="first">The prefix of the first line.</param>
		/// <param name="others">The prefix of the following lines.</param>
		/// <returns>The prefixed text.</returns>
		private static string PrefixLines(string text, string first, string others)
		{
			var lines = text.Split('\n');
			var sb = new StringBuilder();

			for (var i = 0; i < lines.Length; i++)
			{
				if (i > 0)
					sb.Append('\n');

				sb.Append(i == 0 ? first : others).Append(lines[i]);
			}

			return sb.ToString();
		}

		#endregion

		#region Emphasis

		/// <summary>
		/// The emphasis markers of Markdown.
		/// </summary>
		private enum Emphasis
		{
			/// <summary>Strong emphasis (<c>**</c>).</summary>
			Bold,

			/// <summary>Emphasis (<c>*</c>).</summary>
			Italic,

			/// <summary>Strikethrough (<c>~~</c>).</summary>
			Strike,

			/// <summary>Superscript (<c>&lt;sup&gt;</c>).</summary>
			Superscript,

			/// <summary>Subscript (<c>&lt;sub&gt;</c>).</summary>
			Subscript,
		}

		#endregion
	}
}

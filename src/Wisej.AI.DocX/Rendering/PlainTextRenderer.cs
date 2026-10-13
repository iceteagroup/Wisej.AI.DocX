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
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// Renders the document as plain text.
	/// </summary>
	/// <remarks>
	/// The plain text keeps the structure that matters without markup: paragraphs separated by blank
	/// lines, list labels, indented list levels, tables as rows of pipe-separated cells, image and chart
	/// placeholders, and hyperlink targets in parentheses.
	/// </remarks>
	internal sealed class PlainTextRenderer : DocumentRenderer
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PlainTextRenderer"/> class.
		/// </summary>
		/// <param name="model">The paginated document model.</param>
		/// <param name="options">The extraction options.</param>
		public PlainTextRenderer(DocxModel model, DocxExtractorOptions options)
			: base(model, options, null)
		{
		}

		/// <inheritdoc/>
		protected override string DefaultPageHeaderFormat => "--- Page {0} ---";

		#region Paragraphs

		/// <inheritdoc/>
		protected override void WriteParagraph(ParagraphBlock paragraph, List<Span> spans, bool isContinuation)
		{
			var content = FormatSpans(spans, InlineMode.Block).Trim();
			if (content.Length == 0)
				return;

			var properties = paragraph.Properties;

			if (properties.Kind == ParagraphKind.TableOfContents)
			{
				Pages.AppendBlock(content.Replace('\n', ' '), OutputBlockKind.ListItem);
				return;
			}

			if (paragraph.List != null)
			{
				var indent = new string(' ', paragraph.List.Level * 2);
				var label = paragraph.List.IsBullet ? "•" : paragraph.List.Text;
				var hanging = indent + new string(' ', label.Length + 1);
				var first = isContinuation ? hanging : indent + label + " ";
				var isHeading = properties.Kind == ParagraphKind.Heading || properties.Kind == ParagraphKind.Title;

				Pages.AppendBlock(first + content.Replace("\n", "\n" + hanging), isHeading ? OutputBlockKind.Paragraph : OutputBlockKind.ListItem);
				return;
			}

			Pages.AppendBlock(content, OutputBlockKind.Paragraph);
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
				var span = spans[index];

				// Consecutive spans with the same hyperlink and revision are written together.
				var end = index + 1;
				while (end < spans.Count && spans[end].Link == span.Link && spans[end].Revision == span.Revision)
					end++;

				var text = String.Concat(spans.Skip(index).Take(end - index).Select(s => s.Text));
				if (mode != InlineMode.Block)
					text = text.Replace("\r", "").Replace('\n', ' ');

				if (span.Link != null && text.Trim().Length > 0 && !String.Equals(text.Trim(), span.Link, StringComparison.OrdinalIgnoreCase))
					text = text.TrimEnd() + " (" + span.Link + ")" + text.Substring(text.TrimEnd().Length);

				if (span.Revision == RevisionKind.Inserted)
					text = "{++" + text + "++}";
				else if (span.Revision == RevisionKind.Deleted)
					text = "{--" + text + "--}";

				sb.Append(text);
				index = end;
			}

			return sb.ToString();
		}

		/// <inheritdoc/>
		protected override Span FormatLineBreak(InlineMode mode, Inline inline)
		{
			return Markup(mode == InlineMode.Block ? "\n" : " ", inline);
		}

		#endregion

		#region Tables

		/// <inheritdoc/>
		protected override void WriteTable(TableGrid grid, List<TableGrid.GridRow> headerRows, List<TableGrid.GridRow> bodyRows, List<Action> deferred)
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

			// The first row is repeated on the following pages when the table declares no header rows.
			var header = headerRows.Count > 0 ? headerRows : new List<TableGrid.GridRow> { grid.Rows[0] };
			var body = headerRows.Count == 0 && bodyRows.Count > 0 && bodyRows[0] == grid.Rows[0] ? bodyRows.Skip(1) : bodyRows;

			var lines = header.Concat(body).Select(r => String.Join(" | ", r.Slots.Select(Text)).Trim());
			Pages.AppendBlock(String.Join("\n", lines), OutputBlockKind.Paragraph);
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
							var line = String.Join(" / ", row.Cells.Select(c => RenderCellText(c, deferred)).Where(t => t.Length > 0));
							if (line.Length > 0)
								parts.Add(line);
						}
						break;

					case RawTextBlock raw:
						parts.Add(raw.Text.Replace('\n', ' '));
						break;
				}
			}

			return String.Join(" ", parts).Replace("|", "/");
		}

		#endregion

		#region Elements

		/// <inheritdoc/>
		protected override string FormatImage(DocxImage image)
		{
			var alt = image.AltText ?? image.Title;
			return alt == null ? "[Image]" : "[Image: " + alt.Replace('\n', ' ') + "]";
		}

		/// <inheritdoc/>
		protected override string FormatImageDescription(DocxImage image)
		{
			return $"[{image.FileName}] " + image.Description.Trim();
		}

		/// <inheritdoc/>
		protected override string FormatInlineImageDescription(DocxImage image)
		{
			return "(" + image.Description.Trim().Replace('\n', ' ') + ")";
		}

		/// <inheritdoc/>
		protected override string FormatMath(string latex, bool display)
		{
			return latex;
		}

		/// <inheritdoc/>
		protected override string FormatNoteReference(string label)
		{
			return "[" + label + "]";
		}

		/// <inheritdoc/>
		protected override string FormatNoteDefinition(string label, string text)
		{
			return "[" + label + "] " + text;
		}

		/// <inheritdoc/>
		protected override string FormatComment(Comment comment, string text)
		{
			var sb = new StringBuilder("Comment");

			if (comment.Author != null)
				sb.Append(" by ").Append(comment.Author);

			if (comment.Date != null && comment.Date.Length >= 10)
				sb.Append(" (").Append(comment.Date.Substring(0, 10)).Append(')');

			if (comment.AnchorText != null)
				sb.Append(" on “").Append(comment.AnchorText.Replace('\n', ' ')).Append('”');

			return sb.Append(": ").Append(text).ToString();
		}

		/// <inheritdoc/>
		protected override string FormatHeaderFooter(bool isHeader, string text)
		{
			return (isHeader ? "Header: " : "Footer: ") + text;
		}

		/// <inheritdoc/>
		protected override string FormatChart(ChartData chart)
		{
			var sb = new StringBuilder("Chart");
			if (chart.Title != null)
				sb.Append(": ").Append(chart.Title);
			if (chart.ChartTypes.Count > 0)
				sb.Append(" (").Append(String.Join(" + ", chart.ChartTypes)).Append(" chart)");

			sb.Append('\n').Append(chart.CategoryLabel).Append(" | ").Append(String.Join(" | ", chart.Series.Select(s => s.Name)));

			for (var i = 0; i < chart.Categories.Count; i++)
			{
				sb.Append('\n').Append(chart.Categories[i]);
				foreach (var series in chart.Series)
					sb.Append(" | ").Append(i < series.Values.Count ? series.Values[i] : "");
			}

			return sb.ToString();
		}

		/// <inheritdoc/>
		protected override string FormatSmartArt(List<SmartArtNode> nodes)
		{
			var sb = new StringBuilder("Diagram:");

			void Write(List<SmartArtNode> level, int depth)
			{
				foreach (var node in level)
				{
					sb.Append('\n').Append(new string(' ', depth * 2)).Append("• ").Append(node.Text);
					Write(node.Children, depth + 1);
				}
			}

			Write(nodes, 0);
			return sb.ToString();
		}

		#endregion
	}
}

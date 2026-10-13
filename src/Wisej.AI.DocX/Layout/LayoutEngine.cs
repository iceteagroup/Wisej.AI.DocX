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
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Estimates where the pages of a document start by laying it out.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The engine works in two phases. <see cref="Measure"/> breaks every paragraph into lines and
	/// computes the height of every table row; it is the expensive part and runs once.
	/// <see cref="Place"/> then flows the measured content into pages following the rules Word applies:
	/// explicit and section breaks, keep with next, keep lines together, widow and orphan control,
	/// rows that cannot split, repeated header rows, text columns, odd and even section starts and the
	/// space taken by headers, footers and footnotes.
	/// </para>
	/// <para>
	/// <see cref="Place"/> accepts a vertical scale factor: the paginator uses it to calibrate the
	/// layout on the page count recorded in the document, compensating for the differences between
	/// the estimate and Word's own layout.
	/// </para>
	/// </remarks>
	internal sealed class LayoutEngine
	{
		/// <summary>Height of the separator line above the footnotes of a page, in points.</summary>
		private const double FootnoteSeparatorHeight = 12;

		/// <summary>Tolerance used when comparing heights, in points.</summary>
		private const double Epsilon = 0.01;

		/// <summary>
		/// Fraction of a page under which an estimated page start is replaced by a following page break
		/// marker saved by Word, instead of producing an additional page.
		/// </summary>
		private const double SoftStartMergeLimit = 0.6;

		private readonly DocxModel _model;
		private readonly TextMeasurer _measurer;
		private readonly Dictionary<string, double> _footnoteHeights = new Dictionary<string, double>(StringComparer.Ordinal);
		private readonly List<SectionFlow> _sections = new List<SectionFlow>();

		/// <summary>
		/// Initializes a new instance of the <see cref="LayoutEngine"/> class.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="fonts">The font catalog used to measure the text.</param>
		public LayoutEngine(DocxModel model, FontCatalog fonts)
		{
			_model = model;
			_measurer = new TextMeasurer(fonts, model.DefaultTabStop);
		}

		#region Measure

		/// <summary>
		/// Gets or sets whether <see cref="Place"/> records the position of every paragraph and table row
		/// in <see cref="LayoutResult.Positions"/>, to compare the estimate with Word's layout.
		/// </summary>
		public bool Trace { get; set; }

		/// <summary>
		/// Measures the content of every section.
		/// </summary>
		public void Measure()
		{
			_sections.Clear();

			foreach (var section in _model.Sections)
			{
				var flow = new SectionFlow { Section = section };
				var width = section.Page.ColumnWidth;

				foreach (var block in section.Blocks)
				{
					switch (block)
					{
						case ParagraphBlock paragraph:
							flow.Items.Add(new FlowItem { Paragraph = _measurer.Measure(paragraph, width, section.Page.GridLinePitch, GetFootnoteHeight) });
							break;

						case TableBlock table:
							flow.Items.Add(MeasureTable(table, width, section.Page.GridLinePitch));
							break;

						case RawTextBlock raw:
							flow.Items.Add(new FlowItem { Height = MeasureRawText(raw.Text, width) });
							break;
					}
				}

				ApplyContextualSpacing(flow.Items);
				MeasureHeadersAndFooters(flow);

				_sections.Add(flow);
			}
		}

		/// <summary>
		/// Removes the spacing between consecutive paragraphs of the same style that use contextual spacing.
		/// </summary>
		/// <param name="items">The measured items of a section or a cell.</param>
		private static void ApplyContextualSpacing(List<FlowItem> items)
		{
			for (var i = 1; i < items.Count; i++)
			{
				var previous = items[i - 1].Paragraph;
				var current = items[i].Paragraph;
				if (previous == null || current == null)
					continue;

				if (previous.Paragraph.Properties.StyleId != current.Paragraph.Properties.StyleId)
					continue;

				if (current.Paragraph.Properties.ContextualSpacing)
					current.SpaceBefore = 0;

				if (previous.Paragraph.Properties.ContextualSpacing)
					previous.SpaceAfter = 0;
			}
		}

		/// <summary>
		/// Measures the rows of a table.
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="width">The available width, in points.</param>
		/// <param name="gridPitch">The pitch of the document grid lines, or 0.</param>
		/// <returns>The measured table item.</returns>
		private FlowItem MeasureTable(TableBlock table, double width, double gridPitch)
		{
			var columns = GetColumnWidths(table, width);
			var rows = new List<RowLayout>();

			// Vertically merged cells: content height by grid column, with the row where the merge started.
			var merges = new Dictionary<int, (int StartRow, double Height)>();

			for (var r = 0; r < table.Rows.Count; r++)
			{
				var row = table.Rows[r];
				var column = row.GridBefore;
				var height = 0.0;
				var firstLine = 0.0;

				foreach (var cell in row.Cells)
				{
					var cellWidth = 0.0;
					for (var c = column; c < column + cell.GridSpan; c++)
						cellWidth += c < columns.Count ? columns[c] : 36;

					var contentWidth = Math.Max(12, cellWidth - table.CellMarginLeft - table.CellMarginRight);

					if (cell.VerticalMerge == VerticalMerge.Continue)
					{
						column += cell.GridSpan;
						continue;
					}

					var content = MeasureCell(cell.Blocks, contentWidth, gridPitch, out var cellFirstLine);
					firstLine = Math.Max(firstLine, cellFirstLine);

					if (cell.VerticalMerge == VerticalMerge.Restart)
						merges[column] = (r, content);
					else
						height = Math.Max(height, content);

					column += cell.GridSpan;
				}

				height += table.CellMarginTop + table.CellMarginBottom + (table.HasBorders ? 0.75 : 0);

				if (row.Height > 0)
					height = row.HeightIsExact ? row.Height : Math.Max(height, row.Height);

				ContentHelper.FindRenderedBreaks(row, out var breakBefore, out var breakAfter);

				rows.Add(new RowLayout
				{
					Row = row,
					Height = height,
					MinimumSplitHeight = Math.Min(height, firstLine + table.CellMarginTop),
					RenderedBreakBefore = breakBefore,
					RenderedBreakAfter = breakAfter,
				});
			}

			// A merged cell taller than the rows it spans enlarges the last of them.
			foreach (var pair in merges)
			{
				var start = pair.Value.StartRow;
				var end = start;

				while (end + 1 < table.Rows.Count && GetCellAt(table.Rows[end + 1], pair.Key)?.VerticalMerge == VerticalMerge.Continue)
					end++;

				var spanned = 0.0;
				for (var i = start; i <= end; i++)
					spanned += rows[i].Height;

				var missing = pair.Value.Height + table.CellMarginTop + table.CellMarginBottom - spanned;
				if (missing > 0)
					rows[end].Height += missing;
			}

			var headerHeight = 0.0;
			foreach (var row in rows)
			{
				if (!row.Row.IsHeader)
					break;

				headerHeight += row.Height;
			}

			return new FlowItem { Rows = rows, HeaderRowsHeight = headerHeight };
		}

		/// <summary>
		/// Measures the content of a table cell.
		/// </summary>
		/// <param name="blocks">The blocks of the cell.</param>
		/// <param name="width">The content width of the cell, in points.</param>
		/// <param name="gridPitch">The pitch of the document grid lines, or 0.</param>
		/// <param name="firstLine">Returns the height of the first line of the cell.</param>
		/// <returns>The height of the content, in points.</returns>
		private double MeasureCell(List<Block> blocks, double width, double gridPitch, out double firstLine)
		{
			var items = new List<FlowItem>();
			firstLine = 0;

			foreach (var block in blocks)
			{
				switch (block)
				{
					case ParagraphBlock paragraph:
						items.Add(new FlowItem { Paragraph = _measurer.Measure(paragraph, width, gridPitch, null) });
						break;

					case TableBlock nested:
						var item = MeasureTable(nested, width, gridPitch);
						items.Add(new FlowItem { Height = item.Rows.Sum(r => r.Height) });
						break;

					case RawTextBlock raw:
						items.Add(new FlowItem { Height = MeasureRawText(raw.Text, width) });
						break;
				}
			}

			ApplyContextualSpacing(items);

			var height = 0.0;
			foreach (var item in items)
			{
				var itemHeight = item.Paragraph?.TotalHeight ?? item.Height;
				if (firstLine == 0)
					firstLine = item.Paragraph != null && item.Paragraph.Lines.Count > 0 ? item.Paragraph.SpaceBefore + item.Paragraph.Lines[0].Height : itemHeight;

				height += itemHeight;
			}

			return height;
		}

		/// <summary>
		/// Computes the widths of the grid columns of a table, fitted to the available width.
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="width">The available width, in points.</param>
		/// <returns>The widths of the grid columns, in points.</returns>
		private static List<double> GetColumnWidths(TableBlock table, double width)
		{
			var count = Math.Max(1, table.ColumnCount);
			var columns = table.Grid.Where(w => w > 0).Count() == table.Grid.Count && table.Grid.Count > 0
				? new List<double>(table.Grid)
				: Enumerable.Repeat(width / count, count).ToList();

			while (columns.Count < count)
				columns.Add(columns.Count > 0 ? columns.Average() : width / count);

			var total = columns.Sum();
			var target = table.PreferredWidthPercent > 0
				? width * table.PreferredWidthPercent
				: table.PreferredWidth > 0 ? table.PreferredWidth : total;

			// Tables wider than the page are shrunk to fit, the way Word's autofit does.
			target = Math.Min(target, width * 1.02);

			if (total > 0 && Math.Abs(total - target) > 1)
			{
				var scale = target / total;
				for (var i = 0; i < columns.Count; i++)
					columns[i] *= scale;
			}

			return columns;
		}

		/// <summary>
		/// Returns the cell of a row that starts at a grid column.
		/// </summary>
		/// <param name="row">The row.</param>
		/// <param name="column">The grid column.</param>
		/// <returns>The cell, or <c>null</c>.</returns>
		private static TableCell GetCellAt(TableRow row, int column)
		{
			var current = row.GridBefore;
			foreach (var cell in row.Cells)
			{
				if (current == column)
					return cell;

				current += cell.GridSpan;
				if (current > column)
					return null;
			}

			return null;
		}

		/// <summary>
		/// Estimates the height of preformatted text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="width">The available width, in points.</param>
		/// <returns>The height, in points.</returns>
		private static double MeasureRawText(string text, double width)
		{
			const double fontSize = 11;
			const double lineHeight = fontSize * 1.22;

			var charactersPerLine = Math.Max(10, width / (fontSize * 0.5));
			var lines = 0.0;

			foreach (var line in text.Split('\n'))
				lines += Math.Max(1, Math.Ceiling(line.Length / charactersPerLine));

			return lines * lineHeight;
		}

		/// <summary>
		/// Measures the headers and footers of a section and derives the height available to the body.
		/// </summary>
		/// <param name="flow">The section flow to update.</param>
		private void MeasureHeadersAndFooters(SectionFlow flow)
		{
			var section = flow.Section;
			var page = section.Page;

			(double Top, double Height) Body(HeaderFooterKind kind)
			{
				var header = GetStory(section.Headers, kind, section.TitlePage);
				var footer = GetStory(section.Footers, kind, section.TitlePage);

				var top = Math.Max(page.MarginTop, page.HeaderDistance + MeasureStory(header, page.TextWidth));
				var bottom = Math.Max(page.MarginBottom, page.FooterDistance + MeasureStory(footer, page.TextWidth));

				return (top, Math.Max(72, page.Height - top - bottom));
			}

			(flow.FirstPageTop, flow.FirstPageHeight) = Body(HeaderFooterKind.First);
			(flow.OddPageTop, flow.OddPageHeight) = Body(HeaderFooterKind.Default);
			(flow.EvenPageTop, flow.EvenPageHeight) = _model.EvenAndOddHeaders ? Body(HeaderFooterKind.Even) : (flow.OddPageTop, flow.OddPageHeight);
		}

		/// <summary>
		/// Returns the header or footer used on a kind of page.
		/// </summary>
		/// <param name="stories">The headers or footers of the section.</param>
		/// <param name="kind">The kind of page.</param>
		/// <param name="titlePage">Whether the section has a distinct first page.</param>
		/// <returns>The story, or <c>null</c>.</returns>
		private HeaderFooter GetStory(Dictionary<HeaderFooterKind, HeaderFooter> stories, HeaderFooterKind kind, bool titlePage)
		{
			if (kind == HeaderFooterKind.First && !titlePage)
				kind = HeaderFooterKind.Default;

			if (kind == HeaderFooterKind.Even && !_model.EvenAndOddHeaders)
				kind = HeaderFooterKind.Default;

			return stories.TryGetValue(kind, out var story) ? story : null;
		}

		/// <summary>
		/// Measures the height of a header or footer.
		/// </summary>
		/// <param name="story">The story. Can be <c>null</c>.</param>
		/// <param name="width">The available width, in points.</param>
		/// <returns>The height, in points.</returns>
		private double MeasureStory(HeaderFooter story, double width)
		{
			if (story == null)
				return 0;

			var height = MeasureCell(story.Blocks, width, 0, out _);

			// The last paragraph's space after does not push the body.
			var last = story.Blocks.LastOrDefault() as ParagraphBlock;
			return Math.Max(0, height - (last?.Properties.SpacingAfter ?? 0));
		}

		/// <summary>
		/// Returns the height of a footnote, measuring it on first use.
		/// </summary>
		/// <param name="reference">The reference to the footnote.</param>
		/// <returns>The height of the footnote, in points.</returns>
		private double GetFootnoteHeight(NoteReferenceInline reference)
		{
			if (reference.NoteId == null || !_model.Footnotes.TryGetValue(reference.NoteId, out var note))
				return 0;

			if (!_footnoteHeights.TryGetValue(note.Id, out var height))
			{
				var width = _model.Sections.Count > 0 ? _model.Sections[0].Page.ColumnWidth : 468;
				height = MeasureCell(note.Blocks, width, 0, out _);
				_footnoteHeights[note.Id] = height;
			}

			return height;
		}

		#endregion

		#region Place

		/// <summary>
		/// Flows the measured content into pages.
		/// </summary>
		/// <param name="scale">The vertical scale applied to every height; 1 for the plain estimate.</param>
		/// <param name="useAnchors">
		/// Whether the page break markers saved by Word are honored as page starts. The estimate then only
		/// adds the pages Word did not mark, such as those inside long field results or content controls.
		/// </param>
		/// <returns>The page starts and the page count.</returns>
		public LayoutResult Place(double scale, bool useAnchors = false)
		{
			var placer = new Placer(scale, useAnchors);
			if (Trace)
				placer.Result.Positions = new Dictionary<object, (int Page, double Y)>();

			for (var s = 0; s < _sections.Count; s++)
			{
				var flow = _sections[s];
				placer.BeginSection(flow, s == 0);

				var items = flow.Items;
				var keepGroupEnd = -1;

				for (var i = 0; i < items.Count; i++)
				{
					var item = items[i];

					if (item.Paragraph != null)
					{
						if (i > keepGroupEnd && item.Paragraph.Paragraph.Properties.KeepNext)
							keepGroupEnd = ApplyKeepWithNext(placer, items, i);

						placer.PlaceParagraph(item.Paragraph);
					}
					else if (item.IsTable)
					{
						placer.PlaceTable(item);
					}
					else
					{
						placer.PlaceBlock(item.Height);
					}
				}

				placer.EndSection(s + 1 < _sections.Count ? _sections[s + 1].Section : null);
			}

			return placer.Result;
		}

		/// <summary>
		/// Starts a new page before a chain of paragraphs that must stay with the next block, when the
		/// chain does not fit in the space left on the current page.
		/// </summary>
		/// <param name="placer">The placer.</param>
		/// <param name="items">The items of the section.</param>
		/// <param name="start">The index of the first paragraph of the chain.</param>
		/// <returns>The index of the last item of the chain.</returns>
		private static int ApplyKeepWithNext(Placer placer, List<FlowItem> items, int start)
		{
			var end = start;
			var height = 0.0;

			while (end < items.Count && items[end].Paragraph != null && items[end].Paragraph.Paragraph.Properties.KeepNext)
			{
				height += items[end].Paragraph.TotalHeight;
				end++;
			}

			// The chain stays with the first lines of the following block.
			if (end < items.Count)
			{
				var next = items[end];
				if (next.Paragraph != null)
				{
					var lines = next.Paragraph.Lines;
					height += next.Paragraph.SpaceBefore + lines.Take(lines.Count > 1 && next.Paragraph.Paragraph.Properties.WidowControl ? 2 : 1).Sum(l => l.Height);
				}
				else if (next.IsTable && next.Rows.Count > 0)
				{
					height += next.Rows[0].Height;
				}
			}

			placer.EnsureSpace(height);
			return end;
		}

		#endregion

		#region SectionFlow

		/// <summary>
		/// The measured content of a section.
		/// </summary>
		private sealed class SectionFlow
		{
			/// <summary>Gets or sets the section.</summary>
			public Section Section { get; set; }

			/// <summary>Gets the measured items.</summary>
			public List<FlowItem> Items { get; } = new List<FlowItem>();

			/// <summary>Gets or sets the body height of the first page of the section.</summary>
			public double FirstPageHeight { get; set; }

			/// <summary>Gets or sets the body height of odd pages.</summary>
			public double OddPageHeight { get; set; }

			/// <summary>Gets or sets the body height of even pages.</summary>
			public double EvenPageHeight { get; set; }

			/// <summary>Gets or sets the top of the body of the first page of the section.</summary>
			public double FirstPageTop { get; set; }

			/// <summary>Gets or sets the top of the body of odd pages.</summary>
			public double OddPageTop { get; set; }

			/// <summary>Gets or sets the top of the body of even pages.</summary>
			public double EvenPageTop { get; set; }
		}

		#endregion

		#region Placer

		/// <summary>
		/// The state machine that flows measured items into pages and columns.
		/// </summary>
		private sealed class Placer
		{
			private readonly double _scale;
			private readonly bool _useAnchors;

			private SectionFlow _flow;
			private int _page = 1;
			private int _sectionFirstPage = 1;
			private double _y;
			private double _height;
			private int _columns = 1;
			private int _column;
			private double _columnTop;
			private double _columnContent;
			private bool _atTop = true;
			private bool _hardStart = true;
			private bool _pending;
			private double _footnotes;
			private ContentPosition? _pendingPosition;
			private bool _softStart;
			private bool _anchorBreak;
			private int _softStartIndex = -1;

			/// <summary>
			/// Initializes a new instance of the <see cref="Placer"/> class.
			/// </summary>
			/// <param name="scale">The vertical scale applied to every height.</param>
			/// <param name="useAnchors">Whether the page break markers saved by Word start pages.</param>
			public Placer(double scale, bool useAnchors)
			{
				_scale = scale;
				_useAnchors = useAnchors;
			}

			/// <summary>Gets the result of the placement.</summary>
			public LayoutResult Result { get; } = new LayoutResult();

			/// <summary>Gets the height available on the current page, net of the footnotes.</summary>
			private double Available => _height - _footnotes;

			#region Sections

			/// <summary>
			/// Starts placing a section.
			/// </summary>
			/// <param name="flow">The measured section.</param>
			/// <param name="isFirst">Whether this is the first section of the document.</param>
			public void BeginSection(SectionFlow flow, bool isFirst)
			{
				var previous = _flow;
				_flow = flow;

				var start = isFirst ? SectionStart.Continuous : flow.Section.Start;

				switch (start)
				{
					case SectionStart.Continuous:
						BalanceColumns(previous);
						break;

					case SectionStart.NextColumn:
						if (_columns > 1)
							NextColumn();
						else
							NewPage(hard: true);
						break;

					default:
						if (!_atTop)
							NewPage(hard: true);

						// Odd and even sections may need a blank page to start on the right side.
						if ((start == SectionStart.OddPage && _page % 2 == 0) || (start == SectionStart.EvenPage && _page % 2 == 1))
						{
							_page++;
							_pending = true;
						}

						_sectionFirstPage = _page;
						_hardStart = true;
						break;
				}

				if (isFirst)
					_sectionFirstPage = 1;

				_columns = Math.Max(1, flow.Section.Page.Columns);
				_column = 0;
				_columnTop = _y;
				_columnContent = 0;

				_height = PageHeight();
				Result.PageCount = _page;
			}

			/// <summary>
			/// Completes the placement of a section.
			/// </summary>
			/// <param name="next">The next section, or <c>null</c> at the end of the document.</param>
			public void EndSection(Section next)
			{
				Result.PageCount = _page;
			}

			/// <summary>
			/// Balances the columns of a multi-column section followed by a continuous section.
			/// </summary>
			/// <param name="previous">The previous section, or <c>null</c>.</param>
			private void BalanceColumns(SectionFlow previous)
			{
				if (previous == null || _columns <= 1)
					return;

				_y = _columnTop + _columnContent / _columns;
				_column = 0;
			}

			#endregion

			#region Items

			/// <summary>
			/// Starts a new page unless <paramref name="height"/> fits in the space left on the current page.
			/// </summary>
			/// <param name="height">The height to keep together, unscaled.</param>
			public void EnsureSpace(double height)
			{
				height *= _scale;

				if (!_atTop && _y + height > Available + Epsilon && height <= _height * 0.95)
					NewColumnOrPage();
			}

			/// <summary>
			/// Places a paragraph.
			/// </summary>
			/// <param name="layout">The measured paragraph.</param>
			public void PlaceParagraph(ParagraphLayout layout)
			{
				var properties = layout.Paragraph.Properties;

				if (properties.PageBreakBefore && !_atTop)
					NewPage(hard: true);

				if (_useAnchors && layout.Lines.Count > 0 && layout.Lines[0].RenderedBreak.HasValue && !_atTop)
					BreakAtAnchor(layout.Lines[0].RenderedBreak.Value);

				// Word suppresses the space before a paragraph at the top of a page that follows a natural break.
				var before = _atTop && !_hardStart ? 0 : layout.SpaceBefore * _scale;
				var extra = layout.ExtraHeight * _scale;
				var lines = layout.Lines;

				Advance(before);

				var i = 0;
				while (i < lines.Count)
				{
					// Word's own page break markers start a page in the anchored layout.
					if (_useAnchors && i > 0 && lines[i].RenderedBreak.HasValue && !_atTop)
						BreakAtAnchor(lines[i].RenderedBreak.Value);

					var segmentEnd = i;
					while (segmentEnd < lines.Count - 1 && !lines[segmentEnd].EndsWithPageBreak && !lines[segmentEnd].EndsWithColumnBreak
						&& !(_useAnchors && lines[segmentEnd + 1].RenderedBreak.HasValue))
						segmentEnd++;

					var fit = CountFittingLines(lines, i, segmentEnd, i == 0 ? extra : 0);
					var count = segmentEnd - i + 1;

					if (fit < count)
						fit = ApplyLineRules(properties, lines, i, count, fit);

					// Lines that fit are placed; the rest continue on the next page.
					for (var k = 0; k < fit; k++)
					{
						if (i + k == 0)
							RecordPosition(layout.Paragraph);

						PlaceLine(lines[i + k], i + k == 0 ? extra : 0);
					}

					i += fit;

					if (fit < count)
					{
						NewColumnOrPage();
						continue;
					}

					var last = lines[i - 1];
					if (last.EndsWithPageBreak)
						NewPage(hard: true);
					else if (last.EndsWithColumnBreak)
						NewColumnOrPage();
				}

				Advance(layout.SpaceAfter * _scale);
				if (_y > Available)
					_y = Available;
			}

			/// <summary>
			/// Places a table.
			/// </summary>
			/// <param name="item">The measured table.</param>
			public void PlaceTable(FlowItem item)
			{
				var headerHeight = item.HeaderRowsHeight * _scale;
				var breakBeforeNext = false;

				for (var r = 0; r < item.Rows.Count; r++)
				{
					var row = item.Rows[r];
					var height = row.Height * _scale;

					// Word's own page break markers start a page in the anchored layout.
					if (_useAnchors && (row.RenderedBreakBefore || breakBeforeNext) && !_atTop)
					{
						NewColumnOrPage();
						if (!row.Row.IsHeader)
							Advance(headerHeight);
					}

					breakBeforeNext = row.RenderedBreakAfter;

					if (_y + height > Available + Epsilon)
					{
						var canSplit = !row.Row.CantSplit && Available - _y >= row.MinimumSplitHeight * _scale + 6;

						if (_atTop || canSplit)
						{
							// The row starts on this page and continues on the next ones.
							MarkRow(row.Row);
							RecordPosition(row.Row);
							height -= Available - _y;

							while (height > 0)
							{
								NewColumnOrPage();
								Advance(row.Row.IsHeader ? 0 : headerHeight);

								var room = Available - _y;
								if (height <= room)
									break;

								height -= room;
								if (room <= 0)
									break;
							}

							Advance(height);
							_atTop = false;
							continue;
						}

						NewColumnOrPage();
						if (!row.Row.IsHeader)
							Advance(headerHeight);
					}

					MarkRow(row.Row);
					RecordPosition(row.Row);
					Advance(height);
					_atTop = false;
				}
			}

			/// <summary>
			/// Places a block that cannot be split, such as preformatted text.
			/// </summary>
			/// <param name="height">The height of the block, unscaled.</param>
			public void PlaceBlock(double height)
			{
				height *= _scale;

				if (_y + height > Available + Epsilon && !_atTop)
					NewColumnOrPage();

				Advance(height);
				_atTop = false;
			}

			/// <summary>
			/// Counts the lines of a segment that fit in the space left on the page.
			/// </summary>
			/// <param name="lines">The lines of the paragraph.</param>
			/// <param name="start">The first line of the segment.</param>
			/// <param name="end">The last line of the segment.</param>
			/// <param name="extra">Extra height placed with the first line.</param>
			/// <returns>The number of lines that fit.</returns>
			private int CountFittingLines(List<LineBox> lines, int start, int end, double extra)
			{
				var y = _y;
				var footnotes = _footnotes;
				var count = 0;

				for (var i = start; i <= end; i++)
				{
					var line = lines[i];
					var noteHeight = line.FootnoteHeight > 0 ? (line.FootnoteHeight + (footnotes == 0 ? FootnoteSeparatorHeight : 0)) * _scale : 0;

					y += line.Height * _scale + (i == start ? extra : 0);
					footnotes += noteHeight;

					if (y > _height - footnotes + Epsilon)
						break;

					count++;
				}

				return count;
			}

			/// <summary>
			/// Adjusts the number of lines left on a page according to the keep lines together,
			/// widow and orphan control rules.
			/// </summary>
			/// <param name="properties">The paragraph properties.</param>
			/// <param name="lines">The lines of the paragraph.</param>
			/// <param name="start">The first line of the segment.</param>
			/// <param name="count">The number of lines of the segment.</param>
			/// <param name="fit">The number of lines that fit.</param>
			/// <returns>The number of lines to place on the current page.</returns>
			private int ApplyLineRules(ParagraphProperties properties, List<LineBox> lines, int start, int count, int fit)
			{
				var placed = fit;

				if (properties.KeepLines && start == 0 && lines.Sum(l => l.Height) * _scale <= _height)
				{
					placed = 0;
				}
				else if (properties.WidowControl)
				{
					// Orphan: the first line alone at the bottom of the page.
					if (placed == 1 && start == 0 && count > 1)
						placed = 0;

					// Widow: the last line alone at the top of the next page.
					if (count - placed == 1 && placed >= 2)
						placed--;
					else if (count - placed == 1 && placed == 1)
						placed = 0;
				}

				// Content that does not fit an empty page is placed anyway, at least one line at a time.
				if (placed == 0 && _atTop)
					placed = Math.Max(1, fit);

				return placed;
			}

			/// <summary>
			/// Places a line and records the page start when it is the first content of a page.
			/// </summary>
			/// <param name="line">The line.</param>
			/// <param name="extra">Extra height placed with the line.</param>
			private void PlaceLine(LineBox line, double extra)
			{
				if (_pending)
				{
					Result.Starts.Add(new PageStart(_pendingPosition ?? line.Start, null, _page));
					_softStartIndex = _softStart ? Result.Starts.Count - 1 : -1;
					_pending = false;
					_pendingPosition = null;
				}

				if (line.FootnoteHeight > 0)
					_footnotes += (line.FootnoteHeight + (_footnotes == 0 ? FootnoteSeparatorHeight : 0)) * _scale;

				Advance(line.Height * _scale + extra);
				_atTop = false;
			}

			/// <summary>
			/// Records the page and the vertical position of a paragraph or a row, when tracing.
			/// </summary>
			/// <param name="item">The paragraph or the table row.</param>
			private void RecordPosition(object item)
			{
				if (Result.Positions != null && !Result.Positions.ContainsKey(item))
					Result.Positions[item] = (_page, PageTop() + _y);
			}

			/// <summary>
			/// Records the page start at a table row when the row is the first content of a page.
			/// </summary>
			/// <param name="row">The row.</param>
			private void MarkRow(TableRow row)
			{
				if (_pending)
				{
					Result.Starts.Add(new PageStart(default, row, _page));
					_softStartIndex = _softStart ? Result.Starts.Count - 1 : -1;
					_pending = false;
					_pendingPosition = null;
				}
			}

			#endregion

			#region Pages

			/// <summary>
			/// Starts a page or a column at a page break marker saved by Word.
			/// </summary>
			/// <param name="position">The position of the marker, where the new page starts.</param>
			private void BreakAtAnchor(ContentPosition position)
			{
				// The estimate started this page shortly before the marker: the content in between still fits the
				// previous page in Word's layout, so the marker replaces the estimated page start.
				if (_softStart && _columns == 1 && _y < _height * SoftStartMergeLimit)
				{
					if (_softStartIndex >= 0 && _softStartIndex == Result.Starts.Count - 1)
						Result.Starts.RemoveAt(_softStartIndex);

					_y = 0;
					_footnotes = 0;
					_atTop = true;
					_hardStart = false;
					_softStart = false;
					_softStartIndex = -1;
					_pending = true;
					_pendingPosition = position;
					return;
				}

				_anchorBreak = true;
				NewColumnOrPage();
				_anchorBreak = false;

				if (_pending)
					_pendingPosition = position;
			}

			/// <summary>
			/// Moves down on the current page or column.
			/// </summary>
			/// <param name="height">The scaled height.</param>
			private void Advance(double height)
			{
				_y += height;
				_columnContent += height;
			}

			/// <summary>
			/// Moves to the next column, or to the next page after the last column.
			/// </summary>
			private void NewColumnOrPage()
			{
				if (_columns > 1 && _column < _columns - 1)
					NextColumn();
				else
					NewPage(hard: false);
			}

			/// <summary>
			/// Moves to the next column of the current page.
			/// </summary>
			private void NextColumn()
			{
				_column++;
				_y = _columnTop;
				_atTop = true;
				_hardStart = false;
			}

			/// <summary>
			/// Starts a new page.
			/// </summary>
			/// <param name="hard">Whether the page starts because of an explicit break.</param>
			private void NewPage(bool hard)
			{
				_page++;
				_y = 0;
				_column = 0;
				_columnTop = 0;
				_columnContent = 0;
				_footnotes = 0;
				_atTop = true;
				_hardStart = hard;
				_pending = true;
				_softStart = !hard && !_anchorBreak;
				_softStartIndex = -1;
				_height = PageHeight();

				Result.PageCount = _page;
			}

			/// <summary>
			/// Returns the top of the body of the current page, which depends on its header.
			/// </summary>
			/// <returns>The distance from the top edge of the page, in points.</returns>
			private double PageTop()
			{
				if (_page == _sectionFirstPage)
					return _flow.FirstPageTop;

				return _page % 2 == 0 ? _flow.EvenPageTop : _flow.OddPageTop;
			}

			/// <summary>
			/// Returns the body height of the current page, which depends on its header and footer.
			/// </summary>
			/// <returns>The body height, in points.</returns>
			private double PageHeight()
			{
				if (_page == _sectionFirstPage)
					return _flow.FirstPageHeight;

				return _page % 2 == 0 ? _flow.EvenPageHeight : _flow.OddPageHeight;
			}

			#endregion
		}

		#endregion
	}

	/// <summary>
	/// The result of a layout pass.
	/// </summary>
	internal sealed class LayoutResult
	{
		/// <summary>Gets the positions where pages start, in document order.</summary>
		public List<PageStart> Starts { get; } = new List<PageStart>();

		/// <summary>Gets or sets the number of pages.</summary>
		public int PageCount { get; set; } = 1;

		/// <summary>
		/// Gets or sets the page and the vertical position from the top edge of the page of every top level
		/// paragraph and table row, when <see cref="LayoutEngine.Trace"/> is enabled; otherwise <c>null</c>.
		/// </summary>
		public Dictionary<object, (int Page, double Y)> Positions { get; set; }
	}

	/// <summary>
	/// The position where a page starts: inside a paragraph or at a table row.
	/// </summary>
	internal struct PageStart
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PageStart"/> structure.
		/// </summary>
		/// <param name="position">The position in a paragraph; ignored when <paramref name="row"/> is set.</param>
		/// <param name="row">The table row that starts the page, or <c>null</c>.</param>
		/// <param name="pageNumber">The number of the page.</param>
		public PageStart(ContentPosition position, TableRow row, int pageNumber)
		{
			Position = position;
			Row = row;
			PageNumber = pageNumber;
		}

		/// <summary>Gets the position in a paragraph.</summary>
		public ContentPosition Position { get; }

		/// <summary>Gets the table row that starts the page, or <c>null</c>.</summary>
		public TableRow Row { get; }

		/// <summary>Gets the number of the page.</summary>
		public int PageNumber { get; }
	}
}

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
using System.Linq;
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Determines where the pages of a document start and marks them in the model.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A DOCX file does not store pages. When Word saves a document it has laid out, it writes a
	/// <c>w:lastRenderedPageBreak</c> marker where every page started and the page count in
	/// <c>docProps/app.xml</c>. When those are present and consistent they give Word's exact
	/// pagination. Otherwise the <see cref="LayoutEngine"/> estimates the layout; when the recorded
	/// page count is plausible, the estimate is calibrated so that it produces the same number of
	/// pages, which distributes the residual error evenly across the document.
	/// </para>
	/// <para>
	/// The result is written into the model as <see cref="PageStartInline"/> markers in paragraphs and
	/// as <see cref="TableRow.PageStart"/> on table rows, which is all the renderers need.
	/// </para>
	/// </remarks>
	internal static class Paginator
	{
		/// <summary>
		/// Paginates the document.
		/// </summary>
		/// <param name="model">The document model to update.</param>
		/// <param name="options">The extraction options.</param>
		public static void Paginate(DocxModel model, DocxExtractorOptions options)
		{
			switch (options.Pagination)
			{
				case PaginationMode.None:
					model.PageCount = 1;
					model.PaginationSource = PaginationSource.None;
					return;

				case PaginationMode.ExplicitBreaks:
					Apply(model, WalkBreaks(model, useRenderedBreaks: false), PaginationSource.ExplicitBreaks);
					return;

				case PaginationMode.RenderedBreaks:
					Apply(model, WalkBreaks(model, useRenderedBreaks: true), PaginationSource.RenderedBreaks);
					return;

				case PaginationMode.Estimate:
					var estimate = Estimate(model, options, null);
					Apply(model, estimate, PaginationSource.Estimate);
					return;

				default:
					PaginateAuto(model, options);
					return;
			}
		}

		#region Auto

		/// <summary>
		/// Chooses the most reliable pagination available for the document.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="options">The extraction options.</param>
		private static void PaginateAuto(DocxModel model, DocxExtractorOptions options)
		{
			// A document that asks Word to update its fields when opened (a table of contents, for example)
			// can change length when Word opens it: the saved markers and page count are not reliable.
			var reported = model.UpdateFieldsOnOpen ? null : model.Metadata.ReportedPageCount;
			var hasMarkers = model.RenderedPageBreakCount > 0 && !model.UpdateFieldsOnOpen;
			var rendered = WalkBreaks(model, useRenderedBreaks: hasMarkers);

			LayoutEngine engine = null;
			LayoutResult plain = null;

			if (hasMarkers)
			{
				plain = Estimate(model, options, null, out engine);

				// Markers and page count saved together by Word agree, unless another application changed the
				// content afterwards. A content far longer or shorter than the markers say reveals such a change.
				if (!IsConsistentWithEstimate(rendered.PageCount, plain.PageCount))
				{
					hasMarkers = false;
					reported = null;
				}
				else if (reported == null || reported <= 0 || rendered.PageCount == reported.Value)
				{
					Apply(model, rendered, PaginationSource.RenderedBreaks);
					return;
				}
			}
			else if (reported == 1 && rendered.PageCount == 1)
			{
				// Word writes no markers in a single page document; make sure the content really fits one page.
				plain = Estimate(model, options, null, out engine);
				if (plain.PageCount <= 2)
				{
					Apply(model, rendered, PaginationSource.RenderedBreaks);
					return;
				}
			}

			if (plain == null)
				plain = Estimate(model, options, null, out engine);

			var explicitPages = model.RenderedPageBreakCount > 0 ? WalkBreaks(model, useRenderedBreaks: false).PageCount : rendered.PageCount;

			// Word always writes markers in a document with more than one page: without them the recorded
			// page count comes from another application, or from a previous version of the content, and
			// calibrating on it does more harm than good.
			if (hasMarkers && reported > 0 && IsPlausible(reported.Value, plain.PageCount, explicitPages))
			{
				// Word does not write markers inside some content (field results such as a table of contents,
				// content controls): the markers it wrote are kept as anchors and the estimate adds the rest.
				if (hasMarkers && reported.Value > rendered.PageCount)
				{
					var anchored = Calibrate(engine, reported.Value, useAnchors: true);
					if (anchored.PageCount == reported.Value)
					{
						Apply(model, anchored, PaginationSource.CalibratedEstimate);
						return;
					}
				}

				var calibrated = Calibrate(engine, reported.Value, useAnchors: false);
				if (calibrated.PageCount == reported.Value)
				{
					Apply(model, calibrated, PaginationSource.CalibratedEstimate);
					return;
				}
			}

			Apply(model, plain, PaginationSource.Estimate);
		}

		/// <summary>
		/// Determines whether the page count given by the markers saved by Word is compatible with the
		/// estimated layout. A large difference means that the content changed after Word saved it.
		/// </summary>
		/// <param name="rendered">The page count given by the markers.</param>
		/// <param name="estimated">The page count of the plain estimate.</param>
		/// <returns><c>true</c> when the markers can be trusted.</returns>
		private static bool IsConsistentWithEstimate(int rendered, int estimated)
		{
			return estimated <= rendered * 2 + 2 && rendered <= estimated * 2 + 2;
		}

		/// <summary>
		/// Determines whether the recorded page count is plausible for the document, so that it can be
		/// used to calibrate the estimate. Templates and generators often leave a stale count behind.
		/// </summary>
		/// <param name="reported">The recorded page count.</param>
		/// <param name="estimated">The page count of the plain estimate.</param>
		/// <param name="explicitPages">The minimum page count implied by the explicit breaks.</param>
		/// <returns><c>true</c> when the recorded count can be trusted.</returns>
		private static bool IsPlausible(int reported, int estimated, int explicitPages)
		{
			if (reported < explicitPages)
				return false;

			if (Math.Abs(reported - estimated) <= 2)
				return true;

			return reported >= estimated * 0.6 && reported <= estimated * 1.6;
		}

		#endregion

		#region Estimate

		/// <summary>
		/// Estimates the layout of the document.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="options">The extraction options.</param>
		/// <param name="engine">An engine that already measured the document, or <c>null</c>.</param>
		/// <returns>The estimated page starts.</returns>
		private static LayoutResult Estimate(DocxModel model, DocxExtractorOptions options, LayoutEngine engine)
		{
			return Estimate(model, options, engine, out _);
		}

		/// <summary>
		/// Estimates the layout of the document and returns the engine for further calibration.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="options">The extraction options.</param>
		/// <param name="engine">An engine that already measured the document, or <c>null</c>.</param>
		/// <param name="measured">Returns the engine that measured the document.</param>
		/// <returns>The estimated page starts.</returns>
		private static LayoutResult Estimate(DocxModel model, DocxExtractorOptions options, LayoutEngine engine, out LayoutEngine measured)
		{
			if (engine == null)
			{
				engine = new LayoutEngine(model, new FontCatalog(options.UseSystemFonts));
				engine.Measure();
			}

			measured = engine;
			return engine.Place(1.0);
		}

		/// <summary>
		/// Finds the vertical scale that makes the estimate produce the target number of pages.
		/// </summary>
		/// <param name="engine">The engine that measured the document.</param>
		/// <param name="target">The target page count.</param>
		/// <param name="useAnchors">Whether the page break markers saved by Word are honored.</param>
		/// <returns>The calibrated layout, or the closest one when the target cannot be reached.</returns>
		private static LayoutResult Calibrate(LayoutEngine engine, int target, bool useAnchors)
		{
			const double minScale = 0.5;
			const double maxScale = 2.0;
			const int iterations = 18;

			// The page count grows with the scale. Find the range of scales that produce the target
			// and use its middle, which is the most robust choice.
			var low = Search(engine, target, minScale, maxScale, iterations, useAnchors, findFirst: true);
			var high = Search(engine, target, minScale, maxScale, iterations, useAnchors, findFirst: false);

			if (low <= high)
				return engine.Place((low + high) / 2, useAnchors);

			var candidates = new[] { low, high, 1.0 }.Select(scale => engine.Place(scale, useAnchors)).ToList();
			return candidates.OrderBy(r => Math.Abs(r.PageCount - target)).First();
		}

		/// <summary>
		/// Binary searches the scale where the page count reaches the target.
		/// </summary>
		/// <param name="engine">The engine.</param>
		/// <param name="target">The target page count.</param>
		/// <param name="low">The lower bound of the scale.</param>
		/// <param name="high">The upper bound of the scale.</param>
		/// <param name="iterations">The number of iterations.</param>
		/// <param name="useAnchors">Whether the page break markers saved by Word are honored.</param>
		/// <param name="findFirst">
		/// <c>true</c> to find the smallest scale producing at least <paramref name="target"/> pages,
		/// <c>false</c> to find the largest scale producing at most <paramref name="target"/> pages.
		/// </param>
		/// <returns>The scale.</returns>
		private static double Search(LayoutEngine engine, int target, double low, double high, int iterations, bool useAnchors, bool findFirst)
		{
			for (var i = 0; i < iterations; i++)
			{
				var middle = (low + high) / 2;
				var pages = engine.Place(middle, useAnchors).PageCount;

				if (findFirst ? pages >= target : pages > target)
					high = middle;
				else
					low = middle;
			}

			return findFirst ? high : low;
		}

		#endregion

		#region Rendered Breaks

		/// <summary>
		/// Determines the page starts from the explicit breaks and, optionally, from the page break
		/// markers saved by Word.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="useRenderedBreaks">Whether the <c>w:lastRenderedPageBreak</c> markers are used.</param>
		/// <returns>The page starts.</returns>
		private static LayoutResult WalkBreaks(DocxModel model, bool useRenderedBreaks)
		{
			var walker = new BreakWalker(useRenderedBreaks);

			for (var s = 0; s < model.Sections.Count; s++)
			{
				var section = model.Sections[s];
				walker.BeginSection(section, s == 0);

				foreach (var block in section.Blocks)
				{
					switch (block)
					{
						case ParagraphBlock paragraph:
							walker.WalkParagraph(paragraph);
							break;

						case TableBlock table:
							walker.WalkTable(table);
							break;

						default:
							walker.Content();
							break;
					}
				}
			}

			return walker.Result;
		}

		/// <summary>
		/// Walks the content in document order and starts a page at the first content that follows a break.
		/// </summary>
		/// <remarks>
		/// In sections with several text columns Word writes a marker at the start of every column, so
		/// the walker counts columns and starts a page only after the last column of a page.
		/// </remarks>
		private sealed class BreakWalker
		{
			private readonly bool _useRenderedBreaks;
			private int _page = 1;
			private int _columns = 1;
			private int _column;
			private bool _pendingPage;
			private bool _pendingColumn;
			private bool _hasContent;
			private SectionStart _parity = SectionStart.NextPage;

			/// <summary>
			/// Initializes a new instance of the <see cref="BreakWalker"/> class.
			/// </summary>
			/// <param name="useRenderedBreaks">Whether the page break markers saved by Word are used.</param>
			public BreakWalker(bool useRenderedBreaks)
			{
				_useRenderedBreaks = useRenderedBreaks;
			}

			/// <summary>Gets the result of the walk.</summary>
			public LayoutResult Result { get; } = new LayoutResult();

			/// <summary>
			/// Starts a section.
			/// </summary>
			/// <param name="section">The section.</param>
			/// <param name="isFirst">Whether this is the first section.</param>
			public void BeginSection(Section section, bool isFirst)
			{
				var columns = Math.Max(1, section.Page.Columns);

				if (!isFirst)
				{
					switch (section.Start)
					{
						case SectionStart.Continuous:
							// The columns of the new section start below the content of the previous one.
							_column = 0;
							_pendingColumn = false;
							break;

						case SectionStart.NextColumn:
							if (columns > 1)
								ColumnBreak();
							else
								PageBreak();
							break;

						default:
							PageBreak();
							_parity = section.Start;
							break;
					}
				}

				_columns = columns;
			}

			/// <summary>
			/// Walks the content of a paragraph.
			/// </summary>
			/// <param name="paragraph">The paragraph.</param>
			public void WalkParagraph(ParagraphBlock paragraph)
			{
				if (paragraph.Properties.PageBreakBefore)
					PageBreak();

				// An empty paragraph is content of its own: its mark takes a line.
				if (!paragraph.Inlines.Any(ContentHelper.IsContent))
					Content(new PageStart(new ContentPosition(paragraph, 0, 0), null, 0));

				for (var i = 0; i < paragraph.Inlines.Count; i++)
				{
					var inline = paragraph.Inlines[i];

					if (inline is RenderedPageBreakInline)
					{
						if (_useRenderedBreaks)
							RenderedBreak();
					}
					else if (inline is BreakInline br && br.Kind == BreakKind.Page)
					{
						PageBreak();
					}
					else if (inline is BreakInline column && column.Kind == BreakKind.Column)
					{
						if (_columns > 1)
							ColumnBreak();
						else
							PageBreak();
					}
					else if (ContentHelper.IsContent(inline))
					{
						Content(new PageStart(new ContentPosition(paragraph, i, 0), null, 0));
					}
				}
			}

			/// <summary>
			/// Walks the rows of a table.
			/// </summary>
			/// <param name="table">The table.</param>
			public void WalkTable(TableBlock table)
			{
				foreach (var row in table.Rows)
				{
					var breakAfter = false;

					if (_useRenderedBreaks)
					{
						ContentHelper.FindRenderedBreaks(row, out var breakBefore, out breakAfter);
						if (breakBefore)
							RenderedBreak();
					}

					Content(new PageStart(default, row, 0));

					// A row that Word split across pages: the next row starts on the new page.
					if (breakAfter)
						RenderedBreak();
				}
			}

			/// <summary>
			/// Records content that cannot hold a page start, such as preformatted text.
			/// </summary>
			public void Content()
			{
				_hasContent = true;
			}

			/// <summary>
			/// Records content, starting the pending page or column there.
			/// </summary>
			/// <param name="position">The position of the content; its page number is ignored.</param>
			private void Content(PageStart position)
			{
				if (_pendingPage)
				{
					StartPage(position);
				}
				else if (_pendingColumn)
				{
					_column++;
					if (_column >= _columns)
						StartPage(position);
				}

				_pendingPage = false;
				_pendingColumn = false;
				_hasContent = true;
			}

			/// <summary>
			/// Handles a rendered break marker, which starts a page or, in multi-column sections, a column.
			/// </summary>
			private void RenderedBreak()
			{
				if (_columns > 1)
					ColumnBreak();
				else
					PageBreak();
			}

			/// <summary>
			/// Requests a new page before the next content. Breaks before any content are merged.
			/// </summary>
			private void PageBreak()
			{
				if (_hasContent)
					_pendingPage = true;
			}

			/// <summary>
			/// Requests a new column before the next content. Breaks before any content are merged.
			/// </summary>
			private void ColumnBreak()
			{
				if (_hasContent)
					_pendingColumn = true;
			}

			/// <summary>
			/// Records the start of a new page, honoring odd and even section starts.
			/// </summary>
			/// <param name="position">The position where the page starts.</param>
			private void StartPage(PageStart position)
			{
				var page = _page + 1;

				if ((_parity == SectionStart.OddPage && page % 2 == 0) || (_parity == SectionStart.EvenPage && page % 2 == 1))
					page++;

				Result.Starts.Add(new PageStart(position.Position, position.Row, page));
				Result.PageCount = _page = page;

				_column = 0;
				_parity = SectionStart.NextPage;
			}

		}

		#endregion

		#region Apply

		/// <summary>
		/// Writes the page starts into the model.
		/// </summary>
		/// <param name="model">The document model.</param>
		/// <param name="result">The page starts.</param>
		/// <param name="source">How the page starts were determined.</param>
		internal static void Apply(DocxModel model, LayoutResult result, PaginationSource source)
		{
			model.PageCount = Math.Max(1, result.PageCount);
			model.PaginationSource = source;

			foreach (var group in result.Starts.Where(s => s.Row == null).GroupBy(s => s.Position.Paragraph))
			{
				var paragraph = group.Key;
				if (paragraph == null)
					continue;

				// Insert from the end, so that the earlier positions remain valid.
				foreach (var start in group.OrderByDescending(s => s.Position.InlineIndex).ThenByDescending(s => s.Position.CharOffset))
					Insert(paragraph, start.Position, start.PageNumber);
			}

			foreach (var start in result.Starts.Where(s => s.Row != null))
				start.Row.PageStart = start.PageNumber;
		}

		/// <summary>
		/// Inserts a page start marker in a paragraph, splitting a text run when needed.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="position">The position of the page start.</param>
		/// <param name="pageNumber">The number of the page.</param>
		private static void Insert(ParagraphBlock paragraph, ContentPosition position, int pageNumber)
		{
			var inlines = paragraph.Inlines;
			var index = Math.Min(position.InlineIndex, inlines.Count);
			var offset = position.CharOffset;

			// A page that starts before any visible content starts with the paragraph itself.
			if (!inlines.Take(index).Any(ContentHelper.IsContent) && (offset == 0 || index >= inlines.Count))
			{
				index = 0;
				offset = 0;
			}

			var marker = new PageStartInline { PageNumber = pageNumber };

			if (offset > 0 && index < inlines.Count && inlines[index] is TextInline text && offset < text.Text.Length)
			{
				var tail = new TextInline
				{
					Text = text.Text.Substring(offset),
					Style = text.Style,
					Hyperlink = text.Hyperlink,
					Revision = text.Revision,
				};

				text.Text = text.Text.Substring(0, offset);
				inlines.Insert(index + 1, tail);
				inlines.Insert(index + 1, marker);
				return;
			}

			if (offset > 0 && index < inlines.Count)
				index++;

			inlines.Insert(index, marker);
		}

		#endregion
	}
}

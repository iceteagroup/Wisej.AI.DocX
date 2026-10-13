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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Reading;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// Base class of the renderers: walks the paginated model and writes the text of every page.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The base class owns everything that does not depend on the output syntax: the page splits,
	/// headers and footers, footnotes, endnotes and comments, page number fields, image bookkeeping
	/// and the conversion of inlines into formatted <see cref="Span"/>s.
	/// </para>
	/// <para>
	/// Derived classes implement the syntax: <see cref="MarkdownRenderer"/> and <see cref="PlainTextRenderer"/>.
	/// </para>
	/// </remarks>
	internal abstract class DocumentRenderer
	{
		/// <summary>Matches header and footer text made only of page numbering ("Page 3 of 10", "- 4 -").</summary>
		private static readonly Regex PageNumberOnly = new Regex(@"^[\s\p{P}\d]*((page|pag|pagina|página|seite|von|of|de|di|sur|\#)[\s\p{P}\d]*)*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		/// <summary>The length, in characters, under which a bold paragraph is considered a heading and keeps its bold.</summary>
		private const int ShortParagraphLength = 100;

		private readonly HashSet<string> _emittedStories = new HashSet<string>(StringComparer.Ordinal);
		private readonly HashSet<string> _emittedStoryTexts = new HashSet<string>(StringComparer.Ordinal);
		private readonly List<NoteReferenceInline> _pageFootnotes = new List<NoteReferenceInline>();
		private readonly List<NoteReferenceInline> _endnotes = new List<NoteReferenceInline>();
		private readonly List<string> _pageComments = new List<string>();
		private readonly Dictionary<string, string> _imageDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);
		private readonly List<(int First, int Last)> _sectionPages = new List<(int, int)>();

		private Section _section;
		private int _sectionIndex;
		private int _sectionFirstPage = 1;
		private bool _renderingStory;
		private bool _endnoteLabelsCollide;
		private bool _neutralizeFields;

		/// <summary>
		/// Initializes a new instance of the <see cref="DocumentRenderer"/> class.
		/// </summary>
		/// <param name="model">The paginated document model.</param>
		/// <param name="options">The extraction options.</param>
		/// <param name="codeFence">The fence of code blocks, or <c>null</c>.</param>
		protected DocumentRenderer(DocxModel model, DocxExtractorOptions options, string codeFence)
		{
			Model = model;
			Options = options;
			Pages = new PageWriter(codeFence);
		}

		#region Properties

		/// <summary>Gets the document model.</summary>
		protected DocxModel Model { get; }

		/// <summary>Gets the extraction options.</summary>
		protected DocxExtractorOptions Options { get; }

		/// <summary>Gets the page writer.</summary>
		protected PageWriter Pages { get; }

		/// <summary>Gets the default format of the line that introduces every page.</summary>
		protected abstract string DefaultPageHeaderFormat { get; }

		#endregion

		#region Render

		/// <summary>
		/// Renders the document.
		/// </summary>
		/// <returns>The extraction result.</returns>
		public DocxExtractionResult Render()
		{
			ComputeSectionPages();

			_endnoteLabelsCollide = Model.Footnotes.Count > 0 && Model.Endnotes.Count > 0
				&& Model.Sections.SelectMany(s => s.Blocks).Any(ContainsNumericEndnote);

			for (_sectionIndex = 0; _sectionIndex < Model.Sections.Count; _sectionIndex++)
			{
				_section = Model.Sections[_sectionIndex];
				if (_sectionIndex == 0)
				{
					_sectionFirstPage = 1;
					BeginPageContent();
				}

				RenderBlocks(_section.Blocks);
			}

			WriteEndnotes();
			EndPageContent();

			return BuildResult();
		}

		/// <summary>
		/// Assembles the extraction result from the written pages.
		/// </summary>
		/// <returns>The result.</returns>
		private DocxExtractionResult BuildResult()
		{
			var pages = Pages.Pages.Select(p => new DocxPage(p.Number, p.Text.ToString().Trim(), p.Images)).ToList();

			// Trailing blank pages (an odd page section start at the end, for example).
			while (pages.Count < Model.PageCount)
				pages.Add(new DocxPage(pages.Count + 1, "", new List<DocxImage>()));

			var format = Options.PageHeaderFormat ?? DefaultPageHeaderFormat;
			var paginated = Model.PaginationSource != PaginationSource.None && format.Length > 0;
			var text = new StringBuilder();

			foreach (var page in pages)
			{
				if (paginated)
				{
					if (text.Length > 0)
						text.Append("\n\n");

					text.Append(String.Format(CultureInfo.InvariantCulture, format, page.Number, pages.Count));
				}

				if (page.Text.Length > 0)
				{
					if (text.Length > 0)
						text.Append("\n\n");

					text.Append(page.Text);
				}
			}

			return new DocxExtractionResult(text.ToString(), pages, Model.Images, Model.Metadata, Model.PaginationSource);
		}

		#endregion

		#region Blocks

		/// <summary>
		/// Renders a sequence of blocks to the current page.
		/// </summary>
		/// <param name="blocks">The blocks.</param>
		protected void RenderBlocks(IEnumerable<Block> blocks)
		{
			foreach (var block in blocks)
			{
				switch (block)
				{
					case ParagraphBlock paragraph:
						RenderParagraph(paragraph);
						break;

					case TableBlock table:
						RenderTable(table);
						break;

					case RawTextBlock raw:
						Pages.AppendBlock(raw.Text, OutputBlockKind.Paragraph);
						break;
				}
			}
		}

		/// <summary>
		/// Renders a paragraph, splitting it where pages start.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		private void RenderParagraph(ParagraphBlock paragraph)
		{
			if (paragraph.Properties.Kind == ParagraphKind.TableOfContents && !Options.IncludeTableOfContents)
			{
				// The content is skipped, but the pages that start inside the table of contents still start.
				foreach (var marker in paragraph.Inlines.OfType<PageStartInline>())
					BreakPage(marker.PageNumber);

				return;
			}

			var fragment = new List<Inline>();
			var isContinuation = false;

			foreach (var inline in paragraph.Inlines)
			{
				if (inline is PageStartInline marker)
				{
					WriteParagraphFragment(paragraph, fragment, isContinuation);
					isContinuation |= fragment.Any(IsVisibleContent);
					fragment.Clear();

					BreakPage(marker.PageNumber);
					continue;
				}

				fragment.Add(inline);
			}

			WriteParagraphFragment(paragraph, fragment, isContinuation);
		}

		/// <summary>
		/// Writes the part of a paragraph that is on a single page.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="inlines">The inlines of the fragment.</param>
		/// <param name="isContinuation">Whether the fragment continues a paragraph started on a previous page.</param>
		private void WriteParagraphFragment(ParagraphBlock paragraph, List<Inline> inlines, bool isContinuation)
		{
			var deferred = new List<Action>();
			var spans = BuildSpans(paragraph, inlines, InlineMode.Block, deferred);

			if (spans.Any(s => !String.IsNullOrWhiteSpace(s.Text)))
				WriteParagraph(paragraph, spans, isContinuation);

			foreach (var action in deferred)
				action();
		}

		/// <summary>
		/// Writes a paragraph to the current page.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="spans">The formatted content of the paragraph.</param>
		/// <param name="isContinuation">Whether the paragraph continues from a previous page; its label or heading marker is not repeated.</param>
		protected abstract void WriteParagraph(ParagraphBlock paragraph, List<Span> spans, bool isContinuation);

		/// <summary>
		/// Renders a table, splitting it where pages start.
		/// </summary>
		/// <param name="table">The table.</param>
		private void RenderTable(TableBlock table)
		{
			// Tables with a single row are frames or side by side layouts (a picture next to a caption),
			// not data: their cells are written as regular content.
			if (Options.UnwrapLayoutTables && table.Rows.Count == 1)
			{
				if (table.Rows[0].PageStart > 0)
					BreakPage(table.Rows[0].PageStart);

				foreach (var cell in table.Rows[0].Cells.Where(c => c.VerticalMerge != VerticalMerge.Continue))
					RenderBlocks(cell.Blocks);

				return;
			}

			var grid = new TableGrid(table);
			if (grid.Rows.Count == 0)
			{
				foreach (var row in table.Rows.Where(r => r.PageStart > 0))
					BreakPage(row.PageStart);

				return;
			}

			var deferred = new List<Action>();
			var headerCount = grid.HeaderRowCount;
			var headerRows = grid.Rows.Take(headerCount).ToList();

			// A page that starts with the header rows starts before the table.
			var firstStart = grid.Rows.Take(Math.Max(1, headerCount)).Select(r => r.Row.PageStart).FirstOrDefault(p => p > 0);
			if (firstStart > 0)
				BreakPage(firstStart);

			var body = new List<TableGrid.GridRow>();

			for (var r = headerCount; r < grid.Rows.Count; r++)
			{
				var row = grid.Rows[r];

				if (row.Row.PageStart > 0 && (body.Count > 0 || r > headerCount))
				{
					WriteTable(grid, headerRows, body, deferred);
					body.Clear();
					BreakPage(row.Row.PageStart);
				}

				body.Add(row);
			}

			WriteTable(grid, headerRows, body, deferred);

			foreach (var action in deferred)
				action();
		}

		/// <summary>
		/// Writes the part of a table that is on a single page, repeating the header rows.
		/// </summary>
		/// <param name="grid">The table grid.</param>
		/// <param name="headerRows">The header rows declared by the table; empty when the table declares none.</param>
		/// <param name="bodyRows">The rows on the current page.</param>
		/// <param name="deferred">Receives the content written after the table, such as image descriptions.</param>
		protected abstract void WriteTable(TableGrid grid, List<TableGrid.GridRow> headerRows, List<TableGrid.GridRow> bodyRows, List<Action> deferred);

		/// <summary>
		/// Returns the text of a table cell on a single line.
		/// </summary>
		/// <param name="cell">The cell.</param>
		/// <param name="deferred">Receives the content written after the table, or <c>null</c> to discard it.</param>
		/// <returns>The formatted text of the cell.</returns>
		protected abstract string RenderCellText(TableCell cell, List<Action> deferred);

		#endregion

		#region Inlines

		/// <summary>
		/// Converts inlines into formatted spans.
		/// </summary>
		/// <param name="paragraph">The paragraph that contains the inlines.</param>
		/// <param name="inlines">The inlines.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <param name="deferred">Receives the blocks written after the paragraph (charts, text boxes, image descriptions), or <c>null</c>.</param>
		/// <returns>The spans.</returns>
		protected List<Span> BuildSpans(ParagraphBlock paragraph, IEnumerable<Inline> inlines, InlineMode mode, List<Action> deferred)
		{
			var spans = new List<Span>();
			var list = inlines as IList<Inline> ?? inlines.ToList();
			var isHeading = paragraph.Properties.Kind == ParagraphKind.Heading || paragraph.Properties.Kind == ParagraphKind.Title;
			var isToc = paragraph.Properties.Kind == ParagraphKind.TableOfContents;

			foreach (var inline in list)
			{
				// Placeholder text of empty content controls is laid out but not extracted.
				if (inline.IsPlaceholder)
					continue;

				switch (inline)
				{
					case TextInline text:
						AddSpan(spans, CreateSpan(text.Text, inline, isHeading));
						break;

					case TabInline _:
						AddSpan(spans, CreateSpan(mode == InlineMode.Block && !isToc ? "\t" : " ", inline, isHeading));
						break;

					case BreakInline br:
						if (br.Kind == BreakKind.Line)
							AddSpan(spans, FormatLineBreak(mode, inline));
						break;

					case FieldInline field:
						AddSpan(spans, CreateSpan(GetFieldValue(field), inline, isHeading));
						break;

					case NoteReferenceInline note:
						AddNoteReference(spans, note);
						break;

					case MathInline math:
						if (math.Latex != null)
							AddSpan(spans, Markup(FormatMath(math.Latex, math.IsDisplay && mode == InlineMode.Block && list.Count(IsVisibleContent) == 1), inline));
						break;

					case ImageInline image:
						if (!image.IsExcluded && image.Image != null)
							AddImage(spans, image, mode, deferred);
						break;

					case ChartInline chart:
						if (!chart.IsExcluded)
							AddBlockDrawing(spans, mode, deferred, () => FormatChart(chart.Chart), FlattenChart(chart.Chart));
						break;

					case SmartArtInline smartArt:
						if (!smartArt.IsExcluded)
							AddBlockDrawing(spans, mode, deferred, () => FormatSmartArt(smartArt.Nodes), FlattenSmartArt(smartArt.Nodes));
						break;

					case TextBoxInline textBox:
						if (!textBox.IsExcluded)
							AddTextBox(spans, textBox, mode, deferred);
						break;

					case CommentReferenceInline comment:
						if (!_renderingStory)
							AddComment(comment);
						break;
				}
			}

			NormalizeEmphasis(spans, paragraph, mode);
			return spans;
		}

		/// <summary>
		/// Removes the emphasis that covers a whole paragraph or cell, which is styling rather than meaning.
		/// </summary>
		/// <param name="spans">The spans of the paragraph.</param>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <remarks>
		/// Bold table headers, italic captions or a bold and italic cover page add markers to every line
		/// without telling the reader anything. Partial emphasis is kept, and so is the bold of short
		/// paragraphs, which usually act as headings.
		/// </remarks>
		private static void NormalizeEmphasis(List<Span> spans, ParagraphBlock paragraph, InlineMode mode)
		{
			var text = spans.Where(s => !s.IsMarkup && !String.IsNullOrWhiteSpace(s.Text)).ToList();
			if (text.Count == 0)
				return;

			var kind = paragraph.Properties.Kind;
			var isLong = text.Sum(s => s.Text.Trim().Length) > ShortParagraphLength;
			var isStyled = mode != InlineMode.Block || kind == ParagraphKind.TableOfContents || kind == ParagraphKind.Quote || kind == ParagraphKind.Caption;

			var dropBold = text.All(s => s.Bold) && (isStyled || isLong);
			var dropItalic = text.All(s => s.Italic) && (isStyled || isLong || text.All(s => s.Bold));

			foreach (var span in spans)
			{
				if (dropBold)
					span.Bold = false;

				if (dropItalic)
					span.Italic = false;
			}
		}

		/// <summary>
		/// Creates a text span with the formatting of an inline.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="inline">The inline that provides the formatting.</param>
		/// <param name="isHeading">Whether the text is in a heading, where bold is implicit.</param>
		/// <returns>The span.</returns>
		protected Span CreateSpan(string text, Inline inline, bool isHeading)
		{
			var style = inline.Style;
			var formatting = Options.IncludeFormatting;

			return new Span
			{
				Text = text,
				Bold = formatting && style.Bold && !isHeading,
				Italic = formatting && style.Italic,
				Strike = formatting && style.Strike,
				Superscript = formatting && style.Superscript,
				Subscript = formatting && style.Subscript,
				Code = formatting && style.Monospace,
				Link = Options.IncludeHyperlinks && inline.Hyperlink != null && !inline.Hyperlink.StartsWith("#", StringComparison.Ordinal) ? inline.Hyperlink : null,
				Revision = Options.TrackedChanges == TrackedChangesMode.Markup ? inline.Revision : RevisionKind.None,
			};
		}

		/// <summary>
		/// Creates a span of formatted output written as is.
		/// </summary>
		/// <param name="markup">The formatted output.</param>
		/// <param name="inline">The inline that provides the hyperlink and revision.</param>
		/// <returns>The span.</returns>
		protected Span Markup(string markup, Inline inline)
		{
			return new Span
			{
				Text = markup,
				IsMarkup = true,
				Revision = Options.TrackedChanges == TrackedChangesMode.Markup && inline != null ? inline.Revision : RevisionKind.None,
			};
		}

		/// <summary>
		/// Adds a span, merging it with the previous one when the formatting is the same.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="span">The span to add.</param>
		private static void AddSpan(List<Span> spans, Span span)
		{
			if (span == null || String.IsNullOrEmpty(span.Text))
				return;

			var last = spans.Count > 0 ? spans[spans.Count - 1] : null;
			if (last != null && last.CanMergeWith(span))
				last.Text += span.Text;
			else
				spans.Add(span);
		}

		/// <summary>
		/// Adds a footnote or endnote reference and records the note for the page or the document end.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="note">The reference.</param>
		private void AddNoteReference(List<Span> spans, NoteReferenceInline note)
		{
			if (!Options.IncludeFootnotes)
				return;

			var notes = note.Kind == NoteKind.Footnote ? Model.Footnotes : Model.Endnotes;
			if (note.NoteId == null || !notes.ContainsKey(note.NoteId))
				return;

			if (note.Kind == NoteKind.Footnote)
				_pageFootnotes.Add(note);
			else
				_endnotes.Add(note);

			AddSpan(spans, Markup(FormatNoteReference(NoteLabel(note)), note));
		}

		/// <summary>
		/// Adds an image reference and its description.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="inline">The image inline.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <param name="deferred">Receives the description block, or <c>null</c>.</param>
		private void AddImage(List<Span> spans, ImageInline inline, InlineMode mode, List<Action> deferred)
		{
			var image = inline.Image;

			// Images of headers and footers report the first page where they are written.
			if (image.PageNumber == 0)
				image.PageNumber = Pages.Current.Number;

			if (!Pages.Current.Images.Contains(image))
				Pages.Current.Images.Add(image);

			var span = Markup(FormatImage(image), inline);
			span.Link = Options.IncludeHyperlinks && inline.Hyperlink != null && !inline.Hyperlink.StartsWith("#", StringComparison.Ordinal) ? inline.Hyperlink : null;
			AddSpan(spans, span);

			// Every distinct picture is described once.
			if (!String.IsNullOrWhiteSpace(image.Description) && _imageDescriptions.ContainsKey(image.PartUri ?? image.FileName) == false)
			{
				_imageDescriptions[image.PartUri ?? image.FileName] = image.Description;

				if (mode == InlineMode.Block && deferred != null)
					deferred.Add(() => Pages.AppendBlock(FormatImageDescription(image), OutputBlockKind.Paragraph));
				else
					AddSpan(spans, Markup(" " + FormatInlineImageDescription(image), inline));
			}
		}

		/// <summary>
		/// Adds a drawing that is written as a block (a chart, a diagram) after the paragraph,
		/// or flattened on one line inside cells and summaries.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <param name="deferred">Receives the block, or <c>null</c>.</param>
		/// <param name="block">Formats the drawing as a block.</param>
		/// <param name="flat">The drawing flattened on one line.</param>
		private void AddBlockDrawing(List<Span> spans, InlineMode mode, List<Action> deferred, Func<string> block, string flat)
		{
			if (mode == InlineMode.Block && deferred != null)
				deferred.Add(() => Pages.AppendBlock(block(), OutputBlockKind.Paragraph));
			else
				AddSpan(spans, CreatePlainSpan(" " + flat + " "));
		}

		/// <summary>
		/// Adds the content of a text box: as blocks after the paragraph, or flattened on one line.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="textBox">The text box.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <param name="deferred">Receives the blocks, or <c>null</c>.</param>
		private void AddTextBox(List<Span> spans, TextBoxInline textBox, InlineMode mode, List<Action> deferred)
		{
			if (mode == InlineMode.Block && deferred != null)
			{
				deferred.Add(() =>
				{
					Pages.EndGroup();
					RenderBlocks(textBox.Blocks);
					Pages.EndGroup();
				});
			}
			else
			{
				AddSpan(spans, CreatePlainSpan(" " + RenderFlat(textBox.Blocks) + " "));
			}
		}

		/// <summary>
		/// Creates an unformatted text span.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The span.</returns>
		private static Span CreatePlainSpan(string text)
		{
			return new Span { Text = text };
		}

		/// <summary>
		/// Determines whether an inline produces visible output.
		/// </summary>
		/// <param name="inline">The inline.</param>
		/// <returns><c>true</c> for visible content.</returns>
		protected static bool IsVisibleContent(Inline inline)
		{
			if (inline.IsPlaceholder)
				return false;

			switch (inline)
			{
				case TextInline text:
					return text.IsVisible && !String.IsNullOrWhiteSpace(text.Text);

				case DrawingInline drawing:
					return !drawing.IsExcluded;

				case MathInline math:
					return math.Latex != null;

				case FieldInline _:
				case NoteReferenceInline _:
					return true;

				default:
					return false;
			}
		}

		#endregion

		#region Pages

		/// <summary>
		/// Ends the current page and starts a new one.
		/// </summary>
		/// <param name="number">The number of the new page.</param>
		protected void BreakPage(int number)
		{
			if (number <= Pages.Current.Number)
				return;

			EndPageContent();
			Pages.StartPage(number);
			BeginPageContent();
		}

		/// <summary>
		/// Writes the content that opens a page: its header.
		/// </summary>
		private void BeginPageContent()
		{
			_sectionFirstPage = _sectionIndex < _sectionPages.Count ? _sectionPages[_sectionIndex].First : 1;

			var header = GetHeaderFooter(_section?.Headers);
			if (header != null)
				WriteStory(header, isHeader: true);
		}

		/// <summary>
		/// Writes the content that closes a page: footnotes, comments and footer.
		/// </summary>
		private void EndPageContent()
		{
			Pages.EndGroup();

			foreach (var note in _pageFootnotes)
			{
				if (Model.Footnotes.TryGetValue(note.NoteId, out var footnote))
					Pages.AppendBlock(FormatNoteDefinition(NoteLabel(note), RenderFlat(footnote.Blocks)), OutputBlockKind.ListItem);
			}

			_pageFootnotes.Clear();
			Pages.EndGroup();

			foreach (var comment in _pageComments)
				Pages.AppendBlock(comment, OutputBlockKind.Paragraph);

			_pageComments.Clear();

			var footer = GetHeaderFooter(_section?.Footers);
			if (footer != null)
				WriteStory(footer, isHeader: false);

			Pages.EndGroup();
		}

		/// <summary>
		/// Writes the endnotes at the end of the document.
		/// </summary>
		private void WriteEndnotes()
		{
			Pages.EndGroup();

			foreach (var note in _endnotes)
			{
				if (Model.Endnotes.TryGetValue(note.NoteId, out var endnote))
					Pages.AppendBlock(FormatNoteDefinition(NoteLabel(note), RenderFlat(endnote.Blocks)), OutputBlockKind.ListItem);
			}

			_endnotes.Clear();
		}

		/// <summary>
		/// Records a comment referenced on the current page; comments are written at the end of the page.
		/// </summary>
		/// <param name="reference">The comment reference.</param>
		private void AddComment(CommentReferenceInline reference)
		{
			if (reference.CommentId != null && Model.Comments.TryGetValue(reference.CommentId, out var comment))
				_pageComments.Add(FormatComment(comment, RenderFlat(comment.Blocks)));
		}

		/// <summary>
		/// Returns the header or footer that applies to the current page, or <c>null</c> when it must not be written.
		/// </summary>
		/// <param name="stories">The headers or footers of the current section. Can be <c>null</c>.</param>
		/// <returns>The story to write, or <c>null</c>.</returns>
		private HeaderFooter GetHeaderFooter(Dictionary<HeaderFooterKind, HeaderFooter> stories)
		{
			if (stories == null || Options.HeadersAndFooters == HeaderFooterMode.None || Model.PaginationSource == PaginationSource.None && Pages.Current.Number > 1)
				return null;

			var page = Pages.Current.Number;
			HeaderFooter story;

			if (page == _sectionFirstPage && _section.TitlePage)
				stories.TryGetValue(HeaderFooterKind.First, out story);
			else if (Model.EvenAndOddHeaders && page % 2 == 0)
				stories.TryGetValue(HeaderFooterKind.Even, out story);
			else
				stories.TryGetValue(HeaderFooterKind.Default, out story);

			if (story == null)
				return null;

			if (Options.HeadersAndFooters == HeaderFooterMode.Once && _emittedStories.Contains(story.Id))
				return null;

			return story;
		}

		/// <summary>
		/// Writes a header or a footer on one line.
		/// </summary>
		/// <param name="story">The header or footer.</param>
		/// <param name="isHeader">Whether the story is a header.</param>
		private void WriteStory(HeaderFooter story, bool isHeader)
		{
			_emittedStories.Add(story.Id);

			// The text without its page numbers identifies the story: "Page 1" and "Page 2" are the same footer.
			_neutralizeFields = true;
			var key = RenderFlat(story.Blocks);
			_neutralizeFields = false;

			if (String.IsNullOrWhiteSpace(key) || PageNumberOnly.IsMatch(key))
				return;

			// Different stories with the same text (first page and default header, for example) are written once.
			if (Options.HeadersAndFooters == HeaderFooterMode.Once && !_emittedStoryTexts.Add((isHeader ? "h:" : "f:") + key))
				return;

			var text = RenderFlat(story.Blocks);

			Pages.EndGroup();
			Pages.AppendBlock(FormatHeaderFooter(isHeader, text), OutputBlockKind.Paragraph);
			Pages.EndGroup();
		}

		/// <summary>
		/// Renders blocks on a single line, for headers, footers, notes and comments.
		/// </summary>
		/// <param name="blocks">The blocks.</param>
		/// <returns>The flattened text.</returns>
		protected string RenderFlat(IEnumerable<Block> blocks)
		{
			var wasRenderingStory = _renderingStory;
			_renderingStory = true;

			try
			{
				var parts = new List<string>();

				foreach (var block in blocks)
				{
					switch (block)
					{
						case ParagraphBlock paragraph:
							var spans = BuildSpans(paragraph, paragraph.Inlines, InlineMode.Flat, null);
							var text = FormatSpans(spans, InlineMode.Flat).Trim();
							if (text.Length > 0)
								parts.Add(paragraph.List != null ? paragraph.List.Text + " " + text : text);
							break;

						case TableBlock table:
							foreach (var row in table.Rows)
							{
								var cells = row.Cells.Select(c => RenderFlat(c.Blocks)).Where(c => c.Length > 0).ToList();
								if (cells.Count > 0)
									parts.Add(String.Join(" | ", cells));
							}
							break;

						case RawTextBlock raw:
							parts.Add(raw.Text.Replace('\n', ' ').Trim());
							break;
					}
				}

				return String.Join(" | ", parts.Where(p => p.Length > 0));
			}
			finally
			{
				_renderingStory = wasRenderingStory;
			}
		}

		#endregion

		#region Fields And Labels

		/// <summary>
		/// Computes the value of a layout dependent field for the current page.
		/// </summary>
		/// <param name="field">The field.</param>
		/// <returns>The field value.</returns>
		private string GetFieldValue(FieldInline field)
		{
			if (_neutralizeFields)
				return "#";

			var page = Pages.Current.Number;

			switch (field.Kind)
			{
				case FieldKind.NumPages:
					return Model.PageCount.ToString(CultureInfo.InvariantCulture);

				case FieldKind.SectionPages:
					if (_sectionIndex < _sectionPages.Count)
						return (_sectionPages[_sectionIndex].Last - _sectionPages[_sectionIndex].First + 1).ToString(CultureInfo.InvariantCulture);

					return Model.PageCount.ToString(CultureInfo.InvariantCulture);

				default:
					var section = _section;
					var number = section?.PageNumberStart != null ? section.PageNumberStart.Value + page - _sectionFirstPage : page;
					return NumberFormatter.Format(number, section?.PageNumberFormat);
			}
		}

		/// <summary>
		/// Returns the label of a note reference, distinguishing endnotes from footnotes when their labels could collide.
		/// </summary>
		/// <param name="note">The reference.</param>
		/// <returns>The label.</returns>
		private string NoteLabel(NoteReferenceInline note)
		{
			return note.Kind == NoteKind.Endnote && _endnoteLabelsCollide ? "e" + note.Label : note.Label;
		}

		/// <summary>
		/// Determines whether a block references an endnote with a numeric label.
		/// </summary>
		/// <param name="block">The block.</param>
		/// <returns><c>true</c> when an endnote label could collide with a footnote label.</returns>
		private static bool ContainsNumericEndnote(Block block)
		{
			if (block is ParagraphBlock paragraph)
				return paragraph.Inlines.OfType<NoteReferenceInline>().Any(n => n.Kind == NoteKind.Endnote && n.Label.All(Char.IsDigit));

			if (block is TableBlock table)
				return table.Rows.SelectMany(r => r.Cells).SelectMany(c => c.Blocks).Any(ContainsNumericEndnote);

			return false;
		}

		/// <summary>
		/// Computes the first and last page of every section from the page start markers.
		/// </summary>
		private void ComputeSectionPages()
		{
			var page = 1;

			foreach (var section in Model.Sections)
			{
				var first = -1;

				foreach (var block in section.Blocks)
				{
					if (block is ParagraphBlock paragraph)
					{
						foreach (var inline in paragraph.Inlines)
						{
							if (inline is PageStartInline marker)
								page = marker.PageNumber;
							else if (first < 0 && IsVisibleContent(inline))
								first = page;
						}

						if (first < 0)
							first = page;
					}
					else if (block is TableBlock table)
					{
						foreach (var row in table.Rows)
						{
							if (row.PageStart > 0)
								page = row.PageStart;

							if (first < 0)
								first = page;
						}
					}
				}

				_sectionPages.Add((first < 0 ? page : first, page));
			}
		}

		#endregion

		#region Syntax

		/// <summary>
		/// Formats spans of inline content.
		/// </summary>
		/// <param name="spans">The spans.</param>
		/// <param name="mode">Where the content is written.</param>
		/// <returns>The formatted text.</returns>
		protected abstract string FormatSpans(List<Span> spans, InlineMode mode);

		/// <summary>
		/// Returns the span that represents a line break.
		/// </summary>
		/// <param name="mode">Where the content is written.</param>
		/// <param name="inline">The break inline.</param>
		/// <returns>The span.</returns>
		protected abstract Span FormatLineBreak(InlineMode mode, Inline inline);

		/// <summary>
		/// Formats an image reference.
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns>The formatted reference.</returns>
		protected abstract string FormatImage(DocxImage image);

		/// <summary>
		/// Formats the description of an image as a block written after the paragraph.
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns>The formatted description.</returns>
		protected abstract string FormatImageDescription(DocxImage image);

		/// <summary>
		/// Formats the description of an image on one line.
		/// </summary>
		/// <param name="image">The image.</param>
		/// <returns>The formatted description.</returns>
		protected abstract string FormatInlineImageDescription(DocxImage image);

		/// <summary>
		/// Formats an equation.
		/// </summary>
		/// <param name="latex">The LaTeX source.</param>
		/// <param name="display">Whether the equation is displayed on its own line.</param>
		/// <returns>The formatted equation.</returns>
		protected abstract string FormatMath(string latex, bool display);

		/// <summary>
		/// Formats a note reference.
		/// </summary>
		/// <param name="label">The note label.</param>
		/// <returns>The formatted reference.</returns>
		protected abstract string FormatNoteReference(string label);

		/// <summary>
		/// Formats the definition of a note.
		/// </summary>
		/// <param name="label">The note label.</param>
		/// <param name="text">The text of the note.</param>
		/// <returns>The formatted definition.</returns>
		protected abstract string FormatNoteDefinition(string label, string text);

		/// <summary>
		/// Formats a comment.
		/// </summary>
		/// <param name="comment">The comment.</param>
		/// <param name="text">The text of the comment.</param>
		/// <returns>The formatted comment.</returns>
		protected abstract string FormatComment(Comment comment, string text);

		/// <summary>
		/// Formats a header or a footer.
		/// </summary>
		/// <param name="isHeader">Whether the story is a header.</param>
		/// <param name="text">The flattened text of the story.</param>
		/// <returns>The formatted header or footer.</returns>
		protected abstract string FormatHeaderFooter(bool isHeader, string text);

		/// <summary>
		/// Formats the data of a chart as a block.
		/// </summary>
		/// <param name="chart">The chart data.</param>
		/// <returns>The formatted chart.</returns>
		protected abstract string FormatChart(ChartData chart);

		/// <summary>
		/// Formats the nodes of a SmartArt diagram as a block.
		/// </summary>
		/// <param name="nodes">The top level nodes.</param>
		/// <returns>The formatted diagram.</returns>
		protected abstract string FormatSmartArt(List<SmartArtNode> nodes);

		/// <summary>
		/// Flattens the data of a chart on one line.
		/// </summary>
		/// <param name="chart">The chart data.</param>
		/// <returns>The flattened chart.</returns>
		protected string FlattenChart(ChartData chart)
		{
			var series = chart.Series.Select(s => s.Name + ": " + String.Join(", ", chart.Categories.Zip(s.Values, (c, v) => c + " " + v)));
			return $"[Chart{(chart.Title != null ? ": " + chart.Title : "")}] " + String.Join("; ", series);
		}

		/// <summary>
		/// Flattens the nodes of a SmartArt diagram on one line.
		/// </summary>
		/// <param name="nodes">The nodes.</param>
		/// <returns>The flattened diagram.</returns>
		protected static string FlattenSmartArt(List<SmartArtNode> nodes)
		{
			return String.Join("; ", nodes.Select(n => n.Children.Count == 0 ? n.Text : n.Text + " (" + FlattenSmartArt(n.Children) + ")"));
		}

		#endregion
	}
}

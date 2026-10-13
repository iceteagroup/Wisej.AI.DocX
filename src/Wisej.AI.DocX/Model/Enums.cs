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

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// Kinds of explicit breaks inside a run (<c>w:br/@w:type</c>).
	/// </summary>
	internal enum BreakKind
	{
		/// <summary>A line break (text wrapping break).</summary>
		Line,

		/// <summary>A hard page break.</summary>
		Page,

		/// <summary>A column break; it acts as a page break in single column sections.</summary>
		Column,
	}

	/// <summary>
	/// Tracked change state of a piece of content.
	/// </summary>
	internal enum RevisionKind
	{
		/// <summary>The content is not part of a tracked change.</summary>
		None,

		/// <summary>The content was inserted (<c>w:ins</c>, <c>w:moveTo</c>).</summary>
		Inserted,

		/// <summary>The content was deleted (<c>w:del</c>, <c>w:moveFrom</c>).</summary>
		Deleted,
	}

	/// <summary>
	/// Vertical merge state of a table cell (<c>w:vMerge</c>).
	/// </summary>
	internal enum VerticalMerge
	{
		/// <summary>The cell is not vertically merged.</summary>
		None,

		/// <summary>The cell starts a vertically merged region.</summary>
		Restart,

		/// <summary>The cell continues the merged region started above it.</summary>
		Continue,
	}

	/// <summary>
	/// How a section starts relative to the previous one (<c>w:sectPr/w:type</c>).
	/// </summary>
	internal enum SectionStart
	{
		/// <summary>The section starts on the next page (the default).</summary>
		NextPage,

		/// <summary>The section starts on the same page.</summary>
		Continuous,

		/// <summary>The section starts on the next odd page.</summary>
		OddPage,

		/// <summary>The section starts on the next even page.</summary>
		EvenPage,

		/// <summary>The section starts in the next column.</summary>
		NextColumn,
	}

	/// <summary>
	/// Interpretation of the line spacing value of a paragraph (<c>w:spacing/@w:lineRule</c>).
	/// </summary>
	internal enum LineSpacingRule
	{
		/// <summary>The value is a multiple of single line spacing.</summary>
		Auto,

		/// <summary>The value is the exact line height in points.</summary>
		Exact,

		/// <summary>The value is the minimum line height in points.</summary>
		AtLeast,
	}

	/// <summary>
	/// Horizontal alignment of a paragraph.
	/// </summary>
	internal enum ParagraphAlignment
	{
		/// <summary>Aligned to the leading edge.</summary>
		Left,

		/// <summary>Centered.</summary>
		Center,

		/// <summary>Aligned to the trailing edge.</summary>
		Right,

		/// <summary>Justified.</summary>
		Justify,
	}

	/// <summary>
	/// Semantic role of a paragraph, derived from its style.
	/// </summary>
	internal enum ParagraphKind
	{
		/// <summary>Regular body text.</summary>
		Normal,

		/// <summary>The document title (<c>Title</c> style).</summary>
		Title,

		/// <summary>The document subtitle (<c>Subtitle</c> style).</summary>
		Subtitle,

		/// <summary>A heading; see <see cref="ParagraphProperties.HeadingLevel"/>.</summary>
		Heading,

		/// <summary>A quotation (<c>Quote</c>, <c>Intense Quote</c>, <c>Block Text</c>).</summary>
		Quote,

		/// <summary>A caption of a figure or a table.</summary>
		Caption,

		/// <summary>An entry of a table of contents.</summary>
		TableOfContents,
	}

	/// <summary>
	/// Kinds of notes.
	/// </summary>
	internal enum NoteKind
	{
		/// <summary>A footnote, rendered at the bottom of the page that references it.</summary>
		Footnote,

		/// <summary>An endnote, rendered at the end of the document.</summary>
		Endnote,
	}

	/// <summary>
	/// Fields whose value depends on the page layout and is computed while rendering.
	/// </summary>
	internal enum FieldKind
	{
		/// <summary>The current page number (<c>PAGE</c>).</summary>
		Page,

		/// <summary>The total number of pages (<c>NUMPAGES</c>).</summary>
		NumPages,

		/// <summary>The number of pages in the current section (<c>SECTIONPAGES</c>).</summary>
		SectionPages,
	}

	/// <summary>
	/// The page selector of a header or footer.
	/// </summary>
	internal enum HeaderFooterKind
	{
		/// <summary>Used on every page unless a more specific header or footer applies.</summary>
		Default,

		/// <summary>Used on the first page of a section with a distinct first page.</summary>
		First,

		/// <summary>Used on even pages when odd and even headers differ.</summary>
		Even,
	}

	/// <summary>
	/// Alignment of a custom tab stop.
	/// </summary>
	internal enum TabStopKind
	{
		/// <summary>Text starts at the tab position.</summary>
		Left,

		/// <summary>Text is centered on the tab position.</summary>
		Center,

		/// <summary>Text ends at the tab position.</summary>
		Right,

		/// <summary>The decimal separator is aligned on the tab position.</summary>
		Decimal,
	}

	/// <summary>
	/// Text wrapping of a floating drawing.
	/// </summary>
	internal enum WrapKind
	{
		/// <summary>The drawing is inline with the text.</summary>
		Inline,

		/// <summary>The drawing floats in front of or behind the text and does not displace it.</summary>
		None,

		/// <summary>Text wraps around the drawing on its sides.</summary>
		Around,

		/// <summary>Text is placed above and below the drawing only.</summary>
		TopAndBottom,
	}
}

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

using System.Collections.Generic;

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// Base class of the content that flows inside a paragraph.
	/// </summary>
	internal abstract class Inline
	{
		/// <summary>Gets or sets the formatting of the inline content.</summary>
		public RunStyle Style { get; set; } = RunStyle.Default;

		/// <summary>Gets or sets the hyperlink target of the content, or <c>null</c>.</summary>
		public string Hyperlink { get; set; }

		/// <summary>Gets or sets the tracked change state of the content.</summary>
		public RevisionKind Revision { get; set; }

		/// <summary>
		/// Gets or sets whether the content is the placeholder text of an empty content control, such as
		/// "Click or tap here to enter text.". Word displays it, so it takes space in the layout, but it is
		/// not part of the extracted text.
		/// </summary>
		public bool IsPlaceholder { get; set; }

		/// <summary>
		/// Gets whether the content is visible on the printed page, and therefore takes space in the layout.
		/// </summary>
		public virtual bool IsVisible => !Style.Hidden;
	}

	/// <summary>
	/// A run of text with uniform formatting.
	/// </summary>
	internal sealed class TextInline : Inline
	{
		/// <summary>Gets or sets the text.</summary>
		public string Text { get; set; }

		/// <summary>
		/// Returns the text, useful while debugging.
		/// </summary>
		/// <returns>The text of the run.</returns>
		public override string ToString() => Text;
	}

	/// <summary>
	/// A tab character.
	/// </summary>
	internal sealed class TabInline : Inline
	{
	}

	/// <summary>
	/// An explicit line, page or column break.
	/// </summary>
	internal sealed class BreakInline : Inline
	{
		/// <summary>Gets or sets the kind of break.</summary>
		public BreakKind Kind { get; set; }
	}

	/// <summary>
	/// The marker Word writes where a page started the last time it laid out the document
	/// (<c>w:lastRenderedPageBreak</c>).
	/// </summary>
	internal sealed class RenderedPageBreakInline : Inline
	{
		/// <inheritdoc/>
		public override bool IsVisible => false;
	}

	/// <summary>
	/// Marks the position where a new page starts. Inserted by the paginator; the renderer splits pages here.
	/// </summary>
	internal sealed class PageStartInline : Inline
	{
		/// <summary>Gets or sets the number of the page that starts here.</summary>
		public int PageNumber { get; set; }

		/// <inheritdoc/>
		public override bool IsVisible => false;
	}

	/// <summary>
	/// A field whose value depends on the layout (<c>PAGE</c>, <c>NUMPAGES</c>, <c>SECTIONPAGES</c>).
	/// </summary>
	internal sealed class FieldInline : Inline
	{
		/// <summary>Gets or sets the kind of field.</summary>
		public FieldKind Kind { get; set; }

		/// <summary>Gets or sets the result cached in the document by the last application that updated the field.</summary>
		public string CachedText { get; set; }
	}

	/// <summary>
	/// A reference to a footnote or an endnote.
	/// </summary>
	internal sealed class NoteReferenceInline : Inline
	{
		/// <summary>Gets or sets whether the reference points to a footnote or to an endnote.</summary>
		public NoteKind Kind { get; set; }

		/// <summary>Gets or sets the identifier of the note.</summary>
		public string NoteId { get; set; }

		/// <summary>Gets or sets the label displayed for the reference (1, 2, i, ii, ...).</summary>
		public string Label { get; set; }
	}

	/// <summary>
	/// A reference to a comment.
	/// </summary>
	internal sealed class CommentReferenceInline : Inline
	{
		/// <summary>Gets or sets the identifier of the comment.</summary>
		public string CommentId { get; set; }

		/// <inheritdoc/>
		public override bool IsVisible => false;
	}

	/// <summary>
	/// An equation, converted to LaTeX.
	/// </summary>
	internal sealed class MathInline : Inline
	{
		/// <summary>Gets or sets the LaTeX source of the equation.</summary>
		public string Latex { get; set; }

		/// <summary>Gets or sets whether the equation is a display equation on its own line.</summary>
		public bool IsDisplay { get; set; }
	}

	/// <summary>
	/// Base class of drawings: pictures, charts, diagrams and text boxes.
	/// </summary>
	internal abstract class DrawingInline : Inline
	{
		/// <summary>Gets or sets the width of the drawing in points.</summary>
		public double Width { get; set; }

		/// <summary>Gets or sets the height of the drawing in points.</summary>
		public double Height { get; set; }

		/// <summary>Gets or sets how the text wraps around the drawing.</summary>
		public WrapKind Wrap { get; set; }

		/// <summary>Gets whether the drawing is anchored (floating) instead of inline with the text.</summary>
		public bool IsFloating => Wrap != WrapKind.Inline;

		/// <summary>
		/// Gets or sets whether the drawing is excluded from the text output by the options.
		/// Excluded drawings are kept in the model because they still take space on the page.
		/// </summary>
		public bool IsExcluded { get; set; }
	}

	/// <summary>
	/// A picture.
	/// </summary>
	internal sealed class ImageInline : DrawingInline
	{
		/// <summary>Gets or sets the extracted image.</summary>
		public DocxImage Image { get; set; }
	}

	/// <summary>
	/// A chart, with the data cached in the chart part.
	/// </summary>
	internal sealed class ChartInline : DrawingInline
	{
		/// <summary>Gets or sets the chart data.</summary>
		public ChartData Chart { get; set; }
	}

	/// <summary>
	/// A SmartArt diagram, reduced to its tree of text nodes.
	/// </summary>
	internal sealed class SmartArtInline : DrawingInline
	{
		/// <summary>Gets or sets the top level nodes of the diagram.</summary>
		public List<SmartArtNode> Nodes { get; set; } = new List<SmartArtNode>();
	}

	/// <summary>
	/// A text box or a shape containing text.
	/// </summary>
	internal sealed class TextBoxInline : DrawingInline
	{
		/// <summary>Gets or sets the content of the text box.</summary>
		public List<Block> Blocks { get; set; } = new List<Block>();
	}
}

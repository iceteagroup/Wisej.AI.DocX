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

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// A section of the document: a run of blocks that share the same page setup, headers and footers.
	/// </summary>
	internal sealed class Section
	{
		/// <summary>Gets the content of the section.</summary>
		public List<Block> Blocks { get; } = new List<Block>();

		/// <summary>Gets or sets the page geometry of the section.</summary>
		public PageSetup Page { get; set; } = new PageSetup();

		/// <summary>Gets or sets how the section starts relative to the previous one.</summary>
		public SectionStart Start { get; set; }

		/// <summary>Gets or sets whether the first page of the section has its own header and footer.</summary>
		public bool TitlePage { get; set; }

		/// <summary>Gets or sets the page number of the first page when numbering restarts, or <c>null</c>.</summary>
		public int? PageNumberStart { get; set; }

		/// <summary>Gets or sets the number format of the page numbers (<c>decimal</c>, <c>lowerRoman</c>, ...).</summary>
		public string PageNumberFormat { get; set; }

		/// <summary>Gets the headers of the section by page selector, inherited from previous sections when not redefined.</summary>
		public Dictionary<HeaderFooterKind, HeaderFooter> Headers { get; } = new Dictionary<HeaderFooterKind, HeaderFooter>();

		/// <summary>Gets the footers of the section by page selector, inherited from previous sections when not redefined.</summary>
		public Dictionary<HeaderFooterKind, HeaderFooter> Footers { get; } = new Dictionary<HeaderFooterKind, HeaderFooter>();
	}

	/// <summary>
	/// Page geometry of a section. All measures are in points.
	/// </summary>
	internal sealed class PageSetup
	{
		/// <summary>Gets or sets the page width (US Letter by default).</summary>
		public double Width { get; set; } = 612;

		/// <summary>Gets or sets the page height (US Letter by default).</summary>
		public double Height { get; set; } = 792;

		/// <summary>Gets or sets the top margin.</summary>
		public double MarginTop { get; set; } = 72;

		/// <summary>Gets or sets the bottom margin.</summary>
		public double MarginBottom { get; set; } = 72;

		/// <summary>Gets or sets the left margin.</summary>
		public double MarginLeft { get; set; } = 72;

		/// <summary>Gets or sets the right margin.</summary>
		public double MarginRight { get; set; } = 72;

		/// <summary>Gets or sets the gutter added to the binding margin.</summary>
		public double Gutter { get; set; }

		/// <summary>Gets or sets the distance from the top edge of the page to the header.</summary>
		public double HeaderDistance { get; set; } = 36;

		/// <summary>Gets or sets the distance from the bottom edge of the page to the footer.</summary>
		public double FooterDistance { get; set; } = 36;

		/// <summary>Gets or sets the number of text columns.</summary>
		public int Columns { get; set; } = 1;

		/// <summary>Gets or sets the space between text columns.</summary>
		public double ColumnSpacing { get; set; } = 36;

		/// <summary>Gets or sets the pitch of the document grid lines, or 0 when lines do not snap to a grid.</summary>
		public double GridLinePitch { get; set; }

		/// <summary>Gets the width available to the text of the section, across all columns.</summary>
		public double TextWidth => Math.Max(36, Width - MarginLeft - MarginRight - Gutter);

		/// <summary>Gets the width of a single text column.</summary>
		public double ColumnWidth => Columns <= 1 ? TextWidth : Math.Max(36, (TextWidth - (Columns - 1) * ColumnSpacing) / Columns);
	}

	/// <summary>
	/// A header or a footer story.
	/// </summary>
	internal sealed class HeaderFooter
	{
		/// <summary>Gets or sets the identifier of the story, unique within the document.</summary>
		public string Id { get; set; }

		/// <summary>Gets or sets whether the story is a header rather than a footer.</summary>
		public bool IsHeader { get; set; }

		/// <summary>Gets the content of the story.</summary>
		public List<Block> Blocks { get; } = new List<Block>();
	}

	/// <summary>
	/// A footnote or an endnote.
	/// </summary>
	internal sealed class Note
	{
		/// <summary>Gets or sets the identifier of the note.</summary>
		public string Id { get; set; }

		/// <summary>Gets or sets whether this is a footnote or an endnote.</summary>
		public NoteKind Kind { get; set; }

		/// <summary>Gets the content of the note.</summary>
		public List<Block> Blocks { get; } = new List<Block>();
	}

	/// <summary>
	/// A comment.
	/// </summary>
	internal sealed class Comment
	{
		/// <summary>Gets or sets the identifier of the comment.</summary>
		public string Id { get; set; }

		/// <summary>Gets or sets the author of the comment.</summary>
		public string Author { get; set; }

		/// <summary>Gets or sets the date of the comment, as written in the document.</summary>
		public string Date { get; set; }

		/// <summary>Gets or sets the text the comment is attached to.</summary>
		public string AnchorText { get; set; }

		/// <summary>Gets the content of the comment.</summary>
		public List<Block> Blocks { get; } = new List<Block>();
	}
}

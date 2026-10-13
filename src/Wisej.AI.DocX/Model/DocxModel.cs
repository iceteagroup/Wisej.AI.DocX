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
	/// The complete, format independent model of a Word document produced by the reader,
	/// paginated by the paginator and consumed by the renderers.
	/// </summary>
	internal sealed class DocxModel
	{
		/// <summary>Gets the sections of the main story, in document order.</summary>
		public List<Section> Sections { get; } = new List<Section>();

		/// <summary>Gets the footnotes by identifier.</summary>
		public Dictionary<string, Note> Footnotes { get; } = new Dictionary<string, Note>();

		/// <summary>Gets the endnotes by identifier.</summary>
		public Dictionary<string, Note> Endnotes { get; } = new Dictionary<string, Note>();

		/// <summary>Gets the comments by identifier.</summary>
		public Dictionary<string, Comment> Comments { get; } = new Dictionary<string, Comment>();

		/// <summary>Gets every image placement, in the order it was read.</summary>
		public List<DocxImage> Images { get; } = new List<DocxImage>();

		/// <summary>Gets or sets the document properties.</summary>
		public DocxMetadata Metadata { get; set; } = new DocxMetadata();

		/// <summary>Gets or sets the default tab stop interval in points.</summary>
		public double DefaultTabStop { get; set; } = 36;

		/// <summary>Gets or sets whether odd and even pages have different headers and footers.</summary>
		public bool EvenAndOddHeaders { get; set; }

		/// <summary>Gets or sets whether the document asks Word to update its fields when it is opened.</summary>
		public bool UpdateFieldsOnOpen { get; set; }

		/// <summary>Gets or sets the number of <c>w:lastRenderedPageBreak</c> markers in the main story.</summary>
		public int RenderedPageBreakCount { get; set; }

		/// <summary>Gets or sets the number of pages computed by the paginator.</summary>
		public int PageCount { get; set; } = 1;

		/// <summary>Gets or sets how the page boundaries were determined.</summary>
		public PaginationSource PaginationSource { get; set; } = PaginationSource.None;
	}
}

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

namespace Wisej.AI.DocX
{
	/// <summary>
	/// Strategy used to determine where the pages of the document start.
	/// </summary>
	/// <remarks>
	/// A DOCX file is a flow of content: it does not store pages. The page boundaries can only be
	/// recovered from the markers Word writes when it saves a document it has laid out, or estimated
	/// by laying out the document again.
	/// </remarks>
	public enum PaginationMode
	{
		/// <summary>
		/// Uses the page break markers saved by Word when they are present and consistent with the page
		/// count recorded in the document; otherwise estimates the layout, calibrated on the recorded page
		/// count when it is plausible. This is the recommended mode.
		/// </summary>
		Auto,

		/// <summary>
		/// Uses only the page break markers saved by Word (<c>w:lastRenderedPageBreak</c>) and the explicit
		/// page and section breaks.
		/// </summary>
		RenderedBreaks,

		/// <summary>
		/// Estimates the page boundaries with the built-in layout engine, ignoring the markers saved by Word.
		/// </summary>
		Estimate,

		/// <summary>
		/// Starts new pages only at explicit page breaks and at section breaks that start a new page.
		/// </summary>
		ExplicitBreaks,

		/// <summary>
		/// Does not split the document into pages: the whole content is returned as a single page.
		/// </summary>
		None,
	}
}

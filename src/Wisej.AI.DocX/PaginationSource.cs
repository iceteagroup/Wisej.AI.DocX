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
	/// Describes how the page boundaries of an extracted document were determined.
	/// </summary>
	public enum PaginationSource
	{
		/// <summary>The document was not paginated.</summary>
		None,

		/// <summary>The pages come from the explicit page and section breaks only.</summary>
		ExplicitBreaks,

		/// <summary>The pages come from the layout Word saved in the document: they match Word exactly.</summary>
		RenderedBreaks,

		/// <summary>The pages were estimated by the layout engine and calibrated on the page count recorded in the document.</summary>
		CalibratedEstimate,

		/// <summary>The pages were estimated by the layout engine.</summary>
		Estimate,
	}
}

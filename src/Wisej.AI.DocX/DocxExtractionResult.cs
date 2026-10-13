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

namespace Wisej.AI.DocX
{
	/// <summary>
	/// The result of the extraction of a Word document.
	/// </summary>
	public sealed class DocxExtractionResult
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DocxExtractionResult"/> class.
		/// </summary>
		/// <param name="text">The text of the whole document.</param>
		/// <param name="pages">The pages of the document.</param>
		/// <param name="images">The images of the document.</param>
		/// <param name="metadata">The document properties.</param>
		/// <param name="paginationSource">How the page boundaries were determined.</param>
		internal DocxExtractionResult(
			string text,
			IReadOnlyList<DocxPage> pages,
			IReadOnlyList<DocxImage> images,
			DocxMetadata metadata,
			PaginationSource paginationSource)
		{
			Text = text;
			Pages = pages;
			Images = images;
			Metadata = metadata;
			PaginationSource = paginationSource;
		}

		/// <summary>
		/// Gets the text of the whole document, with a header line introducing every page
		/// (see <see cref="DocxExtractorOptions.PageHeaderFormat"/>).
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// Gets the pages of the document. A document that is not paginated has a single page.
		/// </summary>
		public IReadOnlyList<DocxPage> Pages { get; }

		/// <summary>
		/// Gets the number of pages.
		/// </summary>
		public int PageCount => Pages.Count;

		/// <summary>
		/// Gets every image placed in the document, in reading order.
		/// </summary>
		public IReadOnlyList<DocxImage> Images { get; }

		/// <summary>
		/// Gets the document properties.
		/// </summary>
		public DocxMetadata Metadata { get; }

		/// <summary>
		/// Gets how the page boundaries were determined, which tells how reliable the page numbers are.
		/// </summary>
		public PaginationSource PaginationSource { get; }

		/// <summary>
		/// Returns the text of the whole document.
		/// </summary>
		/// <returns>The value of <see cref="Text"/>.</returns>
		public override string ToString() => Text;
	}
}

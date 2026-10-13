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

namespace Wisej.AI.DocX
{
	/// <summary>
	/// Options that control what the <see cref="DocxExtractor"/> extracts and how it is formatted.
	/// </summary>
	/// <remarks>
	/// The defaults produce Markdown tuned for language model context and retrieval: every piece of
	/// information in the document is kept (tables, list numbers, image alternative text, chart data,
	/// footnotes) while layout noise (repeated headers, decorative images, placeholder text) is removed.
	/// </remarks>
	public sealed class DocxExtractorOptions
	{
		#region Output

		/// <summary>
		/// Gets or sets the format of the extracted text. The default is <see cref="OutputFormat.Markdown"/>.
		/// </summary>
		public OutputFormat Format { get; set; } = OutputFormat.Markdown;

		/// <summary>
		/// Gets or sets the composite format of the line that introduces every page in
		/// <see cref="DocxExtractionResult.Text"/>, where <c>{0}</c> is the page number and <c>{1}</c>
		/// the total number of pages.
		/// </summary>
		/// <remarks>
		/// When <c>null</c> (the default), Markdown output uses <c>&lt;!-- Page {0} --&gt;</c> and plain text
		/// output uses <c>--- Page {0} ---</c>. Set it to an empty string to concatenate the pages
		/// without separators.
		/// </remarks>
		public string PageHeaderFormat { get; set; }

		/// <summary>
		/// Gets or sets whether character formatting (bold, italic, strikethrough, superscript,
		/// subscript and code fonts) is written as Markdown emphasis. The default is <c>true</c>.
		/// </summary>
		public bool IncludeFormatting { get; set; } = true;

		/// <summary>
		/// Gets or sets whether hyperlinks are written as Markdown links. The default is <c>true</c>.
		/// </summary>
		public bool IncludeHyperlinks { get; set; } = true;

		#endregion

		#region Pagination

		/// <summary>
		/// Gets or sets how the page boundaries are determined. The default is <see cref="PaginationMode.Auto"/>.
		/// </summary>
		public PaginationMode Pagination { get; set; } = PaginationMode.Auto;

		/// <summary>
		/// Gets or sets whether the fonts installed on the machine are read to measure text when the
		/// layout is estimated. The built-in metrics cover the most common Office fonts; system fonts
		/// improve the accuracy for the others. The default is <c>true</c>.
		/// </summary>
		public bool UseSystemFonts { get; set; } = true;

		#endregion

		#region Content

		/// <summary>
		/// Gets or sets how headers and footers are included. The default is <see cref="HeaderFooterMode.Once"/>.
		/// </summary>
		public HeaderFooterMode HeadersAndFooters { get; set; } = HeaderFooterMode.Once;

		/// <summary>
		/// Gets or sets whether footnotes and endnotes are included. Footnotes are written at the end
		/// of the page that references them, endnotes at the end of the document. The default is <c>true</c>.
		/// </summary>
		public bool IncludeFootnotes { get; set; } = true;

		/// <summary>
		/// Gets or sets whether comments are included at the end of the page that references them.
		/// The default is <c>false</c>.
		/// </summary>
		public bool IncludeComments { get; set; }

		/// <summary>
		/// Gets or sets how tracked changes are handled. The default is <see cref="TrackedChangesMode.Accept"/>.
		/// </summary>
		public TrackedChangesMode TrackedChanges { get; set; } = TrackedChangesMode.Accept;

		/// <summary>
		/// Gets or sets whether hidden text is included. The default is <c>false</c>.
		/// </summary>
		public bool IncludeHiddenText { get; set; }

		/// <summary>
		/// Gets or sets whether the table of contents is included. The default is <c>true</c>.
		/// </summary>
		/// <remarks>
		/// A table of contents repeats the headings of the document; excluding it saves tokens
		/// and avoids retrieval hits on the contents page instead of the actual section.
		/// </remarks>
		public bool IncludeTableOfContents { get; set; } = true;

		/// <summary>
		/// Gets or sets whether the content of text boxes and shapes is included. The default is <c>true</c>.
		/// </summary>
		public bool IncludeTextBoxes { get; set; } = true;

		/// <summary>
		/// Gets or sets whether the data of charts is included as a table. The default is <c>true</c>.
		/// </summary>
		public bool IncludeCharts { get; set; } = true;

		/// <summary>
		/// Gets or sets whether the text of SmartArt diagrams is included as a nested list. The default is <c>true</c>.
		/// </summary>
		public bool IncludeSmartArt { get; set; } = true;

		/// <summary>
		/// Gets or sets whether equations are included as LaTeX. The default is <c>true</c>.
		/// </summary>
		public bool IncludeEquations { get; set; } = true;

		#endregion

		#region Tables

		/// <summary>
		/// Gets or sets how tables are written in Markdown. The default is <see cref="TableFormat.Auto"/>.
		/// </summary>
		public TableFormat Tables { get; set; } = TableFormat.Auto;

		/// <summary>
		/// Gets or sets how merged cells are written in pipe tables. The default is <see cref="MergedCellMode.Repeat"/>.
		/// </summary>
		public MergedCellMode MergedCells { get; set; } = MergedCellMode.Repeat;

		/// <summary>
		/// Gets or sets whether tables with a single row, commonly used to draw boxes around text or to place
		/// a picture next to its caption, are written as regular content instead of tables. The default is <c>true</c>.
		/// </summary>
		public bool UnwrapLayoutTables { get; set; } = true;

		#endregion

		#region Images

		/// <summary>
		/// Gets or sets whether images are referenced in the text and listed in
		/// <see cref="DocxExtractionResult.Images"/>. The default is <c>true</c>.
		/// </summary>
		public bool IncludeImages { get; set; } = true;

		/// <summary>
		/// Gets or sets whether the bytes of the images are loaded into <see cref="DocxImage.Data"/>.
		/// The default is <c>true</c>.
		/// </summary>
		public bool IncludeImageData { get; set; } = true;

		/// <summary>
		/// Gets or sets whether images marked as decorative by the author are skipped. The default is <c>true</c>.
		/// </summary>
		public bool SkipDecorativeImages { get; set; } = true;

		/// <summary>
		/// Gets or sets the minimum displayed size, in points, of an image to be included; smaller images
		/// (bullets, spacers, icons) are skipped. The default is <c>0</c>, which includes every image.
		/// </summary>
		public double MinimumImageSize { get; set; }

		/// <summary>
		/// Gets or sets an optional callback that describes an image, for example with a vision model or OCR.
		/// The returned text is written after the image reference. Return <c>null</c> to add nothing.
		/// </summary>
		/// <remarks>
		/// The callback is invoked once per distinct picture: placements of the same picture reuse the description.
		/// <see cref="DocxImage.Data"/> is always loaded for the callback, regardless of <see cref="IncludeImageData"/>.
		/// </remarks>
		public Func<DocxImage, string> ImageDescriber { get; set; }

		#endregion

		/// <summary>
		/// Creates a copy of these options.
		/// </summary>
		/// <returns>A new <see cref="DocxExtractorOptions"/> with the same values.</returns>
		public DocxExtractorOptions Clone()
		{
			return (DocxExtractorOptions)MemberwiseClone();
		}
	}
}

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
using System.IO;
using Wisej.AI.DocX.Layout;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;
using Wisej.AI.DocX.Reading;
using Wisej.AI.DocX.Rendering;

namespace Wisej.AI.DocX
{
	/// <summary>
	/// Extracts the content of Word documents (DOCX, DOCM, DOTX, DOTM and Flat OPC) as Markdown or
	/// plain text, split into pages, ready to be used as language model context.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The extractor reads every part of the document that carries information: body text with its
	/// formatting, headings, numbered and bulleted lists with their real labels, tables (including
	/// merged and nested cells), images with their alternative text, charts (as data tables), SmartArt
	/// diagrams, equations (as LaTeX), text boxes, footnotes, endnotes, comments, headers, footers,
	/// tracked changes and document properties.
	/// </para>
	/// <para>
	/// Pages are recovered from the layout Word saved in the document when it is available and
	/// consistent, otherwise they are estimated by a layout engine that measures text with real font
	/// metrics and applies Word's pagination rules; see <see cref="PaginationMode"/>.
	/// </para>
	/// <para>
	/// Instances are not thread safe but are cheap: create one per thread or per document.
	/// </para>
	/// <para>
	/// Example:
	/// <code><![CDATA[
	/// var extractor = new DocxExtractor();
	/// var result = extractor.Extract("report.docx");
	///
	/// foreach (var page in result.Pages)
	///     Console.WriteLine($"Page {page.Number}: {page.Text.Length} characters");
	///
	/// File.WriteAllText("report.md", result.Text);
	/// ]]></code>
	/// </para>
	/// </remarks>
	public sealed class DocxExtractor
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DocxExtractor"/> class with the default options.
		/// </summary>
		public DocxExtractor()
			: this(new DocxExtractorOptions())
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DocxExtractor"/> class.
		/// </summary>
		/// <param name="options">The extraction options.</param>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <c>null</c>.</exception>
		public DocxExtractor(DocxExtractorOptions options)
		{
			Options = options ?? throw new ArgumentNullException(nameof(options));
		}

		/// <summary>
		/// Gets the extraction options.
		/// </summary>
		public DocxExtractorOptions Options { get; }

		#region Extract

		/// <summary>
		/// Extracts the content of a Word document stored in a file.
		/// </summary>
		/// <param name="path">The path of the document.</param>
		/// <returns>The extracted content.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <c>null</c>.</exception>
		/// <exception cref="InvalidDataException">Thrown when the file is not a valid Word document.</exception>
		public DocxExtractionResult Extract(string path)
		{
			if (path == null)
				throw new ArgumentNullException(nameof(path));

			using (var stream = File.OpenRead(path))
				return Extract(stream);
		}

		/// <summary>
		/// Extracts the content of a Word document from a stream.
		/// </summary>
		/// <param name="stream">The stream containing the document. It is read from its current position and is not closed.</param>
		/// <returns>The extracted content.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
		/// <exception cref="InvalidDataException">Thrown when the stream does not contain a valid Word document.</exception>
		public DocxExtractionResult Extract(Stream stream)
		{
			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			var model = ReadModel(stream);

			Paginator.Paginate(model, Options);
			DescribeImages(model);

			var renderer = Options.Format == OutputFormat.PlainText
				? (DocumentRenderer)new PlainTextRenderer(model, Options)
				: new MarkdownRenderer(model, Options);

			var result = renderer.Render();

			// The bytes were only loaded for the describer.
			if (!Options.IncludeImageData)
			{
				foreach (var image in model.Images)
					image.Data = null;
			}

			return result;
		}

		/// <summary>
		/// Extracts the text of a Word document with the default options.
		/// </summary>
		/// <param name="stream">The stream containing the document. It is read from its current position and is not closed.</param>
		/// <param name="options">The extraction options, or <c>null</c> for the defaults.</param>
		/// <returns>The text of the document, with a header line introducing every page.</returns>
		/// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is <c>null</c>.</exception>
		/// <exception cref="InvalidDataException">Thrown when the stream does not contain a valid Word document.</exception>
		public static string ExtractText(Stream stream, DocxExtractorOptions options = null)
		{
			return new DocxExtractor(options ?? new DocxExtractorOptions()).Extract(stream).Text;
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Reads a document into the model, without paginating it.
		/// </summary>
		/// <param name="stream">The stream containing the document.</param>
		/// <returns>The document model.</returns>
		internal DocxModel ReadModel(Stream stream)
		{
			var buffer = DocxPackage.Buffer(stream);

			try
			{
				return ReadModel(buffer, repair: false);
			}
			catch (Exception ex) when (DocxPackage.IsRepairable(ex))
			{
				// Word opens many packages that do not strictly follow the Open Packaging Conventions.
				return ReadModel(buffer, repair: true);
			}
		}

		/// <summary>
		/// Opens a buffered document and reads it into the model.
		/// </summary>
		/// <param name="buffer">The buffered document.</param>
		/// <param name="repair">Whether the package is repaired before it is opened.</param>
		/// <returns>The document model.</returns>
		private DocxModel ReadModel(MemoryStream buffer, bool repair)
		{
			using (var package = DocxPackage.Open(buffer, repair))
				return new DocumentReader(package, Options).Read();
		}

		/// <summary>
		/// Invokes the image describer once for every distinct picture and assigns the description to all its placements.
		/// </summary>
		/// <param name="model">The document model.</param>
		private void DescribeImages(DocxModel model)
		{
			var describer = Options.ImageDescriber;
			if (describer == null)
				return;

			var descriptions = new Dictionary<string, string>(StringComparer.Ordinal);

			foreach (var image in model.Images)
			{
				var key = image.PartUri ?? image.FileName;
				if (!descriptions.TryGetValue(key, out var description))
				{
					description = describer(image);
					descriptions[key] = description;
				}

				image.Description = String.IsNullOrWhiteSpace(description) ? null : description.Trim();
			}
		}

		#endregion
	}
}

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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Builds minimal WordprocessingML packages from raw XML, so that every test controls exactly the markup it exercises.
	/// </summary>
	internal sealed class DocxBuilder
	{
		/// <summary>The namespace declarations added to the root element of every part.</summary>
		public const string Namespaces =
			"xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\" " +
			"xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\" " +
			"xmlns:wp=\"http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing\" " +
			"xmlns:a=\"http://schemas.openxmlformats.org/drawingml/2006/main\" " +
			"xmlns:pic=\"http://schemas.openxmlformats.org/drawingml/2006/picture\" " +
			"xmlns:c=\"http://schemas.openxmlformats.org/drawingml/2006/chart\" " +
			"xmlns:m=\"http://schemas.openxmlformats.org/officeDocument/2006/math\" " +
			"xmlns:mc=\"http://schemas.openxmlformats.org/markup-compatibility/2006\" " +
			"xmlns:v=\"urn:schemas-microsoft-com:vml\" " +
			"xmlns:o=\"urn:schemas-microsoft-com:office:office\" " +
			"xmlns:wps=\"http://schemas.microsoft.com/office/word/2010/wordprocessingShape\" " +
			"xmlns:w14=\"http://schemas.microsoft.com/office/word/2010/wordml\"";

		/// <summary>A style sheet with the built-in styles used by the tests.</summary>
		public const string DefaultStyles =
			"<w:docDefaults><w:rPrDefault><w:rPr><w:rFonts w:ascii=\"Calibri\" w:hAnsi=\"Calibri\"/><w:sz w:val=\"22\"/></w:rPr></w:rPrDefault>" +
			"<w:pPrDefault><w:pPr><w:spacing w:after=\"160\" w:line=\"259\" w:lineRule=\"auto\"/></w:pPr></w:pPrDefault></w:docDefaults>" +
			"<w:style w:type=\"paragraph\" w:default=\"1\" w:styleId=\"Normal\"><w:name w:val=\"Normal\"/></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"Heading1\"><w:name w:val=\"heading 1\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:keepNext/><w:outlineLvl w:val=\"0\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"32\"/></w:rPr></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"Heading2\"><w:name w:val=\"heading 2\"/><w:basedOn w:val=\"Normal\"/><w:pPr><w:keepNext/><w:outlineLvl w:val=\"1\"/></w:pPr><w:rPr><w:b/><w:sz w:val=\"26\"/></w:rPr></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"Title\"><w:name w:val=\"Title\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:sz w:val=\"56\"/></w:rPr></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"Quote\"><w:name w:val=\"Quote\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:i/></w:rPr></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"TOC1\"><w:name w:val=\"toc 1\"/><w:basedOn w:val=\"Normal\"/></w:style>" +
			"<w:style w:type=\"paragraph\" w:styleId=\"Code\"><w:name w:val=\"Code\"/><w:basedOn w:val=\"Normal\"/><w:rPr><w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/></w:rPr></w:style>" +
			"<w:style w:type=\"table\" w:default=\"1\" w:styleId=\"TableNormal\"><w:name w:val=\"Normal Table\"/></w:style>" +
			"<w:style w:type=\"table\" w:styleId=\"TableGrid\"><w:name w:val=\"Table Grid\"/><w:pPr><w:spacing w:after=\"0\" w:line=\"240\" w:lineRule=\"auto\"/></w:pPr></w:style>";

		private readonly Dictionary<string, byte[]> _parts = new Dictionary<string, byte[]>();
		private readonly List<(string Id, string Type, string Target, bool External)> _documentRelationships = new List<(string, string, string, bool)>();
		private readonly List<(string PartName, string ContentType)> _overrides = new List<(string, string)>();
		private string _body = "";
		private string _app;

		/// <summary>
		/// Sets the inner XML of <c>w:body</c>.
		/// </summary>
		/// <param name="xml">The body content.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Body(string xml)
		{
			_body = xml;
			return this;
		}

		/// <summary>
		/// Adds the styles part, with the inner XML of <c>w:styles</c>.
		/// </summary>
		/// <param name="xml">The style definitions; <see cref="DefaultStyles"/> when omitted.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Styles(string xml = DefaultStyles)
		{
			return AddWordPart("styles.xml", "styles", "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml", $"<w:styles {Namespaces}>{xml}</w:styles>");
		}

		/// <summary>
		/// Adds the numbering part, with the inner XML of <c>w:numbering</c>.
		/// </summary>
		/// <param name="xml">The numbering definitions.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Numbering(string xml)
		{
			return AddWordPart("numbering.xml", "numbering", "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml", $"<w:numbering {Namespaces}>{xml}</w:numbering>");
		}

		/// <summary>
		/// Adds the footnotes part, with the inner XML of <c>w:footnotes</c>.
		/// </summary>
		/// <param name="xml">The footnotes.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Footnotes(string xml)
		{
			return AddWordPart("footnotes.xml", "footnotes", "application/vnd.openxmlformats-officedocument.wordprocessingml.footnotes+xml", $"<w:footnotes {Namespaces}>{xml}</w:footnotes>");
		}

		/// <summary>
		/// Adds the comments part, with the inner XML of <c>w:comments</c>.
		/// </summary>
		/// <param name="xml">The comments.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Comments(string xml)
		{
			return AddWordPart("comments.xml", "comments", "application/vnd.openxmlformats-officedocument.wordprocessingml.comments+xml", $"<w:comments {Namespaces}>{xml}</w:comments>");
		}

		/// <summary>
		/// Adds the settings part, with the inner XML of <c>w:settings</c>.
		/// </summary>
		/// <param name="xml">The settings.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Settings(string xml)
		{
			return AddWordPart("settings.xml", "settings", "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml", $"<w:settings {Namespaces}>{xml}</w:settings>");
		}

		/// <summary>
		/// Adds a header part referenced by <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The relationship identifier used in <c>w:headerReference</c>.</param>
		/// <param name="xml">The inner XML of <c>w:hdr</c>.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Header(string id, string xml)
		{
			var name = $"header{_documentRelationships.Count + 1}.xml";
			_parts["word/" + name] = Encoding.UTF8.GetBytes($"<w:hdr {Namespaces}>{xml}</w:hdr>");
			_overrides.Add(("/word/" + name, "application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"));
			_documentRelationships.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/header", name, false));
			return this;
		}

		/// <summary>
		/// Adds a footer part referenced by <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The relationship identifier used in <c>w:footerReference</c>.</param>
		/// <param name="xml">The inner XML of <c>w:ftr</c>.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Footer(string id, string xml)
		{
			var name = $"footer{_documentRelationships.Count + 1}.xml";
			_parts["word/" + name] = Encoding.UTF8.GetBytes($"<w:ftr {Namespaces}>{xml}</w:ftr>");
			_overrides.Add(("/word/" + name, "application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"));
			_documentRelationships.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer", name, false));
			return this;
		}

		/// <summary>
		/// Adds an image part referenced by <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The relationship identifier used in <c>a:blip/@r:embed</c>.</param>
		/// <param name="name">The file name of the image, such as <c>image1.png</c>.</param>
		/// <param name="data">The image bytes.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Image(string id, string name, byte[] data)
		{
			_parts["word/media/" + name] = data;
			_documentRelationships.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", "media/" + name, false));
			return this;
		}

		/// <summary>
		/// Adds a chart part referenced by <paramref name="id"/>.
		/// </summary>
		/// <param name="id">The relationship identifier used in <c>c:chart/@r:id</c>.</param>
		/// <param name="xml">The inner XML of <c>c:chartSpace</c>.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Chart(string id, string xml)
		{
			var name = $"charts/chart{_documentRelationships.Count + 1}.xml";
			_parts["word/" + name] = Encoding.UTF8.GetBytes($"<c:chartSpace {Namespaces}>{xml}</c:chartSpace>");
			_overrides.Add(("/word/" + name, "application/vnd.openxmlformats-officedocument.drawingml.chart+xml"));
			_documentRelationships.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/chart", name, false));
			return this;
		}

		/// <summary>
		/// Adds an external hyperlink relationship.
		/// </summary>
		/// <param name="id">The relationship identifier used in <c>w:hyperlink/@r:id</c>.</param>
		/// <param name="url">The target URL.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder Hyperlink(string id, string url)
		{
			_documentRelationships.Add((id, "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink", url, true));
			return this;
		}

		/// <summary>
		/// Adds the extended properties part with the page count recorded by the application.
		/// </summary>
		/// <param name="pages">The recorded page count.</param>
		/// <returns>This builder.</returns>
		public DocxBuilder App(int pages)
		{
			_app = "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\">" +
				$"<Application>Microsoft Office Word</Application><Pages>{pages}</Pages><Company>ICE TEA GROUP</Company></Properties>";
			return this;
		}

		/// <summary>
		/// Builds the package.
		/// </summary>
		/// <returns>A stream positioned at the start of the package.</returns>
		public MemoryStream Build()
		{
			var parts = new Dictionary<string, byte[]>(_parts)
			{
				["word/document.xml"] = Encoding.UTF8.GetBytes($"<w:document {Namespaces}><w:body>{_body}</w:body></w:document>"),
			};

			var rootRelationships = new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
			rootRelationships.Append("<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/>");

			var types = new StringBuilder("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
			types.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
			types.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
			types.Append("<Default Extension=\"png\" ContentType=\"image/png\"/>");
			types.Append("<Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/>");

			foreach (var item in _overrides)
				types.Append($"<Override PartName=\"{item.PartName}\" ContentType=\"{item.ContentType}\"/>");

			if (_app != null)
			{
				parts["docProps/app.xml"] = Encoding.UTF8.GetBytes(_app);
				types.Append("<Override PartName=\"/docProps/app.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.extended-properties+xml\"/>");
				rootRelationships.Append("<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties\" Target=\"docProps/app.xml\"/>");
			}

			types.Append("</Types>");
			rootRelationships.Append("</Relationships>");

			var documentRelationships = new StringBuilder("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
			foreach (var relationship in _documentRelationships)
			{
				documentRelationships.Append($"<Relationship Id=\"{relationship.Id}\" Type=\"{relationship.Type}\" Target=\"{System.Security.SecurityElement.Escape(relationship.Target)}\"");
				documentRelationships.Append(relationship.External ? " TargetMode=\"External\"/>" : "/>");
			}

			documentRelationships.Append("</Relationships>");

			parts["[Content_Types].xml"] = Encoding.UTF8.GetBytes(types.ToString());
			parts["_rels/.rels"] = Encoding.UTF8.GetBytes(rootRelationships.ToString());
			parts["word/_rels/document.xml.rels"] = Encoding.UTF8.GetBytes(documentRelationships.ToString());

			var stream = new MemoryStream();
			using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var part in parts.OrderBy(p => p.Key == "[Content_Types].xml" ? 0 : 1))
				{
					using (var entry = zip.CreateEntry(part.Key).Open())
						entry.Write(part.Value, 0, part.Value.Length);
				}
			}

			stream.Position = 0;
			return stream;
		}

		/// <summary>
		/// Extracts the document with the specified options.
		/// </summary>
		/// <param name="options">The options, or <c>null</c> for the defaults without pagination headers.</param>
		/// <returns>The extraction result.</returns>
		public DocxExtractionResult Extract(DocxExtractorOptions options = null)
		{
			options = options ?? new DocxExtractorOptions();

			using (var stream = Build())
				return new DocxExtractor(options).Extract(stream);
		}

		/// <summary>
		/// Adds a part related to the main document part.
		/// </summary>
		/// <param name="name">The file name of the part in the <c>word</c> folder.</param>
		/// <param name="type">The last segment of the relationship type.</param>
		/// <param name="contentType">The content type of the part.</param>
		/// <param name="xml">The XML of the part.</param>
		/// <returns>This builder.</returns>
		private DocxBuilder AddWordPart(string name, string type, string contentType, string xml)
		{
			_parts["word/" + name] = Encoding.UTF8.GetBytes(xml);
			_overrides.Add(("/word/" + name, contentType));
			_documentRelationships.Add(($"rIdPart{_documentRelationships.Count}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/" + type, name, false));
			return this;
		}
	}
}

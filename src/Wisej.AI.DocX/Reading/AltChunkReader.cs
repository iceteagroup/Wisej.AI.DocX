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
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Extracts the text of embedded chunks in other formats (<c>w:altChunk</c>): HTML, plain text and nested Word documents.
	/// </summary>
	/// <remarks>
	/// Word merges these chunks into the document the first time it saves it, but documents generated
	/// by software often keep them, and without this reader their content would be lost.
	/// </remarks>
	internal static class AltChunkReader
	{
		private static readonly Regex Scripts = new Regex(@"<(script|style|head)[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);
		private static readonly Regex BlockTags = new Regex(@"<\s*/?\s*(p|div|br|li|tr|h[1-6]|table|ul|ol|blockquote|pre)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		private static readonly Regex CellTags = new Regex(@"<\s*/?\s*t[dh]\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
		private static readonly Regex Tags = new Regex(@"<[^>]+>", RegexOptions.CultureInvariant);
		private static readonly Regex BlankLines = new Regex(@"\n\s*\n\s*(\n\s*)+", RegexOptions.CultureInvariant);

		/// <summary>
		/// Extracts the text of an alternative format part.
		/// </summary>
		/// <param name="part">The alternative format import part.</param>
		/// <param name="options">The extraction options, used for nested Word documents.</param>
		/// <returns>The extracted text, or <c>null</c> when the format is not supported.</returns>
		public static string Read(OpenXmlPart part, DocxExtractorOptions options)
		{
			try
			{
				var contentType = part.ContentType?.ToLowerInvariant() ?? "";
				var data = DocxPackage.GetBytes(part);

				if (contentType.Contains("wordprocessingml") || contentType.Contains("msword"))
				{
					var nested = options.Clone();
					nested.Pagination = PaginationMode.None;
					nested.PageHeaderFormat = "";

					using (var stream = new MemoryStream(data))
						return new DocxExtractor(nested).Extract(stream).Text;
				}

				if (contentType.Contains("html"))
					return HtmlToText(Decode(data));

				if (contentType.StartsWith("text/plain"))
					return Decode(data);

				return null;
			}
			catch (Exception ex) when (!(ex is OutOfMemoryException))
			{
				return null;
			}
		}

		/// <summary>
		/// Converts HTML to plain text, keeping the paragraph structure.
		/// </summary>
		/// <param name="html">The HTML source.</param>
		/// <returns>The text.</returns>
		private static string HtmlToText(string html)
		{
			var text = Scripts.Replace(html, "");
			text = CellTags.Replace(text, " | ");
			text = BlockTags.Replace(text, "\n");
			text = Tags.Replace(text, "");
			text = WebUtility.HtmlDecode(text).Replace(' ', ' ');

			return BlankLines.Replace(text, "\n\n").Trim();
		}

		/// <summary>
		/// Decodes text bytes, honoring a byte order mark.
		/// </summary>
		/// <param name="data">The bytes.</param>
		/// <returns>The decoded text.</returns>
		private static string Decode(byte[] data)
		{
			using (var reader = new StreamReader(new MemoryStream(data), Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
				return reader.ReadToEnd();
		}
	}
}

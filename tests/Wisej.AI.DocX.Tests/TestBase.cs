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

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Helpers shared by the tests: markup shortcuts and extraction with test friendly defaults.
	/// </summary>
	public abstract class TestBase
	{
		/// <summary>
		/// Returns a paragraph.
		/// </summary>
		/// <param name="content">The runs and other inline markup.</param>
		/// <param name="pPr">The inner XML of <c>w:pPr</c>.</param>
		/// <returns>The paragraph markup.</returns>
		protected static string P(string content, string pPr = "")
		{
			return pPr.Length == 0 ? $"<w:p>{content}</w:p>" : $"<w:p><w:pPr>{pPr}</w:pPr>{content}</w:p>";
		}

		/// <summary>
		/// Returns a run of text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="rPr">The inner XML of <c>w:rPr</c>.</param>
		/// <returns>The run markup.</returns>
		protected static string R(string text, string rPr = "")
		{
			var t = $"<w:t xml:space=\"preserve\">{System.Security.SecurityElement.Escape(text)}</w:t>";
			return rPr.Length == 0 ? $"<w:r>{t}</w:r>" : $"<w:r><w:rPr>{rPr}</w:rPr>{t}</w:r>";
		}

		/// <summary>
		/// Returns a paragraph with the specified style containing a single run.
		/// </summary>
		/// <param name="style">The style identifier.</param>
		/// <param name="text">The text.</param>
		/// <returns>The paragraph markup.</returns>
		protected static string Styled(string style, string text)
		{
			return P(R(text), $"<w:pStyle w:val=\"{style}\"/>");
		}

		/// <summary>
		/// Extracts a document as Markdown without page separators.
		/// </summary>
		/// <param name="builder">The document builder.</param>
		/// <param name="configure">Changes the options before the extraction. Can be <c>null</c>.</param>
		/// <returns>The extracted text.</returns>
		internal static string Markdown(DocxBuilder builder, Action<DocxExtractorOptions> configure = null)
		{
			var options = new DocxExtractorOptions { Pagination = PaginationMode.None };
			configure?.Invoke(options);

			return builder.Extract(options).Text.Replace("\r\n", "\n");
		}

		/// <summary>
		/// Extracts a document as plain text without page separators.
		/// </summary>
		/// <param name="builder">The document builder.</param>
		/// <param name="configure">Changes the options before the extraction. Can be <c>null</c>.</param>
		/// <returns>The extracted text.</returns>
		internal static string PlainText(DocxBuilder builder, Action<DocxExtractorOptions> configure = null)
		{
			return Markdown(builder, options =>
			{
				options.Format = OutputFormat.PlainText;
				configure?.Invoke(options);
			});
		}
	}
}

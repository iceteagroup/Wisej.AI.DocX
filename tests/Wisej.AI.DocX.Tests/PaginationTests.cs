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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Tests the page detection strategies.
	/// </summary>
	public class PaginationTests : TestBase
	{
		/// <summary>A long paragraph of filler text.</summary>
		private const string Lorem =
			"Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. " +
			"Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. " +
			"Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.";

		/// <summary>The marker Word writes where a page started.</summary>
		private const string Rendered = "<w:r><w:lastRenderedPageBreak/></w:r>";

		/// <summary>US Letter page with one inch margins, so the tests do not depend on Word's regional defaults.</summary>
		private const string Section =
			"<w:sectPr><w:pgSz w:w=\"12240\" w:h=\"15840\"/>" +
			"<w:pgMar w:top=\"1440\" w:right=\"1440\" w:bottom=\"1440\" w:left=\"1440\" w:header=\"720\" w:footer=\"720\" w:gutter=\"0\"/></w:sectPr>";

		[Fact]
		public void ExplicitPageBreaks_StartNewPages()
		{
			var builder = new DocxBuilder().Styles().Body(
				P(R("One")) + P("<w:r><w:br w:type=\"page\"/></w:r>") + P(R("Two")) + P(R("Three"), "<w:pageBreakBefore/>"));

			var result = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.ExplicitBreaks });

			Assert.Equal(3, result.PageCount);
			Assert.Equal(new[] { "One", "Two", "Three" }, result.Pages.Select(p => p.Text));
			Assert.Equal("<!-- Page 1 -->\n\nOne\n\n<!-- Page 2 -->\n\nTwo\n\n<!-- Page 3 -->\n\nThree", result.Text);
		}

		[Fact]
		public void RenderedBreaks_ConsistentWithThePageCount_AreUsedAsIs()
		{
			var builder = new DocxBuilder().Styles().App(pages: 2).Body(
				P(R("First page text.")) + P(Rendered + R("Second page text.")));

			var result = builder.Extract();

			Assert.Equal(PaginationSource.RenderedBreaks, result.PaginationSource);
			Assert.Equal(new[] { "First page text.", "Second page text." }, result.Pages.Select(p => p.Text));
		}

		[Fact]
		public void RenderedBreaks_InsideAParagraph_SplitTheParagraph()
		{
			var builder = new DocxBuilder().Styles().App(pages: 2).Body(
				P(R("End of page one ") + "<w:r><w:lastRenderedPageBreak/><w:t>start of page two.</w:t></w:r>"));

			var result = builder.Extract();

			Assert.Equal("End of page one", result.Pages[0].Text);
			Assert.Equal("start of page two.", result.Pages[1].Text);
		}

		[Fact]
		public void RenderedBreaks_AfterAnExplicitBreak_AreMerged()
		{
			var builder = new DocxBuilder().Styles().App(pages: 2).Body(
				P(R("One") + "<w:r><w:br w:type=\"page\"/></w:r>") + P(Rendered + R("Two")));

			var result = builder.Extract();

			Assert.Equal(2, result.PageCount);
			Assert.Equal("Two", result.Pages[1].Text);
		}

		[Fact]
		public void RenderedBreaks_InMultiColumnSections_CountColumns()
		{
			var builder = new DocxBuilder().Styles().App(pages: 2).Body(
				P(R("Column 1")) + P(Rendered + R("Column 2")) + P(Rendered + R("Column 3")) + P(Rendered + R("Column 4")) +
				"<w:sectPr><w:cols w:num=\"2\" w:space=\"720\"/></w:sectPr>");

			var result = builder.Extract();

			Assert.Equal(2, result.PageCount);
			Assert.Equal("Column 1\n\nColumn 2", result.Pages[0].Text);
			Assert.Equal("Column 3\n\nColumn 4", result.Pages[1].Text);
		}

		[Fact]
		public void TableRows_StartPagesAndRepeatTheHeader()
		{
			var row = "<w:tr><w:tc><w:p>{0}<w:r><w:t>{1}</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>{2}</w:t></w:r></w:p></w:tc></w:tr>";
			var table = "<w:tbl><w:tblGrid><w:gridCol w:w=\"3000\"/><w:gridCol w:w=\"3000\"/></w:tblGrid>" +
				String.Format(row, "", "Item", "Qty") +
				String.Format(row, "", "Apples", "3") +
				String.Format(row, Rendered, "Pears", "5") +
				"</w:tbl>";

			var builder = new DocxBuilder().Styles().App(pages: 2).Body(table);
			var result = builder.Extract();

			Assert.Equal("| Item | Qty |\n| --- | --- |\n| Apples | 3 |", result.Pages[0].Text);
			Assert.Equal("| Item | Qty |\n| --- | --- |\n| Pears | 5 |", result.Pages[1].Text);
		}

		[Fact]
		public void SectionBreaks_StartNewPagesAndEvenSectionsAddBlankPages()
		{
			// The third section must start on an even page: page 3 is left blank.
			var builder = new DocxBuilder().Styles().Body(
				P(R("Chapter one"), "<w:sectPr><w:type w:val=\"nextPage\"/></w:sectPr>") +
				P(R("Chapter two"), "<w:sectPr><w:type w:val=\"nextPage\"/></w:sectPr>") +
				P(R("Chapter three")) + "<w:sectPr><w:type w:val=\"evenPage\"/></w:sectPr>");

			var result = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.ExplicitBreaks });

			Assert.Equal(new[] { "Chapter one", "Chapter two", "", "Chapter three" }, result.Pages.Select(p => p.Text));
		}

		[Fact]
		public void Estimate_LaysOutLongDocuments()
		{
			// 60 paragraphs of 4 lines in 11pt Calibri on Letter paper. Microsoft Word lays them out on
			// 7 pages starting with paragraphs 0, 10, 19, 29, 38, 48 and 57 (recorded with Word 16).
			var body = new StringBuilder();
			for (var i = 0; i < 60; i++)
				body.Append(P(R($"{i}. {Lorem}")));

			body.Append(Section);

			var result = new DocxBuilder().Styles().Body(body.ToString()).Extract(new DocxExtractorOptions { Pagination = PaginationMode.Estimate });

			// A page can begin with the end of a paragraph started on the previous page: compare the
			// first paragraph that starts on each page. "10." is escaped as "10\." in Markdown.
			var firstParagraphs = result.Pages.Select(p => Regex.Match(p.Text, @"(?m)^(\d+)\\\.").Groups[1].Value);

			Assert.Equal(PaginationSource.Estimate, result.PaginationSource);
			Assert.Equal(7, result.PageCount);
			Assert.Equal(new[] { "0", "10", "19", "29", "38", "48", "57" }, firstParagraphs);
		}

		[Fact]
		public void Estimate_KeepsHeadingsWithTheFollowingParagraph()
		{
			// Exact 12pt lines on a 648pt body: 54 lines per page. After 53 lines the heading would fit
			// on the last line, but keep with next moves it to the second page with its paragraph.
			const string line = "<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"exact\"/>";

			var body = new StringBuilder();
			for (var i = 0; i < 53; i++)
				body.Append(P(R($"Line {i}"), line));

			body.Append(P(R("Next chapter"), "<w:pStyle w:val=\"Heading1\"/>" + line));
			body.Append(P(R("First paragraph of the chapter."), line));
			body.Append(Section);

			var result = new DocxBuilder().Styles().Body(body.ToString()).Extract(new DocxExtractorOptions { Pagination = PaginationMode.Estimate });

			Assert.Equal(2, result.PageCount);
			Assert.EndsWith("Line 52", result.Pages[0].Text);
			Assert.Equal("# Next chapter\n\nFirst paragraph of the chapter.", result.Pages[1].Text);
		}

		[Fact]
		public void Estimate_FillsPagesWithExactLines()
		{
			const string line = "<w:spacing w:before=\"0\" w:after=\"0\" w:line=\"240\" w:lineRule=\"exact\"/>";

			var body = new StringBuilder();
			for (var i = 0; i < 120; i++)
				body.Append(P(R($"Line {i}"), line));

			body.Append(Section);

			var result = new DocxBuilder().Styles().Body(body.ToString()).Extract(new DocxExtractorOptions { Pagination = PaginationMode.Estimate });

			Assert.Equal(3, result.PageCount);
			Assert.StartsWith("Line 54", result.Pages[1].Text);
			Assert.StartsWith("Line 108", result.Pages[2].Text);
		}

		[Fact]
		public void Auto_WithoutMarkersOrRecordedCount_Estimates()
		{
			var body = new StringBuilder();
			for (var i = 0; i < 30; i++)
				body.Append(P(R(Lorem)));

			var result = new DocxBuilder().Styles().Body(body.ToString()).Extract();

			Assert.Equal(PaginationSource.Estimate, result.PaginationSource);
			Assert.True(result.PageCount >= 2);
		}

		[Fact]
		public void None_ProducesASinglePageWithoutSeparators()
		{
			var builder = new DocxBuilder().Styles().Body(P(R("One")) + P("<w:r><w:br w:type=\"page\"/></w:r>" + R("Two")));

			var result = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.None });

			Assert.Equal(1, result.PageCount);
			Assert.Equal("One\n\nTwo", result.Text);
		}

		[Fact]
		public void PageHeaderFormat_IsConfigurable()
		{
			var builder = new DocxBuilder().Styles().Body(P(R("One")) + P("<w:r><w:br w:type=\"page\"/></w:r>" + R("Two")));

			var result = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.ExplicitBreaks, PageHeaderFormat = "Page: {0} of {1}\n===" });

			Assert.Equal("Page: 1 of 2\n===\n\nOne\n\nPage: 2 of 2\n===\n\nTwo", result.Text);
		}
	}
}

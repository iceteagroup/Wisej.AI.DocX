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
using Xunit;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Tests the extraction of links, notes, comments, drawings, equations, fields and tracked changes.
	/// </summary>
	public class ContentTests : TestBase
	{
		/// <summary>A 1x1 PNG image.</summary>
		private static readonly byte[] Png =
		{
			0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01,
			0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
			0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
			0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82,
		};

		/// <summary>
		/// Returns an inline picture.
		/// </summary>
		/// <param name="id">The relationship identifier of the image.</param>
		/// <param name="description">The alternative text.</param>
		/// <returns>The drawing markup.</returns>
		private static string Picture(string id, string description)
		{
			return "<w:r><w:drawing><wp:inline><wp:extent cx=\"914400\" cy=\"914400\"/>" +
				$"<wp:docPr id=\"1\" name=\"Picture 1\" descr=\"{description}\"/>" +
				"<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/picture\">" +
				$"<pic:pic><pic:nvPicPr><pic:cNvPr id=\"0\" name=\"logo.png\"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed=\"{id}\"/></pic:blipFill><pic:spPr/></pic:pic>" +
				"</a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";
		}

		[Fact]
		public void Hyperlinks_BecomeMarkdownLinks()
		{
			var builder = new DocxBuilder().Styles().Hyperlink("rIdLink", "https://wisej.com/ai").Body(P(
				R("Visit ") + "<w:hyperlink r:id=\"rIdLink\">" + R("Wisej.AI") + "</w:hyperlink>" + R(" or the ") +
				"<w:hyperlink w:anchor=\"_Toc1\">" + R("summary") + "</w:hyperlink>" + R(".")));

			Assert.Equal("Visit [Wisej.AI](https://wisej.com/ai) or the summary.", Markdown(builder));
			Assert.Equal("Visit Wisej.AI (https://wisej.com/ai) or the summary.", PlainText(builder));
		}

		[Fact]
		public void HyperlinkFields_BecomeMarkdownLinks()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				"<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText xml:space=\"preserve\"> HYPERLINK \"https://example.com\" </w:instrText></w:r>" +
				"<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>" + R("Example") + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>"));

			Assert.Equal("[Example](https://example.com)", Markdown(builder));
		}

		[Fact]
		public void Footnotes_AreWrittenAtTheEndOfThePage()
		{
			var builder = new DocxBuilder().Styles()
				.Footnotes(
					"<w:footnote w:type=\"separator\" w:id=\"-1\"><w:p/></w:footnote>" +
					"<w:footnote w:id=\"1\">" + P("<w:r><w:footnoteRef/></w:r>" + R(" Source: annual report.")) + "</w:footnote>")
				.Body(P(R("Revenue grew") + "<w:r><w:rPr><w:vertAlign w:val=\"superscript\"/></w:rPr><w:footnoteReference w:id=\"1\"/></w:r>" + R(".")));

			Assert.Equal("Revenue grew[^1].\n\n[^1]: Source: annual report.", Markdown(builder));
			Assert.Equal("Revenue grew.", Markdown(builder, o => o.IncludeFootnotes = false));
		}

		[Fact]
		public void Comments_QuoteTheTextTheyReferTo()
		{
			var builder = new DocxBuilder().Styles()
				.Comments("<w:comment w:id=\"0\" w:author=\"Alice\" w:date=\"2026-01-15T10:00:00Z\">" + P(R("Please verify.")) + "</w:comment>")
				.Body(P(R("The ") + "<w:commentRangeStart w:id=\"0\"/>" + R("total") + "<w:commentRangeEnd w:id=\"0\"/>" +
					"<w:r><w:commentReference w:id=\"0\"/></w:r>" + R(" is 42.")));

			Assert.Equal("The total is 42.", Markdown(builder));
			Assert.Equal(
				"The total is 42.\n\n> **Comment** by Alice (2026-01-15) on “total”: Please verify.",
				Markdown(builder, o => o.IncludeComments = true));
		}

		[Fact]
		public void Images_AreReferencedWithTheirAlternativeText()
		{
			var builder = new DocxBuilder().Styles().Image("rIdImg", "image1.png", Png).Body(P(Picture("rIdImg", "Company logo")));

			var result = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.None });

			Assert.Equal("![Company logo](image1.png)", result.Text);
			Assert.Single(result.Images);
			Assert.Equal("image/png", result.Images[0].ContentType);
			Assert.Equal(Png, result.Images[0].Data);
			Assert.Equal(72, result.Images[0].Width, 1);
			Assert.Equal(1, result.Images[0].PageNumber);
		}

		[Fact]
		public void ImageDescriber_IsCalledOncePerPicture()
		{
			var builder = new DocxBuilder().Styles().Image("rIdImg", "image1.png", Png).Body(
				P(Picture("rIdImg", "Logo")) + P(R("Text")) + P(Picture("rIdImg", "Logo again")));

			var calls = new List<string>();
			var markdown = Markdown(builder, o => o.ImageDescriber = image =>
			{
				calls.Add(image.FileName);
				return "A blue square.";
			});

			Assert.Single(calls);
			Assert.Equal("![Logo](image1.png)\n\n> **image1.png:** A blue square.\n\nText\n\n![Logo again](image1.png)", markdown);
		}

		[Fact]
		public void Charts_BecomeDataTables()
		{
			var chart =
				"<c:chart><c:title><c:tx><c:rich><a:p><a:r><a:t>Sales</a:t></a:r></a:p></c:rich></c:tx></c:title><c:plotArea><c:barChart><c:barDir c:val=\"col\"/>" +
				"<c:ser><c:tx><c:strRef><c:strCache><c:pt idx=\"0\"><c:v>2025</c:v></c:pt></c:strCache></c:strRef></c:tx>" +
				"<c:cat><c:strRef><c:strCache><c:ptCount val=\"2\"/><c:pt idx=\"0\"><c:v>North</c:v></c:pt><c:pt idx=\"1\"><c:v>South</c:v></c:pt></c:strCache></c:strRef></c:cat>" +
				"<c:val><c:numRef><c:numCache><c:formatCode>General</c:formatCode><c:ptCount val=\"2\"/><c:pt idx=\"0\"><c:v>10.5</c:v></c:pt><c:pt idx=\"1\"><c:v>7</c:v></c:pt></c:numCache></c:numRef></c:val>" +
				"</c:ser></c:barChart></c:plotArea></c:chart>";

			var drawing = "<w:r><w:drawing><wp:inline><wp:extent cx=\"5000000\" cy=\"3000000\"/><wp:docPr id=\"2\" name=\"Chart 1\"/>" +
				"<a:graphic><a:graphicData uri=\"http://schemas.openxmlformats.org/drawingml/2006/chart\"><c:chart r:id=\"rIdChart\"/></a:graphicData></a:graphic></wp:inline></w:drawing></w:r>";

			var builder = new DocxBuilder().Styles().Chart("rIdChart", chart).Body(P(drawing));

			Assert.Equal("**Chart: Sales** (column chart)\n\n| Category | 2025 |\n| --- | --- |\n| North | 10.5 |\n| South | 7 |", Markdown(builder));
		}

		[Fact]
		public void Equations_BecomeLatex()
		{
			var fraction = "<m:oMath><m:f><m:num><m:r><m:t>a</m:t></m:r></m:num><m:den><m:r><m:t>b</m:t></m:r></m:den></m:f>" +
				"<m:r><m:t>+</m:t></m:r><m:sSup><m:e><m:r><m:t>x</m:t></m:r></m:e><m:sup><m:r><m:t>2</m:t></m:r></m:sup></m:sSup></m:oMath>";

			var builder = new DocxBuilder().Styles().Body(P(R("Compute ") + fraction + R(".")));

			Assert.Equal("Compute $\\frac{a}{b}+x^2$.", Markdown(builder));
		}

		[Fact]
		public void TextBoxes_AreWrittenAfterTheirParagraph()
		{
			var textBox = "<w:r><w:pict><v:shape style=\"position:absolute;width:100pt;height:50pt\"><v:textbox><w:txbxContent>" +
				P(R("Sidebar note")) + "</w:txbxContent></v:textbox></v:shape></w:pict></w:r>";

			var builder = new DocxBuilder().Styles().Body(P(R("Main text.") + textBox));

			Assert.Equal("Main text.\n\nSidebar note", Markdown(builder));
			Assert.Equal("Main text.", Markdown(builder, o => o.IncludeTextBoxes = false));
		}

		[Fact]
		public void TrackedChanges_FollowTheMode()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("The price is ") +
				"<w:del w:id=\"1\" w:author=\"Bob\"><w:r><w:delText>10</w:delText></w:r></w:del>" +
				"<w:ins w:id=\"2\" w:author=\"Bob\">" + R("12") + "</w:ins>" + R(" euros.")));

			Assert.Equal("The price is 12 euros.", Markdown(builder));
			Assert.Equal("The price is 10 euros.", Markdown(builder, o => o.TrackedChanges = TrackedChangesMode.Reject));
			Assert.Equal("The price is {--10--}{++12++} euros.", Markdown(builder, o => o.TrackedChanges = TrackedChangesMode.Markup));
		}

		[Fact]
		public void TableOfContents_CanBeSkipped()
		{
			var builder = new DocxBuilder().Styles().Body(
				Styled("TOC1", "Introduction") + Styled("Heading1", "Introduction") + P(R("Text.")));

			Assert.Equal("Introduction\n\n# Introduction\n\nText.", Markdown(builder));
			Assert.Equal("# Introduction\n\nText.", Markdown(builder, o => o.IncludeTableOfContents = false));
		}

		[Fact]
		public void CheckBoxes_ShowTheirState()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				"<w:r><w:fldChar w:fldCharType=\"begin\"><w:ffData><w:checkBox><w:default w:val=\"0\"/><w:checked/></w:checkBox></w:ffData></w:fldChar></w:r>" +
				"<w:r><w:instrText> FORMCHECKBOX </w:instrText></w:r><w:r><w:fldChar w:fldCharType=\"end\"/></w:r>" + R(" Approved")));

			Assert.Equal("☒ Approved", Markdown(builder));
		}

		[Fact]
		public void HeadersAndFooters_AreWrittenOnceWithComputedPageNumbers()
		{
			var page = "<w:r><w:fldChar w:fldCharType=\"begin\"/></w:r><w:r><w:instrText> PAGE </w:instrText></w:r>" +
				"<w:r><w:fldChar w:fldCharType=\"separate\"/></w:r>" + R("9") + "<w:r><w:fldChar w:fldCharType=\"end\"/></w:r>";

			var builder = new DocxBuilder().Styles()
				.Header("rIdHeader", P(R("ACME Corporation")))
				.Footer("rIdFooter", P(R("Confidential - page ") + page))
				.Body(P(R("First page.")) + P("<w:r><w:br w:type=\"page\"/></w:r>" + R("Second page.")) +
					"<w:sectPr><w:headerReference w:type=\"default\" r:id=\"rIdHeader\"/><w:footerReference w:type=\"default\" r:id=\"rIdFooter\"/></w:sectPr>");

			var once = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.ExplicitBreaks });
			Assert.Equal("**Header:** ACME Corporation\n\nFirst page.\n\n**Footer:** Confidential - page 1", once.Pages[0].Text);
			Assert.Equal("Second page.", once.Pages[1].Text);

			var every = builder.Extract(new DocxExtractorOptions { Pagination = PaginationMode.ExplicitBreaks, HeadersAndFooters = HeaderFooterMode.EveryPage });
			Assert.Equal("**Header:** ACME Corporation\n\nSecond page.\n\n**Footer:** Confidential - page 2", every.Pages[1].Text);
		}
	}
}

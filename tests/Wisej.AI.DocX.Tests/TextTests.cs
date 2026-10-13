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

using Xunit;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Tests the extraction of paragraphs, headings and character formatting.
	/// </summary>
	public class TextTests : TestBase
	{
		[Fact]
		public void Headings_UseTheOutlineLevelOfTheirStyle()
		{
			var builder = new DocxBuilder().Styles().Body(
				Styled("Title", "Annual Report") +
				Styled("Heading1", "Introduction") +
				Styled("Heading2", "Scope") +
				P(R("Body text.")));

			Assert.Equal("# Annual Report\n\n# Introduction\n\n## Scope\n\nBody text.", Markdown(builder));
		}

		[Fact]
		public void Headings_FromDirectOutlineLevel()
		{
			var builder = new DocxBuilder().Styles().Body(P(R("Custom heading"), "<w:outlineLvl w:val=\"2\"/>"));

			Assert.Equal("### Custom heading", Markdown(builder));
		}

		[Fact]
		public void Emphasis_IsMergedAcrossRunsAndKeepsSpacesOutsideMarkers()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("This is ") + R("bo", "<w:b/>") + R("ld ", "<w:b/>") + R("and ") + R("italic", "<w:i/>") + R(" text.")));

			Assert.Equal("This is **bold** and *italic* text.", Markdown(builder));
		}

		[Fact]
		public void Emphasis_NestsBoldAndItalic()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("A ", "<w:b/>") + R("B", "<w:b/><w:i/>") + R(" C", "<w:b/>")));

			Assert.Equal("**A *B* C**", Markdown(builder));
		}

		[Fact]
		public void Emphasis_StrikeSuperscriptAndCode()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("old", "<w:strike/>") + R(" E=mc") + R("2", "<w:vertAlign w:val=\"superscript\"/>") + R(" call ") +
				R("Main()", "<w:rFonts w:ascii=\"Consolas\" w:hAnsi=\"Consolas\"/>")));

			Assert.Equal("~~old~~ E=mc<sup>2</sup> call `Main()`", Markdown(builder));
		}

		[Fact]
		public void Emphasis_IsNotRepeatedInHeadings()
		{
			var builder = new DocxBuilder().Styles().Body(Styled("Heading1", "Bold by style"));

			Assert.Equal("# Bold by style", Markdown(builder));
		}

		[Fact]
		public void Emphasis_CoveringAWholeParagraph_IsStylingAndIsRemoved()
		{
			var longText = "This paragraph is set entirely in bold italic by its author, which is a matter of style and not of meaning.";
			var builder = new DocxBuilder().Styles().Body(
				P(R("Short bold heading", "<w:b/>")) +
				P(R("Fall Catalog", "<w:b/><w:i/>")) +
				P(R(longText, "<w:b/><w:i/>")) +
				P(R("Mixed ") + R("emphasis", "<w:i/>")));

			Assert.Equal($"**Short bold heading**\n\n**Fall Catalog**\n\n{longText}\n\nMixed *emphasis*", Markdown(builder));
		}

		[Fact]
		public void Formatting_CanBeDisabled()
		{
			var builder = new DocxBuilder().Styles().Body(P(R("bold", "<w:b/>")));

			Assert.Equal("bold", Markdown(builder, o => o.IncludeFormatting = false));
		}

		[Fact]
		public void Escaping_ProtectsMarkdownSyntax()
		{
			var builder = new DocxBuilder().Styles().Body(
				P(R("# not a heading")) +
				P(R("1. not a list")) +
				P(R("Use *stars* and snake_case names")));

			Assert.Equal("\\# not a heading\n\n1\\. not a list\n\nUse \\*stars\\* and snake_case names", Markdown(builder));
		}

		[Fact]
		public void CodeParagraphs_BecomeFencedBlocks()
		{
			var builder = new DocxBuilder().Styles().Body(
				P(R("Example:")) +
				Styled("Code", "int x = 1;") +
				Styled("Code", "x++;") +
				P(R("Done.")));

			Assert.Equal("Example:\n\n```\nint x = 1;\nx++;\n```\n\nDone.", Markdown(builder));
		}

		[Fact]
		public void Quotes_ArePrefixed()
		{
			var builder = new DocxBuilder().Styles().Body(Styled("Quote", "To be or not to be."));

			Assert.Equal("> To be or not to be.", Markdown(builder));
		}

		[Fact]
		public void LineBreaksAndTabs_AreKept()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("Line one") + "<w:r><w:br/></w:r>" + R("Name:") + "<w:r><w:tab/></w:r>" + R("Value")));

			Assert.Equal("Line one\nName:\tValue", Markdown(builder));
		}

		[Fact]
		public void HiddenText_IsExcludedByDefault()
		{
			var builder = new DocxBuilder().Styles().Body(P(R("Visible ") + R("secret", "<w:vanish/>")));

			Assert.Equal("Visible", Markdown(builder));
			Assert.Equal("Visible secret", Markdown(builder, o => o.IncludeHiddenText = true));
		}

		[Fact]
		public void Symbols_AreMappedToUnicode()
		{
			var builder = new DocxBuilder().Styles().Body(P(
				R("Done ") + "<w:r><w:sym w:font=\"Wingdings\" w:char=\"F0FC\"/></w:r>" +
				R(" angle ") + "<w:r><w:sym w:font=\"Symbol\" w:char=\"F061\"/></w:r>"));

			Assert.Equal("Done ✓ angle α", Markdown(builder));
		}

		[Fact]
		public void PlaceholderContentControls_AreSkipped()
		{
			var builder = new DocxBuilder().Styles().Body(
				"<w:sdt><w:sdtPr><w:showingPlcHdr/></w:sdtPr><w:sdtContent>" + P(R("Click or tap here to enter text.")) + "</w:sdtContent></w:sdt>" +
				"<w:sdt><w:sdtPr/><w:sdtContent>" + P(R("Filled value")) + "</w:sdtContent></w:sdt>");

			Assert.Equal("Filled value", Markdown(builder));
		}

		[Fact]
		public void PlainText_HasNoMarkup()
		{
			var builder = new DocxBuilder().Styles().Body(
				Styled("Heading1", "Title") +
				P(R("Some ") + R("bold", "<w:b/>") + R(" text.")));

			Assert.Equal("Title\n\nSome bold text.", PlainText(builder));
		}
	}
}

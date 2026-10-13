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
	/// Tests the generation of list labels from the numbering definitions.
	/// </summary>
	public class ListTests : TestBase
	{
		/// <summary>A multi-level list: "1." / "a)" / "i." and a bullet list.</summary>
		private const string Numbering =
			"<w:abstractNum w:abstractNumId=\"0\">" +
			"<w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/><w:pPr><w:ind w:left=\"720\" w:hanging=\"360\"/></w:pPr></w:lvl>" +
			"<w:lvl w:ilvl=\"1\"><w:start w:val=\"1\"/><w:numFmt w:val=\"lowerLetter\"/><w:lvlText w:val=\"%2)\"/><w:pPr><w:ind w:left=\"1440\" w:hanging=\"360\"/></w:pPr></w:lvl>" +
			"<w:lvl w:ilvl=\"2\"><w:start w:val=\"1\"/><w:numFmt w:val=\"lowerRoman\"/><w:lvlText w:val=\"%3.\"/></w:lvl>" +
			"</w:abstractNum>" +
			"<w:abstractNum w:abstractNumId=\"1\">" +
			"<w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"bullet\"/><w:lvlText w:val=\"\"/><w:rPr><w:rFonts w:ascii=\"Symbol\" w:hAnsi=\"Symbol\"/></w:rPr></w:lvl>" +
			"</w:abstractNum>" +
			"<w:abstractNum w:abstractNumId=\"2\">" +
			"<w:lvl w:ilvl=\"0\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.\"/></w:lvl>" +
			"<w:lvl w:ilvl=\"1\"><w:start w:val=\"1\"/><w:numFmt w:val=\"decimal\"/><w:lvlText w:val=\"%1.%2.\"/></w:lvl>" +
			"</w:abstractNum>" +
			"<w:num w:numId=\"1\"><w:abstractNumId w:val=\"0\"/></w:num>" +
			"<w:num w:numId=\"2\"><w:abstractNumId w:val=\"1\"/></w:num>" +
			"<w:num w:numId=\"3\"><w:abstractNumId w:val=\"0\"/><w:lvlOverride w:ilvl=\"0\"><w:startOverride w:val=\"1\"/></w:lvlOverride></w:num>" +
			"<w:num w:numId=\"4\"><w:abstractNumId w:val=\"0\"/></w:num>" +
			"<w:num w:numId=\"5\"><w:abstractNumId w:val=\"2\"/></w:num>";

		/// <summary>
		/// Returns a list item paragraph.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="numId">The numbering instance.</param>
		/// <param name="level">The level.</param>
		/// <returns>The paragraph markup.</returns>
		private static string Item(string text, int numId, int level = 0)
		{
			return P(R(text), $"<w:numPr><w:ilvl w:val=\"{level}\"/><w:numId w:val=\"{numId}\"/></w:numPr>");
		}

		[Fact]
		public void NestedLists_KeepTheirLabelsAndIndentation()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(
				Item("First", 1) +
				Item("Detail", 1, 1) +
				Item("Deeper", 1, 2) +
				Item("Second detail", 1, 1) +
				Item("Second", 1));

			Assert.Equal("1. First\n   a) Detail\n      i. Deeper\n   b) Second detail\n2. Second", Markdown(builder));
		}

		[Fact]
		public void Bullets_UseAHyphen()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(Item("Apples", 2) + Item("Pears", 2));

			Assert.Equal("- Apples\n- Pears", Markdown(builder));
			Assert.Equal("• Apples\n• Pears", PlainText(builder));
		}

		[Fact]
		public void InstancesOfTheSameList_ContinueTheNumbering()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(
				Item("One", 1) + Item("Two", 1) + P(R("Interruption")) + Item("Three", 4));

			Assert.Equal("1. One\n2. Two\n\nInterruption\n\n3. Three", Markdown(builder));
		}

		[Fact]
		public void StartOverride_RestartsTheNumbering()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(
				Item("One", 1) + Item("Two", 1) + P(R("New list")) + Item("Again one", 3));

			Assert.Equal("1. One\n2. Two\n\nNew list\n\n1. Again one", Markdown(builder));
		}

		[Fact]
		public void MultiLevelLabels_IncludeTheParentNumbers()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(
				Item("Chapter", 5) + Item("Section", 5, 1) + Item("Section", 5, 1) + Item("Chapter", 5) + Item("Section", 5, 1));

			Assert.Equal("1. Chapter\n   1.1. Section\n   1.2. Section\n2. Chapter\n   2.1. Section", Markdown(builder));
		}

		[Fact]
		public void NumberedHeadings_KeepTheirNumber()
		{
			var builder = new DocxBuilder().Styles().Numbering(Numbering).Body(
				P(R("Overview"), "<w:pStyle w:val=\"Heading1\"/><w:numPr><w:ilvl w:val=\"0\"/><w:numId w:val=\"5\"/></w:numPr>") +
				P(R("Details"), "<w:pStyle w:val=\"Heading2\"/><w:numPr><w:ilvl w:val=\"1\"/><w:numId w:val=\"5\"/></w:numPr>"));

			Assert.Equal("# 1. Overview\n\n## 1.1. Details", Markdown(builder));
		}
	}
}

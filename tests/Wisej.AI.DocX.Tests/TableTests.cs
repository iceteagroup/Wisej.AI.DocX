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
using Xunit;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Tests the conversion of tables.
	/// </summary>
	public class TableTests : TestBase
	{
		/// <summary>
		/// Returns a table.
		/// </summary>
		/// <param name="columns">The number of grid columns.</param>
		/// <param name="rows">The rows markup.</param>
		/// <returns>The table markup.</returns>
		private static string Table(int columns, params string[] rows)
		{
			var grid = String.Concat(Enumerable.Repeat("<w:gridCol w:w=\"2000\"/>", columns));
			return $"<w:tbl><w:tblPr><w:tblStyle w:val=\"TableGrid\"/></w:tblPr><w:tblGrid>{grid}</w:tblGrid>{String.Concat(rows)}</w:tbl>";
		}

		/// <summary>
		/// Returns a row.
		/// </summary>
		/// <param name="cells">The cells markup.</param>
		/// <returns>The row markup.</returns>
		private static string Row(params string[] cells)
		{
			return $"<w:tr>{String.Concat(cells)}</w:tr>";
		}

		/// <summary>
		/// Returns a header row, repeated at the top of every page.
		/// </summary>
		/// <param name="cells">The cells markup.</param>
		/// <returns>The row markup.</returns>
		private static string HeaderRow(params string[] cells)
		{
			return $"<w:tr><w:trPr><w:tblHeader/></w:trPr>{String.Concat(cells)}</w:tr>";
		}

		/// <summary>
		/// Returns a cell containing a paragraph of text.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="tcPr">The inner XML of <c>w:tcPr</c>.</param>
		/// <returns>The cell markup.</returns>
		private static string Cell(string text, string tcPr = "")
		{
			return $"<w:tc><w:tcPr>{tcPr}</w:tcPr>{P(text.Length == 0 ? "" : R(text))}</w:tc>";
		}

		[Fact]
		public void SimpleTable_BecomesAPipeTable()
		{
			var builder = new DocxBuilder().Styles().Body(Table(2,
				Row(Cell("Name"), Cell("Price")),
				Row(Cell("Apple"), Cell("1.20")),
				Row(Cell("Pear | Bartlett"), Cell("0.90"))));

			Assert.Equal("| Name | Price |\n| --- | --- |\n| Apple | 1.20 |\n| Pear \\| Bartlett | 0.90 |", Markdown(builder));
		}

		[Fact]
		public void MergedCells_AreRepeatedSoEveryRowIsSelfContained()
		{
			var builder = new DocxBuilder().Styles().Body(Table(3,
				Row(Cell("Region"), Cell("Sales", "<w:gridSpan w:val=\"2\"/>")),
				Row(Cell("North", "<w:vMerge w:val=\"restart\"/>"), Cell("Q1"), Cell("10")),
				Row(Cell("", "<w:vMerge/>"), Cell("Q2"), Cell("12"))));

			Assert.Equal(
				"| Region | Sales | Sales |\n| --- | --- | --- |\n| North | Q1 | 10 |\n| North | Q2 | 12 |",
				Markdown(builder));

			Assert.Equal(
				"| Region | Sales |  |\n| --- | --- | --- |\n| North | Q1 | 10 |\n|  | Q2 | 12 |",
				Markdown(builder, o => o.MergedCells = MergedCellMode.Empty));
		}

		[Fact]
		public void MultipleHeaderRows_AreCombined()
		{
			var builder = new DocxBuilder().Styles().Body(Table(3,
				HeaderRow(Cell("Item"), Cell("2024", "<w:gridSpan w:val=\"2\"/>")),
				HeaderRow(Cell(""), Cell("Q1"), Cell("Q2")),
				Row(Cell("Revenue"), Cell("5"), Cell("7"))));

			Assert.Equal("| Item | 2024 / Q1 | 2024 / Q2 |\n| --- | --- | --- |\n| Revenue | 5 | 7 |", Markdown(builder));
		}

		[Fact]
		public void EmptyColumnsAndRows_AreRemoved()
		{
			var builder = new DocxBuilder().Styles().Body(Table(3,
				Row(Cell("A"), Cell(""), Cell("B")),
				Row(Cell(""), Cell(""), Cell("")),
				Row(Cell("1"), Cell(""), Cell("2"))));

			Assert.Equal("| A | B |\n| --- | --- |\n| 1 | 2 |", Markdown(builder));
		}

		[Fact]
		public void SingleCellTables_AreUnwrapped()
		{
			var builder = new DocxBuilder().Styles().Body(Table(1, Row(Cell("Boxed note"))));

			Assert.Equal("Boxed note", Markdown(builder));
			Assert.Equal("| Boxed note |\n| --- |", Markdown(builder, o => o.UnwrapLayoutTables = false));
		}

		[Fact]
		public void SingleRowTables_AreUnwrapped()
		{
			var builder = new DocxBuilder().Styles().Body(Table(2, Row(Cell("Beverages"), Cell("Soft drinks and teas"))));

			Assert.Equal("Beverages\n\nSoft drinks and teas", Markdown(builder));
		}

		[Fact]
		public void UniformCellEmphasis_IsRemoved()
		{
			var builder = new DocxBuilder().Styles().Body(Table(2,
				Row($"<w:tc>{P(R("Product", "<w:b/>"))}</w:tc>", $"<w:tc>{P(R("Price", "<w:b/>"))}</w:tc>"),
				Row($"<w:tc>{P(R("Chai", "<w:b/>"))}</w:tc>", $"<w:tc>{P(R("Only ") + R("18", "<w:b/>"))}</w:tc>")));

			Assert.Equal("| Product | Price |\n| --- | --- |\n| Chai | Only **18** |", Markdown(builder));
		}

		[Fact]
		public void NestedTables_UseHtml()
		{
			var nested = Table(2, Row(Cell("x"), Cell("y")));
			var builder = new DocxBuilder().Styles().Body(Table(2,
				Row(Cell("Outer"), Cell("Data")),
				Row(Cell("Left"), $"<w:tc><w:tcPr/>{nested}{P("")}</w:tc>")));

			var markdown = Markdown(builder);

			Assert.StartsWith("<table>", markdown);
			Assert.Contains("<td>Left</td>", markdown);
			Assert.Contains("<td><table>\n<tr><td>x</td><td>y</td></tr>\n</table></td>", markdown);
		}

		[Fact]
		public void CellContent_KeepsListsAndLineBreaks()
		{
			var builder = new DocxBuilder().Styles().Body(Table(2,
				Row(Cell("Key"), Cell("Notes")),
				Row(Cell("A"), $"<w:tc><w:tcPr/>{P(R("first"))}{P(R("second"))}</w:tc>")));

			Assert.Equal("| Key | Notes |\n| --- | --- |\n| A | first<br>second |", Markdown(builder));
		}

		[Fact]
		public void PlainText_WritesPipeSeparatedRows()
		{
			var builder = new DocxBuilder().Styles().Body(Table(2,
				Row(Cell("Name"), Cell("Price")),
				Row(Cell("Apple"), Cell("1.20"))));

			Assert.Equal("Name | Price\nApple | 1.20", PlainText(builder));
		}
	}
}

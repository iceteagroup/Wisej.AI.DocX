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
using System.Linq;
using System.Text;

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// Base class of the block level content of a story: paragraphs, tables and raw text.
	/// </summary>
	internal abstract class Block
	{
	}

	/// <summary>
	/// A paragraph.
	/// </summary>
	internal sealed class ParagraphBlock : Block
	{
		/// <summary>Gets or sets the resolved paragraph formatting.</summary>
		public ParagraphProperties Properties { get; set; }

		/// <summary>Gets or sets the list label when the paragraph is a list item, otherwise <c>null</c>.</summary>
		public ListLabel List { get; set; }

		/// <summary>Gets the content of the paragraph.</summary>
		public List<Inline> Inlines { get; } = new List<Inline>();

		/// <summary>
		/// Returns the plain text of the paragraph, without formatting.
		/// </summary>
		/// <returns>The concatenated text of the text runs.</returns>
		public string GetPlainText()
		{
			var sb = new StringBuilder();
			foreach (var inline in Inlines)
			{
				switch (inline)
				{
					case TextInline text:
						sb.Append(text.Text);
						break;

					case TabInline _:
						sb.Append('\t');
						break;

					case BreakInline _:
						sb.Append('\n');
						break;
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Returns the plain text of the paragraph, useful while debugging.
		/// </summary>
		/// <returns>The plain text of the paragraph.</returns>
		public override string ToString() => GetPlainText();
	}

	/// <summary>
	/// A table.
	/// </summary>
	internal sealed class TableBlock : Block
	{
		/// <summary>Gets the rows of the table.</summary>
		public List<TableRow> Rows { get; } = new List<TableRow>();

		/// <summary>Gets the widths of the grid columns, in points.</summary>
		public List<double> Grid { get; } = new List<double>();

		/// <summary>Gets or sets the default left margin of the cells, in points.</summary>
		public double CellMarginLeft { get; set; } = 5.4;

		/// <summary>Gets or sets the default right margin of the cells, in points.</summary>
		public double CellMarginRight { get; set; } = 5.4;

		/// <summary>Gets or sets the default top margin of the cells, in points.</summary>
		public double CellMarginTop { get; set; }

		/// <summary>Gets or sets the default bottom margin of the cells, in points.</summary>
		public double CellMarginBottom { get; set; }

		/// <summary>Gets or sets the preferred table width in points, or 0 when automatic.</summary>
		public double PreferredWidth { get; set; }

		/// <summary>Gets or sets the preferred table width as a fraction of the available width, or 0.</summary>
		public double PreferredWidthPercent { get; set; }

		/// <summary>Gets or sets whether the table has borders between rows, which add to the row heights.</summary>
		public bool HasBorders { get; set; }

		/// <summary>Gets the number of grid columns, taking the cells into account when the grid is missing.</summary>
		public int ColumnCount
		{
			get
			{
				var cells = Rows.Count == 0 ? 0 : Rows.Max(r => r.GridBefore + r.Cells.Sum(c => c.GridSpan) + r.GridAfter);
				return Math.Max(Grid.Count, cells);
			}
		}
	}

	/// <summary>
	/// A row of a table.
	/// </summary>
	internal sealed class TableRow
	{
		/// <summary>Gets the cells of the row.</summary>
		public List<TableCell> Cells { get; } = new List<TableCell>();

		/// <summary>Gets or sets whether the row is a header row repeated at the top of every page.</summary>
		public bool IsHeader { get; set; }

		/// <summary>Gets or sets whether the row cannot be split across pages.</summary>
		public bool CantSplit { get; set; }

		/// <summary>Gets or sets the specified row height in points, or 0 when automatic.</summary>
		public double Height { get; set; }

		/// <summary>Gets or sets whether <see cref="Height"/> is exact rather than a minimum.</summary>
		public bool HeightIsExact { get; set; }

		/// <summary>Gets or sets the number of grid columns skipped before the first cell.</summary>
		public int GridBefore { get; set; }

		/// <summary>Gets or sets the number of grid columns skipped after the last cell.</summary>
		public int GridAfter { get; set; }

		/// <summary>Gets or sets the number of the page that starts with this row, or 0.</summary>
		public int PageStart { get; set; }

		/// <summary>Gets or sets whether a page break was rendered by Word at the start of this row.</summary>
		public bool HasRenderedPageBreak { get; set; }
	}

	/// <summary>
	/// A cell of a table row.
	/// </summary>
	internal sealed class TableCell
	{
		/// <summary>Gets the content of the cell.</summary>
		public List<Block> Blocks { get; } = new List<Block>();

		/// <summary>Gets or sets the number of grid columns spanned by the cell.</summary>
		public int GridSpan { get; set; } = 1;

		/// <summary>Gets or sets the vertical merge state of the cell.</summary>
		public VerticalMerge VerticalMerge { get; set; }

		/// <summary>Gets or sets the preferred width of the cell in points, or 0 when unknown.</summary>
		public double Width { get; set; }

		/// <summary>
		/// Returns the plain text of the cell.
		/// </summary>
		/// <returns>The text of the paragraphs in the cell, separated by new lines.</returns>
		public string GetPlainText()
		{
			return String.Join("\n", Blocks.OfType<ParagraphBlock>().Select(p => p.GetPlainText()));
		}
	}

	/// <summary>
	/// Block of text that is already formatted, such as the content of an embedded chunk.
	/// </summary>
	internal sealed class RawTextBlock : Block
	{
		/// <summary>Gets or sets the text.</summary>
		public string Text { get; set; }
	}

	/// <summary>
	/// The label of a list item.
	/// </summary>
	internal sealed class ListLabel
	{
		/// <summary>Gets or sets the formatted label, such as <c>1.</c>, <c>a)</c>, <c>2.1</c> or a bullet symbol.</summary>
		public string Text { get; set; }

		/// <summary>Gets or sets the zero based list level.</summary>
		public int Level { get; set; }

		/// <summary>Gets or sets whether the item is bulleted rather than numbered.</summary>
		public bool IsBullet { get; set; }

		/// <summary>Gets or sets the width taken by the label and its separator, in points.</summary>
		public double Width { get; set; }
	}
}

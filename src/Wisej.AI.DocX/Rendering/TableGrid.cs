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
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// A rectangular view of a table: every row has a slot for every grid column, and every slot
	/// knows which cell covers it, with horizontal and vertical merges resolved.
	/// </summary>
	/// <remarks>
	/// Columns and rows that are empty everywhere (spacer columns, blank rows used for layout) are
	/// removed, since they carry no information and waste tokens.
	/// </remarks>
	internal sealed class TableGrid
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="TableGrid"/> class.
		/// </summary>
		/// <param name="table">The table.</param>
		public TableGrid(TableBlock table)
		{
			Table = table;

			var columnCount = Math.Max(1, table.ColumnCount);
			var slots = new List<Slot[]>();

			for (var r = 0; r < table.Rows.Count; r++)
			{
				var row = table.Rows[r];
				var slotRow = new Slot[columnCount];
				var column = row.GridBefore;

				foreach (var cell in row.Cells)
				{
					if (column >= columnCount)
						break;

					var origin = cell;
					var isContinuation = false;

					// A continued vertical merge shows the cell that started the merge above.
					if (cell.VerticalMerge == VerticalMerge.Continue && r > 0 && slots[r - 1][column] != null)
					{
						origin = slots[r - 1][column].Cell;
						isContinuation = true;
					}

					for (var c = column; c < Math.Min(columnCount, column + cell.GridSpan); c++)
					{
						slotRow[c] = new Slot
						{
							Cell = origin,
							IsHorizontalContinuation = c > column,
							IsVerticalContinuation = isContinuation,
						};
					}

					column += cell.GridSpan;
				}

				slots.Add(slotRow);
			}

			RemoveEmpty(slots, columnCount);
		}

		#region Properties

		/// <summary>Gets the table.</summary>
		public TableBlock Table { get; }

		/// <summary>Gets the rows that are kept, with their slots.</summary>
		public List<GridRow> Rows { get; } = new List<GridRow>();

		/// <summary>Gets the number of columns that are kept.</summary>
		public int ColumnCount { get; private set; }

		/// <summary>
		/// Gets the number of leading header rows (rows repeated at the top of every page),
		/// or 0 when the table does not declare any.
		/// </summary>
		public int HeaderRowCount => Rows.TakeWhile(r => r.Row.IsHeader).Count();

		/// <summary>Gets whether the table contains a nested table.</summary>
		public bool HasNestedTables => Table.Rows.SelectMany(r => r.Cells).Any(c => c.Blocks.OfType<TableBlock>().Any());

		#endregion

		#region Implementation

		/// <summary>
		/// Keeps the rows and columns that contain text.
		/// </summary>
		/// <param name="slots">The slots of every row.</param>
		/// <param name="columnCount">The number of grid columns.</param>
		private void RemoveEmpty(List<Slot[]> slots, int columnCount)
		{
			var texts = new Dictionary<TableCell, bool>();
			bool HasText(Slot slot)
			{
				if (slot == null)
					return false;

				if (!texts.TryGetValue(slot.Cell, out var hasText))
					texts[slot.Cell] = hasText = HasContent(slot.Cell.Blocks);

				return hasText;
			}

			var keepColumns = Enumerable.Range(0, columnCount)
				.Where(c => slots.Any(row => HasText(row[c]) && !row[c].IsHorizontalContinuation))
				.ToList();

			// A table without any text keeps its structure.
			if (keepColumns.Count == 0)
				keepColumns = Enumerable.Range(0, columnCount).ToList();

			ColumnCount = keepColumns.Count;

			for (var r = 0; r < slots.Count; r++)
			{
				var row = slots[r];
				var kept = keepColumns.Select(c => row[c]).ToArray();

				// Rows without text of their own (blank, or only covered by cells merged from above) are
				// dropped, except when a page starts there.
				var hasOwnText = kept.Any(s => HasText(s) && !s.IsVerticalContinuation);
				if (!hasOwnText && Table.Rows[r].PageStart == 0)
					continue;

				Rows.Add(new GridRow { Row = Table.Rows[r], Slots = kept });
			}
		}

		/// <summary>
		/// Determines whether blocks contain anything that produces output: text, drawings or equations.
		/// </summary>
		/// <param name="blocks">The blocks of a cell.</param>
		/// <returns><c>true</c> when the blocks have content.</returns>
		private static bool HasContent(IEnumerable<Block> blocks)
		{
			foreach (var block in blocks)
			{
				switch (block)
				{
					case ParagraphBlock paragraph:
						foreach (var inline in paragraph.Inlines)
						{
							if (inline.IsPlaceholder)
								continue;

							if (inline is TextInline text && inline.IsVisible && !String.IsNullOrWhiteSpace(text.Text))
								return true;

							if ((inline is DrawingInline drawing && !drawing.IsExcluded) || inline is MathInline || inline is FieldInline)
								return true;
						}
						break;

					case TableBlock table:
						if (table.Rows.SelectMany(r => r.Cells).Any(c => HasContent(c.Blocks)))
							return true;
						break;

					case RawTextBlock raw:
						if (!String.IsNullOrWhiteSpace(raw.Text))
							return true;
						break;
				}
			}

			return false;
		}

		#endregion

		#region Types

		/// <summary>
		/// A row of the grid.
		/// </summary>
		internal sealed class GridRow
		{
			/// <summary>Gets or sets the table row.</summary>
			public TableRow Row { get; set; }

			/// <summary>Gets or sets the slot of every kept column; <c>null</c> where no cell covers the column.</summary>
			public Slot[] Slots { get; set; }
		}

		/// <summary>
		/// A slot of the grid: the cell that covers a column of a row.
		/// </summary>
		internal sealed class Slot
		{
			/// <summary>Gets or sets the cell that covers the slot.</summary>
			public TableCell Cell { get; set; }

			/// <summary>Gets or sets whether the slot is covered by a cell that starts in a previous column.</summary>
			public bool IsHorizontalContinuation { get; set; }

			/// <summary>Gets or sets whether the slot is covered by a cell that starts in a previous row.</summary>
			public bool IsVerticalContinuation { get; set; }

			/// <summary>Gets whether the slot is the top-left slot of its cell.</summary>
			public bool IsOrigin => !IsHorizontalContinuation && !IsVerticalContinuation;
		}

		#endregion
	}
}

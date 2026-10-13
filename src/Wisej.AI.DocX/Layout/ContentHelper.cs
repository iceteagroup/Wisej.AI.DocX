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
using System.Linq;
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Content queries shared by the paginator and the layout engine.
	/// </summary>
	internal static class ContentHelper
	{
		/// <summary>
		/// Determines whether an inline is visible content that occupies a page.
		/// </summary>
		/// <param name="inline">The inline.</param>
		/// <returns><c>true</c> for visible content.</returns>
		public static bool IsContent(Inline inline)
		{
			if (!inline.IsVisible || inline is BreakInline)
				return false;

			if (inline is TextInline text)
				return text.Text.Length > 0;

			if (inline is DrawingInline drawing)
				return !drawing.IsFloating;

			return true;
		}

		/// <summary>
		/// Looks for a page break marker saved by Word (<c>w:lastRenderedPageBreak</c>) in the content of a table cell.
		/// </summary>
		/// <param name="blocks">The blocks of the cell.</param>
		/// <param name="atStart">Returns whether the marker precedes all the content of the cell.</param>
		/// <returns><c>true</c> when the cell contains a marker.</returns>
		public static bool FindRenderedBreak(List<Block> blocks, out bool atStart)
		{
			atStart = true;

			foreach (var block in blocks)
			{
				if (block is ParagraphBlock paragraph)
				{
					foreach (var inline in paragraph.Inlines)
					{
						if (inline is RenderedPageBreakInline)
							return true;

						if (IsContent(inline))
							atStart = false;
					}

					atStart = false;
				}
				else if (block is TableBlock nested)
				{
					foreach (var cell in nested.Rows.SelectMany(r => r.Cells))
					{
						if (FindRenderedBreak(cell.Blocks, out var nestedStart))
						{
							atStart &= nestedStart;
							return true;
						}
					}

					atStart = false;
				}
			}

			return false;
		}

		/// <summary>
		/// Finds the page break markers saved by Word in a table row.
		/// </summary>
		/// <param name="row">The row.</param>
		/// <param name="before">Returns whether a page starts with the row.</param>
		/// <param name="after">Returns whether Word split the row across pages, so that the next row starts a page.</param>
		public static void FindRenderedBreaks(TableRow row, out bool before, out bool after)
		{
			before = false;
			after = false;

			foreach (var cell in row.Cells)
			{
				if (FindRenderedBreak(cell.Blocks, out var atStart))
				{
					if (atStart)
						before = true;
					else
						after = true;
				}
			}
		}
	}
}

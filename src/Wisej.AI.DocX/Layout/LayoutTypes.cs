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
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// A position inside the content of a paragraph, where a page can start.
	/// </summary>
	internal struct ContentPosition
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ContentPosition"/> structure.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="inlineIndex">The index of the inline in the paragraph.</param>
		/// <param name="charOffset">The character offset inside a text inline.</param>
		public ContentPosition(ParagraphBlock paragraph, int inlineIndex, int charOffset)
		{
			Paragraph = paragraph;
			InlineIndex = inlineIndex;
			CharOffset = charOffset;
		}

		/// <summary>Gets the paragraph.</summary>
		public ParagraphBlock Paragraph { get; }

		/// <summary>Gets the index of the inline in the paragraph.</summary>
		public int InlineIndex { get; }

		/// <summary>Gets the character offset inside a text inline; 0 for other inlines.</summary>
		public int CharOffset { get; }
	}

	/// <summary>
	/// A line of a measured paragraph.
	/// </summary>
	internal sealed class LineBox
	{
		/// <summary>Gets or sets the height of the line, in points.</summary>
		public double Height { get; set; }

		/// <summary>Gets or sets the position of the first content of the line.</summary>
		public ContentPosition Start { get; set; }

		/// <summary>Gets or sets whether the line ends with an explicit page break.</summary>
		public bool EndsWithPageBreak { get; set; }

		/// <summary>Gets or sets whether the line ends with an explicit column break.</summary>
		public bool EndsWithColumnBreak { get; set; }

		/// <summary>Gets or sets the height of the footnotes referenced by the line, in points.</summary>
		public double FootnoteHeight { get; set; }

		/// <summary>
		/// Gets or sets the position of the page break marker saved by Word at the start of the line, or <c>null</c>.
		/// Used as an anchor when the layout is estimated around Word's own page breaks.
		/// </summary>
		public ContentPosition? RenderedBreak { get; set; }
	}

	/// <summary>
	/// A paragraph broken into lines, with the properties that control its placement on the pages.
	/// </summary>
	internal sealed class ParagraphLayout
	{
		/// <summary>Gets or sets the paragraph.</summary>
		public ParagraphBlock Paragraph { get; set; }

		/// <summary>Gets the lines of the paragraph.</summary>
		public List<LineBox> Lines { get; } = new List<LineBox>();

		/// <summary>Gets or sets the space above the paragraph, after contextual spacing.</summary>
		public double SpaceBefore { get; set; }

		/// <summary>Gets or sets the space below the paragraph, after contextual spacing.</summary>
		public double SpaceAfter { get; set; }

		/// <summary>Gets or sets extra height not attached to a line: borders and floating drawings that push the text.</summary>
		public double ExtraHeight { get; set; }

		/// <summary>Gets the total height of the paragraph, in points.</summary>
		public double TotalHeight
		{
			get
			{
				var height = SpaceBefore + SpaceAfter + ExtraHeight;
				foreach (var line in Lines)
					height += line.Height;

				return height;
			}
		}
	}

	/// <summary>
	/// A measured row of a table.
	/// </summary>
	internal sealed class RowLayout
	{
		/// <summary>Gets or sets the row.</summary>
		public TableRow Row { get; set; }

		/// <summary>Gets or sets the height of the row, in points.</summary>
		public double Height { get; set; }

		/// <summary>Gets or sets the height of the first line of the tallest cell, the minimum that can be left at the bottom of a page.</summary>
		public double MinimumSplitHeight { get; set; }

		/// <summary>Gets or sets whether Word started a page with this row.</summary>
		public bool RenderedBreakBefore { get; set; }

		/// <summary>Gets or sets whether Word split this row across pages, so that the next row starts a page.</summary>
		public bool RenderedBreakAfter { get; set; }
	}

	/// <summary>
	/// An item of the measured flow of a section: a paragraph, a table or raw text.
	/// </summary>
	internal sealed class FlowItem
	{
		/// <summary>Gets or sets the measured paragraph, for paragraph items.</summary>
		public ParagraphLayout Paragraph { get; set; }

		/// <summary>Gets or sets the measured rows, for table items.</summary>
		public List<RowLayout> Rows { get; set; }

		/// <summary>Gets or sets the height of the rows repeated at the top of every page, for table items.</summary>
		public double HeaderRowsHeight { get; set; }

		/// <summary>Gets or sets the height of the item, for raw text items.</summary>
		public double Height { get; set; }

		/// <summary>Gets whether the item is a table.</summary>
		public bool IsTable => Rows != null;
	}
}

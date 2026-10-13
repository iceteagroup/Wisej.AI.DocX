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

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// Fully resolved paragraph formatting.
	/// </summary>
	/// <remarks>
	/// Measures are expressed in points. The semantic properties (<see cref="Kind"/>,
	/// <see cref="HeadingLevel"/>) drive the text output, the layout properties drive
	/// the page estimation.
	/// </remarks>
	internal sealed class ParagraphProperties
	{
		#region Semantics

		/// <summary>Gets or sets the identifier of the paragraph style.</summary>
		public string StyleId { get; set; }

		/// <summary>Gets or sets the display name of the paragraph style.</summary>
		public string StyleName { get; set; }

		/// <summary>Gets or sets the semantic role of the paragraph.</summary>
		public ParagraphKind Kind { get; set; }

		/// <summary>Gets or sets the heading level, from 1 to 9, or 0 when the paragraph is not a heading.</summary>
		public int HeadingLevel { get; set; }

		#endregion

		#region Layout

		/// <summary>Gets or sets the horizontal alignment.</summary>
		public ParagraphAlignment Alignment { get; set; }

		/// <summary>Gets or sets the space above the paragraph in points.</summary>
		public double SpacingBefore { get; set; }

		/// <summary>Gets or sets the space below the paragraph in points.</summary>
		public double SpacingAfter { get; set; }

		/// <summary>Gets or sets the line spacing: a multiple for <see cref="LineSpacingRule.Auto"/>, otherwise points.</summary>
		public double LineSpacing { get; set; } = 1.0;

		/// <summary>Gets or sets how <see cref="LineSpacing"/> is interpreted.</summary>
		public LineSpacingRule LineSpacingRule { get; set; }

		/// <summary>Gets or sets the left (leading) indentation in points.</summary>
		public double IndentLeft { get; set; }

		/// <summary>Gets or sets the right (trailing) indentation in points.</summary>
		public double IndentRight { get; set; }

		/// <summary>Gets or sets the first line indentation in points; negative values are hanging indents.</summary>
		public double IndentFirstLine { get; set; }

		/// <summary>Gets or sets whether the paragraph is kept on the same page as the next one.</summary>
		public bool KeepNext { get; set; }

		/// <summary>Gets or sets whether all the lines of the paragraph are kept on the same page.</summary>
		public bool KeepLines { get; set; }

		/// <summary>Gets or sets whether the paragraph always starts a new page.</summary>
		public bool PageBreakBefore { get; set; }

		/// <summary>Gets or sets whether widow and orphan lines are prevented.</summary>
		public bool WidowControl { get; set; } = true;

		/// <summary>Gets or sets whether spacing is ignored between paragraphs of the same style.</summary>
		public bool ContextualSpacing { get; set; }

		/// <summary>Gets or sets whether lines snap to the document grid, when the section defines one.</summary>
		public bool SnapToGrid { get; set; } = true;

		/// <summary>Gets or sets the extra height of the top and bottom paragraph borders, in points.</summary>
		public double BorderHeight { get; set; }

		/// <summary>Gets or sets the custom tab stops, sorted by position.</summary>
		public List<TabStop> Tabs { get; set; }

		/// <summary>Gets or sets the formatting of the paragraph mark, which sizes empty paragraphs.</summary>
		public RunStyle MarkStyle { get; set; } = RunStyle.Default;

		#endregion
	}

	/// <summary>
	/// A custom tab stop of a paragraph.
	/// </summary>
	internal sealed class TabStop
	{
		/// <summary>Gets or sets the position of the tab stop, in points from the leading page margin.</summary>
		public double Position { get; set; }

		/// <summary>Gets or sets the alignment of the text at the tab stop.</summary>
		public TabStopKind Kind { get; set; }
	}
}

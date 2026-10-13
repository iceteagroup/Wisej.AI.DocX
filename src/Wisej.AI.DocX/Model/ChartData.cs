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
	/// The data of a chart, as cached in the chart part.
	/// </summary>
	internal sealed class ChartData
	{
		/// <summary>Gets or sets the chart title, or <c>null</c>.</summary>
		public string Title { get; set; }

		/// <summary>Gets the kinds of plots in the chart, such as <c>bar</c>, <c>line</c> or <c>pie</c>.</summary>
		public List<string> ChartTypes { get; } = new List<string>();

		/// <summary>Gets or sets the label of the category column (<c>Category</c>, or <c>X</c> for scatter charts).</summary>
		public string CategoryLabel { get; set; } = "Category";

		/// <summary>Gets the category labels shared by the series.</summary>
		public List<string> Categories { get; } = new List<string>();

		/// <summary>Gets the data series.</summary>
		public List<ChartSeries> Series { get; } = new List<ChartSeries>();

		/// <summary>Gets or sets the title of the category (horizontal) axis, or <c>null</c>.</summary>
		public string CategoryAxisTitle { get; set; }

		/// <summary>Gets or sets the title of the value (vertical) axis, or <c>null</c>.</summary>
		public string ValueAxisTitle { get; set; }
	}

	/// <summary>
	/// A data series of a chart.
	/// </summary>
	internal sealed class ChartSeries
	{
		/// <summary>Gets or sets the name of the series.</summary>
		public string Name { get; set; }

		/// <summary>Gets the formatted values, aligned with <see cref="ChartData.Categories"/>.</summary>
		public List<string> Values { get; } = new List<string>();
	}

	/// <summary>
	/// A node of a SmartArt diagram.
	/// </summary>
	internal sealed class SmartArtNode
	{
		/// <summary>Gets or sets the text of the node.</summary>
		public string Text { get; set; }

		/// <summary>Gets the child nodes.</summary>
		public List<SmartArtNode> Children { get; } = new List<SmartArtNode>();
	}
}

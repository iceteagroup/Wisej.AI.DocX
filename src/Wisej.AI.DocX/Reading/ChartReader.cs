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
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Reads the data cached in chart parts: DrawingML charts (<c>c:chartSpace</c>) and
	/// Office 2016 extended charts (<c>cx:chartSpace</c>).
	/// </summary>
	/// <remarks>
	/// A chart rendered as a picture is invisible to a language model, but its parts carry the
	/// cached data of every series. The reader turns them into a table: one row per category,
	/// one column per series.
	/// </remarks>
	internal static class ChartReader
	{
		/// <summary>
		/// Reads a DrawingML chart.
		/// </summary>
		/// <param name="chartSpace">The root element of the chart part.</param>
		/// <returns>The chart data, or <c>null</c> when the chart has no series.</returns>
		public static ChartData Read(XElement chartSpace)
		{
			var chart = chartSpace?.Element(Ns.C + "chart");
			var plotArea = chart?.Element(Ns.C + "plotArea");
			if (plotArea == null)
				return null;

			var data = new ChartData();

			if (!XmlExtensions.ParseOnOff(chart.Element(Ns.C + "autoTitleDeleted")?.Attribute("val")?.Value, false))
				data.Title = RichText(chart.Element(Ns.C + "title"));

			var categories = new List<string>();
			var isScatter = false;

			foreach (var plot in plotArea.Elements().Where(e => e.Name.Namespace == Ns.C && e.Name.LocalName.EndsWith("Chart", StringComparison.Ordinal)))
			{
				var type = DescribeChartType(plot);
				if (!data.ChartTypes.Contains(type))
					data.ChartTypes.Add(type);

				var scatter = plot.Name.LocalName == "scatterChart" || plot.Name.LocalName == "bubbleChart";
				isScatter |= scatter;

				foreach (var ser in plot.Elements(Ns.C + "ser"))
				{
					var series = new ChartSeries { Name = SeriesName(ser.Element(Ns.C + "tx")) ?? $"Series {data.Series.Count + 1}" };

					var categoryElement = scatter ? ser.Element(Ns.C + "xVal") : ser.Element(Ns.C + "cat");
					var valueElement = scatter ? ser.Element(Ns.C + "yVal") : ser.Element(Ns.C + "val");

					var seriesCategories = ReadPoints(categoryElement);
					var values = ReadPoints(valueElement);

					if (seriesCategories.Count > categories.Count)
						categories = seriesCategories;

					series.Values.AddRange(values);
					data.Series.Add(series);
				}
			}

			if (data.Series.Count == 0)
				return null;

			FinishTable(data, categories, isScatter ? "X" : "Category");

			data.CategoryAxisTitle = RichText(plotArea.Element(Ns.C + "catAx")?.Element(Ns.C + "title"))
				?? RichText(plotArea.Element(Ns.C + "dateAx")?.Element(Ns.C + "title"));
			data.ValueAxisTitle = RichText(plotArea.Element(Ns.C + "valAx")?.Element(Ns.C + "title"));

			if (data.Title == null && data.Series.Count == 1)
				data.Title = data.Series[0].Name;

			return data;
		}

		/// <summary>
		/// Reads an Office 2016 extended chart (waterfall, treemap, sunburst, histogram, box and whisker, funnel).
		/// </summary>
		/// <param name="chartSpace">The root element of the extended chart part.</param>
		/// <returns>The chart data, or <c>null</c> when the chart has no series.</returns>
		public static ChartData ReadExtended(XElement chartSpace)
		{
			var chartData = chartSpace?.Element(Ns.CX + "chartData");
			var chart = chartSpace?.Element(Ns.CX + "chart");
			if (chartData == null || chart == null)
				return null;

			var data = new ChartData { Title = RichText(chart.Element(Ns.CX + "title")) };

			// The data sets are stored separately and referenced by the series.
			var sets = chartData.Elements(Ns.CX + "data").ToDictionary(d => d.Attribute("id")?.Value ?? "", d => d);
			var categories = new List<string>();

			var regions = chart.Element(Ns.CX + "plotArea")?.Element(Ns.CX + "plotAreaRegion");
			foreach (var series in regions?.Elements(Ns.CX + "series") ?? Enumerable.Empty<XElement>())
			{
				var type = series.Attribute("layoutId")?.Value ?? "chart";
				if (!data.ChartTypes.Contains(type))
					data.ChartTypes.Add(type);

				var dataId = series.Element(Ns.CX + "dataId")?.Attribute("val")?.Value ?? "";
				if (!sets.TryGetValue(dataId, out var set))
					continue;

				var labels = set.Element(Ns.CX + "strDim")?.Elements(Ns.CX + "lvl").FirstOrDefault()
					?.Elements(Ns.CX + "pt").Select(p => p.Value).ToList() ?? new List<string>();

				var values = set.Element(Ns.CX + "numDim")?.Elements(Ns.CX + "lvl").FirstOrDefault()
					?.Elements(Ns.CX + "pt").Select(p => FormatNumber(p.Value, null)).ToList() ?? new List<string>();

				if (labels.Count > categories.Count)
					categories = labels;

				var item = new ChartSeries
				{
					Name = series.Element(Ns.CX + "tx")?.Descendants(Ns.CX + "v").FirstOrDefault()?.Value ?? $"Series {data.Series.Count + 1}",
				};

				item.Values.AddRange(values);
				data.Series.Add(item);
			}

			if (data.Series.Count == 0)
				return null;

			FinishTable(data, categories, "Category");
			return data;
		}

		#region Implementation

		/// <summary>
		/// Aligns the series values with the categories and fills the category list.
		/// </summary>
		/// <param name="data">The chart data to complete.</param>
		/// <param name="categories">The longest category list found in the series.</param>
		/// <param name="label">The label of the category column.</param>
		private static void FinishTable(ChartData data, List<string> categories, string label)
		{
			var rows = Math.Max(categories.Count, data.Series.Max(s => s.Values.Count));

			for (var i = 0; i < rows; i++)
				data.Categories.Add(i < categories.Count && !String.IsNullOrEmpty(categories[i]) ? categories[i] : (i + 1).ToString(CultureInfo.InvariantCulture));

			foreach (var series in data.Series)
			{
				while (series.Values.Count < rows)
					series.Values.Add("");
			}

			data.CategoryLabel = label;
		}

		/// <summary>
		/// Reads the cached points of a category or value reference.
		/// </summary>
		/// <param name="element">The <c>c:cat</c>, <c>c:val</c>, <c>c:xVal</c> or <c>c:yVal</c> element. Can be <c>null</c>.</param>
		/// <returns>The values, indexed by point.</returns>
		private static List<string> ReadPoints(XElement element)
		{
			var result = new List<string>();
			if (element == null)
				return result;

			// Multi-level categories: use the innermost level, which labels every point.
			var cache = element.Descendants(Ns.C + "strCache").FirstOrDefault()
				?? element.Descendants(Ns.C + "numCache").FirstOrDefault()
				?? element.Descendants(Ns.C + "multiLvlStrCache").FirstOrDefault()?.Elements(Ns.C + "lvl").FirstOrDefault()
				?? element.Element(Ns.C + "strLit")
				?? element.Element(Ns.C + "numLit");

			if (cache == null)
				return result;

			var formatCode = cache.Element(Ns.C + "formatCode")?.Value;
			var count = XmlExtensions.ParseInt(cache.Element(Ns.C + "ptCount")?.Attribute("val")?.Value) ?? 0;
			var points = new SortedDictionary<int, string>();

			foreach (var pt in cache.Elements(Ns.C + "pt"))
			{
				var index = XmlExtensions.ParseInt(pt.Attribute("idx")?.Value) ?? points.Count;
				var value = pt.Element(Ns.C + "v")?.Value ?? "";
				var pointFormat = pt.Attribute("formatCode")?.Value ?? formatCode;

				points[index] = cache.Name.LocalName == "numCache" || cache.Name.LocalName == "numLit"
					? FormatNumber(value, pointFormat)
					: value;
			}

			var size = Math.Max(count, points.Count == 0 ? 0 : points.Keys.Max() + 1);
			for (var i = 0; i < size; i++)
				result.Add(points.TryGetValue(i, out var value) ? value : "");

			return result;
		}

		/// <summary>
		/// Formats a cached number using the essential parts of its Excel number format.
		/// </summary>
		/// <param name="value">The cached value, in invariant culture.</param>
		/// <param name="formatCode">The Excel format code. Can be <c>null</c>.</param>
		/// <returns>The formatted number.</returns>
		private static string FormatNumber(string value, string formatCode)
		{
			if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
				return value;

			if (formatCode != null && formatCode.Contains("%"))
			{
				var decimals = DecimalPlaces(formatCode);
				return (number * 100).ToString("F" + decimals, CultureInfo.InvariantCulture) + "%";
			}

			if (formatCode != null && formatCode.Contains("0") && formatCode.Contains("."))
				return number.ToString("F" + DecimalPlaces(formatCode), CultureInfo.InvariantCulture);

			// Round floating point noise such as 0.30000000000000004.
			return Math.Round(number, 10).ToString("G15", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Counts the decimal places requested by an Excel number format.
		/// </summary>
		/// <param name="formatCode">The format code.</param>
		/// <returns>The number of zeros after the decimal point.</returns>
		private static int DecimalPlaces(string formatCode)
		{
			var dot = formatCode.IndexOf('.');
			if (dot < 0)
				return 0;

			var count = 0;
			for (var i = dot + 1; i < formatCode.Length && (formatCode[i] == '0' || formatCode[i] == '#'); i++)
				count++;

			return count;
		}

		/// <summary>
		/// Returns the name of a series from its <c>c:tx</c> element.
		/// </summary>
		/// <param name="tx">The series text element. Can be <c>null</c>.</param>
		/// <returns>The name, or <c>null</c>.</returns>
		private static string SeriesName(XElement tx)
		{
			if (tx == null)
				return null;

			var value = tx.Descendants(Ns.C + "v").FirstOrDefault()?.Value;
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		/// <summary>
		/// Returns the text of a chart title or of an axis title.
		/// </summary>
		/// <param name="title">The title element. Can be <c>null</c>.</param>
		/// <returns>The text, or <c>null</c>.</returns>
		private static string RichText(XElement title)
		{
			if (title == null)
				return null;

			var paragraphs = title.Descendants(Ns.A + "p")
				.Select(p => String.Concat(p.Descendants(Ns.A + "t").Select(t => t.Value)))
				.Where(t => t.Length > 0);

			var text = String.Join(" ", paragraphs);

			// Titles linked to a cell keep their text in a string cache.
			if (text.Length == 0)
				text = title.Descendants(Ns.C + "v").FirstOrDefault()?.Value ?? title.Descendants(Ns.CX + "v").FirstOrDefault()?.Value ?? "";

			return String.IsNullOrWhiteSpace(text) ? null : text.Trim();
		}

		/// <summary>
		/// Describes the type of a plot, such as <c>bar</c>, <c>stacked column</c> or <c>pie</c>.
		/// </summary>
		/// <param name="plot">The plot element (<c>c:barChart</c>, <c>c:lineChart</c>, ...).</param>
		/// <returns>The description.</returns>
		private static string DescribeChartType(XElement plot)
		{
			var name = plot.Name.LocalName.Replace("Chart", "").Replace("3D", "");
			var description = name;

			switch (name)
			{
				case "bar":
					description = plot.Element(Ns.C + "barDir")?.Attribute("val")?.Value == "bar" ? "bar" : "column";
					break;

				case "ofPie":
					description = "pie of pie";
					break;

				case "doughnut":
				case "pie":
				case "line":
				case "area":
				case "scatter":
				case "bubble":
				case "radar":
				case "stock":
				case "surface":
					break;
			}

			var grouping = plot.Element(Ns.C + "grouping")?.Attribute("val")?.Value;
			if (grouping == "stacked")
				description = "stacked " + description;
			else if (grouping == "percentStacked")
				description = "100% stacked " + description;

			return description;
		}

		#endregion
	}
}

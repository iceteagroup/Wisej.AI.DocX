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
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Accumulates paragraph properties (<c>w:pPr</c>) across the layers of the style cascade.
	/// </summary>
	/// <remarks>
	/// Every property is nullable: a layer only overrides the properties it specifies.
	/// <see cref="Apply"/> is called once per layer, from the weakest (document defaults)
	/// to the strongest (direct formatting).
	/// </remarks>
	internal sealed class ParagraphPropertiesBuilder
	{
		private ParagraphAlignment? _alignment;
		private double? _before;
		private double? _after;
		private bool? _beforeAuto;
		private bool? _afterAuto;
		private double? _line;
		private LineSpacingRule? _lineRule;
		private double? _left;
		private double? _right;
		private double? _firstLine;
		private bool? _keepNext;
		private bool? _keepLines;
		private bool? _pageBreakBefore;
		private bool? _widowControl;
		private bool? _contextualSpacing;
		private bool? _snapToGrid;
		private double? _borderHeight;
		private SortedDictionary<double, TabStop> _tabs;

		#region Properties

		/// <summary>Gets or sets the outline level (0 to 9) specified by the layers, or <c>null</c>.</summary>
		public int? OutlineLevel { get; set; }

		/// <summary>Gets or sets the numbering instance specified by the layers, or <c>null</c>.</summary>
		public int? NumberingId { get; set; }

		/// <summary>Gets or sets the numbering level specified by the layers, or <c>null</c>.</summary>
		public int? NumberingLevel { get; set; }

		#endregion

		/// <summary>
		/// Creates a copy of the accumulated properties.
		/// </summary>
		/// <returns>A new builder with the same values.</returns>
		public ParagraphPropertiesBuilder Clone()
		{
			var clone = (ParagraphPropertiesBuilder)MemberwiseClone();
			if (_tabs != null)
				clone._tabs = new SortedDictionary<double, TabStop>(_tabs);

			return clone;
		}

		/// <summary>
		/// Applies the properties specified by a <c>w:pPr</c> element over the accumulated ones.
		/// </summary>
		/// <param name="pPr">The paragraph properties element. Can be <c>null</c>.</param>
		/// <param name="includeNumbering">Whether <c>w:numPr</c> is read; numbering levels must not redefine it.</param>
		public void Apply(XElement pPr, bool includeNumbering = true)
		{
			if (pPr == null)
				return;

			foreach (var e in pPr.Elements())
			{
				if (e.Name.Namespace != Ns.W)
					continue;

				switch (e.Name.LocalName)
				{
					case "jc":
						_alignment = ParseAlignment(e.WVal()) ?? _alignment;
						break;

					case "spacing":
						ApplySpacing(e);
						break;

					case "ind":
						ApplyIndentation(e);
						break;

					case "keepNext":
						_keepNext = e.OnOff();
						break;

					case "keepLines":
						_keepLines = e.OnOff();
						break;

					case "pageBreakBefore":
						_pageBreakBefore = e.OnOff();
						break;

					case "widowControl":
						_widowControl = e.OnOff();
						break;

					case "contextualSpacing":
						_contextualSpacing = e.OnOff();
						break;

					case "snapToGrid":
						_snapToGrid = e.OnOff();
						break;

					case "outlineLvl":
						OutlineLevel = XmlExtensions.ParseInt(e.WVal()) ?? OutlineLevel;
						break;

					case "numPr":
						if (includeNumbering)
							ApplyNumbering(e);
						break;

					case "tabs":
						ApplyTabs(e);
						break;

					case "pBdr":
						_borderHeight = BorderHeight(e.Element(Ns.W + "top")) + BorderHeight(e.Element(Ns.W + "bottom"));
						break;
				}
			}
		}

		/// <summary>
		/// Produces the final paragraph properties.
		/// </summary>
		/// <returns>A new <see cref="ParagraphProperties"/> with the accumulated values or their defaults.</returns>
		public ParagraphProperties Build()
		{
			var before = _beforeAuto == true ? 14.0 : _before ?? 0;
			var after = _afterAuto == true ? 14.0 : _after ?? 0;

			return new ParagraphProperties
			{
				Alignment = _alignment ?? ParagraphAlignment.Left,
				SpacingBefore = before,
				SpacingAfter = after,
				LineSpacing = _line ?? 1.0,
				LineSpacingRule = _lineRule ?? LineSpacingRule.Auto,
				IndentLeft = _left ?? 0,
				IndentRight = _right ?? 0,
				IndentFirstLine = _firstLine ?? 0,
				KeepNext = _keepNext ?? false,
				KeepLines = _keepLines ?? false,
				PageBreakBefore = _pageBreakBefore ?? false,
				WidowControl = _widowControl ?? true,
				ContextualSpacing = _contextualSpacing ?? false,
				SnapToGrid = _snapToGrid ?? true,
				BorderHeight = _borderHeight ?? 0,
				Tabs = _tabs?.Values.ToList(),
			};
		}

		#region Implementation

		/// <summary>
		/// Applies <c>w:spacing</c>.
		/// </summary>
		/// <param name="e">The spacing element.</param>
		private void ApplySpacing(XElement e)
		{
			var before = XmlExtensions.TwipsToPoints(e.WAttr("before"));
			var beforeLines = XmlExtensions.ParseInt(e.WAttr("beforeLines"));
			var after = XmlExtensions.TwipsToPoints(e.WAttr("after"));
			var afterLines = XmlExtensions.ParseInt(e.WAttr("afterLines"));

			// Line based spacing (hundredths of a 12pt line) takes precedence over absolute spacing.
			if (beforeLines != null)
				_before = beforeLines.Value * 12.0 / 100.0;
			else if (before != null)
				_before = before;

			if (afterLines != null)
				_after = afterLines.Value * 12.0 / 100.0;
			else if (after != null)
				_after = after;

			if (e.WAttr("beforeAutospacing") != null)
				_beforeAuto = XmlExtensions.ParseOnOff(e.WAttr("beforeAutospacing"), true);

			if (e.WAttr("afterAutospacing") != null)
				_afterAuto = XmlExtensions.ParseOnOff(e.WAttr("afterAutospacing"), true);

			var line = XmlExtensions.ParseDouble(e.WAttr("line"));
			if (line != null)
			{
				switch (e.WAttr("lineRule"))
				{
					case "exact":
						_lineRule = LineSpacingRule.Exact;
						_line = System.Math.Abs(line.Value) / 20.0;
						break;

					case "atLeast":
						_lineRule = LineSpacingRule.AtLeast;
						_line = line.Value / 20.0;
						break;

					default:
						_lineRule = LineSpacingRule.Auto;
						_line = line.Value / 240.0;
						break;
				}
			}
		}

		/// <summary>
		/// Applies <c>w:ind</c>.
		/// </summary>
		/// <param name="e">The indentation element.</param>
		private void ApplyIndentation(XElement e)
		{
			var left = XmlExtensions.TwipsToPoints(e.WAttr("start") ?? e.WAttr("left"));
			var right = XmlExtensions.TwipsToPoints(e.WAttr("end") ?? e.WAttr("right"));
			var firstLine = XmlExtensions.TwipsToPoints(e.WAttr("firstLine"));
			var hanging = XmlExtensions.TwipsToPoints(e.WAttr("hanging"));

			if (left != null)
				_left = left;

			if (right != null)
				_right = right;

			// A hanging indent wins over a first line indent specified at the same level.
			if (hanging != null)
				_firstLine = -hanging;
			else if (firstLine != null)
				_firstLine = firstLine;
		}

		/// <summary>
		/// Applies <c>w:numPr</c>.
		/// </summary>
		/// <param name="e">The numbering properties element.</param>
		private void ApplyNumbering(XElement e)
		{
			var numId = XmlExtensions.ParseInt(e.WChildVal("numId"));
			var ilvl = XmlExtensions.ParseInt(e.WChildVal("ilvl"));

			if (numId != null)
				NumberingId = numId;

			if (ilvl != null)
				NumberingLevel = ilvl;
		}

		/// <summary>
		/// Applies <c>w:tabs</c>, merging the stops with the inherited ones.
		/// </summary>
		/// <param name="e">The tabs element.</param>
		private void ApplyTabs(XElement e)
		{
			foreach (var tab in e.Elements(Ns.W + "tab"))
			{
				var position = XmlExtensions.TwipsToPoints(tab.WAttr("pos"));
				if (position == null)
					continue;

				_tabs = _tabs ?? new SortedDictionary<double, TabStop>();

				var kind = tab.WVal();
				if (kind == "clear")
				{
					_tabs.Remove(position.Value);
					continue;
				}

				_tabs[position.Value] = new TabStop
				{
					Position = position.Value,
					Kind = kind == "right" || kind == "end"
						? TabStopKind.Right
						: kind == "center"
							? TabStopKind.Center
							: kind == "decimal"
								? TabStopKind.Decimal
								: TabStopKind.Left,
				};
			}
		}

		/// <summary>
		/// Returns the height added by a paragraph border: the line width plus its spacing.
		/// </summary>
		/// <param name="border">The <c>w:top</c> or <c>w:bottom</c> border element. Can be <c>null</c>.</param>
		/// <returns>The height in points.</returns>
		private static double BorderHeight(XElement border)
		{
			if (border == null || border.WVal() == "nil" || border.WVal() == "none")
				return 0;

			var size = XmlExtensions.ParseDouble(border.WAttr("sz")) ?? 4;
			var space = XmlExtensions.ParseDouble(border.WAttr("space")) ?? 0;

			// The width is in eighths of a point, the spacing in points.
			return size / 8.0 + space;
		}

		/// <summary>
		/// Parses <c>w:jc/@w:val</c>.
		/// </summary>
		/// <param name="value">The attribute value.</param>
		/// <returns>The alignment, or <c>null</c> when not recognized.</returns>
		private static ParagraphAlignment? ParseAlignment(string value)
		{
			switch (value)
			{
				case "left":
				case "start":
					return ParagraphAlignment.Left;

				case "center":
					return ParagraphAlignment.Center;

				case "right":
				case "end":
					return ParagraphAlignment.Right;

				case "both":
				case "distribute":
				case "lowKashida":
				case "mediumKashida":
				case "highKashida":
				case "thaiDistribute":
					return ParagraphAlignment.Justify;

				default:
					return null;
			}
		}

		#endregion
	}
}

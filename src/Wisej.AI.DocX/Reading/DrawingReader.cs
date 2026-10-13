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
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Reads drawings: DrawingML (<c>w:drawing</c>) and VML (<c>w:pict</c>, <c>w:object</c>).
	/// </summary>
	/// <remarks>
	/// A drawing can be a picture, a chart, a SmartArt diagram, a shape with text (text box),
	/// or a group or canvas containing any of those. The reader produces one inline per item.
	/// </remarks>
	internal sealed class DrawingReader
	{
		private readonly DocxPackage _package;
		private readonly DocxExtractorOptions _options;
		private readonly ImageRegistry _images;
		private readonly Func<XElement, ReadContext, List<Block>> _readBlocks;

		/// <summary>
		/// Initializes a new instance of the <see cref="DrawingReader"/> class.
		/// </summary>
		/// <param name="package">The package, used to load chart and diagram parts.</param>
		/// <param name="options">The extraction options.</param>
		/// <param name="images">The image registry.</param>
		/// <param name="readBlocks">Reads the block content of a text box.</param>
		public DrawingReader(
			DocxPackage package,
			DocxExtractorOptions options,
			ImageRegistry images,
			Func<XElement, ReadContext, List<Block>> readBlocks)
		{
			_package = package;
			_options = options;
			_images = images;
			_readBlocks = readBlocks;
		}

		#region DrawingML

		/// <summary>
		/// Reads a DrawingML drawing (<c>w:drawing</c>).
		/// </summary>
		/// <param name="drawing">The drawing element.</param>
		/// <param name="context">The read context.</param>
		/// <returns>The inlines of the drawing.</returns>
		public List<Inline> ReadDrawing(XElement drawing, ReadContext context)
		{
			var result = new List<Inline>();

			foreach (var container in drawing.ContentElements())
			{
				if (container.Name != Ns.WP + "inline" && container.Name != Ns.WP + "anchor")
					continue;

				var docPr = container.Element(Ns.WP + "docPr");
				if (XmlExtensions.ParseOnOff(docPr?.Attribute("hidden")?.Value, false))
					continue;

				var extent = container.Element(Ns.WP + "extent");
				var info = new DrawingInfo
				{
					Width = XmlExtensions.EmuToPoints(extent?.Attribute("cx")?.Value) ?? 0,
					Height = XmlExtensions.EmuToPoints(extent?.Attribute("cy")?.Value) ?? 0,
					Wrap = container.Name == Ns.WP + "inline" ? WrapKind.Inline : GetWrap(container),
					Name = docPr?.Attribute("name")?.Value,
					AltText = NullIfEmpty(docPr?.Attribute("descr")?.Value),
					Title = NullIfEmpty(docPr?.Attribute("title")?.Value),
					IsDecorative = docPr?.Descendants(Ns.ADec + "decorative").Any(d => XmlExtensions.ParseOnOff(d.Attribute("val")?.Value, true)) == true,
				};

				var graphicData = container.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
				if (graphicData == null)
					continue;

				var count = result.Count;
				ReadGraphic(graphicData, info, context, result);

				// The members of a group float inside it: an invisible spacer keeps the space the group takes.
				var takesSpace = info.Wrap == WrapKind.Inline || info.Wrap == WrapKind.TopAndBottom;
				if (takesSpace && !result.Skip(count).OfType<DrawingInline>().Any(d => d.Wrap == info.Wrap))
					result.Add(new ImageInline { Width = info.Width, Height = info.Height, Wrap = info.Wrap, IsExcluded = true });
			}

			return result;
		}

		/// <summary>
		/// Reads the content of an <c>a:graphicData</c> element.
		/// </summary>
		/// <param name="graphicData">The graphic data element.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inlines.</param>
		private void ReadGraphic(XElement graphicData, DrawingInfo info, ReadContext context, List<Inline> result)
		{
			var uri = graphicData.Attribute("uri")?.Value;

			switch (uri)
			{
				case Ns.GraphicPicture:
					foreach (var pic in graphicData.Elements(Ns.Pic + "pic"))
						AddPicture(pic, info, context, result);
					break;

				case Ns.GraphicChart:
					AddChart(graphicData.Element(Ns.C + "chart"), info, context, result, extended: false);
					break;

				case Ns.GraphicChartEx:
					AddChart(graphicData.Element(Ns.CX + "chart"), info, context, result, extended: true);
					break;

				case Ns.GraphicDiagram:
					AddSmartArt(graphicData.Element(Ns.Dgm + "relIds"), info, context, result);
					break;

				case Ns.GraphicShape:
					foreach (var shape in graphicData.ContentElements().Where(e => e.Name == Ns.Wps + "wsp"))
						AddShape(shape, info, context, result);
					break;

				case Ns.GraphicGroup:
				case Ns.GraphicCanvas:
					foreach (var group in graphicData.ContentElements())
						ReadGroup(group, info, context, result, 0);
					break;

				default:
					// Unknown graphic types often still carry a picture.
					foreach (var pic in graphicData.Descendants(Ns.Pic + "pic"))
						AddPicture(pic, info, context, result);
					break;
			}
		}

		/// <summary>
		/// Reads the members of a shape group or drawing canvas.
		/// </summary>
		/// <param name="group">The group (<c>wpg:wgp</c>, <c>wpg:grpSp</c>) or canvas (<c>wpc:wpc</c>) element.</param>
		/// <param name="info">The size and description of the enclosing drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inlines.</param>
		/// <param name="depth">The nesting depth of the group.</param>
		private void ReadGroup(XElement group, DrawingInfo info, ReadContext context, List<Inline> result, int depth)
		{
			if (depth > ReadLimits.MaxNestingDepth)
				return;

			foreach (var member in group.ContentElements())
			{
				var memberInfo = info.ForMember(member);

				if (member.Name == Ns.Wps + "wsp")
					AddShape(member, memberInfo, context, result);
				else if (member.Name == Ns.Pic + "pic")
					AddPicture(member, memberInfo, context, result);
				else if (member.Name == Ns.Wpg + "grpSp" || member.Name == Ns.Wpg + "wgp")
					ReadGroup(member, memberInfo, context, result, depth + 1);
				else if (member.Name == Ns.Wpg + "graphicFrame" || member.Name == Ns.Wpc + "graphicFrame")
				{
					var graphicData = member.Element(Ns.A + "graphic")?.Element(Ns.A + "graphicData");
					if (graphicData != null)
						ReadGraphic(graphicData, memberInfo, context, result);
				}
			}
		}

		/// <summary>
		/// Adds a picture (<c>pic:pic</c>).
		/// </summary>
		/// <param name="pic">The picture element.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		private void AddPicture(XElement pic, DrawingInfo info, ReadContext context, List<Inline> result)
		{
			var blip = pic.Element(Ns.Pic + "blipFill")?.Element(Ns.A + "blip");
			if (blip == null)
				return;

			// The picture's own description is used when the drawing has none.
			var cNvPr = pic.Element(Ns.Pic + "nvPicPr")?.Element(Ns.Pic + "cNvPr");
			var altText = info.AltText ?? NullIfEmpty(cNvPr?.Attribute("descr")?.Value);

			AddImage(blip.Attribute(Ns.R + "embed")?.Value, blip.Attribute(Ns.R + "link")?.Value, info, altText, context, result);
		}

		/// <summary>
		/// Adds a chart referenced by a <c>c:chart</c> or <c>cx:chart</c> element.
		/// </summary>
		/// <param name="chart">The chart reference element. Can be <c>null</c>.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		/// <param name="extended">Whether the chart is an Office 2016 extended chart.</param>
		private void AddChart(XElement chart, DrawingInfo info, ReadContext context, List<Inline> result, bool extended)
		{
			var part = DocxPackage.GetRelatedPart(context.Part, chart?.Attribute(Ns.R + "id")?.Value);
			var xml = _package.GetXml(part);

			var data = extended ? ChartReader.ReadExtended(xml) : ChartReader.Read(xml);
			if (data == null)
				return;

			if (data.Title == null)
				data.Title = info.Title ?? info.AltText;

			result.Add(new ChartInline
			{
				Chart = data,
				Width = info.Width,
				Height = info.Height,
				Wrap = info.Wrap,
				IsExcluded = !_options.IncludeCharts,
			});
		}

		/// <summary>
		/// Adds a SmartArt diagram referenced by a <c>dgm:relIds</c> element.
		/// </summary>
		/// <param name="relIds">The diagram relationships element. Can be <c>null</c>.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		private void AddSmartArt(XElement relIds, DrawingInfo info, ReadContext context, List<Inline> result)
		{
			var part = DocxPackage.GetRelatedPart(context.Part, relIds?.Attribute(Ns.R + "dm")?.Value);
			var nodes = SmartArtReader.Read(_package.GetXml(part));
			if (nodes.Count == 0)
				return;

			result.Add(new SmartArtInline
			{
				Width = info.Width,
				Height = info.Height,
				Wrap = info.Wrap,
				IsExcluded = !_options.IncludeSmartArt,
				Nodes = nodes,
			});
		}

		/// <summary>
		/// Adds the text of a Word shape (<c>wps:wsp</c>), when it has a text box.
		/// </summary>
		/// <param name="shape">The shape element.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		private void AddShape(XElement shape, DrawingInfo info, ReadContext context, List<Inline> result)
		{
			var content = shape.Element(Ns.Wps + "txbx")?.Element(Ns.W + "txbxContent");
			if (content == null)
				return;

			AddTextBox(content, info, context, result);
		}

		#endregion

		#region VML

		/// <summary>
		/// Reads a VML drawing (<c>w:pict</c>) or an embedded object (<c>w:object</c>).
		/// </summary>
		/// <param name="pict">The <c>w:pict</c> or <c>w:object</c> element.</param>
		/// <param name="context">The read context.</param>
		/// <returns>The inlines of the drawing.</returns>
		public List<Inline> ReadVml(XElement pict, ReadContext context)
		{
			var result = new List<Inline>();

			// An embedded object shows a preview picture; its program identifier describes the content.
			var ole = pict.Element(Ns.O + "OLEObject");
			var oleDescription = ole == null ? null : $"Embedded {ole.Attribute("ProgID")?.Value ?? "object"}";

			foreach (var shape in pict.ContentElements())
				ReadVmlShape(shape, context, result, oleDescription, 0);

			return result;
		}

		/// <summary>
		/// Reads a VML shape and, for groups, its members.
		/// </summary>
		/// <param name="shape">The VML element.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inlines.</param>
		/// <param name="oleDescription">The description of the embedded object, or <c>null</c>.</param>
		/// <param name="depth">The nesting depth of the shape.</param>
		private void ReadVmlShape(XElement shape, ReadContext context, List<Inline> result, string oleDescription, int depth)
		{
			if (shape.Name.Namespace != Ns.V || depth > ReadLimits.MaxNestingDepth)
				return;

			var style = XmlExtensions.ParseCssStyle(shape.Attribute("style")?.Value);
			if (style.TryGetValue("visibility", out var visibility) && visibility == "hidden")
				return;

			var info = new DrawingInfo
			{
				Width = XmlExtensions.CssLengthToPoints(style.TryGetValue("width", out var width) ? width : null) ?? 0,
				Height = XmlExtensions.CssLengthToPoints(style.TryGetValue("height", out var height) ? height : null) ?? 0,
				Wrap = style.TryGetValue("position", out var position) && position == "absolute" ? GetVmlWrap(shape) : WrapKind.Inline,
				AltText = NullIfEmpty(shape.Attribute("alt")?.Value) ?? oleDescription,
			};

			if (shape.Name.LocalName == "group")
			{
				foreach (var member in shape.Elements())
					ReadVmlShape(member, context, result, oleDescription, depth + 1);

				return;
			}

			var imageData = shape.Element(Ns.V + "imagedata");
			if (imageData != null)
			{
				var id = imageData.Attribute(Ns.R + "id")?.Value ?? imageData.Attribute(Ns.O + "relid")?.Value;
				var title = NullIfEmpty(imageData.Attribute(Ns.O + "title")?.Value);

				AddImage(id, imageData.Attribute(Ns.R + "href")?.Value, info, info.AltText ?? title, context, result);
			}

			var textBox = shape.Element(Ns.V + "textbox")?.Element(Ns.W + "txbxContent");
			if (textBox != null)
				AddTextBox(textBox, info, context, result);

			// WordArt. In headers and footers it is almost always a watermark ("DRAFT", "CONFIDENTIAL").
			var textPath = shape.Element(Ns.V + "textpath")?.Attribute("string")?.Value;
			if (!String.IsNullOrWhiteSpace(textPath) && context.Location != "header" && context.Location != "footer" && _options.IncludeTextBoxes)
			{
				var paragraph = new ParagraphBlock { Properties = new ParagraphProperties() };
				paragraph.Inlines.Add(new TextInline { Text = textPath.Trim() });

				var inline = new TextBoxInline { Width = info.Width, Height = info.Height, Wrap = info.Wrap };
				inline.Blocks.Add(paragraph);
				result.Add(inline);
			}
		}

		/// <summary>
		/// Determines the wrapping of a floating VML shape from its <c>w10:wrap</c> child.
		/// </summary>
		/// <param name="shape">The VML shape.</param>
		/// <returns>The wrapping.</returns>
		private static WrapKind GetVmlWrap(XElement shape)
		{
			var wrap = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "wrap")?.Attribute("type")?.Value;

			switch (wrap)
			{
				case "topAndBottom":
					return WrapKind.TopAndBottom;

				case "square":
				case "tight":
				case "through":
					return WrapKind.Around;

				default:
					return WrapKind.None;
			}
		}

		#endregion

		#region Common

		/// <summary>
		/// Adds an image placement, applying the image options.
		/// </summary>
		/// <param name="embedId">The relationship of an embedded picture. Can be <c>null</c>.</param>
		/// <param name="linkId">The relationship of a linked picture. Can be <c>null</c>.</param>
		/// <param name="info">The size and description of the drawing.</param>
		/// <param name="altText">The alternative text. Can be <c>null</c>.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		private void AddImage(string embedId, string linkId, DrawingInfo info, string altText, ReadContext context, List<Inline> result)
		{
			var excluded = !_options.IncludeImages
				|| (_options.SkipDecorativeImages && info.IsDecorative)
				|| (_options.MinimumImageSize > 0 && Math.Max(info.Width, info.Height) < _options.MinimumImageSize);

			// Excluded images still take space on the page, so they are kept for the layout.
			var image = excluded ? null : _images.Create(context.Part, embedId, linkId, context.Location);
			if (image != null)
			{
				image.AltText = altText;
				image.Title = info.Title;
				image.Name = info.Name;
				image.Width = info.Width;
				image.Height = info.Height;
			}

			result.Add(new ImageInline
			{
				Image = image,
				Width = info.Width,
				Height = info.Height,
				Wrap = info.Wrap,
				IsExcluded = image == null,
			});
		}

		/// <summary>
		/// Adds a text box.
		/// </summary>
		/// <param name="content">The <c>w:txbxContent</c> element.</param>
		/// <param name="info">The size of the text box.</param>
		/// <param name="context">The read context.</param>
		/// <param name="result">The list that receives the inline.</param>
		private void AddTextBox(XElement content, DrawingInfo info, ReadContext context, List<Inline> result)
		{
			var blocks = _readBlocks(content, context.WithTextBox());
			if (blocks.Count == 0)
				return;

			var inline = new TextBoxInline
			{
				Width = info.Width,
				Height = info.Height,
				Wrap = info.Wrap,
				IsExcluded = !_options.IncludeTextBoxes,
			};

			inline.Blocks.AddRange(blocks);
			result.Add(inline);
		}

		/// <summary>
		/// Determines the text wrapping of an anchored drawing.
		/// </summary>
		/// <param name="anchor">The <c>wp:anchor</c> element.</param>
		/// <returns>The wrapping.</returns>
		private static WrapKind GetWrap(XElement anchor)
		{
			foreach (var e in anchor.Elements())
			{
				switch (e.Name.LocalName)
				{
					case "wrapTopAndBottom":
						return WrapKind.TopAndBottom;

					case "wrapSquare":
					case "wrapTight":
					case "wrapThrough":
						return WrapKind.Around;

					case "wrapNone":
						return WrapKind.None;
				}
			}

			return WrapKind.None;
		}

		/// <summary>
		/// Returns <c>null</c> for empty or white space strings.
		/// </summary>
		/// <param name="value">The value.</param>
		/// <returns>The trimmed value, or <c>null</c>.</returns>
		private static string NullIfEmpty(string value)
		{
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}

		#endregion

		#region DrawingInfo

		/// <summary>
		/// The size, wrapping and description shared by the items of a drawing.
		/// </summary>
		private sealed class DrawingInfo
		{
			/// <summary>Gets or sets the width in points.</summary>
			public double Width { get; set; }

			/// <summary>Gets or sets the height in points.</summary>
			public double Height { get; set; }

			/// <summary>Gets or sets the text wrapping.</summary>
			public WrapKind Wrap { get; set; }

			/// <summary>Gets or sets the name of the drawing object.</summary>
			public string Name { get; set; }

			/// <summary>Gets or sets the alternative text.</summary>
			public string AltText { get; set; }

			/// <summary>Gets or sets the title.</summary>
			public string Title { get; set; }

			/// <summary>Gets or sets whether the author marked the drawing as decorative.</summary>
			public bool IsDecorative { get; set; }

			/// <summary>
			/// Returns the information of a member of a group, using its own size and description when available.
			/// </summary>
			/// <param name="member">The group member element.</param>
			/// <returns>The member information.</returns>
			public DrawingInfo ForMember(XElement member)
			{
				var clone = (DrawingInfo)MemberwiseClone();

				var extent = member.Descendants(Ns.A + "xfrm").FirstOrDefault()?.Element(Ns.A + "ext");
				if (extent != null)
				{
					clone.Width = XmlExtensions.EmuToPoints(extent.Attribute("cx")?.Value) ?? Width;
					clone.Height = XmlExtensions.EmuToPoints(extent.Attribute("cy")?.Value) ?? Height;
				}

				// Members float inside the group: the group takes the space, not every member.
				clone.Wrap = WrapKind.None;

				var cNvPr = member.Descendants().FirstOrDefault(e => e.Name.LocalName == "cNvPr");
				var descr = NullIfEmpty(cNvPr?.Attribute("descr")?.Value);
				if (descr != null)
					clone.AltText = descr;

				return clone;
			}
		}

		#endregion
	}
}

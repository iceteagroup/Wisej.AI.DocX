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
using System.Xml.Linq;

namespace Wisej.AI.DocX.Packaging
{
	/// <summary>
	/// XML namespaces used by WordprocessingML documents and the DrawingML parts they reference.
	/// </summary>
	/// <remarks>
	/// Documents saved in the ISO/IEC 29500 <i>Strict</i> conformance class use different namespace
	/// URIs for the same vocabularies. <see cref="StrictToTransitional"/> maps them so that the rest of
	/// the library only needs to know the transitional names.
	/// </remarks>
	internal static class Ns
	{
		/// <summary>WordprocessingML main namespace (<c>w:</c>).</summary>
		public static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

		/// <summary>Office relationships namespace (<c>r:</c>).</summary>
		public static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

		/// <summary>WordprocessingML drawing namespace (<c>wp:</c>).</summary>
		public static readonly XNamespace WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";

		/// <summary>DrawingML main namespace (<c>a:</c>).</summary>
		public static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

		/// <summary>DrawingML picture namespace (<c>pic:</c>).</summary>
		public static readonly XNamespace Pic = "http://schemas.openxmlformats.org/drawingml/2006/picture";

		/// <summary>DrawingML chart namespace (<c>c:</c>).</summary>
		public static readonly XNamespace C = "http://schemas.openxmlformats.org/drawingml/2006/chart";

		/// <summary>DrawingML extended chart namespace used by Office 2016 chart types (<c>cx:</c>).</summary>
		public static readonly XNamespace CX = "http://schemas.microsoft.com/office/drawing/2014/chartex";

		/// <summary>DrawingML diagram (SmartArt) namespace (<c>dgm:</c>).</summary>
		public static readonly XNamespace Dgm = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

		/// <summary>Office Math Markup Language namespace (<c>m:</c>).</summary>
		public static readonly XNamespace M = "http://schemas.openxmlformats.org/officeDocument/2006/math";

		/// <summary>Markup compatibility namespace (<c>mc:</c>).</summary>
		public static readonly XNamespace MC = "http://schemas.openxmlformats.org/markup-compatibility/2006";

		/// <summary>VML namespace (<c>v:</c>).</summary>
		public static readonly XNamespace V = "urn:schemas-microsoft-com:vml";

		/// <summary>VML Office extensions namespace (<c>o:</c>).</summary>
		public static readonly XNamespace O = "urn:schemas-microsoft-com:office:office";

		/// <summary>Word 2010 drawing shape namespace (<c>wps:</c>).</summary>
		public static readonly XNamespace Wps = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

		/// <summary>Word 2010 drawing group namespace (<c>wpg:</c>).</summary>
		public static readonly XNamespace Wpg = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";

		/// <summary>Word 2010 drawing canvas namespace (<c>wpc:</c>).</summary>
		public static readonly XNamespace Wpc = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas";

		/// <summary>Word 2010 extensions namespace (<c>w14:</c>).</summary>
		public static readonly XNamespace W14 = "http://schemas.microsoft.com/office/word/2010/wordml";

		/// <summary>Decorative image extension namespace (<c>adec:</c>).</summary>
		public static readonly XNamespace ADec = "http://schemas.microsoft.com/office/drawing/2017/decorative";

		/// <summary>Package relationships namespace used by <c>.rels</c> parts.</summary>
		public static readonly XNamespace PackageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

		/// <summary>Extended (application) properties namespace used by <c>docProps/app.xml</c>.</summary>
		public static readonly XNamespace ExtendedProperties = "http://schemas.openxmlformats.org/officeDocument/2006/extended-properties";

		/// <summary>Custom properties namespace used by <c>docProps/custom.xml</c>.</summary>
		public static readonly XNamespace CustomProperties = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";

		/// <summary>Document property variant types namespace (<c>vt:</c>).</summary>
		public static readonly XNamespace VT = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";

		/// <summary><c>graphicData/@uri</c> of a picture.</summary>
		public const string GraphicPicture = "http://schemas.openxmlformats.org/drawingml/2006/picture";

		/// <summary><c>graphicData/@uri</c> of a chart.</summary>
		public const string GraphicChart = "http://schemas.openxmlformats.org/drawingml/2006/chart";

		/// <summary><c>graphicData/@uri</c> of an extended (Office 2016) chart.</summary>
		public const string GraphicChartEx = "http://schemas.microsoft.com/office/drawing/2014/chartex";

		/// <summary><c>graphicData/@uri</c> of a SmartArt diagram.</summary>
		public const string GraphicDiagram = "http://schemas.openxmlformats.org/drawingml/2006/diagram";

		/// <summary><c>graphicData/@uri</c> of a Word shape.</summary>
		public const string GraphicShape = "http://schemas.microsoft.com/office/word/2010/wordprocessingShape";

		/// <summary><c>graphicData/@uri</c> of a Word shape group.</summary>
		public const string GraphicGroup = "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup";

		/// <summary><c>graphicData/@uri</c> of a Word drawing canvas.</summary>
		public const string GraphicCanvas = "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas";

		/// <summary>
		/// Maps the namespaces of the Strict conformance class to their transitional equivalents.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, XNamespace> StrictToTransitional = new Dictionary<string, XNamespace>
		{
			["http://purl.oclc.org/ooxml/wordprocessingml/main"] = W,
			["http://purl.oclc.org/ooxml/officeDocument/relationships"] = R,
			["http://purl.oclc.org/ooxml/drawingml/wordprocessingDrawing"] = WP,
			["http://purl.oclc.org/ooxml/drawingml/main"] = A,
			["http://purl.oclc.org/ooxml/drawingml/picture"] = Pic,
			["http://purl.oclc.org/ooxml/drawingml/chart"] = C,
			["http://purl.oclc.org/ooxml/drawingml/diagram"] = Dgm,
			["http://purl.oclc.org/ooxml/officeDocument/math"] = M,
			["http://purl.oclc.org/ooxml/officeDocument/extendedProperties"] = ExtendedProperties,
			["http://purl.oclc.org/ooxml/officeDocument/customProperties"] = CustomProperties,
			["http://purl.oclc.org/ooxml/officeDocument/docPropsVTypes"] = VT,
		};
	}
}

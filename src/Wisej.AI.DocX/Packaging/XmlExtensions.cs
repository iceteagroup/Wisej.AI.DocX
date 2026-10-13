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
using System.Xml.Linq;

namespace Wisej.AI.DocX.Packaging
{
	/// <summary>
	/// Helpers that read the attribute and value conventions used by WordprocessingML.
	/// </summary>
	internal static class XmlExtensions
	{
		/// <summary>
		/// Namespaces of <c>mc:Choice</c> branches that the library cannot interpret
		/// (ink and 3D models): the <c>mc:Fallback</c> branch is used instead.
		/// </summary>
		private static readonly HashSet<string> UnsupportedChoiceNamespaces = new HashSet<string>(StringComparer.Ordinal)
		{
			"http://schemas.microsoft.com/office/drawing/2016/ink",
			"http://schemas.microsoft.com/office/drawing/2017/model3d",
		};

		#region Attributes

		/// <summary>
		/// Returns the value of the attribute with the specified name, or <c>null</c>.
		/// </summary>
		/// <param name="element">The element to read. Can be <c>null</c>.</param>
		/// <param name="name">The qualified attribute name.</param>
		/// <returns>The attribute value or <c>null</c> when the element or the attribute are missing.</returns>
		public static string Attr(this XElement element, XName name)
		{
			return element?.Attribute(name)?.Value;
		}

		/// <summary>
		/// Returns the value of the WordprocessingML attribute with the specified local name, or <c>null</c>.
		/// </summary>
		/// <param name="element">The element to read. Can be <c>null</c>.</param>
		/// <param name="localName">The local name of the attribute in the <c>w:</c> namespace.</param>
		/// <returns>The attribute value or <c>null</c>.</returns>
		public static string WAttr(this XElement element, string localName)
		{
			return element?.Attribute(Ns.W + localName)?.Value;
		}

		/// <summary>
		/// Returns the <c>w:val</c> attribute of the element, or <c>null</c>.
		/// </summary>
		/// <param name="element">The element to read. Can be <c>null</c>.</param>
		/// <returns>The value of <c>w:val</c> or <c>null</c>.</returns>
		public static string WVal(this XElement element)
		{
			return element?.Attribute(Ns.W + "val")?.Value;
		}

		/// <summary>
		/// Returns the <c>w:val</c> attribute of the child element with the specified local name, or <c>null</c>.
		/// </summary>
		/// <param name="element">The parent element. Can be <c>null</c>.</param>
		/// <param name="localName">The local name of the child element in the <c>w:</c> namespace.</param>
		/// <returns>The value of <c>w:val</c> or <c>null</c>.</returns>
		public static string WChildVal(this XElement element, string localName)
		{
			return element?.Element(Ns.W + localName)?.Attribute(Ns.W + "val")?.Value;
		}

		/// <summary>
		/// Reads an <c>ST_OnOff</c> toggle element such as <c>&lt;w:b/&gt;</c> or <c>&lt;w:b w:val="0"/&gt;</c>.
		/// </summary>
		/// <param name="element">The toggle element. Can be <c>null</c>.</param>
		/// <returns><c>null</c> when the element is missing, otherwise the toggle state.</returns>
		public static bool? OnOff(this XElement element)
		{
			if (element == null)
				return null;

			return ParseOnOff(element.Attribute(Ns.W + "val")?.Value, true);
		}

		/// <summary>
		/// Reads an <c>ST_OnOff</c> attribute value.
		/// </summary>
		/// <param name="value">The attribute value. Can be <c>null</c>.</param>
		/// <param name="defaultValue">Value returned when <paramref name="value"/> is <c>null</c> or empty.</param>
		/// <returns>The parsed toggle state.</returns>
		public static bool ParseOnOff(string value, bool defaultValue)
		{
			if (String.IsNullOrEmpty(value))
				return defaultValue;

			switch (value.Trim().ToLowerInvariant())
			{
				case "0":
				case "false":
				case "off":
				case "none":
					return false;

				default:
					return true;
			}
		}

		/// <summary>
		/// Parses an integer attribute value, tolerating decimals and surrounding white space.
		/// </summary>
		/// <param name="value">The attribute value. Can be <c>null</c>.</param>
		/// <returns>The parsed value or <c>null</c> when the value is missing or invalid.</returns>
		public static int? ParseInt(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
				return null;

			if (Int32.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
				return result;

			if (Double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
				return (int)Math.Round(real);

			return null;
		}

		/// <summary>
		/// Parses a floating point attribute value using the invariant culture.
		/// </summary>
		/// <param name="value">The attribute value. Can be <c>null</c>.</param>
		/// <returns>The parsed value or <c>null</c> when the value is missing or invalid.</returns>
		public static double? ParseDouble(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
				return null;

			if (Double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
				return result;

			return null;
		}

		#endregion

		#region Measurements

		/// <summary>
		/// Converts a twentieth-of-a-point measure (<c>ST_TwipsMeasure</c> or <c>ST_SignedTwipsMeasure</c>) to points.
		/// </summary>
		/// <param name="value">The attribute value. It may also carry a unit suffix (mm, cm, in, pt, pc, pi).</param>
		/// <returns>The measure in points, or <c>null</c> when the value is missing or invalid.</returns>
		public static double? TwipsToPoints(string value)
		{
			return ParseMeasure(value, 20.0);
		}

		/// <summary>
		/// Converts an English Metric Unit measure (<c>ST_Coordinate</c>) to points.
		/// </summary>
		/// <param name="value">The attribute value.</param>
		/// <returns>The measure in points, or <c>null</c> when the value is missing or invalid.</returns>
		public static double? EmuToPoints(string value)
		{
			return ParseMeasure(value, 12700.0);
		}

		/// <summary>
		/// Parses a measure expressed either as a number of <paramref name="unitsPerPoint"/> units
		/// or as a universal measure with an explicit unit suffix.
		/// </summary>
		/// <param name="value">The attribute value.</param>
		/// <param name="unitsPerPoint">How many raw units make one point (20 for twips, 2 for half-points, 12700 for EMUs).</param>
		/// <returns>The measure in points, or <c>null</c> when the value is missing or invalid.</returns>
		public static double? ParseMeasure(string value, double unitsPerPoint)
		{
			if (String.IsNullOrWhiteSpace(value))
				return null;

			value = value.Trim();

			if (value.Length > 2 && Char.IsLetter(value[value.Length - 1]))
			{
				var unit = value.Substring(value.Length - 2).ToLowerInvariant();
				var number = ParseDouble(value.Substring(0, value.Length - 2));
				if (number == null)
					return null;

				switch (unit)
				{
					case "pt": return number;
					case "in": return number * 72.0;
					case "cm": return number * 72.0 / 2.54;
					case "mm": return number * 72.0 / 25.4;
					case "pc":
					case "pi": return number * 12.0;
					default: return null;
				}
			}

			var raw = ParseDouble(value);
			return raw / unitsPerPoint;
		}

		/// <summary>
		/// Parses a VML CSS style attribute (<c>width:120pt;height:3in;position:absolute</c>) into a dictionary.
		/// </summary>
		/// <param name="style">The style attribute value. Can be <c>null</c>.</param>
		/// <returns>A case-insensitive dictionary of the declared properties.</returns>
		public static Dictionary<string, string> ParseCssStyle(string style)
		{
			var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			if (String.IsNullOrEmpty(style))
				return result;

			foreach (var declaration in style.Split(';'))
			{
				var colon = declaration.IndexOf(':');
				if (colon > 0)
					result[declaration.Substring(0, colon).Trim()] = declaration.Substring(colon + 1).Trim();
			}

			return result;
		}

		/// <summary>
		/// Converts a CSS length (<c>120pt</c>, <c>2in</c>, <c>3cm</c>, <c>96px</c>, <c>5mm</c>) to points.
		/// </summary>
		/// <param name="value">The CSS length. Can be <c>null</c>.</param>
		/// <returns>The length in points, or <c>null</c> when it cannot be parsed.</returns>
		public static double? CssLengthToPoints(string value)
		{
			if (String.IsNullOrWhiteSpace(value))
				return null;

			value = value.Trim().ToLowerInvariant();

			if (value.EndsWith("px"))
				return ParseDouble(value.Substring(0, value.Length - 2)) * 0.75;

			if (value.Length > 2 && Char.IsLetter(value[value.Length - 1]))
				return ParseMeasure(value, 1.0);

			// Unitless VML lengths are expressed in pixels.
			return ParseDouble(value) * 0.75;
		}

		#endregion

		#region Markup Compatibility

		/// <summary>
		/// Enumerates the child elements of <paramref name="element"/>, replacing every
		/// <c>mc:AlternateContent</c> with the content of the branch that should be processed.
		/// </summary>
		/// <param name="element">The parent element.</param>
		/// <returns>The child elements with markup compatibility resolved.</returns>
		/// <remarks>
		/// The first <c>mc:Choice</c> is preferred because it carries the richer, modern markup
		/// (DrawingML shapes, chart extensions, emoji symbols). The <c>mc:Fallback</c> branch is used
		/// only when the choice requires a vocabulary the library cannot interpret.
		/// </remarks>
		public static IEnumerable<XElement> ContentElements(this XElement element)
		{
			foreach (var child in element.Elements())
			{
				if (child.Name == Ns.MC + "AlternateContent")
				{
					var branch = SelectAlternateContentBranch(child);
					if (branch == null)
						continue;

					foreach (var resolved in branch.ContentElements())
						yield return resolved;
				}
				else
				{
					yield return child;
				}
			}
		}

		/// <summary>
		/// Picks the branch of an <c>mc:AlternateContent</c> element that should be processed.
		/// </summary>
		/// <param name="alternateContent">The <c>mc:AlternateContent</c> element.</param>
		/// <returns>The selected <c>mc:Choice</c> or <c>mc:Fallback</c> element, or <c>null</c>.</returns>
		private static XElement SelectAlternateContentBranch(XElement alternateContent)
		{
			foreach (var choice in alternateContent.Elements(Ns.MC + "Choice"))
			{
				if (IsChoiceSupported(choice))
					return choice;
			}

			return alternateContent.Element(Ns.MC + "Fallback");
		}

		/// <summary>
		/// Determines whether all the namespaces required by an <c>mc:Choice</c> can be interpreted.
		/// </summary>
		/// <param name="choice">The <c>mc:Choice</c> element.</param>
		/// <returns><c>true</c> when the choice can be processed.</returns>
		private static bool IsChoiceSupported(XElement choice)
		{
			var requires = choice.Attribute("Requires")?.Value;
			if (String.IsNullOrWhiteSpace(requires))
				return true;

			foreach (var prefix in requires.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
			{
				var ns = choice.GetNamespaceOfPrefix(prefix);
				if (ns == null || UnsupportedChoiceNamespaces.Contains(ns.NamespaceName))
					return false;
			}

			return true;
		}

		#endregion
	}
}

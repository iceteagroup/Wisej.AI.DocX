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
	/// Reads the text of SmartArt diagrams from their data model part.
	/// </summary>
	/// <remarks>
	/// The data model (<c>dgm:dataModel</c>) stores the diagram as a list of points and a list of
	/// parent-of connections. Rebuilding the tree preserves the hierarchy the diagram expresses
	/// (organization charts, processes, cycles), which a flat list of texts would lose.
	/// </remarks>
	internal static class SmartArtReader
	{
		/// <summary>
		/// Reads the tree of text nodes of a diagram.
		/// </summary>
		/// <param name="dataModel">The root element of the diagram data part.</param>
		/// <returns>The top level nodes; empty when the diagram has no text.</returns>
		public static List<SmartArtNode> Read(XElement dataModel)
		{
			var result = new List<SmartArtNode>();
			if (dataModel == null)
				return result;

			var points = new Dictionary<string, XElement>(StringComparer.Ordinal);
			string root = null;

			foreach (var pt in dataModel.Element(Ns.Dgm + "ptLst")?.Elements(Ns.Dgm + "pt") ?? Enumerable.Empty<XElement>())
			{
				var id = pt.Attribute("modelId")?.Value;
				if (id == null)
					continue;

				var type = pt.Attribute("type")?.Value ?? "node";
				if (type == "doc")
					root = id;

				// Only content points carry text; transitions and presentation points do not.
				if (type == "doc" || type == "node" || type == "asst")
					points[id] = pt;
			}

			var children = new Dictionary<string, List<(int Order, string Id)>>(StringComparer.Ordinal);
			foreach (var cxn in dataModel.Element(Ns.Dgm + "cxnLst")?.Elements(Ns.Dgm + "cxn") ?? Enumerable.Empty<XElement>())
			{
				var type = cxn.Attribute("type")?.Value ?? "parOf";
				if (type != "parOf")
					continue;

				var source = cxn.Attribute("srcId")?.Value;
				var destination = cxn.Attribute("destId")?.Value;
				if (source == null || destination == null || !points.ContainsKey(destination))
					continue;

				if (!children.TryGetValue(source, out var list))
					children[source] = list = new List<(int, string)>();

				list.Add((XmlExtensions.ParseInt(cxn.Attribute("srcOrd")?.Value) ?? list.Count, destination));
			}

			if (root != null)
			{
				var visited = new HashSet<string>(StringComparer.Ordinal) { root };
				foreach (var child in GetChildren(root, children))
					AddNode(child, points, children, result, visited, 0);
			}

			// Without a document point (or connections), fall back to the texts in document order.
			if (result.Count == 0)
			{
				foreach (var pt in points.Values)
				{
					var text = PointText(pt);
					if (text != null)
						result.Add(new SmartArtNode { Text = text });
				}
			}

			return result;
		}

		/// <summary>
		/// Adds the node of a point and, recursively, its descendants.
		/// </summary>
		/// <param name="id">The point identifier.</param>
		/// <param name="points">The content points by identifier.</param>
		/// <param name="children">The children of every point, with their order.</param>
		/// <param name="target">The list that receives the node.</param>
		/// <param name="visited">The points already added, which protects against cycles.</param>
		/// <param name="depth">The depth of the node in the tree.</param>
		private static void AddNode(
			string id,
			Dictionary<string, XElement> points,
			Dictionary<string, List<(int Order, string Id)>> children,
			List<SmartArtNode> target,
			HashSet<string> visited,
			int depth)
		{
			if (depth > ReadLimits.MaxNestingDepth || !visited.Add(id) || !points.TryGetValue(id, out var pt))
				return;

			var text = PointText(pt);
			var node = new SmartArtNode { Text = text ?? "" };

			foreach (var child in GetChildren(id, children))
				AddNode(child, points, children, node.Children, visited, depth + 1);

			// Points without text only group their children: promote them.
			if (text == null)
				target.AddRange(node.Children);
			else
				target.Add(node);
		}

		/// <summary>
		/// Returns the children of a point, in order.
		/// </summary>
		/// <param name="id">The point identifier.</param>
		/// <param name="children">The children of every point.</param>
		/// <returns>The identifiers of the children.</returns>
		private static IEnumerable<string> GetChildren(string id, Dictionary<string, List<(int Order, string Id)>> children)
		{
			return children.TryGetValue(id, out var list) ? list.OrderBy(c => c.Order).Select(c => c.Id) : Enumerable.Empty<string>();
		}

		/// <summary>
		/// Returns the text of a point, joining its paragraphs.
		/// </summary>
		/// <param name="pt">The point element.</param>
		/// <returns>The text, or <c>null</c> when the point has no text.</returns>
		private static string PointText(XElement pt)
		{
			var body = pt.Element(Ns.Dgm + "t");
			if (body == null)
				return null;

			var paragraphs = body.Elements(Ns.A + "p")
				.Select(p => String.Concat(p.Descendants(Ns.A + "t").Select(t => t.Value)).Trim())
				.Where(t => t.Length > 0);

			var text = String.Join(" ", paragraphs);
			return text.Length == 0 ? null : text;
		}
	}
}

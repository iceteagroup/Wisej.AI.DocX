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
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;

namespace Wisej.AI.DocX.Packaging
{
	/// <summary>
	/// Provides safe, cached access to the parts of a WordprocessingML package.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The package is opened with the Open XML SDK, which deals with the Open Packaging
	/// Conventions (relationships, content types, malformed URIs). The XML of every part is then
	/// loaded with LINQ to XML so that the readers can walk the markup directly, including elements
	/// that the SDK object model does not know about.
	/// </para>
	/// <para>
	/// XML is parsed with DTD processing disabled and no resolver, which protects against XML external
	/// entity attacks. Documents saved in the Strict conformance class are normalized to the
	/// transitional namespaces on load.
	/// </para>
	/// </remarks>
	internal sealed class DocxPackage : IDisposable
	{
		private readonly WordprocessingDocument _document;
		private readonly Dictionary<OpenXmlPart, XElement> _xmlCache = new Dictionary<OpenXmlPart, XElement>();

		/// <summary>
		/// Initializes a new instance of the <see cref="DocxPackage"/> class.
		/// </summary>
		/// <param name="document">The opened Open XML SDK document.</param>
		private DocxPackage(WordprocessingDocument document)
		{
			_document = document;
		}

		#region Properties

		/// <summary>
		/// Returns the main document part.
		/// </summary>
		public MainDocumentPart MainPart => _document.MainDocumentPart;

		// IPackageProperties is flagged as experimental by the Open XML SDK, but it is the only
		// access to the core properties that does not require System.IO.Packaging directly.
#pragma warning disable OOXML0001

		/// <summary>
		/// Returns the core properties of the package (title, author, dates).
		/// </summary>
		public IPackageProperties CoreProperties => _document.PackageProperties;

#pragma warning restore OOXML0001

		#endregion

		#region Opening

		/// <summary>
		/// Opens a DOCX, DOCM, DOTX or DOTM package, or a Flat OPC XML document.
		/// </summary>
		/// <param name="buffer">The document, buffered in memory by <see cref="Buffer"/>.</param>
		/// <param name="repair">Whether the package is repaired first; see <see cref="Repair"/>.</param>
		/// <returns>The opened package.</returns>
		/// <exception cref="InvalidDataException">Thrown when the buffer does not contain a Word document.</exception>
		public static DocxPackage Open(MemoryStream buffer, bool repair)
		{
			buffer.Position = 0;

			if (IsCompoundFile(buffer))
				throw new InvalidDataException("The document is encrypted or saved in the legacy Word 97-2003 binary format (.doc), which is not supported.");

			WordprocessingDocument document;

			try
			{
				if (IsFlatOpc(buffer))
				{
					var xml = Encoding.UTF8.GetString(buffer.ToArray());
					document = WordprocessingDocument.FromFlatOpcString(xml);
				}
				else
				{
					var source = repair ? Repair(buffer) : new MemoryStream(buffer.GetBuffer(), 0, (int)buffer.Length, false);
					document = WordprocessingDocument.Open(source, false, new OpenSettings { AutoSave = false });
				}
			}
			catch (Exception ex) when (!(ex is OutOfMemoryException))
			{
				throw new InvalidDataException("The stream does not contain a valid Word document.", ex);
			}

			if (document.MainDocumentPart == null)
			{
				document.Dispose();
				throw new InvalidDataException("The package does not contain a main document part.");
			}

			return new DocxPackage(document);
		}

		/// <summary>
		/// Copies a stream to memory, since the packaging API requires a seekable stream.
		/// </summary>
		/// <param name="stream">The source stream. It is read from its current position and is not closed.</param>
		/// <returns>The buffered content.</returns>
		public static MemoryStream Buffer(Stream stream)
		{
			var buffer = new MemoryStream();
			stream.CopyTo(buffer);
			buffer.Position = 0;
			return buffer;
		}

		/// <summary>
		/// Determines whether an exception raised while opening or reading a package may be solved by
		/// repairing the package.
		/// </summary>
		/// <param name="ex">The exception.</param>
		/// <returns><c>true</c> when the package should be repaired and read again.</returns>
		public static bool IsRepairable(Exception ex)
		{
			for (var e = ex; e != null; e = e.InnerException)
			{
				if (e is UriFormatException || e is InvalidOperationException || e is IOException || e is InvalidDataException || e is OpenXmlPackageException)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Returns a repaired copy of a package that Word opens but the packaging API rejects.
		/// </summary>
		/// <param name="buffer">The original package.</param>
		/// <returns>The repaired package, positioned at the start.</returns>
		/// <remarks>
		/// Three defects are repaired: ZIP entry names written with backslashes, relationships whose
		/// target URI is malformed (replaced with a placeholder URI) and internal relationships whose
		/// target part does not exist (removed).
		/// </remarks>
		private static MemoryStream Repair(MemoryStream buffer)
		{
			var repaired = new MemoryStream();
			buffer.Position = 0;

			using (var input = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true))
			using (var output = new ZipArchive(repaired, ZipArchiveMode.Create, leaveOpen: true))
			{
				var names = new HashSet<string>(input.Entries.Select(e => NormalizeEntryName(e.FullName)), StringComparer.OrdinalIgnoreCase);

				foreach (var entry in input.Entries)
				{
					var name = NormalizeEntryName(entry.FullName);
					if (name.Length == 0 || name.EndsWith("/", StringComparison.Ordinal))
						continue;

					using (var source = entry.Open())
					using (var target = output.CreateEntry(name).Open())
					{
						if (name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
							RepairRelationships(source, target, name, names);
						else
							source.CopyTo(target);
					}
				}
			}

			repaired.Position = 0;
			return repaired;
		}

		/// <summary>
		/// Copies a relationships part, fixing malformed targets and removing relationships to missing parts.
		/// </summary>
		/// <param name="source">The original relationships part.</param>
		/// <param name="target">The stream that receives the repaired part.</param>
		/// <param name="name">The entry name of the relationships part.</param>
		/// <param name="names">The entry names of all the parts of the package.</param>
		private static void RepairRelationships(Stream source, Stream target, string name, HashSet<string> names)
		{
			XDocument rels;
			try
			{
				using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
					rels = XDocument.Load(reader);
			}
			catch (XmlException)
			{
				return;
			}

			// "word/_rels/document.xml.rels" describes the relationships of "word/document.xml".
			var folder = name.Substring(0, name.LastIndexOf("_rels/", StringComparison.OrdinalIgnoreCase));
			var baseUri = new Uri("http://package/" + folder);

			foreach (var relationship in (rels.Root?.Elements(Ns.PackageRelationships + "Relationship") ?? Enumerable.Empty<XElement>()).ToList())
			{
				var targetUri = relationship.Attribute("Target")?.Value;
				if (targetUri == null)
					continue;

				if (relationship.Attribute("TargetMode")?.Value == "External")
				{
					if (!Uri.TryCreate(targetUri, UriKind.RelativeOrAbsolute, out _))
						relationship.SetAttributeValue("Target", "http://invalid.uri/");

					continue;
				}

				if (!Uri.TryCreate(baseUri, targetUri.Replace('\\', '/'), out var resolved)
					|| !names.Contains(Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/'))))
				{
					relationship.Remove();
				}
			}

			rels.Save(target);
		}

		/// <summary>
		/// Normalizes the name of a ZIP entry: forward slashes, no leading slash.
		/// </summary>
		/// <param name="name">The entry name.</param>
		/// <returns>The normalized name.</returns>
		private static string NormalizeEntryName(string name)
		{
			return name.Replace('\\', '/').TrimStart('/');
		}

		/// <summary>
		/// Determines whether the buffer contains an OLE compound file: an encrypted package or a binary .doc document.
		/// </summary>
		/// <param name="buffer">The buffered document.</param>
		/// <returns><c>true</c> when the buffer starts with the compound file signature.</returns>
		private static bool IsCompoundFile(MemoryStream buffer)
		{
			byte[] signature = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
			var bytes = buffer.GetBuffer();

			return buffer.Length >= signature.Length && signature.Select((b, i) => bytes[i] == b).All(match => match);
		}

		/// <summary>
		/// Determines whether the buffer contains a Flat OPC XML document instead of a ZIP package.
		/// </summary>
		/// <param name="buffer">The buffered document, positioned at the start.</param>
		/// <returns><c>true</c> for an XML document.</returns>
		private static bool IsFlatOpc(MemoryStream buffer)
		{
			var bytes = buffer.GetBuffer();
			var length = (int)Math.Min(buffer.Length, 64);

			for (var i = 0; i < length; i++)
			{
				var b = bytes[i];

				// Skip a UTF-8 byte order mark and leading white space.
				if (b == 0xEF || b == 0xBB || b == 0xBF || b == ' ' || b == '\r' || b == '\n' || b == '\t')
					continue;

				return b == '<';
			}

			return false;
		}

		/// <summary>
		/// Releases the underlying package.
		/// </summary>
		public void Dispose()
		{
			_document.Dispose();
		}

		#endregion

		#region Parts

		/// <summary>
		/// Returns the root element of the main document part (<c>w:document</c>).
		/// </summary>
		/// <returns>The document root element.</returns>
		public XElement GetMainDocument()
		{
			return GetXml(MainPart);
		}

		/// <summary>
		/// Returns the root element of the first part related to the main document part whose
		/// relationship type ends with <paramref name="relationshipSuffix"/>, or <c>null</c>.
		/// </summary>
		/// <param name="relationshipSuffix">The last segment of the relationship type, e.g. <c>"/styles"</c>.</param>
		/// <returns>The root element of the part or <c>null</c>.</returns>
		/// <remarks>
		/// Parts are looked up by the suffix of the relationship type so that the transitional
		/// and the Strict relationship URIs are both recognized.
		/// </remarks>
		public XElement GetMainRelatedXml(string relationshipSuffix)
		{
			var part = GetMainRelatedPart(relationshipSuffix);
			return part == null ? null : GetXml(part);
		}

		/// <summary>
		/// Returns the first part related to the main document part whose relationship type
		/// ends with <paramref name="relationshipSuffix"/>, or <c>null</c>.
		/// </summary>
		/// <param name="relationshipSuffix">The last segment of the relationship type, e.g. <c>"/footnotes"</c>.</param>
		/// <returns>The part or <c>null</c>.</returns>
		public OpenXmlPart GetMainRelatedPart(string relationshipSuffix)
		{
			foreach (var pair in MainPart.Parts)
			{
				if (pair.OpenXmlPart.RelationshipType.EndsWith(relationshipSuffix, StringComparison.OrdinalIgnoreCase))
					return pair.OpenXmlPart;
			}

			return null;
		}

		/// <summary>
		/// Returns the root element of the extended properties part (<c>docProps/app.xml</c>), or <c>null</c>.
		/// </summary>
		/// <returns>The root element of the part or <c>null</c>.</returns>
		public XElement GetExtendedProperties()
		{
			var part = _document.ExtendedFilePropertiesPart;
			return part == null ? null : GetXml(part);
		}

		/// <summary>
		/// Returns the root element of the custom properties part (<c>docProps/custom.xml</c>), or <c>null</c>.
		/// </summary>
		/// <returns>The root element of the part or <c>null</c>.</returns>
		public XElement GetCustomProperties()
		{
			var part = _document.CustomFilePropertiesPart;
			return part == null ? null : GetXml(part);
		}

		/// <summary>
		/// Returns the part referenced by <paramref name="relationshipId"/> from <paramref name="source"/>, or <c>null</c>.
		/// </summary>
		/// <param name="source">The part that owns the relationship.</param>
		/// <param name="relationshipId">The relationship identifier (<c>r:id</c>, <c>r:embed</c>).</param>
		/// <returns>The target part or <c>null</c> when it does not exist.</returns>
		public static OpenXmlPart GetRelatedPart(OpenXmlPart source, string relationshipId)
		{
			if (source == null || String.IsNullOrEmpty(relationshipId))
				return null;

			foreach (var pair in source.Parts)
			{
				if (pair.RelationshipId == relationshipId)
					return pair.OpenXmlPart;
			}

			return null;
		}

		/// <summary>
		/// Returns the target of an external relationship (hyperlinks, linked images), or <c>null</c>.
		/// </summary>
		/// <param name="source">The part that owns the relationship.</param>
		/// <param name="relationshipId">The relationship identifier.</param>
		/// <returns>The target URI as written in the package, or <c>null</c>.</returns>
		public static string GetExternalTarget(OpenXmlPart source, string relationshipId)
		{
			if (source == null || String.IsNullOrEmpty(relationshipId))
				return null;

			var hyperlink = source.HyperlinkRelationships.FirstOrDefault(r => r.Id == relationshipId);
			if (hyperlink != null)
				return hyperlink.Uri?.OriginalString;

			var external = source.ExternalRelationships.FirstOrDefault(r => r.Id == relationshipId);
			return external?.Uri?.OriginalString;
		}

		/// <summary>
		/// Returns the root element of the XML content of <paramref name="part"/>, loading and caching it on first use.
		/// </summary>
		/// <param name="part">The part to load.</param>
		/// <returns>The root element, or <c>null</c> when the part does not contain well-formed XML.</returns>
		public XElement GetXml(OpenXmlPart part)
		{
			if (part == null)
				return null;

			if (!_xmlCache.TryGetValue(part, out var root))
			{
				root = LoadXml(part);
				_xmlCache[part] = root;
			}

			return root;
		}

		/// <summary>
		/// Reads the binary content of <paramref name="part"/>.
		/// </summary>
		/// <param name="part">The part to read.</param>
		/// <returns>The bytes of the part.</returns>
		public static byte[] GetBytes(OpenXmlPart part)
		{
			using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
			using (var buffer = new MemoryStream())
			{
				stream.CopyTo(buffer);
				return buffer.ToArray();
			}
		}

		#endregion

		#region XML Loading

		/// <summary>
		/// Loads the XML of a part securely and normalizes Strict namespaces.
		/// </summary>
		/// <param name="part">The part to load.</param>
		/// <returns>The root element, or <c>null</c> when the XML is not well formed.</returns>
		private static XElement LoadXml(OpenXmlPart part)
		{
			var settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit,
				XmlResolver = null,
				IgnoreComments = true,
				IgnoreProcessingInstructions = true,
				CheckCharacters = false,
			};

			try
			{
				using (var stream = part.GetStream(FileMode.Open, FileAccess.Read))
				using (var reader = XmlReader.Create(stream, settings))
				{
					var root = XDocument.Load(reader, LoadOptions.PreserveWhitespace).Root;
					if (root != null && Ns.StrictToTransitional.ContainsKey(root.Name.NamespaceName))
						NormalizeStrictNamespaces(root);

					return root;
				}
			}
			catch (XmlException)
			{
				return null;
			}
		}

		/// <summary>
		/// Rewrites the element and attribute names of a Strict document to the transitional namespaces.
		/// </summary>
		/// <param name="root">The root element to normalize in place.</param>
		private static void NormalizeStrictNamespaces(XElement root)
		{
			foreach (var element in root.DescendantsAndSelf())
			{
				if (Ns.StrictToTransitional.TryGetValue(element.Name.NamespaceName, out var ns))
					element.Name = ns + element.Name.LocalName;

				if (!element.HasAttributes)
					continue;

				var attributes = element.Attributes().ToList();
				if (!attributes.Any(a => Ns.StrictToTransitional.ContainsKey(a.Name.NamespaceName)))
					continue;

				element.RemoveAttributes();
				foreach (var attribute in attributes)
				{
					if (attribute.IsNamespaceDeclaration)
						continue;

					var name = Ns.StrictToTransitional.TryGetValue(attribute.Name.NamespaceName, out var attributeNs)
						? attributeNs + attribute.Name.LocalName
						: attribute.Name;

					element.SetAttributeValue(name, attribute.Value);
				}
			}
		}

		#endregion
	}
}

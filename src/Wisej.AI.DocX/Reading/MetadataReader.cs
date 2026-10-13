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
using System.Xml.Linq;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Reads the core, extended and custom properties of a package.
	/// </summary>
	internal static class MetadataReader
	{
		/// <summary>
		/// Reads the document properties.
		/// </summary>
		/// <param name="package">The package.</param>
		/// <returns>The document metadata; properties that cannot be read are left empty.</returns>
		public static DocxMetadata Read(DocxPackage package)
		{
			var metadata = new DocxMetadata();

			ReadCoreProperties(package, metadata);
			ReadExtendedProperties(package.GetExtendedProperties(), metadata);
			ReadCustomProperties(package.GetCustomProperties(), metadata);

			return metadata;
		}

		/// <summary>
		/// Reads the core properties (<c>docProps/core.xml</c>).
		/// </summary>
		/// <param name="package">The package.</param>
		/// <param name="metadata">The metadata to fill.</param>
		private static void ReadCoreProperties(DocxPackage package, DocxMetadata metadata)
		{
			try
			{
				var core = package.CoreProperties;

				metadata.Title = Clean(core.Title);
				metadata.Subject = Clean(core.Subject);
				metadata.Author = Clean(core.Creator);
				metadata.Keywords = Clean(core.Keywords);
				metadata.Description = Clean(core.Description);
				metadata.Category = Clean(core.Category);
				metadata.LastModifiedBy = Clean(core.LastModifiedBy);
				metadata.Revision = Clean(core.Revision);
				metadata.Language = Clean(core.Language);
				metadata.Created = core.Created;
				metadata.Modified = core.Modified;
			}
			catch (Exception ex) when (ex is FormatException || ex is System.Xml.XmlException || ex is InvalidOperationException)
			{
				// Malformed core properties (an invalid date, for example) must not prevent the extraction.
			}
		}

		/// <summary>
		/// Reads the extended properties (<c>docProps/app.xml</c>).
		/// </summary>
		/// <param name="root">The root element of the part. Can be <c>null</c>.</param>
		/// <param name="metadata">The metadata to fill.</param>
		private static void ReadExtendedProperties(XElement root, DocxMetadata metadata)
		{
			if (root == null)
				return;

			string Value(string name) => Clean(root.Element(Ns.ExtendedProperties + name)?.Value);

			metadata.Company = Value("Company");
			metadata.Manager = Value("Manager");
			metadata.Application = Value("Application");
			metadata.Template = Value("Template");
			metadata.ReportedPageCount = XmlExtensions.ParseInt(Value("Pages"));
			metadata.ReportedWordCount = XmlExtensions.ParseInt(Value("Words"));
		}

		/// <summary>
		/// Reads the custom properties (<c>docProps/custom.xml</c>).
		/// </summary>
		/// <param name="root">The root element of the part. Can be <c>null</c>.</param>
		/// <param name="metadata">The metadata to fill.</param>
		private static void ReadCustomProperties(XElement root, DocxMetadata metadata)
		{
			if (root == null)
				return;

			foreach (var property in root.Elements(Ns.CustomProperties + "property"))
			{
				var name = property.Attribute("name")?.Value;
				var value = Clean(property.Value);

				if (!String.IsNullOrEmpty(name) && value != null)
					metadata.CustomProperties[name] = value;
			}
		}

		/// <summary>
		/// Trims a property value, returning <c>null</c> for empty values.
		/// </summary>
		/// <param name="value">The value. Can be <c>null</c>.</param>
		/// <returns>The trimmed value or <c>null</c>.</returns>
		private static string Clean(string value)
		{
			return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
		}
	}
}

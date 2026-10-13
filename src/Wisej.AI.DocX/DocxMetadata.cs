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

namespace Wisej.AI.DocX
{
	/// <summary>
	/// The properties of a Word document: core properties, application statistics and custom properties.
	/// </summary>
	public sealed class DocxMetadata
	{
		/// <summary>Gets the document title.</summary>
		public string Title { get; internal set; }

		/// <summary>Gets the document subject.</summary>
		public string Subject { get; internal set; }

		/// <summary>Gets the author of the document.</summary>
		public string Author { get; internal set; }

		/// <summary>Gets the keywords of the document.</summary>
		public string Keywords { get; internal set; }

		/// <summary>Gets the description (comments) of the document.</summary>
		public string Description { get; internal set; }

		/// <summary>Gets the category of the document.</summary>
		public string Category { get; internal set; }

		/// <summary>Gets the user who last modified the document.</summary>
		public string LastModifiedBy { get; internal set; }

		/// <summary>Gets the revision number of the document.</summary>
		public string Revision { get; internal set; }

		/// <summary>Gets the language of the document.</summary>
		public string Language { get; internal set; }

		/// <summary>Gets the creation date of the document.</summary>
		public DateTime? Created { get; internal set; }

		/// <summary>Gets the date the document was last modified.</summary>
		public DateTime? Modified { get; internal set; }

		/// <summary>Gets the company recorded in the application properties.</summary>
		public string Company { get; internal set; }

		/// <summary>Gets the manager recorded in the application properties.</summary>
		public string Manager { get; internal set; }

		/// <summary>Gets the name of the application that last saved the document.</summary>
		public string Application { get; internal set; }

		/// <summary>Gets the template the document is based on.</summary>
		public string Template { get; internal set; }

		/// <summary>
		/// Gets the number of pages recorded by the application that last saved the document
		/// (<c>docProps/app.xml</c>), or <c>null</c>. It can be stale or missing.
		/// </summary>
		public int? ReportedPageCount { get; internal set; }

		/// <summary>Gets the number of words recorded by the application that last saved the document, or <c>null</c>.</summary>
		public int? ReportedWordCount { get; internal set; }

		/// <summary>Gets the custom properties of the document.</summary>
		public IDictionary<string, string> CustomProperties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
	}
}

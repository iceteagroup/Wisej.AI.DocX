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

namespace Wisej.AI.DocX
{
	/// <summary>
	/// Controls how tables are written in Markdown.
	/// </summary>
	public enum TableFormat
	{
		/// <summary>
		/// Pipe tables for regular tables, HTML tables when a table contains nested tables or block
		/// content that a pipe table cannot represent.
		/// </summary>
		Auto,

		/// <summary>Always pipe tables. Nested tables are flattened into their cells.</summary>
		Markdown,

		/// <summary>Always HTML tables, with <c>colspan</c> and <c>rowspan</c> for merged cells.</summary>
		Html,
	}
}

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
	/// Controls how tracked changes (revisions) are reflected in the extracted text.
	/// </summary>
	public enum TrackedChangesMode
	{
		/// <summary>The text is extracted as if all the changes were accepted.</summary>
		Accept,

		/// <summary>The text is extracted as if all the changes were rejected.</summary>
		Reject,

		/// <summary>
		/// Both the inserted and the deleted text are extracted and marked with CriticMarkup:
		/// <c>{++inserted++}</c> and <c>{--deleted--}</c>.
		/// </summary>
		Markup,
	}
}

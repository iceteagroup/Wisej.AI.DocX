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

using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// Where inline content is being written, which determines how line breaks and blocks are expressed.
	/// </summary>
	internal enum InlineMode
	{
		/// <summary>A regular paragraph: line breaks are kept.</summary>
		Block,

		/// <summary>A table cell: everything stays on one line.</summary>
		Cell,

		/// <summary>A single line summary, used for headers, footers, notes and comments.</summary>
		Flat,
	}

	/// <summary>
	/// A piece of inline output with uniform formatting.
	/// </summary>
	internal sealed class Span
	{
		/// <summary>Gets or sets the text, unescaped unless <see cref="IsMarkup"/> is set.</summary>
		public string Text { get; set; }

		/// <summary>Gets or sets whether the text is already formatted output (an image reference, an equation) that must be written as is.</summary>
		public bool IsMarkup { get; set; }

		/// <summary>Gets or sets whether the text is bold.</summary>
		public bool Bold { get; set; }

		/// <summary>Gets or sets whether the text is italic.</summary>
		public bool Italic { get; set; }

		/// <summary>Gets or sets whether the text is struck through.</summary>
		public bool Strike { get; set; }

		/// <summary>Gets or sets whether the text is superscript.</summary>
		public bool Superscript { get; set; }

		/// <summary>Gets or sets whether the text is subscript.</summary>
		public bool Subscript { get; set; }

		/// <summary>Gets or sets whether the text is set in a code font.</summary>
		public bool Code { get; set; }

		/// <summary>Gets or sets the hyperlink target, or <c>null</c>.</summary>
		public string Link { get; set; }

		/// <summary>Gets or sets the tracked change state.</summary>
		public RevisionKind Revision { get; set; }

		/// <summary>
		/// Determines whether two spans have the same formatting and can be merged.
		/// </summary>
		/// <param name="other">The other span.</param>
		/// <returns><c>true</c> when the spans can be merged.</returns>
		public bool CanMergeWith(Span other)
		{
			return !IsMarkup && !other.IsMarkup
				&& Bold == other.Bold
				&& Italic == other.Italic
				&& Strike == other.Strike
				&& Superscript == other.Superscript
				&& Subscript == other.Subscript
				&& Code == other.Code
				&& Link == other.Link
				&& Revision == other.Revision;
		}
	}
}

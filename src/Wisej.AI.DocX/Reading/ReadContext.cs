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

using DocumentFormat.OpenXml.Packaging;
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// The context in which a piece of content is read: the story it belongs to and the
	/// formatting inherited from the containers around it.
	/// </summary>
	/// <remarks>
	/// Contexts are immutable: entering a container (a table, a hyperlink, a tracked change)
	/// creates a derived context with the <c>With*</c> methods.
	/// </remarks>
	internal sealed class ReadContext
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ReadContext"/> class for a story.
		/// </summary>
		/// <param name="part">The part that contains the story; it resolves the relationships.</param>
		/// <param name="location">The location reported for images: <c>body</c>, <c>header</c>, <c>footer</c>, ...</param>
		public ReadContext(OpenXmlPart part, string location)
		{
			Part = part;
			Location = location;
		}

		#region Properties

		/// <summary>Gets the part that contains the story.</summary>
		public OpenXmlPart Part { get; }

		/// <summary>Gets the location of the story: <c>body</c>, <c>header</c>, <c>footer</c>, <c>footnote</c>, <c>endnote</c> or <c>comment</c>.</summary>
		public string Location { get; }

		/// <summary>Gets whether the story is the main document body.</summary>
		public bool IsMainStory => Location == "body";

		/// <summary>Gets the style of the innermost table containing the content, or <c>null</c>.</summary>
		public string TableStyleId { get; private set; }

		/// <summary>Gets the nesting depth of tables around the content.</summary>
		public int TableDepth { get; private set; }

		/// <summary>Gets whether the content is inside a text box.</summary>
		public bool InTextBox { get; private set; }

		/// <summary>Gets the hyperlink target inherited from an enclosing <c>w:hyperlink</c>, or <c>null</c>.</summary>
		public string Hyperlink { get; private set; }

		/// <summary>Gets the tracked change state inherited from an enclosing <c>w:ins</c> or <c>w:del</c>.</summary>
		public RevisionKind Revision { get; private set; }

		/// <summary>Gets whether the content is the placeholder text of an empty content control.</summary>
		public bool IsPlaceholder { get; private set; }

		#endregion

		#region Methods

		/// <summary>
		/// Returns a context for the content of a table cell.
		/// </summary>
		/// <param name="tableStyleId">The style of the table. Can be <c>null</c>.</param>
		/// <returns>The derived context.</returns>
		public ReadContext WithTable(string tableStyleId)
		{
			var clone = Clone();
			clone.TableStyleId = tableStyleId;
			clone.TableDepth++;
			return clone;
		}

		/// <summary>
		/// Returns a context for the content of a text box, which does not inherit the table style.
		/// </summary>
		/// <returns>The derived context.</returns>
		public ReadContext WithTextBox()
		{
			var clone = Clone();
			clone.TableStyleId = null;
			clone.InTextBox = true;
			clone.Hyperlink = null;
			return clone;
		}

		/// <summary>
		/// Returns a context for the content of a hyperlink.
		/// </summary>
		/// <param name="target">The hyperlink target.</param>
		/// <returns>The derived context.</returns>
		public ReadContext WithHyperlink(string target)
		{
			var clone = Clone();
			clone.Hyperlink = target;
			return clone;
		}

		/// <summary>
		/// Returns a context for the placeholder text of an empty content control.
		/// </summary>
		/// <returns>The derived context.</returns>
		public ReadContext WithPlaceholder()
		{
			var clone = Clone();
			clone.IsPlaceholder = true;
			return clone;
		}

		/// <summary>
		/// Returns a context for the content of a tracked change.
		/// </summary>
		/// <param name="revision">The kind of change.</param>
		/// <returns>The derived context.</returns>
		public ReadContext WithRevision(RevisionKind revision)
		{
			var clone = Clone();
			clone.Revision = revision;
			return clone;
		}

		/// <summary>
		/// Creates a shallow copy of this context.
		/// </summary>
		/// <returns>The copy.</returns>
		private ReadContext Clone()
		{
			return (ReadContext)MemberwiseClone();
		}

		#endregion
	}
}

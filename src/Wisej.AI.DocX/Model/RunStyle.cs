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

namespace Wisej.AI.DocX.Model
{
	/// <summary>
	/// Fully resolved character formatting of a run.
	/// </summary>
	/// <remarks>
	/// The values are the result of the style cascade: document defaults, table style,
	/// paragraph style, character style and direct formatting. Instances are treated as
	/// immutable once created by the style resolver and are shared between runs.
	/// </remarks>
	internal sealed class RunStyle
	{
		/// <summary>
		/// The formatting used when nothing else is known: 11pt Calibri.
		/// </summary>
		public static readonly RunStyle Default = new RunStyle { FontFamily = "Calibri", FontSize = 11 };

		#region Emphasis

		/// <summary>Gets or sets whether the text is bold.</summary>
		public bool Bold { get; set; }

		/// <summary>Gets or sets whether the text is italic.</summary>
		public bool Italic { get; set; }

		/// <summary>Gets or sets whether the text is underlined.</summary>
		public bool Underline { get; set; }

		/// <summary>Gets or sets whether the text is struck through (single or double).</summary>
		public bool Strike { get; set; }

		/// <summary>Gets or sets whether the text is raised as superscript.</summary>
		public bool Superscript { get; set; }

		/// <summary>Gets or sets whether the text is lowered as subscript.</summary>
		public bool Subscript { get; set; }

		/// <summary>Gets or sets whether the text is hidden (<c>w:vanish</c>).</summary>
		public bool Hidden { get; set; }

		/// <summary>Gets or sets whether the text is displayed in capital letters.</summary>
		public bool Caps { get; set; }

		/// <summary>Gets or sets whether the text is displayed in small capital letters.</summary>
		public bool SmallCaps { get; set; }

		#endregion

		#region Font

		/// <summary>Gets or sets the font family used for Latin text.</summary>
		public string FontFamily { get; set; }

		/// <summary>Gets or sets the font family used for East Asian text.</summary>
		public string EastAsiaFontFamily { get; set; }

		/// <summary>Gets or sets the font size in points.</summary>
		public double FontSize { get; set; }

		/// <summary>Gets or sets whether the font is a monospaced (code) font.</summary>
		public bool Monospace { get; set; }

		#endregion

		/// <summary>
		/// Determines whether this style renders with the same Markdown emphasis as <paramref name="other"/>.
		/// </summary>
		/// <param name="other">The style to compare with. Can be <c>null</c>.</param>
		/// <returns><c>true</c> when both styles produce identical emphasis markers.</returns>
		public bool HasSameEmphasis(RunStyle other)
		{
			if (other == null)
				return false;

			if (ReferenceEquals(this, other))
				return true;

			return Bold == other.Bold
				&& Italic == other.Italic
				&& Strike == other.Strike
				&& Superscript == other.Superscript
				&& Subscript == other.Subscript
				&& Monospace == other.Monospace;
		}

		/// <summary>
		/// Creates a shallow copy of this style.
		/// </summary>
		/// <returns>A new <see cref="RunStyle"/> with the same values.</returns>
		public RunStyle Clone()
		{
			return (RunStyle)MemberwiseClone();
		}

		/// <summary>
		/// Returns a compact description of the style, useful while debugging.
		/// </summary>
		/// <returns>A string such as <c>Calibri 11pt bold italic</c>.</returns>
		public override string ToString()
		{
			return String.Concat(
				FontFamily, " ", FontSize, "pt",
				Bold ? " bold" : "",
				Italic ? " italic" : "",
				Strike ? " strike" : "",
				Hidden ? " hidden" : "",
				Monospace ? " mono" : "");
		}
	}
}

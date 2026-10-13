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

using System.Collections.Generic;

namespace Wisej.AI.DocX
{
	/// <summary>
	/// A page of an extracted document.
	/// </summary>
	public sealed class DocxPage
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DocxPage"/> class.
		/// </summary>
		/// <param name="number">The one based physical page number.</param>
		/// <param name="text">The extracted text of the page.</param>
		/// <param name="images">The images displayed on the page.</param>
		internal DocxPage(int number, string text, IReadOnlyList<DocxImage> images)
		{
			Number = number;
			Text = text;
			Images = images;
		}

		/// <summary>
		/// Gets the one based physical page number.
		/// </summary>
		public int Number { get; }

		/// <summary>
		/// Gets the extracted text of the page, without the page header line.
		/// </summary>
		public string Text { get; }

		/// <summary>
		/// Gets the images displayed on the page.
		/// </summary>
		public IReadOnlyList<DocxImage> Images { get; }

		/// <summary>
		/// Returns the page number and the length of its text.
		/// </summary>
		/// <returns>A string describing the page.</returns>
		public override string ToString()
		{
			return $"Page {Number} ({Text.Length} characters)";
		}
	}
}

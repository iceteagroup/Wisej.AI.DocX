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
using System.Text;

namespace Wisej.AI.DocX.Rendering
{
	/// <summary>
	/// Kinds of output blocks, which determine how consecutive blocks are separated.
	/// </summary>
	internal enum OutputBlockKind
	{
		/// <summary>A paragraph, separated from the previous block by a blank line.</summary>
		Paragraph,

		/// <summary>A list item; consecutive items are separated by a single line break.</summary>
		ListItem,

		/// <summary>A line of code; consecutive lines form a single fenced code block.</summary>
		Code,

		/// <summary>A table row; consecutive rows are separated by a single line break.</summary>
		TableRow,
	}

	/// <summary>
	/// Accumulates the text of the pages and separates the blocks written to them.
	/// </summary>
	internal sealed class PageWriter
	{
		private readonly List<PageBuffer> _pages = new List<PageBuffer>();
		private readonly string _codeFence;
		private OutputBlockKind? _lastKind;

		/// <summary>
		/// Initializes a new instance of the <see cref="PageWriter"/> class.
		/// </summary>
		/// <param name="codeFence">The fence that opens and closes code blocks, or <c>null</c> to write code as plain lines.</param>
		public PageWriter(string codeFence)
		{
			_codeFence = codeFence;
			_pages.Add(new PageBuffer(1));
		}

		#region Properties

		/// <summary>Gets the page being written.</summary>
		public PageBuffer Current => _pages[_pages.Count - 1];

		/// <summary>Gets all the pages written so far.</summary>
		public IReadOnlyList<PageBuffer> Pages => _pages;

		#endregion

		#region Methods

		/// <summary>
		/// Appends a block of text to the current page.
		/// </summary>
		/// <param name="text">The text of the block. Empty blocks are ignored.</param>
		/// <param name="kind">The kind of block.</param>
		public void AppendBlock(string text, OutputBlockKind kind)
		{
			if (String.IsNullOrEmpty(text))
				return;

			var sb = Current.Text;

			if (kind != OutputBlockKind.Code)
				CloseCodeBlock();

			if (sb.Length > 0)
			{
				var sameKind = _lastKind == kind && (kind == OutputBlockKind.ListItem || kind == OutputBlockKind.TableRow || kind == OutputBlockKind.Code);
				sb.Append(sameKind ? "\n" : "\n\n");
			}

			if (kind == OutputBlockKind.Code && _lastKind != OutputBlockKind.Code && _codeFence != null)
				sb.Append(_codeFence).Append('\n');

			sb.Append(text);
			_lastKind = kind;
		}

		/// <summary>
		/// Ends the current block group, so that the next block is separated by a blank line.
		/// </summary>
		public void EndGroup()
		{
			CloseCodeBlock();
			_lastKind = null;
		}

		/// <summary>
		/// Starts a new page. Pages skipped by the page number (blank pages) are created empty.
		/// </summary>
		/// <param name="number">The number of the new page.</param>
		public void StartPage(int number)
		{
			EndGroup();

			while (Current.Number < number - 1)
				_pages.Add(new PageBuffer(Current.Number + 1));

			_pages.Add(new PageBuffer(Math.Max(number, Current.Number + 1)));
		}

		/// <summary>
		/// Closes an open code block.
		/// </summary>
		private void CloseCodeBlock()
		{
			if (_lastKind == OutputBlockKind.Code && _codeFence != null)
			{
				Current.Text.Append('\n').Append(_codeFence);
				_lastKind = OutputBlockKind.Paragraph;
			}
		}

		#endregion
	}

	/// <summary>
	/// The text and the images of a page being written.
	/// </summary>
	internal sealed class PageBuffer
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="PageBuffer"/> class.
		/// </summary>
		/// <param name="number">The page number.</param>
		public PageBuffer(int number)
		{
			Number = number;
		}

		/// <summary>Gets the page number.</summary>
		public int Number { get; }

		/// <summary>Gets the text of the page.</summary>
		public StringBuilder Text { get; } = new StringBuilder();

		/// <summary>Gets the images displayed on the page.</summary>
		public List<DocxImage> Images { get; } = new List<DocxImage>();
	}
}

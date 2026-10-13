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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Tracks the state of complex fields (<c>w:fldChar</c>) while the content is read.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A complex field is spread over several runs, and often over several paragraphs:
	/// <c>begin</c>, the instruction text, <c>separate</c>, the cached result, <c>end</c>.
	/// Fields can be nested both in the instruction and in the result. The tracker keeps a stack
	/// of the open fields so that the reader knows, for every piece of content, whether it is
	/// part of an instruction (never displayed), part of a result (displayed) and whether it
	/// belongs to a hyperlink or to a table of contents.
	/// </para>
	/// <para>
	/// The cached results are used as they are, which is how Word displays the document, except for
	/// the fields that depend on the layout (<c>PAGE</c>, <c>NUMPAGES</c>, <c>SECTIONPAGES</c>)
	/// that are computed again when the pages are known.
	/// </para>
	/// </remarks>
	internal sealed class FieldTracker
	{
		/// <summary>Matches the quoted or unquoted target of a <c>HYPERLINK</c> instruction.</summary>
		private static readonly Regex HyperlinkTarget = new Regex(@"^\s*HYPERLINK\s+(?:""(?<url>[^""]*)""|(?<url>[^\s\\]+))?(?<switches>.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

		/// <summary>Matches the <c>\l</c> (location) switch of a <c>HYPERLINK</c> instruction.</summary>
		private static readonly Regex HyperlinkLocation = new Regex(@"\\l\s+(?:""(?<anchor>[^""]*)""|(?<anchor>\S+))", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private readonly List<FieldFrame> _stack = new List<FieldFrame>();

		#region Properties

		/// <summary>
		/// Gets whether the content being read must be suppressed, because it is part of a field
		/// instruction or of a result that is replaced by a computed value.
		/// </summary>
		public bool IsSuppressed => _stack.Any(f => !f.HasSeparator || f.SuppressResult);

		/// <summary>
		/// Gets whether the fields that enclose the innermost one suppress their content.
		/// </summary>
		public bool IsSuppressedByOuterFields => _stack.Take(_stack.Count - 1).Any(f => !f.HasSeparator || f.SuppressResult);

		/// <summary>
		/// Gets the hyperlink target of the innermost hyperlink field whose result is being read, or <c>null</c>.
		/// </summary>
		public string Hyperlink
		{
			get
			{
				for (var i = _stack.Count - 1; i >= 0; i--)
				{
					if (_stack[i].Hyperlink != null && _stack[i].HasSeparator)
						return _stack[i].Hyperlink;
				}

				return null;
			}
		}

		/// <summary>
		/// Gets whether the content being read is part of the result of a table of contents field.
		/// </summary>
		public bool IsInTableOfContents => _stack.Any(f => f.HasSeparator && f.Type == "TOC");

		#endregion

		#region Methods

		/// <summary>
		/// Opens a field (<c>w:fldChar w:fldCharType="begin"</c>).
		/// </summary>
		/// <param name="checkBoxState">The state of a legacy form check box defined in <c>w:ffData</c>, or <c>null</c>.</param>
		public void Begin(bool? checkBoxState)
		{
			_stack.Add(new FieldFrame { CheckBoxState = checkBoxState });
		}

		/// <summary>
		/// Appends instruction text (<c>w:instrText</c>) to the innermost open field.
		/// </summary>
		/// <param name="text">The instruction text.</param>
		public void AddInstruction(string text)
		{
			var frame = _stack.LastOrDefault();
			if (frame != null && !frame.HasSeparator)
				frame.Instruction.Append(text);
		}

		/// <summary>
		/// Switches the innermost field from its instruction to its result (<c>w:fldCharType="separate"</c>).
		/// </summary>
		/// <param name="style">The formatting of the run that contains the separator.</param>
		/// <returns>The inline to emit in place of the cached result, or <c>null</c> to read the result normally.</returns>
		public Inline Separate(RunStyle style)
		{
			var frame = _stack.LastOrDefault();
			if (frame == null || frame.HasSeparator)
				return null;

			// The instruction of a field nested in the instruction of another field is not processed.
			var isNestedInInstruction = _stack.Take(_stack.Count - 1).Any(f => !f.HasSeparator);

			frame.HasSeparator = true;
			Interpret(frame);

			if (isNestedInInstruction)
				return null;

			return CreateComputedInline(frame, style, suppressResult: true);
		}

		/// <summary>
		/// Closes the innermost field (<c>w:fldCharType="end"</c>).
		/// </summary>
		/// <param name="style">The formatting of the run that contains the end marker.</param>
		/// <returns>The inline to emit for a field that has no cached result, or <c>null</c>.</returns>
		public Inline End(RunStyle style)
		{
			if (_stack.Count == 0)
				return null;

			var frame = _stack[_stack.Count - 1];
			_stack.RemoveAt(_stack.Count - 1);

			var isNestedInInstruction = _stack.Any(f => !f.HasSeparator);
			if (frame.HasSeparator || isNestedInInstruction)
				return null;

			// A field without a result: only the layout fields and the check boxes produce content.
			Interpret(frame);
			return CreateComputedInline(frame, style, suppressResult: false);
		}

		/// <summary>
		/// Interprets the instruction of a simple field (<c>w:fldSimple</c>).
		/// </summary>
		/// <param name="instruction">The field instruction.</param>
		/// <param name="style">The formatting of the field.</param>
		/// <param name="hyperlink">Returns the hyperlink target of a <c>HYPERLINK</c> field, or <c>null</c>.</param>
		/// <returns>The inline that replaces the cached result, or <c>null</c> to read the result normally.</returns>
		public static Inline InterpretSimpleField(string instruction, RunStyle style, out string hyperlink)
		{
			var frame = new FieldFrame();
			frame.Instruction.Append(instruction);
			frame.HasSeparator = true;
			Interpret(frame);

			hyperlink = frame.Hyperlink;
			return CreateComputedInline(frame, style, suppressResult: false);
		}

		/// <summary>
		/// Returns the type of a field instruction: its first word in upper case.
		/// </summary>
		/// <param name="instruction">The field instruction.</param>
		/// <returns>The field type, such as <c>HYPERLINK</c> or <c>PAGE</c>.</returns>
		public static string GetFieldType(string instruction)
		{
			if (String.IsNullOrWhiteSpace(instruction))
				return "";

			var trimmed = instruction.TrimStart();
			var end = 0;
			while (end < trimmed.Length && !Char.IsWhiteSpace(trimmed[end]) && trimmed[end] != '\\')
				end++;

			return trimmed.Substring(0, end).ToUpperInvariant();
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Parses the instruction of a field.
		/// </summary>
		/// <param name="frame">The field to interpret.</param>
		private static void Interpret(FieldFrame frame)
		{
			var instruction = frame.Instruction.ToString();
			frame.Type = GetFieldType(instruction);

			if (frame.Type == "HYPERLINK")
			{
				var match = HyperlinkTarget.Match(instruction);
				if (match.Success)
				{
					var url = match.Groups["url"].Success ? match.Groups["url"].Value : null;
					var anchor = HyperlinkLocation.Match(match.Groups["switches"].Value);

					if (!String.IsNullOrEmpty(url))
						frame.Hyperlink = anchor.Success ? url + "#" + anchor.Groups["anchor"].Value : url;
					else if (anchor.Success)
						frame.Hyperlink = "#" + anchor.Groups["anchor"].Value;
				}
			}
		}

		/// <summary>
		/// Creates the inline that represents a field with a computed value.
		/// </summary>
		/// <param name="frame">The interpreted field.</param>
		/// <param name="style">The formatting of the field.</param>
		/// <param name="suppressResult">Whether the cached result must be suppressed when a computed inline is returned.</param>
		/// <returns>The computed inline, or <c>null</c> when the field result is used as is.</returns>
		private static Inline CreateComputedInline(FieldFrame frame, RunStyle style, bool suppressResult)
		{
			switch (frame.Type)
			{
				case "PAGE":
					frame.SuppressResult = suppressResult;
					return new FieldInline { Kind = FieldKind.Page, Style = style };

				case "NUMPAGES":
					frame.SuppressResult = suppressResult;
					return new FieldInline { Kind = FieldKind.NumPages, Style = style };

				case "SECTIONPAGES":
					frame.SuppressResult = suppressResult;
					return new FieldInline { Kind = FieldKind.SectionPages, Style = style };

				case "FORMCHECKBOX":
					if (frame.CheckBoxState == null)
						return null;

					frame.SuppressResult = suppressResult;
					return new TextInline { Text = frame.CheckBoxState.Value ? "☒" : "☐", Style = style };

				default:
					return null;
			}
		}

		#endregion

		#region FieldFrame

		/// <summary>
		/// The state of an open field.
		/// </summary>
		private sealed class FieldFrame
		{
			/// <summary>Gets the instruction text collected so far.</summary>
			public StringBuilder Instruction { get; } = new StringBuilder();

			/// <summary>Gets or sets whether the separator was reached and the result is being read.</summary>
			public bool HasSeparator { get; set; }

			/// <summary>Gets or sets whether the cached result is replaced by a computed inline.</summary>
			public bool SuppressResult { get; set; }

			/// <summary>Gets or sets the field type, available after the separator.</summary>
			public string Type { get; set; }

			/// <summary>Gets or sets the hyperlink target of a <c>HYPERLINK</c> field.</summary>
			public string Hyperlink { get; set; }

			/// <summary>Gets or sets the state of a legacy form check box.</summary>
			public bool? CheckBoxState { get; set; }
		}

		#endregion
	}
}

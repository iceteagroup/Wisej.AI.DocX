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
using Wisej.AI.DocX.Model;

namespace Wisej.AI.DocX.Layout
{
	/// <summary>
	/// Breaks paragraphs into lines the way Word does: greedy line filling at word boundaries,
	/// with the real advance widths of the fonts, tab stops, inline drawings and line spacing rules.
	/// </summary>
	internal sealed class TextMeasurer
	{
		/// <summary>Scale of superscript and subscript text relative to the font size.</summary>
		private const double ScriptScale = 0.65;

		/// <summary>Descent added below inline drawings, as a fraction of the font size.</summary>
		private const double DrawingDescent = 0.22;

		private readonly FontCatalog _fonts;
		private readonly double _defaultTabStop;
		private readonly List<Atom> _atoms = new List<Atom>();

		/// <summary>
		/// Initializes a new instance of the <see cref="TextMeasurer"/> class.
		/// </summary>
		/// <param name="fonts">The font catalog.</param>
		/// <param name="defaultTabStop">The default tab stop interval, in points.</param>
		public TextMeasurer(FontCatalog fonts, double defaultTabStop)
		{
			_fonts = fonts;
			_defaultTabStop = defaultTabStop > 1 ? defaultTabStop : 36;
		}

		#region Measure

		/// <summary>
		/// Breaks a paragraph into lines.
		/// </summary>
		/// <param name="paragraph">The paragraph to measure.</param>
		/// <param name="width">The width of the column or cell, in points.</param>
		/// <param name="gridPitch">The pitch of the document grid lines, or 0.</param>
		/// <param name="footnoteHeight">Returns the height of a referenced footnote; <c>null</c> to ignore footnotes.</param>
		/// <returns>The measured paragraph.</returns>
		public ParagraphLayout Measure(ParagraphBlock paragraph, double width, double gridPitch, Func<NoteReferenceInline, double> footnoteHeight)
		{
			var properties = paragraph.Properties;
			var layout = new ParagraphLayout
			{
				Paragraph = paragraph,
				SpaceBefore = properties.SpacingBefore,
				SpaceAfter = properties.SpacingAfter,
				ExtraHeight = properties.BorderHeight,
			};

			BuildAtoms(paragraph, footnoteHeight, layout);

			var textWidth = Math.Max(18, width - properties.IndentLeft - properties.IndentRight);
			var firstLineStart = GetFirstLineStart(paragraph);
			var markHeight = NaturalHeight(properties.MarkStyle);

			var line = new LineState(firstLineStart, new ContentPosition(paragraph, 0, 0));
			var floatingHeight = 0.0;
			ContentPosition? carriedMarker = null;

			for (var i = 0; i < _atoms.Count; i++)
			{
				var atom = _atoms[i];
				var available = textWidth;

				if (carriedMarker != null && !line.HasContent)
				{
					line.RenderedBreak = carriedMarker;
					carriedMarker = null;
				}

				switch (atom.Kind)
				{
					case AtomKind.Space:
						line.X += atom.Width;
						break;

					case AtomKind.Word:
					case AtomKind.Drawing:
						if (line.X + atom.Width > available + 0.01 && line.HasContent)
						{
							FinishLine(layout, ref line, markHeight, properties, gridPitch);
							line = new LineState(0, atom.Position);
						}

						if (!line.HasContent)
							line.Start = atom.Position;

						// A word longer than the line wraps by characters over several lines.
						if (atom.Width > available && atom.Kind == AtomKind.Word)
						{
							var extraLines = (int)Math.Ceiling(atom.Width / available) - 1;
							line.TextHeight = Math.Max(line.TextHeight, atom.Height);
							for (var k = 0; k < extraLines; k++)
							{
								line.HasContent = true;
								FinishLine(layout, ref line, markHeight, properties, gridPitch);
								line = new LineState(0, atom.Position) { TextHeight = atom.Height };
							}

							line.X = atom.Width - extraLines * available;
						}
						else
						{
							line.X += atom.Width;
						}

						line.HasContent = true;
						if (atom.Kind == AtomKind.Drawing)
							line.DrawingHeight = Math.Max(line.DrawingHeight, atom.Height);
						else
							line.TextHeight = Math.Max(line.TextHeight, atom.Height);

						line.FootnoteHeight += atom.FootnoteHeight;
						break;

					case AtomKind.Tab:
						var target = NextTabPosition(properties, line.X, out var alignment);
						if (alignment != TabStopKind.Left)
							target = Math.Max(line.X, target - (alignment == TabStopKind.Center ? SegmentWidth(i + 1) / 2 : SegmentWidth(i + 1)));

						if (target > available + 0.01 && line.HasContent)
						{
							FinishLine(layout, ref line, markHeight, properties, gridPitch);
							line = new LineState(0, atom.Position);
							target = NextTabPosition(properties, 0, out _);
						}

						if (!line.HasContent)
							line.Start = atom.Position;

						line.X = target;
						line.HasContent = true;
						line.TextHeight = Math.Max(line.TextHeight, atom.Height);
						break;

					case AtomKind.Break:
						line.TextHeight = Math.Max(line.TextHeight, atom.Height);
						line.HasContent = true;
						line.EndsWithPageBreak = atom.Break == BreakKind.Page;
						line.EndsWithColumnBreak = atom.Break == BreakKind.Column;

						FinishLine(layout, ref line, markHeight, properties, gridPitch);
						line = new LineState(0, NextPosition(paragraph, i));
						break;

					case AtomKind.Floating:
						floatingHeight += atom.Height;
						break;

					case AtomKind.Marker:
						// In Word's layout the marker is at a line start: attach it to the line where it falls,
						// or to the next line when it falls late in this one.
						if (!line.HasContent || line.X < textWidth / 2)
							line.RenderedBreak = line.RenderedBreak ?? atom.Position;
						else
							carriedMarker = atom.Position;
						break;
				}
			}

			// The last line holds the paragraph mark: it is laid out even when empty, unless a page
			// break ended the previous line (Word keeps that mark on the line of the break).
			var endsAfterBreak = layout.Lines.Count > 0 && (layout.Lines[layout.Lines.Count - 1].EndsWithPageBreak || layout.Lines[layout.Lines.Count - 1].EndsWithColumnBreak);
			if (line.HasContent || !endsAfterBreak)
				FinishLine(layout, ref line, markHeight, properties, gridPitch);

			// Floating drawings that push the text down add the part of their height not covered by the lines.
			if (floatingHeight > 0)
				layout.ExtraHeight += Math.Max(0, floatingHeight - LinesHeight(layout));

			return layout;
		}

		/// <summary>
		/// Returns the natural height of a single line set in the specified style.
		/// </summary>
		/// <param name="style">The text style.</param>
		/// <returns>The line height, in points.</returns>
		public double NaturalHeight(RunStyle style)
		{
			return _fonts.Get(style.FontFamily, style.Bold).LineHeight * style.FontSize;
		}

		/// <summary>
		/// Measures the width of a string set in the specified style.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="style">The text style.</param>
		/// <returns>The width, in points.</returns>
		public double TextWidth(string text, RunStyle style)
		{
			var metrics = _fonts.Get(style.FontFamily, style.Bold);
			var width = 0.0;

			foreach (var c in text)
				width += metrics.GetAdvance(c);

			return width * style.FontSize;
		}

		#endregion

		#region Atoms

		/// <summary>
		/// Splits the content of a paragraph into the atoms of the line breaking algorithm.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="footnoteHeight">Returns the height of a referenced footnote. Can be <c>null</c>.</param>
		/// <param name="layout">The layout, which receives the height of drawings that push the text.</param>
		private void BuildAtoms(ParagraphBlock paragraph, Func<NoteReferenceInline, double> footnoteHeight, ParagraphLayout layout)
		{
			_atoms.Clear();

			var inlines = paragraph.Inlines;
			for (var index = 0; index < inlines.Count; index++)
			{
				var inline = inlines[index];

				// Word's page break markers are kept as zero width atoms, used as layout anchors.
				if (inline is RenderedPageBreakInline)
				{
					_atoms.Add(new Atom(AtomKind.Marker, 0, 0, new ContentPosition(paragraph, index, 0)));
					continue;
				}

				if (!inline.IsVisible)
					continue;

				var position = new ContentPosition(paragraph, index, 0);
				var style = inline.Style;

				switch (inline)
				{
					case TextInline text:
						AddTextAtoms(paragraph, index, text.Text, style);
						break;

					case TabInline _:
						_atoms.Add(new Atom(AtomKind.Tab, 0, NaturalHeight(style), position));
						break;

					case BreakInline br:
						_atoms.Add(new Atom(AtomKind.Break, 0, NaturalHeight(style), position) { Break = br.Kind });
						break;

					case FieldInline field:
						AddTextAtoms(paragraph, index, field.CachedText ?? "1", style);
						break;

					case NoteReferenceInline note:
						_atoms.Add(new Atom(AtomKind.Word, TextWidth(note.Label ?? "1", style) * ScriptScale, NaturalHeight(style), position)
						{
							FootnoteHeight = note.Kind == NoteKind.Footnote && footnoteHeight != null ? footnoteHeight(note) : 0,
						});
						break;

					case MathInline math:
						AddMathAtoms(math, style, position);
						break;

					case DrawingInline drawing:
						AddDrawingAtom(drawing, style, position, layout);
						break;
				}
			}
		}

		/// <summary>
		/// Adds the atoms of a run of text: words, spaces and East Asian characters.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="index">The index of the inline.</param>
		/// <param name="text">The text.</param>
		/// <param name="style">The text style.</param>
		private void AddTextAtoms(ParagraphBlock paragraph, int index, string text, RunStyle style)
		{
			if (String.IsNullOrEmpty(text))
				return;

			var metrics = _fonts.Get(style.FontFamily, style.Bold);
			var wideMetrics = style.EastAsiaFontFamily != null ? _fonts.Get(style.EastAsiaFontFamily, style.Bold) : null;
			var size = style.FontSize * (style.Superscript || style.Subscript ? ScriptScale : 1.0);
			var height = metrics.LineHeight * style.FontSize;
			var upper = style.Caps || style.SmallCaps;

			var wordStart = -1;
			var wordWidth = 0.0;

			for (var i = 0; i < text.Length; i++)
			{
				var c = upper ? Char.ToUpperInvariant(text[i]) : text[i];

				if (c == ' ' || c == '　')
				{
					FlushWord(paragraph, index, ref wordStart, ref wordWidth, height);

					var spaceMetrics = c == '　' && wideMetrics != null ? wideMetrics : metrics;
					_atoms.Add(new Atom(AtomKind.Space, spaceMetrics.GetAdvance(c) * size, height, new ContentPosition(paragraph, index, i)));
					continue;
				}

				if (FontMetrics.IsWide(c))
				{
					// East Asian text can break between any two characters.
					FlushWord(paragraph, index, ref wordStart, ref wordWidth, height);

					var wide = (wideMetrics ?? metrics).GetAdvance(c) * size;
					var wideHeight = Math.Max(height, (wideMetrics?.LineHeight ?? 0) * style.FontSize);
					_atoms.Add(new Atom(AtomKind.Word, wide, wideHeight, new ContentPosition(paragraph, index, i)));
					continue;
				}

				if (wordStart < 0)
					wordStart = i;

				wordWidth += metrics.GetAdvance(c) * size;

				// A line can break after a hyphen or a dash.
				if (c == '-' || c == '–' || c == '—' || c == '/')
					FlushWord(paragraph, index, ref wordStart, ref wordWidth, height);
			}

			FlushWord(paragraph, index, ref wordStart, ref wordWidth, height);
		}

		/// <summary>
		/// Adds the word being accumulated, if any.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="index">The index of the inline.</param>
		/// <param name="wordStart">The offset of the word, or -1; reset to -1.</param>
		/// <param name="wordWidth">The width of the word; reset to 0.</param>
		/// <param name="height">The line height of the text.</param>
		private void FlushWord(ParagraphBlock paragraph, int index, ref int wordStart, ref double wordWidth, double height)
		{
			if (wordStart < 0)
				return;

			// Words split across runs ("bo" + "ld") are joined: no break opportunity between them.
			var last = _atoms.Count > 0 ? _atoms[_atoms.Count - 1] : default;
			if (wordStart == 0 && last.Kind == AtomKind.Word && last.Joinable)
			{
				last.Width += wordWidth;
				last.Height = Math.Max(last.Height, height);
				_atoms[_atoms.Count - 1] = last;
			}
			else
			{
				_atoms.Add(new Atom(AtomKind.Word, wordWidth, height, new ContentPosition(paragraph, index, wordStart)) { Joinable = true });
			}

			wordStart = -1;
			wordWidth = 0;
		}

		/// <summary>
		/// Adds the atoms of an equation.
		/// </summary>
		/// <param name="math">The equation.</param>
		/// <param name="style">The text style.</param>
		/// <param name="position">The position of the equation.</param>
		private void AddMathAtoms(MathInline math, RunStyle style, ContentPosition position)
		{
			var latex = math.Latex ?? "";
			var height = NaturalHeight(style);

			// Built-up fractions, sums and matrices make the line taller.
			var tall = latex.Contains(@"\frac") || latex.Contains(@"\sum") || latex.Contains(@"\int") || latex.Contains(@"\begin");
			var lineHeight = height * (tall ? 2.0 : 1.0);

			// The rendered width is roughly a third of the LaTeX source length in em.
			var width = Math.Max(1, latex.Length) * style.FontSize * 0.33;

			if (math.IsDisplay)
			{
				_atoms.Add(new Atom(AtomKind.Break, 0, height, position) { Break = BreakKind.Line });
				_atoms.Add(new Atom(AtomKind.Word, width, lineHeight, position));
				_atoms.Add(new Atom(AtomKind.Break, 0, height, position) { Break = BreakKind.Line });
			}
			else
			{
				_atoms.Add(new Atom(AtomKind.Word, width, lineHeight, position));
			}
		}

		/// <summary>
		/// Adds the atom of a drawing, or its height to the paragraph when it floats.
		/// </summary>
		/// <param name="drawing">The drawing.</param>
		/// <param name="style">The text style around the drawing.</param>
		/// <param name="position">The position of the drawing.</param>
		/// <param name="layout">The layout, which receives the height of drawings placed above and below the text.</param>
		private void AddDrawingAtom(DrawingInline drawing, RunStyle style, ContentPosition position, ParagraphLayout layout)
		{
			switch (drawing.Wrap)
			{
				case WrapKind.Inline:
					_atoms.Add(new Atom(AtomKind.Drawing, drawing.Width, drawing.Height + style.FontSize * DrawingDescent, position));
					break;

				case WrapKind.TopAndBottom:
					layout.ExtraHeight += drawing.Height;
					break;

				case WrapKind.Around:
					_atoms.Add(new Atom(AtomKind.Floating, 0, drawing.Height, position));
					break;
			}
		}

		#endregion

		#region Lines

		/// <summary>
		/// Completes the current line and adds it to the layout.
		/// </summary>
		/// <param name="layout">The paragraph layout.</param>
		/// <param name="line">The state of the current line.</param>
		/// <param name="markHeight">The line height of the paragraph mark, used for empty lines.</param>
		/// <param name="properties">The paragraph properties.</param>
		/// <param name="gridPitch">The pitch of the document grid lines, or 0.</param>
		private static void FinishLine(ParagraphLayout layout, ref LineState line, double markHeight, ParagraphProperties properties, double gridPitch)
		{
			var text = line.TextHeight > 0 ? line.TextHeight : markHeight;
			var natural = Math.Max(text, line.DrawingHeight);
			double height;

			switch (properties.LineSpacingRule)
			{
				case LineSpacingRule.Exact:
					height = properties.LineSpacing;
					break;

				case LineSpacingRule.AtLeast:
					height = Math.Max(properties.LineSpacing, natural);
					break;

				default:
					height = natural * (properties.LineSpacing > 0 ? properties.LineSpacing : 1.0);

					if (gridPitch > 0 && properties.SnapToGrid)
						height = Math.Ceiling(height / gridPitch - 0.05) * gridPitch;
					break;
			}

			layout.Lines.Add(new LineBox
			{
				Height = height,
				Start = line.Start,
				EndsWithPageBreak = line.EndsWithPageBreak,
				EndsWithColumnBreak = line.EndsWithColumnBreak,
				FootnoteHeight = line.FootnoteHeight,
				RenderedBreak = line.RenderedBreak,
			});
		}

		/// <summary>
		/// Returns the position where the first line text starts, relative to the leading indent.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <returns>The start offset of the first line, in points.</returns>
		private double GetFirstLineStart(ParagraphBlock paragraph)
		{
			var properties = paragraph.Properties;
			var start = properties.IndentFirstLine;

			if (paragraph.List == null)
				return start;

			// The label is followed by a tab to the next tab stop, normally the hanging indent.
			var labelWidth = TextWidth(paragraph.List.Text + " ", properties.MarkStyle);
			var labelEnd = start + labelWidth;

			return labelEnd <= 0 ? 0 : NextTabPosition(properties, labelEnd, out _);
		}

		/// <summary>
		/// Returns the position of the next tab stop after <paramref name="x"/>.
		/// </summary>
		/// <param name="properties">The paragraph properties.</param>
		/// <param name="x">The current position, relative to the leading indent.</param>
		/// <param name="alignment">Returns the alignment of the tab stop.</param>
		/// <returns>The tab stop position, relative to the leading indent.</returns>
		private double NextTabPosition(ParagraphProperties properties, double x, out TabStopKind alignment)
		{
			alignment = TabStopKind.Left;
			var absolute = properties.IndentLeft + x;

			if (properties.Tabs != null)
			{
				foreach (var tab in properties.Tabs)
				{
					if (tab.Position > absolute + 0.5)
					{
						alignment = tab.Kind == TabStopKind.Decimal ? TabStopKind.Right : tab.Kind;
						return tab.Position - properties.IndentLeft;
					}
				}
			}

			// A hanging indent acts as an implicit tab stop.
			if (properties.IndentFirstLine < 0 && x < 0)
				return 0;

			var next = (Math.Floor(absolute / _defaultTabStop + 0.001) + 1) * _defaultTabStop;
			return next - properties.IndentLeft;
		}

		/// <summary>
		/// Returns the width of the atoms that follow a tab, up to the next tab or break.
		/// </summary>
		/// <param name="start">The index of the first atom after the tab.</param>
		/// <returns>The width, in points.</returns>
		private double SegmentWidth(int start)
		{
			var width = 0.0;
			for (var i = start; i < _atoms.Count; i++)
			{
				var atom = _atoms[i];
				if (atom.Kind == AtomKind.Tab || atom.Kind == AtomKind.Break)
					break;

				width += atom.Width;
			}

			return width;
		}

		/// <summary>
		/// Returns the position of the content that follows an atom.
		/// </summary>
		/// <param name="paragraph">The paragraph.</param>
		/// <param name="atomIndex">The index of the atom.</param>
		/// <returns>The position of the next atom, or the end of the paragraph.</returns>
		private ContentPosition NextPosition(ParagraphBlock paragraph, int atomIndex)
		{
			return atomIndex + 1 < _atoms.Count
				? _atoms[atomIndex + 1].Position
				: new ContentPosition(paragraph, paragraph.Inlines.Count, 0);
		}

		/// <summary>
		/// Returns the total height of the lines of a layout.
		/// </summary>
		/// <param name="layout">The layout.</param>
		/// <returns>The height, in points.</returns>
		private static double LinesHeight(ParagraphLayout layout)
		{
			var height = 0.0;
			foreach (var line in layout.Lines)
				height += line.Height;

			return height;
		}

		#endregion

		#region Atom

		/// <summary>
		/// Kinds of atoms of the line breaking algorithm.
		/// </summary>
		private enum AtomKind
		{
			/// <summary>An unbreakable piece of text.</summary>
			Word,

			/// <summary>A space, where a line can break.</summary>
			Space,

			/// <summary>A tab character.</summary>
			Tab,

			/// <summary>An explicit break.</summary>
			Break,

			/// <summary>An inline drawing.</summary>
			Drawing,

			/// <summary>A floating drawing around which the text wraps.</summary>
			Floating,

			/// <summary>A page break marker saved by Word.</summary>
			Marker,
		}

		/// <summary>
		/// An atom of the line breaking algorithm.
		/// </summary>
		private struct Atom
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="Atom"/> structure.
			/// </summary>
			/// <param name="kind">The kind of atom.</param>
			/// <param name="width">The width, in points.</param>
			/// <param name="height">The line height required by the atom, in points.</param>
			/// <param name="position">The position of the atom in the paragraph.</param>
			public Atom(AtomKind kind, double width, double height, ContentPosition position)
			{
				Kind = kind;
				Width = width;
				Height = height;
				Position = position;
				Break = BreakKind.Line;
				FootnoteHeight = 0;
				Joinable = false;
			}

			/// <summary>Gets the kind of atom.</summary>
			public AtomKind Kind { get; }

			/// <summary>Gets or sets the width, in points.</summary>
			public double Width { get; set; }

			/// <summary>Gets or sets the line height required by the atom, in points.</summary>
			public double Height { get; set; }

			/// <summary>Gets the position of the atom in the paragraph.</summary>
			public ContentPosition Position { get; }

			/// <summary>Gets or sets the kind of break, for break atoms.</summary>
			public BreakKind Break { get; set; }

			/// <summary>Gets or sets the height of the footnote referenced by the atom.</summary>
			public double FootnoteHeight { get; set; }

			/// <summary>Gets or sets whether a word that starts the next run continues this word.</summary>
			public bool Joinable { get; set; }
		}

		/// <summary>
		/// The state of the line being filled.
		/// </summary>
		private struct LineState
		{
			/// <summary>
			/// Initializes a new instance of the <see cref="LineState"/> structure.
			/// </summary>
			/// <param name="x">The starting horizontal position, relative to the leading indent.</param>
			/// <param name="start">The position of the first content of the line.</param>
			public LineState(double x, ContentPosition start)
			{
				X = x;
				Start = start;
				TextHeight = 0;
				DrawingHeight = 0;
				FootnoteHeight = 0;
				HasContent = false;
				EndsWithPageBreak = false;
				EndsWithColumnBreak = false;
				RenderedBreak = null;
			}

			/// <summary>Gets or sets the current horizontal position.</summary>
			public double X { get; set; }

			/// <summary>Gets or sets the position of the first content of the line.</summary>
			public ContentPosition Start { get; set; }

			/// <summary>Gets or sets the tallest text on the line.</summary>
			public double TextHeight { get; set; }

			/// <summary>Gets or sets the tallest inline drawing on the line.</summary>
			public double DrawingHeight { get; set; }

			/// <summary>Gets or sets the height of the footnotes referenced on the line.</summary>
			public double FootnoteHeight { get; set; }

			/// <summary>Gets or sets whether the line has content.</summary>
			public bool HasContent { get; set; }

			/// <summary>Gets or sets whether the line ends with a page break.</summary>
			public bool EndsWithPageBreak { get; set; }

			/// <summary>Gets or sets whether the line ends with a column break.</summary>
			public bool EndsWithColumnBreak { get; set; }

			/// <summary>Gets or sets the position of a page break marker saved by Word at the start of the line.</summary>
			public ContentPosition? RenderedBreak { get; set; }
		}

		#endregion
	}
}

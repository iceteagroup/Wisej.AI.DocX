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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Reads a WordprocessingML package into the format independent <see cref="DocxModel"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The reader walks the markup of every story (body, headers, footers, notes, comments),
	/// resolves the formatting through the <see cref="StyleSheet"/>, generates the list labels
	/// with <see cref="NumberingDefinitions"/>, evaluates fields with the <see cref="FieldTracker"/>
	/// and reads drawings with the <see cref="DrawingReader"/>.
	/// </para>
	/// <para>
	/// Tracked changes and hidden text are resolved while reading according to the options, so
	/// that the model only contains the content that is displayed.
	/// </para>
	/// </remarks>
	internal sealed class DocumentReader
	{
		private readonly DocxPackage _package;
		private readonly DocxExtractorOptions _options;
		private readonly DocxModel _model = new DocxModel();
		private readonly Dictionary<string, HeaderFooter> _headerFooters = new Dictionary<string, HeaderFooter>(StringComparer.Ordinal);
		private readonly Dictionary<string, StringBuilder> _openComments = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
		private readonly Dictionary<string, string> _commentAnchors = new Dictionary<string, string>(StringComparer.Ordinal);

		private StyleSheet _styles;
		private NumberingDefinitions _numbering;
		private DrawingReader _drawings;
		private FieldTracker _fields = new FieldTracker();
		private string _defaultTableStyleId;
		private string _footnoteFormat = "decimal";
		private string _endnoteFormat = "lowerRoman";
		private int _footnoteCount;
		private int _endnoteCount;
		private int _depth;

		/// <summary>
		/// Initializes a new instance of the <see cref="DocumentReader"/> class.
		/// </summary>
		/// <param name="package">The opened package.</param>
		/// <param name="options">The extraction options.</param>
		public DocumentReader(DocxPackage package, DocxExtractorOptions options)
		{
			_package = package;
			_options = options;
		}

		#region Read

		/// <summary>
		/// Reads the whole document.
		/// </summary>
		/// <returns>The document model.</returns>
		public DocxModel Read()
		{
			var settings = _package.GetMainRelatedXml("/settings");
			var theme = ThemeFonts.Load(_package.GetMainRelatedXml("/theme"), settings);
			var stylesXml = _package.GetMainRelatedXml("/styles");

			_styles = new StyleSheet(stylesXml, theme);
			_numbering = new NumberingDefinitions(_package.GetMainRelatedXml("/numbering"), _styles);
			_defaultTableStyleId = stylesXml?.Elements(Ns.W + "style")
				.FirstOrDefault(s => s.WAttr("type") == "table" && XmlExtensions.ParseOnOff(s.WAttr("default"), false))
				?.WAttr("styleId");

			var images = new ImageRegistry(_model.Images, _options.IncludeImageData || _options.ImageDescriber != null);
			_drawings = new DrawingReader(_package, _options, images, ReadStoryBlocks);

			ReadSettings(settings);
			_model.Metadata = MetadataReader.Read(_package);

			ReadBody();
			ReadNotes("/footnotes", "footnote", NoteKind.Footnote, _model.Footnotes);
			ReadNotes("/endnotes", "endnote", NoteKind.Endnote, _model.Endnotes);

			if (_options.IncludeComments)
				ReadComments();

			return _model;
		}

		/// <summary>
		/// Reads the document settings that affect the output and the layout.
		/// </summary>
		/// <param name="settings">The root element of the settings part. Can be <c>null</c>.</param>
		private void ReadSettings(XElement settings)
		{
			if (settings == null)
				return;

			_model.DefaultTabStop = XmlExtensions.TwipsToPoints(settings.WChildVal("defaultTabStop")) ?? 36;
			_model.EvenAndOddHeaders = settings.Element(Ns.W + "evenAndOddHeaders").OnOff() ?? false;
			_model.UpdateFieldsOnOpen = settings.Element(Ns.W + "updateFields").OnOff() ?? false;

			_footnoteFormat = settings.Element(Ns.W + "footnotePr")?.WChildVal("numFmt") ?? _footnoteFormat;
			_endnoteFormat = settings.Element(Ns.W + "endnotePr")?.WChildVal("numFmt") ?? _endnoteFormat;
		}

		/// <summary>
		/// Reads the main story and splits it into sections.
		/// </summary>
		private void ReadBody()
		{
			var body = _package.GetMainDocument()?.Element(Ns.W + "body");
			if (body == null)
			{
				_model.Sections.Add(new Section());
				return;
			}

			var context = new ReadContext(_package.MainPart, "body");

			// The blocks of the section being read. A paragraph that carries w:sectPr ends the
			// section: its blocks move to a new section and the list is reused for the next one.
			var blocks = new List<Block>();

			void EndSection(XElement sectPr)
			{
				var section = new Section();
				InheritHeadersAndFooters(section);
				ApplySectionProperties(section, sectPr);

				section.Blocks.AddRange(blocks);
				blocks.Clear();

				_model.Sections.Add(section);
			}

			ReadBlocks(body, blocks, context, EndSection);
			EndSection(body.Element(Ns.W + "sectPr"));

			// Fields never span stories.
			_fields = new FieldTracker();
		}

		/// <summary>
		/// Reads the footnotes or the endnotes.
		/// </summary>
		/// <param name="relationshipSuffix">The relationship type suffix of the part.</param>
		/// <param name="elementName">The local name of the note elements.</param>
		/// <param name="kind">The kind of notes.</param>
		/// <param name="target">The dictionary that receives the notes.</param>
		private void ReadNotes(string relationshipSuffix, string elementName, NoteKind kind, Dictionary<string, Note> target)
		{
			var part = _package.GetMainRelatedPart(relationshipSuffix);
			var root = _package.GetXml(part);
			if (root == null)
				return;

			var context = new ReadContext(part, elementName);

			foreach (var e in root.Elements(Ns.W + elementName))
			{
				var id = e.WAttr("id");
				var type = e.WAttr("type");

				// Separators are layout artifacts, not notes.
				if (id == null || (type != null && type != "normal"))
					continue;

				var note = new Note { Id = id, Kind = kind };
				ReadBlocks(e, note.Blocks, context, null);
				_fields = new FieldTracker();

				target[id] = note;
			}
		}

		/// <summary>
		/// Reads the comments.
		/// </summary>
		private void ReadComments()
		{
			var part = _package.GetMainRelatedPart("/comments");
			var root = _package.GetXml(part);
			if (root == null)
				return;

			var context = new ReadContext(part, "comment");

			foreach (var e in root.Elements(Ns.W + "comment"))
			{
				var id = e.WAttr("id");
				if (id == null)
					continue;

				var comment = new Comment
				{
					Id = id,
					Author = e.WAttr("author"),
					Date = e.WAttr("date"),
					AnchorText = _commentAnchors.TryGetValue(id, out var anchor) ? anchor : null,
				};

				ReadBlocks(e, comment.Blocks, context, null);
				_fields = new FieldTracker();

				_model.Comments[id] = comment;
			}
		}

		/// <summary>
		/// Reads the blocks of a story container, such as a text box, for the drawing reader.
		/// </summary>
		/// <param name="container">The container element.</param>
		/// <param name="context">The read context.</param>
		/// <returns>The blocks of the container.</returns>
		private List<Block> ReadStoryBlocks(XElement container, ReadContext context)
		{
			var blocks = new List<Block>();
			ReadBlocks(container, blocks, context, null);
			return blocks;
		}

		#endregion

		#region Sections

		/// <summary>
		/// Applies a section properties element (<c>w:sectPr</c>) to a section.
		/// </summary>
		/// <param name="section">The section to update.</param>
		/// <param name="sectPr">The section properties. Can be <c>null</c>.</param>
		private void ApplySectionProperties(Section section, XElement sectPr)
		{
			if (sectPr == null)
				return;

			var page = section.Page;

			var pgSz = sectPr.Element(Ns.W + "pgSz");
			page.Width = XmlExtensions.TwipsToPoints(pgSz?.WAttr("w")) ?? page.Width;
			page.Height = XmlExtensions.TwipsToPoints(pgSz?.WAttr("h")) ?? page.Height;

			var pgMar = sectPr.Element(Ns.W + "pgMar");
			if (pgMar != null)
			{
				// Word reads a margin missing from w:pgMar as zero. Negative top and bottom margins
				// mean "exact": the text does not move down for tall headers.
				page.MarginTop = Math.Abs(XmlExtensions.TwipsToPoints(pgMar.WAttr("top")) ?? 0);
				page.MarginBottom = Math.Abs(XmlExtensions.TwipsToPoints(pgMar.WAttr("bottom")) ?? 0);
				page.MarginLeft = XmlExtensions.TwipsToPoints(pgMar.WAttr("left") ?? pgMar.WAttr("start")) ?? 0;
				page.MarginRight = XmlExtensions.TwipsToPoints(pgMar.WAttr("right") ?? pgMar.WAttr("end")) ?? 0;
				page.HeaderDistance = XmlExtensions.TwipsToPoints(pgMar.WAttr("header")) ?? 0;
				page.FooterDistance = XmlExtensions.TwipsToPoints(pgMar.WAttr("footer")) ?? 0;
				page.Gutter = XmlExtensions.TwipsToPoints(pgMar.WAttr("gutter")) ?? 0;
			}

			var cols = sectPr.Element(Ns.W + "cols");
			if (cols != null)
			{
				var columns = cols.Elements(Ns.W + "col").ToList();
				page.Columns = Math.Max(1, XmlExtensions.ParseInt(cols.WAttr("num")) ?? (columns.Count > 0 ? columns.Count : 1));
				page.ColumnSpacing = XmlExtensions.TwipsToPoints(cols.WAttr("space")) ?? 36;
			}

			var docGrid = sectPr.Element(Ns.W + "docGrid");
			var gridType = docGrid?.WAttr("type");
			if (gridType == "lines" || gridType == "linesAndChars" || gridType == "snapToChars")
				page.GridLinePitch = XmlExtensions.TwipsToPoints(docGrid.WAttr("linePitch")) ?? 0;

			switch (sectPr.WChildVal("type"))
			{
				case "continuous":
					section.Start = SectionStart.Continuous;
					break;

				case "oddPage":
					section.Start = SectionStart.OddPage;
					break;

				case "evenPage":
					section.Start = SectionStart.EvenPage;
					break;

				case "nextColumn":
					section.Start = SectionStart.NextColumn;
					break;

				default:
					section.Start = SectionStart.NextPage;
					break;
			}

			section.TitlePage = sectPr.Element(Ns.W + "titlePg").OnOff() ?? false;

			var pgNumType = sectPr.Element(Ns.W + "pgNumType");
			section.PageNumberStart = XmlExtensions.ParseInt(pgNumType?.WAttr("start"));
			section.PageNumberFormat = pgNumType?.WAttr("fmt");

			ReadHeaderFooterReferences(section, sectPr, "headerReference", section.Headers, true);
			ReadHeaderFooterReferences(section, sectPr, "footerReference", section.Footers, false);
		}

		/// <summary>
		/// Copies the headers and footers of the previous section to a new section,
		/// which inherits those it does not redefine.
		/// </summary>
		/// <param name="section">The new section.</param>
		private void InheritHeadersAndFooters(Section section)
		{
			var previous = _model.Sections.LastOrDefault();
			if (previous == null)
				return;

			foreach (var pair in previous.Headers)
				section.Headers[pair.Key] = pair.Value;

			foreach (var pair in previous.Footers)
				section.Footers[pair.Key] = pair.Value;
		}

		/// <summary>
		/// Reads the header or footer references of a section.
		/// </summary>
		/// <param name="section">The section.</param>
		/// <param name="sectPr">The section properties.</param>
		/// <param name="referenceName">The local name of the reference elements.</param>
		/// <param name="target">The dictionary that receives the stories.</param>
		/// <param name="isHeader">Whether the references are headers.</param>
		private void ReadHeaderFooterReferences(Section section, XElement sectPr, string referenceName, Dictionary<HeaderFooterKind, HeaderFooter> target, bool isHeader)
		{
			foreach (var reference in sectPr.Elements(Ns.W + referenceName))
			{
				var id = reference.Attribute(Ns.R + "id")?.Value;
				var part = DocxPackage.GetRelatedPart(_package.MainPart, id);
				if (part == null)
					continue;

				var kind = reference.WAttr("type") == "first"
					? HeaderFooterKind.First
					: reference.WAttr("type") == "even" ? HeaderFooterKind.Even : HeaderFooterKind.Default;

				target[kind] = GetHeaderFooter(part, isHeader);
			}
		}

		/// <summary>
		/// Returns the story of a header or footer part, reading it on first use.
		/// </summary>
		/// <param name="part">The header or footer part.</param>
		/// <param name="isHeader">Whether the part is a header.</param>
		/// <returns>The story.</returns>
		private HeaderFooter GetHeaderFooter(OpenXmlPart part, bool isHeader)
		{
			var key = part.Uri.OriginalString;
			if (_headerFooters.TryGetValue(key, out var story))
				return story;

			story = new HeaderFooter { Id = key, IsHeader = isHeader };
			_headerFooters[key] = story;

			var root = _package.GetXml(part);
			if (root != null)
			{
				// Headers are read in the middle of the body: their fields must not interfere with the body fields.
				var bodyFields = _fields;
				_fields = new FieldTracker();

				ReadBlocks(root, story.Blocks, new ReadContext(part, isHeader ? "header" : "footer"), null);

				_fields = bodyFields;
			}

			return story;
		}

		#endregion

		#region Blocks

		/// <summary>
		/// Reads the block level content of a container: paragraphs, tables, content controls, embedded chunks.
		/// </summary>
		/// <param name="container">The container element.</param>
		/// <param name="target">The list that receives the blocks.</param>
		/// <param name="context">The read context.</param>
		/// <param name="sectionEnd">Called when a paragraph ends a section; <c>null</c> outside the body.</param>
		private void ReadBlocks(XElement container, List<Block> target, ReadContext context, Action<XElement> sectionEnd)
		{
			// Deeply nested content (tables in cells, text boxes, content controls) is ignored past the
			// limit, instead of exhausting the stack.
			if (_depth >= ReadLimits.MaxNestingDepth)
				return;

			_depth++;
			try
			{
				ReadBlockContent(container, target, context, sectionEnd);
			}
			finally
			{
				_depth--;
			}
		}

		/// <summary>
		/// Reads the block level content of a container; see <see cref="ReadBlocks"/>.
		/// </summary>
		/// <param name="container">The container element.</param>
		/// <param name="target">The list that receives the blocks.</param>
		/// <param name="context">The read context.</param>
		/// <param name="sectionEnd">Called when a paragraph ends a section; <c>null</c> outside the body.</param>
		private void ReadBlockContent(XElement container, List<Block> target, ReadContext context, Action<XElement> sectionEnd)
		{
			ParagraphBlock pendingMerge = null;

			foreach (var e in container.ContentElements())
			{
				if (e.Name.Namespace != Ns.W)
					continue;

				switch (e.Name.LocalName)
				{
					case "p":
						ReadParagraph(e, target, context, sectionEnd, ref pendingMerge);
						break;

					case "tbl":
						FlushMerge(target, ref pendingMerge);
						ReadTable(e, target, context);
						break;

					case "sdt":
						var blockContent = e.Element(Ns.W + "sdtContent");
						if (blockContent != null)
							ReadBlocks(blockContent, target, IsShowingPlaceholder(e) ? context.WithPlaceholder() : context, sectionEnd);
						break;

					case "customXml":
					case "smartTag":
						ReadBlocks(e, target, context, sectionEnd);
						break;

					case "ins":
					case "moveTo":
						if (_options.TrackedChanges != TrackedChangesMode.Reject)
							ReadBlocks(e, target, context.WithRevision(RevisionKind.Inserted), sectionEnd);
						break;

					case "del":
					case "moveFrom":
						if (_options.TrackedChanges != TrackedChangesMode.Accept)
							ReadBlocks(e, target, context.WithRevision(RevisionKind.Deleted), sectionEnd);
						break;

					case "altChunk":
						FlushMerge(target, ref pendingMerge);
						ReadAltChunk(e, target, context);
						break;
				}
			}

			FlushMerge(target, ref pendingMerge);
		}

		/// <summary>
		/// Adds a paragraph held for merging when no following paragraph can receive it.
		/// </summary>
		/// <param name="target">The list of blocks.</param>
		/// <param name="pendingMerge">The pending paragraph, reset to <c>null</c>.</param>
		private static void FlushMerge(List<Block> target, ref ParagraphBlock pendingMerge)
		{
			if (pendingMerge != null)
			{
				target.Add(pendingMerge);
				pendingMerge = null;
			}
		}

		/// <summary>
		/// Reads a paragraph.
		/// </summary>
		/// <param name="p">The paragraph element.</param>
		/// <param name="target">The list that receives the paragraph.</param>
		/// <param name="context">The read context.</param>
		/// <param name="sectionEnd">Called when the paragraph ends a section. Can be <c>null</c>.</param>
		/// <param name="pendingMerge">A previous paragraph whose mark was deleted, to merge into this one.</param>
		private void ReadParagraph(XElement p, List<Block> target, ReadContext context, Action<XElement> sectionEnd, ref ParagraphBlock pendingMerge)
		{
			var pPr = p.Element(Ns.W + "pPr");
			var isInTableOfContents = _fields.IsInTableOfContents;

			var properties = _styles.ResolveParagraph(pPr, context.TableStyleId, _numbering, out var numberingId, out var numberingLevel);
			var paragraph = new ParagraphBlock { Properties = properties };

			if (isInTableOfContents)
				properties.Kind = ParagraphKind.TableOfContents;

			var markHidden = properties.MarkStyle.Hidden && !_options.IncludeHiddenText;

			ReadInlines(p, paragraph, context);

			if (_fields.IsInTableOfContents)
				properties.Kind = ParagraphKind.TableOfContents;

			// A paragraph whose content and mark are all hidden is not displayed at all.
			var isHidden = markHidden && paragraph.Inlines.All(i => !i.IsVisible || i is RenderedPageBreakInline);

			if (numberingId > 0 && !isHidden)
				paragraph.List = _numbering.NextLabel(numberingId.Value, numberingLevel, properties.MarkStyle);

			if (pendingMerge != null)
			{
				paragraph.Inlines.InsertRange(0, pendingMerge.Inlines);
				pendingMerge = null;
			}

			var markDeleted = pPr?.Element(Ns.W + "rPr")?.Element(Ns.W + "del") != null;
			var sectPr = pPr?.Element(Ns.W + "sectPr");

			if (markDeleted && _options.TrackedChanges == TrackedChangesMode.Accept && sectPr == null)
			{
				// Accepting the deletion of a paragraph mark joins the paragraph with the next one.
				pendingMerge = paragraph;
				return;
			}

			if (!isHidden || sectPr != null)
				target.Add(paragraph);

			if (sectPr != null && sectionEnd != null)
				sectionEnd(sectPr);
		}

		/// <summary>
		/// Reads a table.
		/// </summary>
		/// <param name="tbl">The table element.</param>
		/// <param name="target">The list that receives the table.</param>
		/// <param name="context">The read context.</param>
		private void ReadTable(XElement tbl, List<Block> target, ReadContext context)
		{
			var tblPr = tbl.Element(Ns.W + "tblPr");
			var styleId = tblPr?.WChildVal("tblStyle") ?? _defaultTableStyleId;
			var table = new TableBlock();

			foreach (var gridCol in tbl.Element(Ns.W + "tblGrid")?.Elements(Ns.W + "gridCol") ?? Enumerable.Empty<XElement>())
				table.Grid.Add(XmlExtensions.TwipsToPoints(gridCol.WAttr("w")) ?? 0);

			ReadTableProperties(tblPr, table);

			var cellContext = context.WithTable(styleId);
			foreach (var row in GetRows(tbl, context))
				ReadRow(row, table, cellContext);

			if (table.Rows.Count > 0)
				target.Add(table);
		}

		/// <summary>
		/// Reads the table properties that affect the layout.
		/// </summary>
		/// <param name="tblPr">The table properties. Can be <c>null</c>.</param>
		/// <param name="table">The table to update.</param>
		private static void ReadTableProperties(XElement tblPr, TableBlock table)
		{
			if (tblPr == null)
				return;

			var margins = tblPr.Element(Ns.W + "tblCellMar");
			if (margins != null)
			{
				table.CellMarginLeft = CellMargin(margins, "left", "start") ?? table.CellMarginLeft;
				table.CellMarginRight = CellMargin(margins, "right", "end") ?? table.CellMarginRight;
				table.CellMarginTop = CellMargin(margins, "top", null) ?? table.CellMarginTop;
				table.CellMarginBottom = CellMargin(margins, "bottom", null) ?? table.CellMarginBottom;
			}

			var width = tblPr.Element(Ns.W + "tblW");
			if (width != null)
			{
				var value = XmlExtensions.ParseDouble(width.WAttr("w")) ?? 0;
				switch (width.WAttr("type"))
				{
					case "dxa":
						table.PreferredWidth = value / 20.0;
						break;

					case "pct":
						// Fiftieths of a percent, or an explicit percentage.
						var text = width.WAttr("w") ?? "";
						table.PreferredWidthPercent = text.EndsWith("%") ? (XmlExtensions.ParseDouble(text.TrimEnd('%')) ?? 0) / 100.0 : value / 5000.0;
						break;
				}
			}

			var borders = tblPr.Element(Ns.W + "tblBorders");
			table.HasBorders = borders != null && borders.Elements().Any(b => b.WVal() != "nil" && b.WVal() != "none");
		}

		/// <summary>
		/// Reads a cell margin from <c>w:tblCellMar</c> or <c>w:tcMar</c>.
		/// </summary>
		/// <param name="margins">The margins element.</param>
		/// <param name="name">The local name of the margin.</param>
		/// <param name="alternateName">The bidirectional name of the margin, or <c>null</c>.</param>
		/// <returns>The margin in points, or <c>null</c>.</returns>
		private static double? CellMargin(XElement margins, string name, string alternateName)
		{
			var e = margins.Element(Ns.W + name) ?? (alternateName != null ? margins.Element(Ns.W + alternateName) : null);
			if (e == null || (e.WAttr("type") != null && e.WAttr("type") != "dxa"))
				return null;

			return XmlExtensions.TwipsToPoints(e.WAttr("w"));
		}

		/// <summary>
		/// Enumerates the rows of a table, looking through content controls and tracked changes.
		/// </summary>
		/// <param name="container">The table or row container.</param>
		/// <param name="context">The read context.</param>
		/// <returns>The row elements.</returns>
		private IEnumerable<XElement> GetRows(XElement container, ReadContext context)
		{
			foreach (var e in container.ContentElements())
			{
				if (e.Name == Ns.W + "tr")
				{
					yield return e;
				}
				else if (e.Name == Ns.W + "sdt")
				{
					var content = e.Element(Ns.W + "sdtContent");
					if (content != null)
					{
						foreach (var row in GetRows(content, context))
							yield return row;
					}
				}
				else if (e.Name == Ns.W + "customXml")
				{
					foreach (var row in GetRows(e, context))
						yield return row;
				}
			}
		}

		/// <summary>
		/// Reads a table row.
		/// </summary>
		/// <param name="tr">The row element.</param>
		/// <param name="table">The table that receives the row.</param>
		/// <param name="context">The read context of the cells.</param>
		private void ReadRow(XElement tr, TableBlock table, ReadContext context)
		{
			var trPr = tr.Element(Ns.W + "trPr");

			// Rows inserted or deleted with tracked changes.
			var deleted = trPr?.Element(Ns.W + "del") != null;
			var inserted = trPr?.Element(Ns.W + "ins") != null;
			if ((deleted && _options.TrackedChanges == TrackedChangesMode.Accept) || (inserted && _options.TrackedChanges == TrackedChangesMode.Reject))
				return;

			var row = new TableRow
			{
				IsHeader = trPr?.Element(Ns.W + "tblHeader").OnOff() ?? false,
				CantSplit = trPr?.Element(Ns.W + "cantSplit").OnOff() ?? false,
				GridBefore = XmlExtensions.ParseInt(trPr?.WChildVal("gridBefore")) ?? 0,
				GridAfter = XmlExtensions.ParseInt(trPr?.WChildVal("gridAfter")) ?? 0,
			};

			var trHeight = trPr?.Element(Ns.W + "trHeight");
			if (trHeight != null && trHeight.WAttr("hRule") != "auto")
			{
				row.Height = XmlExtensions.TwipsToPoints(trHeight.WVal()) ?? 0;
				row.HeightIsExact = trHeight.WAttr("hRule") == "exact";
			}

			foreach (var tc in GetCells(tr))
			{
				var tcPr = tc.Element(Ns.W + "tcPr");
				var hMerge = tcPr?.Element(Ns.W + "hMerge");

				// Legacy horizontal merge: the continuation cells extend the previous cell.
				if (hMerge != null && hMerge.WVal() != "restart" && row.Cells.Count > 0)
				{
					var previous = row.Cells[row.Cells.Count - 1];
					previous.GridSpan += Math.Max(1, XmlExtensions.ParseInt(tcPr.WChildVal("gridSpan")) ?? 1);
					ReadBlocks(tc, previous.Blocks, context, null);
					continue;
				}

				var cell = new TableCell
				{
					GridSpan = Math.Max(1, XmlExtensions.ParseInt(tcPr?.WChildVal("gridSpan")) ?? 1),
				};

				var vMerge = tcPr?.Element(Ns.W + "vMerge");
				if (vMerge != null)
					cell.VerticalMerge = vMerge.WVal() == "restart" ? VerticalMerge.Restart : VerticalMerge.Continue;

				var tcW = tcPr?.Element(Ns.W + "tcW");
				if (tcW?.WAttr("type") == "dxa")
					cell.Width = XmlExtensions.TwipsToPoints(tcW.WAttr("w")) ?? 0;

				ReadBlocks(tc, cell.Blocks, context, null);
				row.Cells.Add(cell);
			}

			if (row.Cells.Count > 0)
				table.Rows.Add(row);
		}

		/// <summary>
		/// Enumerates the cells of a row, looking through content controls.
		/// </summary>
		/// <param name="container">The row or cell container.</param>
		/// <returns>The cell elements.</returns>
		private static IEnumerable<XElement> GetCells(XElement container)
		{
			foreach (var e in container.ContentElements())
			{
				if (e.Name == Ns.W + "tc")
				{
					yield return e;
				}
				else if (e.Name == Ns.W + "sdt" || e.Name == Ns.W + "customXml")
				{
					var content = e.Name == Ns.W + "sdt" ? e.Element(Ns.W + "sdtContent") : e;
					if (content != null)
					{
						foreach (var cell in GetCells(content))
							yield return cell;
					}
				}
			}
		}

		/// <summary>
		/// Reads an embedded chunk of content in another format (<c>w:altChunk</c>).
		/// </summary>
		/// <param name="altChunk">The alternative format chunk element.</param>
		/// <param name="target">The list that receives the content.</param>
		/// <param name="context">The read context.</param>
		private void ReadAltChunk(XElement altChunk, List<Block> target, ReadContext context)
		{
			var part = DocxPackage.GetRelatedPart(context.Part, altChunk.Attribute(Ns.R + "id")?.Value);
			if (part == null)
				return;

			var text = AltChunkReader.Read(part, _options);
			if (!String.IsNullOrWhiteSpace(text))
				target.Add(new RawTextBlock { Text = text.Trim() });
		}

		/// <summary>
		/// Determines whether a content control is displaying its placeholder text.
		/// </summary>
		/// <param name="sdt">The content control element.</param>
		/// <returns><c>true</c> when the control shows placeholder text such as "Click or tap here to enter text."</returns>
		private static bool IsShowingPlaceholder(XElement sdt)
		{
			return sdt.Element(Ns.W + "sdtPr")?.Element(Ns.W + "showingPlcHdr").OnOff() == true;
		}

		#endregion

		#region Inlines

		/// <summary>
		/// Reads the inline content of a paragraph or of an inline container.
		/// </summary>
		/// <param name="container">The paragraph or container element.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadInlines(XElement container, ParagraphBlock paragraph, ReadContext context)
		{
			if (_depth >= ReadLimits.MaxNestingDepth)
				return;

			_depth++;
			try
			{
				ReadInlineContent(container, paragraph, context);
			}
			finally
			{
				_depth--;
			}
		}

		/// <summary>
		/// Reads the inline content of a container; see <see cref="ReadInlines"/>.
		/// </summary>
		/// <param name="container">The paragraph or container element.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadInlineContent(XElement container, ParagraphBlock paragraph, ReadContext context)
		{
			foreach (var e in container.ContentElements())
			{
				if (e.Name.Namespace == Ns.M)
				{
					ReadMath(e, paragraph, context);
					continue;
				}

				if (e.Name.Namespace != Ns.W)
					continue;

				switch (e.Name.LocalName)
				{
					case "r":
						ReadRun(e, paragraph, context);
						break;

					case "hyperlink":
						ReadInlines(e, paragraph, context.WithHyperlink(GetHyperlinkTarget(e, context)));
						break;

					case "fldSimple":
						ReadSimpleField(e, paragraph, context);
						break;

					case "ins":
					case "moveTo":
						if (_options.TrackedChanges != TrackedChangesMode.Reject)
							ReadInlines(e, paragraph, context.WithRevision(RevisionKind.Inserted));
						break;

					case "del":
					case "moveFrom":
						if (_options.TrackedChanges != TrackedChangesMode.Accept)
							ReadInlines(e, paragraph, context.WithRevision(RevisionKind.Deleted));
						break;

					case "sdt":
						var inlineContent = e.Element(Ns.W + "sdtContent");
						if (inlineContent != null)
							ReadInlines(inlineContent, paragraph, IsShowingPlaceholder(e) ? context.WithPlaceholder() : context);
						break;

					case "smartTag":
					case "customXml":
					case "dir":
					case "bdo":
						ReadInlines(e, paragraph, context);
						break;

					case "commentRangeStart":
						if (context.IsMainStory && _options.IncludeComments && e.WAttr("id") != null)
							_openComments[e.WAttr("id")] = new StringBuilder();
						break;

					case "commentRangeEnd":
						CloseCommentRange(e.WAttr("id"));
						break;
				}
			}
		}

		/// <summary>
		/// Reads a run (<c>w:r</c>).
		/// </summary>
		/// <param name="r">The run element.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadRun(XElement r, ParagraphBlock paragraph, ReadContext context)
		{
			var style = _styles.ResolveRun(r.Element(Ns.W + "rPr"), paragraph.Properties.StyleId, context.TableStyleId);
			var hidden = style.Hidden && !_options.IncludeHiddenText;

			foreach (var e in r.ContentElements())
			{
				// Field characters are processed even in hidden runs, or the field structure would break.
				if (e.Name == Ns.W + "fldChar")
				{
					ReadFieldCharacter(e, style, paragraph, context);
					continue;
				}

				if (e.Name == Ns.W + "instrText")
				{
					_fields.AddInstruction(e.Value);
					continue;
				}

				// Page markers are layout information: they count even inside field instructions and hidden text.
				if (e.Name == Ns.W + "lastRenderedPageBreak")
				{
					ReadRunContent(e, style, paragraph, context);
					continue;
				}

				if (_fields.IsSuppressed || hidden)
					continue;

				if (e.Name.Namespace == Ns.W)
					ReadRunContent(e, style, paragraph, context);
				else if (e.Name.LocalName == "symEx")
					AddText(paragraph, CharFromHex(e.Attributes().FirstOrDefault(a => a.Name.LocalName == "char")?.Value), style, context);
			}
		}

		/// <summary>
		/// Reads a single content element of a run.
		/// </summary>
		/// <param name="e">The run content element.</param>
		/// <param name="style">The formatting of the run.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadRunContent(XElement e, RunStyle style, ParagraphBlock paragraph, ReadContext context)
		{
			switch (e.Name.LocalName)
			{
				case "t":
				case "delText":
					var text = e.Value;
					if (FontNames.IsSymbolFont(style.FontFamily) || text.Any(c => c >= '' && c <= ''))
						text = SymbolMap.MapText(text, style.FontFamily);

					AddText(paragraph, text, style, context);
					break;

				case "tab":
				case "ptab":
					AddInline(paragraph, new TabInline(), style, context);
					break;

				case "br":
					var type = e.WAttr("type");
					var kind = type == "page" ? BreakKind.Page : type == "column" ? BreakKind.Column : BreakKind.Line;
					AddInline(paragraph, new BreakInline { Kind = kind }, style, context);
					break;

				case "cr":
					AddInline(paragraph, new BreakInline { Kind = BreakKind.Line }, style, context);
					break;

				case "noBreakHyphen":
					AddText(paragraph, "-", style, context);
					break;

				case "sym":
					var code = Int32.TryParse(e.WAttr("char"), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : 0;
					AddText(paragraph, SymbolMap.Map(code, e.WAttr("font")) ?? "", style, context);
					break;

				case "drawing":
					foreach (var inline in _drawings.ReadDrawing(e, context))
						AddInline(paragraph, inline, style, context);
					break;

				case "pict":
				case "object":
					foreach (var inline in _drawings.ReadVml(e, context))
						AddInline(paragraph, inline, style, context);
					break;

				case "footnoteReference":
					AddNoteReference(paragraph, e, NoteKind.Footnote, style, context);
					break;

				case "endnoteReference":
					AddNoteReference(paragraph, e, NoteKind.Endnote, style, context);
					break;

				case "commentReference":
					if (_options.IncludeComments && context.IsMainStory)
						AddInline(paragraph, new CommentReferenceInline { CommentId = e.WAttr("id") }, style, context);
					break;

				case "lastRenderedPageBreak":
					if (context.IsMainStory && !context.InTextBox)
					{
						AddInline(paragraph, new RenderedPageBreakInline(), style, context);
						_model.RenderedPageBreakCount++;
					}
					break;

				case "pgNum":
					AddInline(paragraph, new FieldInline { Kind = FieldKind.Page }, style, context);
					break;

				case "ruby":
					var rubyBase = e.Element(Ns.W + "rubyBase");
					if (rubyBase != null)
						ReadInlines(rubyBase, paragraph, context);
					break;
			}
		}

		/// <summary>
		/// Processes a field character (<c>w:fldChar</c>).
		/// </summary>
		/// <param name="e">The field character element.</param>
		/// <param name="style">The formatting of the run.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadFieldCharacter(XElement e, RunStyle style, ParagraphBlock paragraph, ReadContext context)
		{
			switch (e.WAttr("fldCharType"))
			{
				case "begin":
					var checkBox = e.Element(Ns.W + "ffData")?.Element(Ns.W + "checkBox");
					bool? state = null;
					if (checkBox != null)
						state = checkBox.Element(Ns.W + "checked").OnOff() ?? (checkBox.Element(Ns.W + "default").OnOff() ?? false);

					_fields.Begin(state);
					break;

				case "separate":
					var computed = _fields.Separate(style);
					if (computed != null && !_fields.IsSuppressedByOuterFields)
						AddInline(paragraph, computed, style, context);
					break;

				case "end":
					var result = _fields.End(style);
					if (result != null && !_fields.IsSuppressed)
						AddInline(paragraph, result, style, context);
					break;
			}
		}

		/// <summary>
		/// Reads a simple field (<c>w:fldSimple</c>).
		/// </summary>
		/// <param name="e">The simple field element.</param>
		/// <param name="paragraph">The paragraph that receives the inlines.</param>
		/// <param name="context">The read context.</param>
		private void ReadSimpleField(XElement e, ParagraphBlock paragraph, ReadContext context)
		{
			if (_fields.IsSuppressed)
				return;

			var style = _styles.ResolveRun(e.Element(Ns.W + "r")?.Element(Ns.W + "rPr"), paragraph.Properties.StyleId, context.TableStyleId);
			var computed = FieldTracker.InterpretSimpleField(e.WAttr("instr"), style, out var hyperlink);

			if (computed != null)
			{
				AddInline(paragraph, computed, style, context);
				return;
			}

			ReadInlines(e, paragraph, hyperlink != null ? context.WithHyperlink(hyperlink) : context);
		}

		/// <summary>
		/// Reads an equation.
		/// </summary>
		/// <param name="e">The <c>m:oMath</c> or <c>m:oMathPara</c> element.</param>
		/// <param name="paragraph">The paragraph that receives the inline.</param>
		/// <param name="context">The read context.</param>
		private void ReadMath(XElement e, ParagraphBlock paragraph, ReadContext context)
		{
			if (_fields.IsSuppressed || (e.Name != Ns.M + "oMath" && e.Name != Ns.M + "oMathPara"))
				return;

			var latex = MathConverter.Convert(e);
			if (latex.Length == 0)
				return;

			var style = _styles.ResolveRun(null, paragraph.Properties.StyleId, context.TableStyleId);
			var inline = new MathInline { Latex = latex, IsDisplay = e.Name == Ns.M + "oMathPara" };

			// Equations are kept for the layout even when excluded, flagged as hidden for the output.
			if (!_options.IncludeEquations)
				inline.Latex = null;

			AddInline(paragraph, inline, style, context);
		}

		/// <summary>
		/// Adds a reference to a footnote or an endnote, assigning its label.
		/// </summary>
		/// <param name="paragraph">The paragraph that receives the inline.</param>
		/// <param name="e">The reference element.</param>
		/// <param name="kind">The kind of note.</param>
		/// <param name="style">The formatting of the run.</param>
		/// <param name="context">The read context.</param>
		private void AddNoteReference(ParagraphBlock paragraph, XElement e, NoteKind kind, RunStyle style, ReadContext context)
		{
			var label = kind == NoteKind.Footnote
				? NumberFormatter.Format(++_footnoteCount, _footnoteFormat)
				: NumberFormatter.Format(++_endnoteCount, _endnoteFormat);

			AddInline(paragraph, new NoteReferenceInline { Kind = kind, NoteId = e.WAttr("id"), Label = label }, style, context);
		}

		/// <summary>
		/// Adds a run of text.
		/// </summary>
		/// <param name="paragraph">The paragraph that receives the text.</param>
		/// <param name="text">The text. Empty text is ignored.</param>
		/// <param name="style">The formatting of the text.</param>
		/// <param name="context">The read context.</param>
		private void AddText(ParagraphBlock paragraph, string text, RunStyle style, ReadContext context)
		{
			if (String.IsNullOrEmpty(text))
				return;

			foreach (var anchor in _openComments.Values)
				anchor.Append(text);

			AddInline(paragraph, new TextInline { Text = text }, style, context);
		}

		/// <summary>
		/// Adds an inline, applying the formatting, hyperlink and revision of the context.
		/// </summary>
		/// <param name="paragraph">The paragraph that receives the inline.</param>
		/// <param name="inline">The inline to add.</param>
		/// <param name="style">The formatting of the inline.</param>
		/// <param name="context">The read context.</param>
		private void AddInline(ParagraphBlock paragraph, Inline inline, RunStyle style, ReadContext context)
		{
			inline.Style = style;
			inline.Hyperlink = _fields.Hyperlink ?? context.Hyperlink;
			inline.Revision = context.Revision;
			inline.IsPlaceholder = context.IsPlaceholder;

			paragraph.Inlines.Add(inline);
		}

		/// <summary>
		/// Returns the target of a <c>w:hyperlink</c> element.
		/// </summary>
		/// <param name="e">The hyperlink element.</param>
		/// <param name="context">The read context, used to resolve the relationship.</param>
		/// <returns>The external URL, the internal <c>#anchor</c>, or <c>null</c>.</returns>
		private static string GetHyperlinkTarget(XElement e, ReadContext context)
		{
			var target = DocxPackage.GetExternalTarget(context.Part, e.Attribute(Ns.R + "id")?.Value);
			var anchor = e.WAttr("anchor");

			if (target != null)
				return anchor != null ? target + "#" + anchor : target;

			return anchor != null ? "#" + anchor : null;
		}

		/// <summary>
		/// Closes a comment range and records the text it covers.
		/// </summary>
		/// <param name="id">The comment identifier. Can be <c>null</c>.</param>
		private void CloseCommentRange(string id)
		{
			if (id == null || !_openComments.TryGetValue(id, out var text))
				return;

			_openComments.Remove(id);

			var anchor = text.ToString().Trim();
			if (anchor.Length > 0)
				_commentAnchors[id] = anchor.Length > 200 ? anchor.Substring(0, 200) + "…" : anchor;
		}

		/// <summary>
		/// Converts a hexadecimal code point to a string.
		/// </summary>
		/// <param name="hex">The hexadecimal code point. Can be <c>null</c>.</param>
		/// <returns>The character, or <c>null</c> when the value is invalid.</returns>
		private static string CharFromHex(string hex)
		{
			if (!Int32.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code) || code <= 0 || code > 0x10FFFF)
				return null;

			return Char.ConvertFromUtf32(code);
		}

		#endregion
	}
}

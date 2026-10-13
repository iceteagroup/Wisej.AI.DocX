# Wisej.AI.DocX

High fidelity extraction of Word documents (DOCX, DOCM, DOTX, DOTM, Flat OPC) into **Markdown or plain text, split into pages**, built to feed language model context and retrieval pipelines in [Wisej.AI](https://wisej.com).

```csharp
var result = new DocxExtractor().Extract("report.docx");

Console.WriteLine(result.Text);                 // the whole document, with "<!-- Page n -->" separators
Console.WriteLine(result.Pages[2].Text);        // the text of page 3
Console.WriteLine(result.PaginationSource);     // RenderedBreaks, CalibratedEstimate, Estimate, ...
File.WriteAllBytes(result.Images[0].FileName, result.Images[0].Data);
```

## What is extracted

| Content | Output |
|---|---|
| Headings, title, quotes | `#` headings (from outline levels and styles), `>` quotes |
| Character formatting | `**bold**`, `*italic*`, `~~strike~~`, `<sup>`/`<sub>`, `` `code` `` and fenced code blocks for code fonts. Emphasis covering a whole cell or paragraph is styling and is dropped. |
| Lists | The real labels Word displays (`1.`, `a)`, `iv.`, `2.1.3`), computed from the numbering definitions, with nesting |
| Tables | Pipe tables with merged cells resolved (repeated by default), combined multi-row headers, empty rows and columns removed, header repeated when a table spans pages; HTML tables for nested tables; single row layout tables unwrapped |
| Images | `![alt text](image3.png)` references, the bytes in `Result.Images`, an optional `ImageDescriber` callback (vision model, OCR) called once per distinct picture |
| Charts | The cached series data as a table, with the chart title, type and axis titles (including Office 2016 chart types) |
| SmartArt | The diagram text as a nested list |
| Equations | LaTeX (`$\frac{a}{b}$`, `$$...$$`) |
| Text boxes and shapes | Their content, after the paragraph that anchors them |
| Footnotes, endnotes | `[^1]` references, definitions at the end of the page (footnotes) or document (endnotes) |
| Comments | Optional, with author, date and the text they refer to |
| Headers, footers | Once (default) or on every page, with `PAGE`/`NUMPAGES` computed for each page |
| Tracked changes | Accepted (default), rejected, or shown with CriticMarkup `{++ ++}` `{-- --}` |
| Fields | Cached results, hyperlinks, check boxes; table of contents optional |
| Metadata | Core, application and custom properties |
| Robustness | Malformed URIs, missing parts and Windows path separators are repaired; content control placeholders, hidden text and decorative images are skipped; nesting depth is bounded |

## Pages

A DOCX file has no pages: they exist only in the layout an application computes. The library recovers them in three ways:

1. **Word's own layout.** When Word saves a document it writes a `w:lastRenderedPageBreak` marker where every page (or column) started, and the page count in `docProps/app.xml`. When they agree, the pages match the layout the author saw exactly.
2. **Anchored, calibrated estimate.** Word does not write markers inside some content (table of contents results, content controls). The markers it did write are used as fixed page starts, and a layout engine adds the missing ones, calibrated so that the total matches the recorded page count.
3. **Layout estimate.** Documents produced by other software have no markers. The layout engine measures every line with real font metrics (75 built-in Office font faces, plus the fonts installed on the machine) and applies Word's rules: line spacing and document grid, spacing collapse at page tops, keep with next, keep lines together, widow and orphan control, rows that cannot split, repeated header rows, columns, odd/even sections, header, footer and footnote space.

`PaginationMode.Auto` (the default) chooses automatically; `DocxExtractionResult.PaginationSource` reports which source was used.

### Accuracy

Measured against Microsoft Word 16 on 768 documents of the Aspose.Words test corpus, comparing the page of every paragraph with the page Word puts it on:

| Documents | Page count exact | Paragraph on the exact page | Paragraph within ±1 page |
|---|---|---|---|
| All (768) | 95.1% | 71.2% | 91.7% |
| Multi-page with Word markers (161) | 89.4% | 76.0% | 94.7% |
| Multi-page without markers (47) | 59.6% | 57.9% | 85.6% |

The corpus is made of test documents for edge cases, created over 20 years with many Word versions and fonts, so the layout Word saved in them often differs from the layout Word 16 computes today on another machine. When the markers agree with the recorded page count, the extracted pages reproduce the layout of the Word that last saved the document, which is the one its author saw.

## Options

| Option | Default | Description |
|---|---|---|
| `Format` | `Markdown` | `Markdown` or `PlainText` |
| `Pagination` | `Auto` | `Auto`, `RenderedBreaks`, `Estimate`, `ExplicitBreaks`, `None` |
| `PageHeaderFormat` | `<!-- Page {0} -->` | Line introducing each page in `Text`; `{0}` page, `{1}` total; empty for none |
| `HeadersAndFooters` | `Once` | `None`, `Once`, `EveryPage` |
| `TrackedChanges` | `Accept` | `Accept`, `Reject`, `Markup` |
| `Tables` | `Auto` | `Auto` (pipe, HTML when nested), `Markdown`, `Html` |
| `MergedCells` | `Repeat` | Repeat merged cell content in every covered cell, or leave them `Empty` |
| `IncludeFootnotes`, `IncludeComments`, `IncludeHiddenText`, `IncludeTableOfContents`, `IncludeTextBoxes`, `IncludeCharts`, `IncludeSmartArt`, `IncludeEquations`, `IncludeImages`, `IncludeImageData`, `IncludeFormatting`, `IncludeHyperlinks` | | Fine grained control of the content |
| `SkipDecorativeImages`, `MinimumImageSize` | `true`, `0` | Filter images that carry no information |
| `ImageDescriber` | `null` | `Func<DocxImage, string>` that describes an image |
| `UseSystemFonts` | `true` | Read installed fonts to measure text more accurately |

## Architecture

```
src/Wisej.AI.DocX
  DocxExtractor            Public entry point
  Packaging/               Secure package access (no DTD, no resolver), Strict namespace normalization, repair
  Reading/                 Style cascade, numbering, fields, drawings, charts, SmartArt, OMML to LaTeX, notes
  Model/                   Format independent document model
  Layout/                  Font metrics, line breaking, page placement, paginator
  Rendering/               Markdown and plain text renderers
tests/Wisej.AI.DocX.Tests  Unit tests, building documents from raw WordprocessingML
```

The library targets **.NET Standard 2.0** and **.NET 8**, so it runs on .NET Framework 4.6.2+ and every modern .NET, on Windows, Linux and macOS. Its only dependency is `DocumentFormat.OpenXml` 3.1.0, the same version Wisej.AI uses. The assembly is strong-named (`extension.snk`), so it can be consumed by the strong-named Wisej.AI assembly on .NET Framework.

### Why not Aspose.Words FOSS

[Aspose.Words FOSS](https://github.com/aspose-words-foss/Aspose.Words-FOSS-for-.NET) was evaluated as a base. Its document model is excellent, but its layout engine is not part of the open source edition, and its loader discards the `w:lastRenderedPageBreak` markers, which are the only exact page information a DOCX contains. It also brings SkiaSharp native dependencies. This library reads the package directly to keep every page signal, and implements its own layout estimate.

## Limitations

- Legacy binary `.doc` files and encrypted documents are not supported (an `InvalidDataException` explains why).
- Field results are used as cached in the document: Word recomputes some fields (such as a table of contents) when printing.
- The layout estimate approximates floating objects, frames and East Asian typography; content bound to custom XML is read from its cached value.

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
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace Wisej.AI.DocX.Tests
{
	/// <summary>
	/// Tests the handling of packages: metadata, damaged packages and alternative formats.
	/// </summary>
	public class PackageTests : TestBase
	{
		[Fact]
		public void Metadata_IsRead()
		{
			var result = new DocxBuilder().Styles().App(pages: 3).Body(P(R("Text"))).Extract();

			Assert.Equal(3, result.Metadata.ReportedPageCount);
			Assert.Equal("ICE TEA GROUP", result.Metadata.Company);
			Assert.Equal("Microsoft Office Word", result.Metadata.Application);
		}

		[Fact]
		public void NonSeekableStreams_AreSupported()
		{
			using (var source = new DocxBuilder().Styles().Body(P(R("Hello"))).Build())
			using (var stream = new ForwardOnlyStream(source))
			{
				var result = new DocxExtractor(new DocxExtractorOptions { Pagination = PaginationMode.None }).Extract(stream);
				Assert.Equal("Hello", result.Text);
			}
		}

		[Fact]
		public void MalformedHyperlinkTargets_DoNotPreventTheExtraction()
		{
			var builder = new DocxBuilder().Styles().Hyperlink("rIdBad", "http://bad uri with spaces/<>").Body(
				P("<w:hyperlink r:id=\"rIdBad\">" + R("broken link") + "</w:hyperlink>"));

			Assert.Contains("broken link", Markdown(builder));
		}

		[Fact]
		public void BackslashEntryNames_AreRepaired()
		{
			var original = new DocxBuilder().Styles().Body(P(R("Recovered"))).Build();
			var damaged = new MemoryStream();

			// Rewrite the package with Windows path separators, as some generators do.
			using (var input = new ZipArchive(original, ZipArchiveMode.Read))
			using (var output = new ZipArchive(damaged, ZipArchiveMode.Create, leaveOpen: true))
			{
				foreach (var entry in input.Entries)
				{
					using (var source = entry.Open())
					using (var target = output.CreateEntry(entry.FullName.Replace('/', '\\')).Open())
						source.CopyTo(target);
				}
			}

			damaged.Position = 0;
			var result = new DocxExtractor(new DocxExtractorOptions { Pagination = PaginationMode.None }).Extract(damaged);

			Assert.Equal("Recovered", result.Text);
		}

		[Fact]
		public void NotADocument_ThrowsInvalidDataException()
		{
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is not a Word document.")))
				Assert.Throws<InvalidDataException>(() => new DocxExtractor().Extract(stream));
		}

		[Fact]
		public void StaticHelper_ReturnsTheText()
		{
			using (var stream = new DocxBuilder().Styles().Body(P(R("Quick"))).Build())
				Assert.Equal("Quick", DocxExtractor.ExtractText(stream, new DocxExtractorOptions { Pagination = PaginationMode.None }));
		}

		/// <summary>
		/// A stream that can only be read forward, like a network stream.
		/// </summary>
		private sealed class ForwardOnlyStream : Stream
		{
			private readonly Stream _inner;

			/// <summary>
			/// Initializes a new instance of the <see cref="ForwardOnlyStream"/> class.
			/// </summary>
			/// <param name="inner">The stream to wrap.</param>
			public ForwardOnlyStream(Stream inner) => _inner = inner;

			/// <inheritdoc/>
			public override bool CanRead => true;

			/// <inheritdoc/>
			public override bool CanSeek => false;

			/// <inheritdoc/>
			public override bool CanWrite => false;

			/// <inheritdoc/>
			public override long Length => throw new NotSupportedException();

			/// <inheritdoc/>
			public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

			/// <inheritdoc/>
			public override void Flush()
			{
			}

			/// <inheritdoc/>
			public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

			/// <inheritdoc/>
			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

			/// <inheritdoc/>
			public override void SetLength(long value) => throw new NotSupportedException();

			/// <inheritdoc/>
			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
		}
	}
}

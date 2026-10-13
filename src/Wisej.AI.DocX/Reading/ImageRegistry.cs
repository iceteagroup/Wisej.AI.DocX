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
using DocumentFormat.OpenXml.Packaging;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Creates the <see cref="DocxImage"/> placements and assigns a file name to every distinct picture.
	/// </summary>
	internal sealed class ImageRegistry
	{
		private readonly Dictionary<string, SharedImage> _images = new Dictionary<string, SharedImage>(StringComparer.OrdinalIgnoreCase);
		private readonly List<DocxImage> _placements;
		private readonly bool _loadData;

		/// <summary>
		/// Initializes a new instance of the <see cref="ImageRegistry"/> class.
		/// </summary>
		/// <param name="placements">The list that receives every placement, in reading order.</param>
		/// <param name="loadData">Whether the image bytes are loaded.</param>
		public ImageRegistry(List<DocxImage> placements, bool loadData)
		{
			_placements = placements;
			_loadData = loadData;
		}

		/// <summary>
		/// Creates the placement of an embedded or linked picture.
		/// </summary>
		/// <param name="source">The part that owns the relationship to the picture.</param>
		/// <param name="embedId">The relationship identifier of an embedded picture. Can be <c>null</c>.</param>
		/// <param name="linkId">The relationship identifier of a linked picture. Can be <c>null</c>.</param>
		/// <param name="location">The location of the placement.</param>
		/// <returns>The placement, or <c>null</c> when the picture cannot be found.</returns>
		public DocxImage Create(OpenXmlPart source, string embedId, string linkId, string location)
		{
			SharedImage shared = null;

			var part = DocxPackage.GetRelatedPart(source, embedId);
			if (part != null)
			{
				var key = part.Uri.OriginalString;
				if (!_images.TryGetValue(key, out shared))
				{
					shared = new SharedImage
					{
						PartUri = key,
						ContentType = part.ContentType,
						FileName = $"image{_images.Count + 1}{GetExtension(part.ContentType, key)}",
						Data = _loadData ? TryGetBytes(part) : null,
					};

					_images[key] = shared;
				}
			}
			else
			{
				var target = DocxPackage.GetExternalTarget(source, linkId ?? embedId);
				if (target == null)
					return null;

				if (!_images.TryGetValue(target, out shared))
				{
					shared = new SharedImage
					{
						PartUri = target,
						ExternalUri = target,
						FileName = $"image{_images.Count + 1}{GetExtension(null, target)}",
					};

					_images[target] = shared;
				}
			}

			var image = new DocxImage
			{
				FileName = shared.FileName,
				ContentType = shared.ContentType,
				Data = shared.Data,
				ExternalUri = shared.ExternalUri,
				PartUri = shared.PartUri,
				Location = location,
			};

			_placements.Add(image);
			return image;
		}

		/// <summary>
		/// Reads the bytes of an image part, ignoring damaged parts.
		/// </summary>
		/// <param name="part">The image part.</param>
		/// <returns>The bytes, or <c>null</c> when the part cannot be read.</returns>
		private static byte[] TryGetBytes(OpenXmlPart part)
		{
			try
			{
				return DocxPackage.GetBytes(part);
			}
			catch (Exception ex) when (!(ex is OutOfMemoryException))
			{
				return null;
			}
		}

		/// <summary>
		/// Returns the file extension of an image from its content type or its name.
		/// </summary>
		/// <param name="contentType">The MIME type. Can be <c>null</c>.</param>
		/// <param name="uri">The part URI or the external target.</param>
		/// <returns>The extension, including the dot.</returns>
		private static string GetExtension(string contentType, string uri)
		{
			switch (contentType?.ToLowerInvariant())
			{
				case "image/png": return ".png";
				case "image/jpeg":
				case "image/jpg": return ".jpg";
				case "image/gif": return ".gif";
				case "image/bmp": return ".bmp";
				case "image/tiff": return ".tif";
				case "image/svg+xml": return ".svg";
				case "image/webp": return ".webp";
				case "image/x-emf":
				case "image/emf": return ".emf";
				case "image/x-wmf":
				case "image/wmf": return ".wmf";
			}

			var path = uri ?? "";
			var query = path.IndexOfAny(new[] { '?', '#' });
			if (query >= 0)
				path = path.Substring(0, query);

			var dot = path.LastIndexOf('.');
			var slash = path.LastIndexOfAny(new[] { '/', '\\' });
			var extension = dot > slash && path.Length - dot <= 6 ? path.Substring(dot).ToLowerInvariant() : ".bin";

			return extension == ".jpeg" ? ".jpg" : extension;
		}

		/// <summary>
		/// The information shared by all the placements of the same picture.
		/// </summary>
		private sealed class SharedImage
		{
			/// <summary>Gets or sets the part URI or external target that identifies the picture.</summary>
			public string PartUri { get; set; }

			/// <summary>Gets or sets the assigned file name.</summary>
			public string FileName { get; set; }

			/// <summary>Gets or sets the MIME type.</summary>
			public string ContentType { get; set; }

			/// <summary>Gets or sets the bytes of the picture.</summary>
			public byte[] Data { get; set; }

			/// <summary>Gets or sets the target of a linked picture.</summary>
			public string ExternalUri { get; set; }
		}
	}
}

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
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Wisej.AI.DocX.Model;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Reads the numbering definitions part and generates the labels of list items.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A paragraph refers to a numbering instance (<c>w:num</c>) and a level. The instance points to an
	/// abstract definition (<c>w:abstractNum</c>) that describes the nine levels: start value, number
	/// format, label template (<c>%1.%2.</c>) and restart rules. Instances can override the start value
	/// or the definition of single levels.
	/// </para>
	/// <para>
	/// Like Word, the counters belong to the abstract definition, so different instances of the same list
	/// continue the numbering, unless an instance overrides the start value: in that case the numbering
	/// restarts the first time the instance is used.
	/// </para>
	/// </remarks>
	internal sealed class NumberingDefinitions
	{
		/// <summary>Matches a level placeholder in a label template.</summary>
		private static readonly Regex Placeholder = new Regex(@"%([1-9])", RegexOptions.CultureInvariant);

		private readonly Dictionary<int, AbstractNumbering> _abstracts = new Dictionary<int, AbstractNumbering>();
		private readonly Dictionary<int, NumberingInstance> _instances = new Dictionary<int, NumberingInstance>();
		private readonly Dictionary<int, int[]> _counters = new Dictionary<int, int[]>();
		private readonly HashSet<int> _usedInstances = new HashSet<int>();
		private readonly StyleSheet _styles;

		/// <summary>
		/// Initializes a new instance of the <see cref="NumberingDefinitions"/> class.
		/// </summary>
		/// <param name="numbering">The root element of the numbering part (<c>w:numbering</c>). Can be <c>null</c>.</param>
		/// <param name="styles">The style sheet, used to follow numbering style links.</param>
		public NumberingDefinitions(XElement numbering, StyleSheet styles)
		{
			_styles = styles;

			if (numbering != null)
				Load(numbering);
		}

		#region Methods

		/// <summary>
		/// Returns the paragraph properties of a list level, which provide the indentation of the items.
		/// </summary>
		/// <param name="numberingId">The numbering instance.</param>
		/// <param name="level">The zero based level.</param>
		/// <returns>The <c>w:pPr</c> element of the level, or <c>null</c>.</returns>
		public XElement GetLevelProperties(int numberingId, int level)
		{
			return GetLevel(numberingId, level)?.ParagraphProperties;
		}

		/// <summary>
		/// Advances the counters of a list and returns the label of the next item.
		/// </summary>
		/// <param name="numberingId">The numbering instance referenced by the paragraph.</param>
		/// <param name="level">The zero based level of the paragraph.</param>
		/// <param name="labelStyle">The formatting of the paragraph mark, used for the label font.</param>
		/// <returns>The label, or <c>null</c> when the instance does not exist or its level has no label.</returns>
		public ListLabel NextLabel(int numberingId, int level, RunStyle labelStyle)
		{
			level = Math.Max(0, Math.Min(8, level));

			if (!_instances.TryGetValue(numberingId, out var instance))
				return null;

			var abstractNumbering = ResolveAbstract(instance.AbstractId);
			if (abstractNumbering == null)
				return null;

			var counters = GetCounters(abstractNumbering.Id);

			// The first use of an instance with start overrides restarts the overridden levels.
			if (_usedInstances.Add(numberingId))
			{
				foreach (var pair in instance.StartOverrides)
					counters[pair.Key] = pair.Value - 1;
			}

			var definition = GetLevel(numberingId, level);
			if (definition == null)
				return null;

			// Levels that were never used start from their start value.
			for (var i = 0; i <= level; i++)
			{
				if (counters[i] == Int32.MinValue)
					counters[i] = (GetLevel(numberingId, i)?.Start ?? 1) - (i == level ? 1 : 0);
			}

			counters[level]++;

			// Deeper levels restart, unless their restart rule says otherwise.
			for (var i = level + 1; i < 9; i++)
			{
				var restart = GetLevel(numberingId, i)?.Restart;
				if (restart == 0)
					continue;

				if (restart == null || level < restart.Value)
					counters[i] = Int32.MinValue;
			}

			var text = FormatLabel(numberingId, definition, counters);
			if (text == null)
				return null;

			return new ListLabel
			{
				Text = text,
				Level = level,
				IsBullet = definition.Format == "bullet",
			};
		}

		#endregion

		#region Implementation

		/// <summary>
		/// Loads the abstract definitions and the instances.
		/// </summary>
		/// <param name="numbering">The root element of the numbering part.</param>
		private void Load(XElement numbering)
		{
			foreach (var e in numbering.Elements(Ns.W + "abstractNum"))
			{
				var id = XmlExtensions.ParseInt(e.WAttr("abstractNumId"));
				if (id == null || _abstracts.ContainsKey(id.Value))
					continue;

				var abstractNumbering = new AbstractNumbering
				{
					Id = id.Value,
					NumberingStyleLink = e.WChildVal("numStyleLink"),
				};

				foreach (var lvl in e.Elements(Ns.W + "lvl"))
				{
					var level = ReadLevel(lvl);
					if (level != null)
						abstractNumbering.Levels[level.Index] = level;
				}

				_abstracts[id.Value] = abstractNumbering;
			}

			foreach (var e in numbering.Elements(Ns.W + "num"))
			{
				var id = XmlExtensions.ParseInt(e.WAttr("numId"));
				var abstractId = XmlExtensions.ParseInt(e.WChildVal("abstractNumId"));
				if (id == null || abstractId == null || _instances.ContainsKey(id.Value))
					continue;

				var instance = new NumberingInstance { Id = id.Value, AbstractId = abstractId.Value };

				foreach (var lvlOverride in e.Elements(Ns.W + "lvlOverride"))
				{
					var index = XmlExtensions.ParseInt(lvlOverride.WAttr("ilvl"));
					if (index == null || index < 0 || index > 8)
						continue;

					var start = XmlExtensions.ParseInt(lvlOverride.WChildVal("startOverride"));
					if (start != null)
						instance.StartOverrides[index.Value] = start.Value;

					var lvl = lvlOverride.Element(Ns.W + "lvl");
					if (lvl != null)
					{
						var level = ReadLevel(lvl);
						if (level != null)
						{
							level.Index = index.Value;
							instance.LevelOverrides[index.Value] = level;

							// A level override with a start value restarts the numbering like a start override.
							if (start == null && lvl.Element(Ns.W + "start") != null)
								instance.StartOverrides[index.Value] = level.Start;
						}
					}
				}

				_instances[id.Value] = instance;
			}
		}

		/// <summary>
		/// Reads a level definition (<c>w:lvl</c>).
		/// </summary>
		/// <param name="lvl">The level element.</param>
		/// <returns>The level, or <c>null</c> when its index is invalid.</returns>
		private static NumberingLevel ReadLevel(XElement lvl)
		{
			var index = XmlExtensions.ParseInt(lvl.WAttr("ilvl"));
			if (index == null || index < 0 || index > 8)
				return null;

			var format = lvl.WChildVal("numFmt") ?? "decimal";

			// Word 2010+ stores custom formats such as "001, 002, 003" in an extension; treat them as decimal.
			if (format == "custom")
				format = "decimal";

			return new NumberingLevel
			{
				Index = index.Value,
				Start = XmlExtensions.ParseInt(lvl.WChildVal("start")) ?? 0,
				Format = format,
				Text = lvl.Element(Ns.W + "lvlText")?.WVal(),
				Restart = XmlExtensions.ParseInt(lvl.WChildVal("lvlRestart")),
				IsLegal = lvl.Element(Ns.W + "isLgl").OnOff() ?? false,
				HasPictureBullet = lvl.Element(Ns.W + "lvlPicBulletId") != null,
				FontFamily = lvl.Element(Ns.W + "rPr")?.Element(Ns.W + "rFonts")?.WAttr("ascii"),
				ParagraphProperties = lvl.Element(Ns.W + "pPr"),
			};
		}

		/// <summary>
		/// Returns the effective definition of a level of an instance, taking the overrides into account.
		/// </summary>
		/// <param name="numberingId">The numbering instance.</param>
		/// <param name="level">The zero based level.</param>
		/// <returns>The level definition, or <c>null</c>.</returns>
		private NumberingLevel GetLevel(int numberingId, int level)
		{
			if (!_instances.TryGetValue(numberingId, out var instance))
				return null;

			if (instance.LevelOverrides.TryGetValue(level, out var overridden))
				return overridden;

			var abstractNumbering = ResolveAbstract(instance.AbstractId);
			return abstractNumbering?.Levels[level];
		}

		/// <summary>
		/// Returns an abstract definition, following its numbering style link when it only refers to a style.
		/// </summary>
		/// <param name="abstractId">The abstract numbering identifier.</param>
		/// <returns>The definition that holds the levels, or <c>null</c>.</returns>
		private AbstractNumbering ResolveAbstract(int abstractId)
		{
			if (!_abstracts.TryGetValue(abstractId, out var abstractNumbering))
				return null;

			if (abstractNumbering.NumberingStyleLink != null
				&& _styles.TryGetStyleNumbering(abstractNumbering.NumberingStyleLink, out var linkedId)
				&& _instances.TryGetValue(linkedId, out var linked)
				&& linked.AbstractId != abstractId
				&& _abstracts.TryGetValue(linked.AbstractId, out var target))
			{
				return target;
			}

			return abstractNumbering;
		}

		/// <summary>
		/// Returns the counters of an abstract definition, creating them on first use.
		/// </summary>
		/// <param name="abstractId">The abstract numbering identifier.</param>
		/// <returns>The nine counters; <see cref="Int32.MinValue"/> marks a level not started yet.</returns>
		private int[] GetCounters(int abstractId)
		{
			if (!_counters.TryGetValue(abstractId, out var counters))
			{
				counters = new int[9];
				for (var i = 0; i < counters.Length; i++)
					counters[i] = Int32.MinValue;

				_counters[abstractId] = counters;
			}

			return counters;
		}

		/// <summary>
		/// Expands the label template of a level with the current counters.
		/// </summary>
		/// <param name="numberingId">The numbering instance.</param>
		/// <param name="definition">The level of the paragraph.</param>
		/// <param name="counters">The current counters.</param>
		/// <returns>The label text, or <c>null</c> when the level displays no label.</returns>
		private string FormatLabel(int numberingId, NumberingLevel definition, int[] counters)
		{
			if (definition.Format == "bullet" || definition.HasPictureBullet)
			{
				var bullet = SymbolMap.MapText(definition.Text ?? "•", definition.FontFamily);
				return String.IsNullOrWhiteSpace(bullet) ? "•" : bullet.Trim();
			}

			if (String.IsNullOrEmpty(definition.Text))
				return null;

			var text = Placeholder.Replace(definition.Text, match =>
			{
				var index = match.Groups[1].Value[0] - '1';
				var value = counters[index] == Int32.MinValue ? (GetLevel(numberingId, index)?.Start ?? 1) : counters[index];
				var format = definition.IsLegal ? "decimal" : GetLevel(numberingId, index)?.Format;

				return NumberFormatter.Format(value, format);
			});

			text = SymbolMap.MapText(text, definition.FontFamily).Trim();
			return text.Length == 0 ? null : text;
		}

		#endregion

		#region Definitions

		/// <summary>
		/// An abstract numbering definition (<c>w:abstractNum</c>).
		/// </summary>
		private sealed class AbstractNumbering
		{
			/// <summary>Gets or sets the abstract numbering identifier.</summary>
			public int Id { get; set; }

			/// <summary>Gets or sets the numbering style that defines the levels, or <c>null</c>.</summary>
			public string NumberingStyleLink { get; set; }

			/// <summary>Gets the nine levels; missing levels are <c>null</c>.</summary>
			public NumberingLevel[] Levels { get; } = new NumberingLevel[9];
		}

		/// <summary>
		/// A numbering instance (<c>w:num</c>).
		/// </summary>
		private sealed class NumberingInstance
		{
			/// <summary>Gets or sets the instance identifier referenced by paragraphs.</summary>
			public int Id { get; set; }

			/// <summary>Gets or sets the abstract definition of the instance.</summary>
			public int AbstractId { get; set; }

			/// <summary>Gets the start values that restart the levels the first time the instance is used.</summary>
			public Dictionary<int, int> StartOverrides { get; } = new Dictionary<int, int>();

			/// <summary>Gets the level definitions that replace the abstract ones.</summary>
			public Dictionary<int, NumberingLevel> LevelOverrides { get; } = new Dictionary<int, NumberingLevel>();
		}

		/// <summary>
		/// A list level (<c>w:lvl</c>).
		/// </summary>
		private sealed class NumberingLevel
		{
			/// <summary>Gets or sets the zero based level index.</summary>
			public int Index { get; set; }

			/// <summary>Gets or sets the start value.</summary>
			public int Start { get; set; }

			/// <summary>Gets or sets the number format.</summary>
			public string Format { get; set; }

			/// <summary>Gets or sets the label template, such as <c>%1.%2.</c>.</summary>
			public string Text { get; set; }

			/// <summary>Gets or sets the one based level after which this level restarts, 0 to never restart, or <c>null</c>.</summary>
			public int? Restart { get; set; }

			/// <summary>Gets or sets whether all the numbers of the label are displayed as decimal (legal numbering).</summary>
			public bool IsLegal { get; set; }

			/// <summary>Gets or sets whether the level uses a picture bullet.</summary>
			public bool HasPictureBullet { get; set; }

			/// <summary>Gets or sets the font of the label, used to decode symbol bullets.</summary>
			public string FontFamily { get; set; }

			/// <summary>Gets or sets the paragraph properties of the level (indentation and tabs).</summary>
			public XElement ParagraphProperties { get; set; }
		}

		#endregion
	}
}

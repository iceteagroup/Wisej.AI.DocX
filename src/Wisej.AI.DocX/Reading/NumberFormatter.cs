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
using System.Globalization;
using System.Text;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Formats numbers using the WordprocessingML number formats (<c>ST_NumberFormat</c>)
	/// used by lists, page numbers and notes.
	/// </summary>
	internal static class NumberFormatter
	{
		private static readonly string[] Ones =
		{
			"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten",
			"eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen",
		};

		private static readonly string[] Tens =
		{
			"", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety",
		};

		private static readonly string[] OrdinalOnes =
		{
			"", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth",
			"eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth", "seventeenth", "eighteenth", "nineteenth",
		};

		private const string ChineseDigits = "〇一二三四五六七八九";

		/// <summary>
		/// Formats a number.
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <param name="format">The number format, such as <c>decimal</c>, <c>lowerRoman</c> or <c>upperLetter</c>.</param>
		/// <returns>The formatted number. Unknown formats fall back to decimal digits.</returns>
		public static string Format(int value, string format)
		{
			switch (format)
			{
				case null:
				case "":
				case "decimal":
					return value.ToString(CultureInfo.InvariantCulture);

				case "none":
				case "bullet":
					return "";

				case "decimalZero":
					return value < 10 && value >= 0 ? "0" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);

				case "upperRoman":
					return ToRoman(value);

				case "lowerRoman":
					return ToRoman(value).ToLowerInvariant();

				case "upperLetter":
					return ToLetters(value, 'A');

				case "lowerLetter":
					return ToLetters(value, 'a');

				case "ordinal":
					return ToOrdinal(value);

				case "cardinalText":
					return Capitalize(ToWords(value));

				case "ordinalText":
					return Capitalize(ToOrdinalWords(value));

				case "decimalEnclosedCircle":
				case "decimalEnclosedCircleChinese":
					return value >= 1 && value <= 20 ? ((char)(0x2460 + value - 1)).ToString() : value.ToString(CultureInfo.InvariantCulture);

				case "decimalEnclosedParen":
					return value >= 1 && value <= 20 ? ((char)(0x2474 + value - 1)).ToString() : $"({value})";

				case "decimalEnclosedFullstop":
					return value >= 1 && value <= 20 ? ((char)(0x2488 + value - 1)).ToString() : $"{value}.";

				case "decimalFullWidth":
				case "decimalFullWidth2":
					return ToFullWidth(value);

				case "ideographDigital":
				case "japaneseDigitalTenThousand":
					return ToChineseDigits(value);

				case "chineseCounting":
				case "chineseCountingThousand":
				case "japaneseCounting":
				case "taiwaneseCounting":
				case "ideographTraditional":
					return ToChineseCounting(value);

				default:
					return value.ToString(CultureInfo.InvariantCulture);
			}
		}

		#region Implementation

		/// <summary>
		/// Formats a number as upper case Roman numerals.
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <returns>The Roman numeral, or decimal digits when out of range.</returns>
		private static string ToRoman(int value)
		{
			if (value <= 0 || value >= 4000)
				return value.ToString(CultureInfo.InvariantCulture);

			int[] values = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
			string[] symbols = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

			var sb = new StringBuilder();
			for (var i = 0; i < values.Length; i++)
			{
				while (value >= values[i])
				{
					sb.Append(symbols[i]);
					value -= values[i];
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Formats a number as letters the way Word does: A..Z, then AA..ZZ, then AAA..ZZZ.
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <param name="first">The first letter, <c>'A'</c> or <c>'a'</c>.</param>
		/// <returns>The letters, or decimal digits when out of range.</returns>
		private static string ToLetters(int value, char first)
		{
			if (value <= 0)
				return value.ToString(CultureInfo.InvariantCulture);

			var letter = (char)(first + (value - 1) % 26);
			var count = (value - 1) / 26 + 1;

			return new string(letter, Math.Min(count, 50));
		}

		/// <summary>
		/// Formats a number with an English ordinal suffix (1st, 2nd, 3rd, 4th).
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <returns>The ordinal number.</returns>
		private static string ToOrdinal(int value)
		{
			var suffix = "th";
			var lastTwo = Math.Abs(value) % 100;

			if (lastTwo < 11 || lastTwo > 13)
			{
				switch (Math.Abs(value) % 10)
				{
					case 1: suffix = "st"; break;
					case 2: suffix = "nd"; break;
					case 3: suffix = "rd"; break;
				}
			}

			return value.ToString(CultureInfo.InvariantCulture) + suffix;
		}

		/// <summary>
		/// Spells out a number in English words.
		/// </summary>
		/// <param name="value">The number to spell.</param>
		/// <returns>The number in words, or decimal digits when out of range.</returns>
		private static string ToWords(int value)
		{
			if (value == 0)
				return "zero";

			if (value < 0 || value >= 1000000)
				return value.ToString(CultureInfo.InvariantCulture);

			var parts = new StringBuilder();

			if (value >= 1000)
			{
				parts.Append(ToWords(value / 1000)).Append(" thousand");
				value %= 1000;
				if (value > 0)
					parts.Append(' ');
			}

			if (value >= 100)
			{
				parts.Append(Ones[value / 100]).Append(" hundred");
				value %= 100;
				if (value > 0)
					parts.Append(" and ");
			}

			if (value >= 20)
			{
				parts.Append(Tens[value / 10]);
				if (value % 10 > 0)
					parts.Append('-').Append(Ones[value % 10]);
			}
			else if (value > 0)
			{
				parts.Append(Ones[value]);
			}

			return parts.ToString();
		}

		/// <summary>
		/// Spells out an ordinal number in English words.
		/// </summary>
		/// <param name="value">The number to spell.</param>
		/// <returns>The ordinal in words.</returns>
		private static string ToOrdinalWords(int value)
		{
			if (value > 0 && value < 20)
				return OrdinalOnes[value];

			if (value >= 20 && value < 100 && value % 10 == 0)
				return Tens[value / 10].TrimEnd('y') + "ieth";

			if (value >= 20 && value < 100)
				return Tens[value / 10] + "-" + OrdinalOnes[value % 10];

			return ToWords(value) + "th";
		}

		/// <summary>
		/// Formats a number with full width digits.
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <returns>The full width digits.</returns>
		private static string ToFullWidth(int value)
		{
			var sb = new StringBuilder();
			foreach (var c in value.ToString(CultureInfo.InvariantCulture))
				sb.Append(Char.IsDigit(c) ? (char)(0xFF10 + c - '0') : c);

			return sb.ToString();
		}

		/// <summary>
		/// Formats a number digit by digit with ideographic digits (一二三 for 123).
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <returns>The ideographic digits.</returns>
		private static string ToChineseDigits(int value)
		{
			var sb = new StringBuilder();
			foreach (var c in value.ToString(CultureInfo.InvariantCulture))
				sb.Append(Char.IsDigit(c) ? ChineseDigits[c - '0'] : c);

			return sb.ToString();
		}

		/// <summary>
		/// Formats a number with ideographic counting (十一 for 11, 二十 for 20).
		/// </summary>
		/// <param name="value">The number to format.</param>
		/// <returns>The ideographic number, or decimal digits when out of range.</returns>
		private static string ToChineseCounting(int value)
		{
			if (value <= 0 || value >= 100)
				return value <= 0 ? value.ToString(CultureInfo.InvariantCulture) : ToChineseDigits(value);

			if (value < 10)
				return ChineseDigits[value].ToString();

			var tens = value / 10;
			var ones = value % 10;

			return (tens > 1 ? ChineseDigits[tens].ToString() : "") + "十" + (ones > 0 ? ChineseDigits[ones].ToString() : "");
		}

		/// <summary>
		/// Capitalizes the first letter of a string.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <returns>The capitalized text.</returns>
		private static string Capitalize(string text)
		{
			return String.IsNullOrEmpty(text) ? text : Char.ToUpperInvariant(text[0]) + text.Substring(1);
		}

		#endregion
	}
}

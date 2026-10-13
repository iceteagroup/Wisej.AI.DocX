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
using System.Xml.Linq;
using Wisej.AI.DocX.Packaging;

namespace Wisej.AI.DocX.Reading
{
	/// <summary>
	/// Converts Office Math Markup Language (OMML) equations to LaTeX.
	/// </summary>
	/// <remarks>
	/// LaTeX is the notation language models know best for mathematics. The converter covers the
	/// OMML structures produced by the Word equation editor: fractions, scripts, radicals, n-ary
	/// operators, delimiters, functions, limits, accents, bars, boxes, group characters, equation
	/// arrays and matrices.
	/// </remarks>
	internal static class MathConverter
	{
		/// <summary>The nesting depth of the conversion in progress on the current thread.</summary>
		[ThreadStatic]
		private static int _depth;

		/// <summary>LaTeX commands for symbols commonly found in equation text.</summary>
		private static readonly Dictionary<char, string> Symbols = new Dictionary<char, string>
		{
			['α'] = @"\alpha",
			['β'] = @"\beta",
			['γ'] = @"\gamma",
			['δ'] = @"\delta",
			['ε'] = @"\varepsilon",
			['ϵ'] = @"\epsilon",
			['ζ'] = @"\zeta",
			['η'] = @"\eta",
			['θ'] = @"\theta",
			['ϑ'] = @"\vartheta",
			['ι'] = @"\iota",
			['κ'] = @"\kappa",
			['λ'] = @"\lambda",
			['μ'] = @"\mu",
			['ν'] = @"\nu",
			['ξ'] = @"\xi",
			['π'] = @"\pi",
			['ρ'] = @"\rho",
			['σ'] = @"\sigma",
			['ς'] = @"\varsigma",
			['τ'] = @"\tau",
			['υ'] = @"\upsilon",
			['φ'] = @"\phi",
			['ϕ'] = @"\phi",
			['χ'] = @"\chi",
			['ψ'] = @"\psi",
			['ω'] = @"\omega",
			['Γ'] = @"\Gamma",
			['Δ'] = @"\Delta",
			['Θ'] = @"\Theta",
			['Λ'] = @"\Lambda",
			['Ξ'] = @"\Xi",
			['Π'] = @"\Pi",
			['Σ'] = @"\Sigma",
			['Υ'] = @"\Upsilon",
			['Φ'] = @"\Phi",
			['Ψ'] = @"\Psi",
			['Ω'] = @"\Omega",
			['≤'] = @"\leq",
			['≥'] = @"\geq",
			['≠'] = @"\neq",
			['≈'] = @"\approx",
			['≡'] = @"\equiv",
			['∼'] = @"\sim",
			['∝'] = @"\propto",
			['×'] = @"\times",
			['÷'] = @"\div",
			['±'] = @"\pm",
			['∓'] = @"\mp",
			['⋅'] = @"\cdot",
			['·'] = @"\cdot",
			['∞'] = @"\infty",
			['∂'] = @"\partial",
			['∇'] = @"\nabla",
			['∈'] = @"\in",
			['∉'] = @"\notin",
			['⊂'] = @"\subset",
			['⊆'] = @"\subseteq",
			['⊃'] = @"\supset",
			['⊇'] = @"\supseteq",
			['∪'] = @"\cup",
			['∩'] = @"\cap",
			['∅'] = @"\emptyset",
			['∀'] = @"\forall",
			['∃'] = @"\exists",
			['¬'] = @"\neg",
			['∧'] = @"\wedge",
			['∨'] = @"\vee",
			['→'] = @"\rightarrow",
			['←'] = @"\leftarrow",
			['↔'] = @"\leftrightarrow",
			['⇒'] = @"\Rightarrow",
			['⇐'] = @"\Leftarrow",
			['⇔'] = @"\Leftrightarrow",
			['↦'] = @"\mapsto",
			['…'] = @"\ldots",
			['⋯'] = @"\cdots",
			['⋮'] = @"\vdots",
			['⋱'] = @"\ddots",
			['∘'] = @"\circ",
			['°'] = @"^{\circ}",
			['′'] = "'",
			['″'] = "''",
			['ℏ'] = @"\hbar",
			['ℓ'] = @"\ell",
			['ℝ'] = @"\mathbb{R}",
			['ℕ'] = @"\mathbb{N}",
			['ℤ'] = @"\mathbb{Z}",
			['ℚ'] = @"\mathbb{Q}",
			['ℂ'] = @"\mathbb{C}",
			['−'] = "-",
			['∗'] = "*",
			['⊗'] = @"\otimes",
			['⊕'] = @"\oplus",
			['⊥'] = @"\perp",
			['∥'] = @"\parallel",
			['∠'] = @"\angle",
			['△'] = @"\triangle",
			['≪'] = @"\ll",
			['≫'] = @"\gg",
			['≅'] = @"\cong",
			['≜'] = @"\triangleq",
			['∴'] = @"\therefore",
			['∵'] = @"\because",
			['{'] = @"\{",
			['}'] = @"\}",
			['%'] = @"\%",
			['#'] = @"\#",
			['&'] = @"\&",
			['$'] = @"\$",
			['_'] = @"\_",
			['\\'] = @"\backslash",
		};

		/// <summary>LaTeX commands of the n-ary operators.</summary>
		private static readonly Dictionary<string, string> NaryOperators = new Dictionary<string, string>
		{
			["∑"] = @"\sum",
			["∏"] = @"\prod",
			["∐"] = @"\coprod",
			["∫"] = @"\int",
			["∬"] = @"\iint",
			["∭"] = @"\iiint",
			["∮"] = @"\oint",
			["∯"] = @"\oiint",
			["⋃"] = @"\bigcup",
			["⋂"] = @"\bigcap",
			["⋁"] = @"\bigvee",
			["⋀"] = @"\bigwedge",
			["⨁"] = @"\bigoplus",
			["⨂"] = @"\bigotimes",
		};

		/// <summary>LaTeX commands of the accents, keyed by the combining character.</summary>
		private static readonly Dictionary<string, string> Accents = new Dictionary<string, string>
		{
			["̂"] = @"\hat",
			["̃"] = @"\tilde",
			["̄"] = @"\bar",
			["̅"] = @"\overline",
			["⃗"] = @"\vec",
			["̇"] = @"\dot",
			["̈"] = @"\ddot",
			["̆"] = @"\breve",
			["́"] = @"\acute",
			["̀"] = @"\grave",
			["̌"] = @"\check",
			["⃡"] = @"\overleftrightarrow",
			["⃖"] = @"\overleftarrow",
		};

		/// <summary>Function names that have a LaTeX command.</summary>
		private static readonly HashSet<string> KnownFunctions = new HashSet<string>(StringComparer.Ordinal)
		{
			"sin", "cos", "tan", "cot", "sec", "csc", "arcsin", "arccos", "arctan", "sinh", "cosh", "tanh", "coth",
			"log", "ln", "lg", "exp", "lim", "max", "min", "sup", "inf", "det", "dim", "ker", "deg", "gcd", "arg", "Pr",
		};

		/// <summary>
		/// Converts an equation to LaTeX.
		/// </summary>
		/// <param name="math">An <c>m:oMath</c> or <c>m:oMathPara</c> element.</param>
		/// <returns>The LaTeX source, without delimiters.</returns>
		public static string Convert(XElement math)
		{
			if (math.Name == Ns.M + "oMathPara")
			{
				var equations = math.Elements(Ns.M + "oMath").Select(e => Normalize(Children(e))).Where(e => e.Length > 0).ToList();
				return equations.Count == 1
					? equations[0]
					: @"\begin{gathered}" + String.Join(@" \\ ", equations) + @"\end{gathered}";
			}

			return Normalize(Children(math));
		}

		#region Implementation

		/// <summary>
		/// Converts the child elements of an argument or container.
		/// </summary>
		/// <param name="element">The container element. Can be <c>null</c>.</param>
		/// <returns>The LaTeX source of the children.</returns>
		private static string Children(XElement element)
		{
			if (element == null || _depth >= ReadLimits.MaxNestingDepth)
				return "";

			_depth++;
			try
			{
				var sb = new StringBuilder();
				foreach (var child in element.ContentElements())
					sb.Append(Node(child));

				return sb.ToString();
			}
			finally
			{
				_depth--;
			}
		}

		/// <summary>
		/// Converts a single OMML element.
		/// </summary>
		/// <param name="e">The element to convert.</param>
		/// <returns>The LaTeX source of the element.</returns>
		private static string Node(XElement e)
		{
			if (e.Name.Namespace == Ns.W)
				return e.Name.LocalName == "r" ? Text(String.Concat(e.Elements(Ns.W + "t").Select(t => t.Value)), false) : "";

			if (e.Name.Namespace != Ns.M)
				return "";

			switch (e.Name.LocalName)
			{
				case "r":
					return Run(e);

				case "f":
					return Fraction(e);

				case "sSup":
					return Group(Arg(e, "e")) + "^" + Group(Arg(e, "sup"));

				case "sSub":
					return Group(Arg(e, "e")) + "_" + Group(Arg(e, "sub"));

				case "sSubSup":
					return Group(Arg(e, "e")) + "_" + Group(Arg(e, "sub")) + "^" + Group(Arg(e, "sup"));

				case "sPre":
					return "{}_" + Group(Arg(e, "sub")) + "^" + Group(Arg(e, "sup")) + Group(Arg(e, "e"));

				case "rad":
					return Radical(e);

				case "nary":
					return Nary(e);

				case "d":
					return Delimiter(e);

				case "func":
					return Function(e);

				case "limLow":
					return Limit(e, lower: true);

				case "limUpp":
					return Limit(e, lower: false);

				case "acc":
					return Accent(e);

				case "bar":
					return (Property(e, "barPr", "pos") == "top" ? @"\overline" : @"\underline") + "{" + Arg(e, "e") + "}";

				case "borderBox":
					return @"\boxed{" + Arg(e, "e") + "}";

				case "groupChr":
					return GroupCharacter(e);

				case "eqArr":
					return @"\begin{aligned}" + String.Join(@" \\ ", e.Elements(Ns.M + "e").Select(Children)) + @"\end{aligned}";

				case "m":
					return Matrix(e);

				case "box":
				case "phant":
				case "e":
				case "oMath":
					return Children(e);

				default:
					// Property elements (*Pr) and unknown elements carry no content of their own.
					return e.Name.LocalName.EndsWith("Pr", StringComparison.Ordinal) ? "" : Children(e);
			}
		}

		/// <summary>
		/// Converts a math run (<c>m:r</c>).
		/// </summary>
		/// <param name="e">The run element.</param>
		/// <returns>The LaTeX source of the run text.</returns>
		private static string Run(XElement e)
		{
			var text = String.Concat(e.Elements(Ns.M + "t").Select(t => t.Value));
			var rPr = e.Element(Ns.M + "rPr");
			var isNormalText = rPr?.Element(Ns.M + "nor") != null || rPr?.Element(Ns.M + "sty")?.Attribute(Ns.M + "val")?.Value == "p" && text.Length > 1;

			return Text(text, isNormalText);
		}

		/// <summary>
		/// Converts equation text, replacing symbols with their LaTeX commands.
		/// </summary>
		/// <param name="text">The text.</param>
		/// <param name="isNormalText">Whether the text is regular (non-math) text, written with <c>\text{}</c>.</param>
		/// <returns>The LaTeX source.</returns>
		private static string Text(string text, bool isNormalText)
		{
			if (String.IsNullOrEmpty(text))
				return "";

			if (isNormalText && text.Any(Char.IsLetter))
				return @"\text{" + text.Replace("{", @"\{").Replace("}", @"\}") + "}";

			var sb = new StringBuilder();
			foreach (var c in text)
			{
				if (Symbols.TryGetValue(c, out var command))
				{
					sb.Append(command);

					// Separate a command from a following letter: \alpha x, not \alphax.
					if (command.Length > 1 && Char.IsLetter(command[command.Length - 1]))
						sb.Append(' ');
				}
				else
				{
					sb.Append(c);
				}
			}

			return sb.ToString();
		}

		/// <summary>
		/// Converts a fraction (<c>m:f</c>).
		/// </summary>
		/// <param name="e">The fraction element.</param>
		/// <returns>The LaTeX source of the fraction.</returns>
		private static string Fraction(XElement e)
		{
			var numerator = Arg(e, "num");
			var denominator = Arg(e, "den");

			switch (Property(e, "fPr", "type"))
			{
				case "noBar":
					return @"\binom{" + numerator + "}{" + denominator + "}";

				case "lin":
				case "skw":
					return Group(numerator) + "/" + Group(denominator);

				default:
					return @"\frac{" + numerator + "}{" + denominator + "}";
			}
		}

		/// <summary>
		/// Converts a radical (<c>m:rad</c>).
		/// </summary>
		/// <param name="e">The radical element.</param>
		/// <returns>The LaTeX source of the radical.</returns>
		private static string Radical(XElement e)
		{
			var degree = Arg(e, "deg");
			var hidden = XmlExtensions.ParseOnOff(Property(e, "radPr", "degHide"), false);

			return hidden || degree.Length == 0
				? @"\sqrt{" + Arg(e, "e") + "}"
				: @"\sqrt[" + degree + "]{" + Arg(e, "e") + "}";
		}

		/// <summary>
		/// Converts an n-ary operator (<c>m:nary</c>): sums, products, integrals.
		/// </summary>
		/// <param name="e">The n-ary element.</param>
		/// <returns>The LaTeX source of the operator and its operand.</returns>
		private static string Nary(XElement e)
		{
			var character = Property(e, "naryPr", "chr") ?? "∫";
			var command = NaryOperators.TryGetValue(character, out var name) ? name : Text(character, false);

			var sb = new StringBuilder(command);

			var sub = Arg(e, "sub");
			if (sub.Length > 0 && !XmlExtensions.ParseOnOff(Property(e, "naryPr", "subHide"), false))
				sb.Append("_").Append(Group(sub));

			var sup = Arg(e, "sup");
			if (sup.Length > 0 && !XmlExtensions.ParseOnOff(Property(e, "naryPr", "supHide"), false))
				sb.Append("^").Append(Group(sup));

			return sb.Append(' ').Append(Arg(e, "e")).ToString();
		}

		/// <summary>
		/// Converts a delimiter (<c>m:d</c>): parentheses, brackets, braces, bars.
		/// </summary>
		/// <param name="e">The delimiter element.</param>
		/// <returns>The LaTeX source of the delimited expression.</returns>
		private static string Delimiter(XElement e)
		{
			var begin = Property(e, "dPr", "begChr") ?? "(";
			var end = Property(e, "dPr", "endChr") ?? ")";
			var separator = Property(e, "dPr", "sepChr") ?? "|";

			var items = e.Elements(Ns.M + "e").Select(Children);
			return @"\left" + Fence(begin) + String.Join(Fence(separator), items) + @"\right" + Fence(end);
		}

		/// <summary>
		/// Converts a delimiter character to its LaTeX form.
		/// </summary>
		/// <param name="character">The delimiter character; empty for an invisible delimiter.</param>
		/// <returns>The LaTeX delimiter.</returns>
		private static string Fence(string character)
		{
			switch (character)
			{
				case "": return ".";
				case "{": return @"\{";
				case "}": return @"\}";
				case "⟨":
				case "〈": return @"\langle ";
				case "⟩":
				case "〉": return @"\rangle ";
				case "‖": return @"\|";
				case "⌈": return @"\lceil ";
				case "⌉": return @"\rceil ";
				case "⌊": return @"\lfloor ";
				case "⌋": return @"\rfloor ";
				default: return character;
			}
		}

		/// <summary>
		/// Converts a function application (<c>m:func</c>).
		/// </summary>
		/// <param name="e">The function element.</param>
		/// <returns>The LaTeX source of the function and its argument.</returns>
		private static string Function(XElement e)
		{
			var name = Arg(e, "fName").Trim();
			var plainName = name.Replace(@"\text{", "").Replace("}", "");

			if (KnownFunctions.Contains(plainName))
				name = "\\" + plainName;
			else if (plainName.Length > 1 && plainName.All(Char.IsLetter))
				name = @"\operatorname{" + plainName + "}";

			return name + " " + Group(Arg(e, "e"));
		}

		/// <summary>
		/// Converts a lower or upper limit (<c>m:limLow</c>, <c>m:limUpp</c>).
		/// </summary>
		/// <param name="e">The limit element.</param>
		/// <param name="lower">Whether the limit is below the base.</param>
		/// <returns>The LaTeX source of the limit.</returns>
		private static string Limit(XElement e, bool lower)
		{
			var body = Arg(e, "e").Trim();
			var limit = Arg(e, "lim");

			if (body == @"\lim" || body == "lim" || body == @"\max" || body == @"\min" || body.StartsWith(@"\operatorname"))
				return (body.StartsWith("\\") ? body : "\\" + body) + (lower ? "_" : "^") + Group(limit);

			return (lower ? @"\underset{" : @"\overset{") + limit + "}{" + body + "}";
		}

		/// <summary>
		/// Converts an accent (<c>m:acc</c>).
		/// </summary>
		/// <param name="e">The accent element.</param>
		/// <returns>The LaTeX source of the accented expression.</returns>
		private static string Accent(XElement e)
		{
			var character = Property(e, "accPr", "chr") ?? "̂";
			var command = Accents.TryGetValue(character, out var name) ? name : @"\hat";

			return command + "{" + Arg(e, "e") + "}";
		}

		/// <summary>
		/// Converts a group character (<c>m:groupChr</c>): braces over or under an expression.
		/// </summary>
		/// <param name="e">The group character element.</param>
		/// <returns>The LaTeX source of the grouped expression.</returns>
		private static string GroupCharacter(XElement e)
		{
			var character = Property(e, "groupChrPr", "chr") ?? "⏟";
			var top = Property(e, "groupChrPr", "pos") == "top";

			if (character == "⏞" || (character == "⏟" && top))
				return @"\overbrace{" + Arg(e, "e") + "}";

			if (character == "⏟")
				return @"\underbrace{" + Arg(e, "e") + "}";

			return (top ? @"\overset{" : @"\underset{") + Text(character, false) + "}{" + Arg(e, "e") + "}";
		}

		/// <summary>
		/// Converts a matrix (<c>m:m</c>).
		/// </summary>
		/// <param name="e">The matrix element.</param>
		/// <returns>The LaTeX source of the matrix.</returns>
		private static string Matrix(XElement e)
		{
			var rows = e.Elements(Ns.M + "mr").Select(r => String.Join(" & ", r.Elements(Ns.M + "e").Select(Children)));
			return @"\begin{matrix}" + String.Join(@" \\ ", rows) + @"\end{matrix}";
		}

		/// <summary>
		/// Converts the argument element with the specified local name.
		/// </summary>
		/// <param name="e">The parent element.</param>
		/// <param name="name">The local name of the argument (<c>e</c>, <c>num</c>, <c>sub</c>, ...).</param>
		/// <returns>The LaTeX source of the argument, or an empty string.</returns>
		private static string Arg(XElement e, string name)
		{
			return Children(e.Element(Ns.M + name));
		}

		/// <summary>
		/// Returns the <c>m:val</c> of a child of a property element, such as <c>m:naryPr/m:chr</c>.
		/// </summary>
		/// <param name="e">The parent element.</param>
		/// <param name="propertiesName">The local name of the property element.</param>
		/// <param name="propertyName">The local name of the property.</param>
		/// <returns>The value, or <c>null</c> when not specified.</returns>
		private static string Property(XElement e, string propertiesName, string propertyName)
		{
			var property = e.Element(Ns.M + propertiesName)?.Element(Ns.M + propertyName);
			if (property == null)
				return null;

			return property.Attribute(Ns.M + "val")?.Value ?? "on";
		}

		/// <summary>
		/// Wraps an expression in braces unless it is a single character.
		/// </summary>
		/// <param name="latex">The expression.</param>
		/// <returns>The expression, grouped when needed.</returns>
		private static string Group(string latex)
		{
			latex = latex.Trim();
			return latex.Length == 1 ? latex : "{" + latex + "}";
		}

		/// <summary>
		/// Collapses the white space of the generated source.
		/// </summary>
		/// <param name="latex">The generated source.</param>
		/// <returns>The normalized source.</returns>
		private static string Normalize(string latex)
		{
			var sb = new StringBuilder(latex.Length);
			var previousSpace = false;

			foreach (var c in latex)
			{
				var isSpace = Char.IsWhiteSpace(c);
				if (isSpace && previousSpace)
					continue;

				sb.Append(isSpace ? ' ' : c);
				previousSpace = isSpace;
			}

			return sb.ToString().Trim();
		}

		#endregion
	}
}

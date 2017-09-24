using System;
using System.Collections.Generic;
using System.Linq;

public class Parser
{
	public const int SemP = 0, EndP = 5, ParP = 10, ComP = 20, LogP = 30, RelP = 40, AddP = 50, TermP = int.MaxValue;
	public const int OneLevelOfPrecendence = 100;

	public static readonly Dictionary<char, int> PrecendenceRules = new Dictionary<char, int>
	{
		{';', SemP},

		{'{', EndP},

		{'(', ParP},
		{')', ParP},
	
		{',', ComP},
		{'?', ComP},
		{'@', ComP},
		
		{'&', LogP},
		{'|', LogP},
		
		{'<', RelP},
		{'>', RelP},
		{'!', RelP},
		{':', RelP},
		{'=', RelP},
		
		{'+', AddP},
		{'-', AddP},
		{'*', AddP},
		{'/', AddP},
		
		{'"', TermP},
	};
	char[] Operators = PrecendenceRules.Select(kv => kv.Key).Concat(new[] { ' ', '\t', '\n', '\r' }).ToArray();
	char[] Whitespace = new[] {' ', '\t', '\n', '\r'};
	
	PrecedenceRuling thisPrecedenceRuling = null;
	RuleDescription thisRuleDescription = null;

	Rule thisRule = new Rule();
	AbstractSyntaxTree current = null;
	RulePart rulePart = RulePart.unknown;
	int linenumber = 1;
	string Volume = "", Book = "", Chapter = "", Section = "";

	public AbstractSyntaxTree Parse(string sourceCode)
	{
		int i, j, k, numParensInside = 1, numBracesInside=0, len = sourceCode.Length;
		char nextChar = ' ';
		bool expectingOperandNext = true;
		const string whitespace = " \t\n\r";

		for (i = 0; i < len; i++)
		{
			nextChar = sourceCode[i];
			switch (nextChar)
			{
				case '\n':
					linenumber++;
					var s = sourceCode.Substring(i);
					if (!s.StartsWith("\nVolume ") && !s.StartsWith("\nBook ") && !s.StartsWith("\nChapter ") && !s.StartsWith("\nSection "))
						continue;
					if (rulePart != RulePart.unknown)
						DefineRulePart(thisRule, rulePart, current);
					DefineRulePart(thisRule, RulePart.end, current);
					j = sourceCode.IndexOf('\n', i + 1);
					if (j == -1)
						j = len - 1;
					var header = sourceCode.Substring(i + 1, j - i);
					i = j - 1;
					if (s.StartsWith("\nVolume "))
					{
						Volume = header;
						Book = Chapter = Section = "";
					}
					;
					if (s.StartsWith("\nBook "))
					{
						Book = header;
						Chapter = Section = "";
					}
					if (s.StartsWith("\nChapter "))
					{
						Chapter = header;
						Section = "";
					}
					if (s.StartsWith("\nSection "))
					{
						Section = header;
					}
					continue;
				case '\t':
				case '\r':
				case ' ':
					continue;

				case '(':
					numParensInside++;
					if (!expectingOperandNext)
					{
						current = current.AppendOperator("(", numParensInside + numBracesInside);
						expectingOperandNext = true;
					}
					continue;
				case ',':
					current = current.AppendOperator(",", numParensInside + numBracesInside);
					break;
				case ')':
					numParensInside--;
					if (numParensInside < 0)
						throw new Exception("Too many ) closing parenthesis at position " + i + " line " + linenumber);
					continue;
				case '?':
					if (numBracesInside == 0)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the ? operation is only valid in the braces of the AS portion of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					current = current.AppendOperator("?", numParensInside + numBracesInside);
					break;
				case '@':
					if (numBracesInside == 0)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the @ operation is only valid in the braces of the AS portion of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					current = current.AppendOperator("@", numParensInside + numBracesInside);
					break;

				case '<':
					if (numBracesInside == 0 && rulePart != RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the < operation is only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					if (sourceCode[i + 1] == '=' || sourceCode[i + 1] == '>')
						current = current.AppendOperator(sourceCode.Substring(i++, 2), numParensInside + numBracesInside); // for <= and <> 
					else
						current = current.AppendOperator("<", numParensInside + numBracesInside);
					break;
				case '>':
					if (numBracesInside == 0 && rulePart != RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the > operation is only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					if (sourceCode[i + 1] == '=')
					{
						current = current.AppendOperator(">=", numParensInside + numBracesInside);
						i++;
					}
					else
						current = current.AppendOperator(">", numParensInside + numBracesInside);
					break;
				case '=':
					if (numBracesInside == 0 && rulePart != RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the = equals operation is only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					if (sourceCode[i + 1] == nextChar)
						i++; // for ==
					current = current.AppendOperator("==", numParensInside + numBracesInside);
					break;
				case ':':
					if (sourceCode[i + 1] != '=')
						throw new Exception("Found a : colon but not := assignment at position " + i + " line " + linenumber);
					if (numBracesInside == 0 && rulePart != RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the := assignment operation is only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					i++;
					current = current.AppendOperator(":=", numParensInside + numBracesInside);
					break;
				case '!':
					if (numBracesInside == 0 && rulePart != RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "the != operation is only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					if (sourceCode[i + 1] != '=')
						goto ParseOperand;
					i++;
					current = current.AppendOperator("<>", numParensInside + numBracesInside);
					break;
				case '&':
				case '|':
					if (numBracesInside==0  && rulePart!= RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "&& and || operations are only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					if (sourceCode[i + 1] == nextChar)
						i++; // for && and || 
					current = current.AppendOperator(nextChar.ToString(), numParensInside + numBracesInside);
					break;
				case '+':
				case '-':
				case '*':
				case '/':
					if (numBracesInside==0 && rulePart!= RulePart.If)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "Math operations are only valid in the AS and IF portions of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					current = current.AppendOperator(nextChar.ToString(), numParensInside + numBracesInside);
					break;

				case 'p':
					if (!sourceCode.Substring(i).StartsWith("private "))
						goto ParseOperand;
					i += 7;
					if (rulePart != RulePart.unknown)
						DefineRulePart(thisRule, rulePart, current);
					DefineRulePart(thisRule, RulePart.end, current);
					rulePart = RulePart.Do;
					thisRule.IsPrivateToFile = Program.CurrentFileScope;
					expectingOperandNext = true;
					continue;
					//case 't':
					//	if (sourceCode[i + 1] != 'h' || sourceCode[i + 2] != 'e' || sourceCode[i + 3] != 'n' || !whitespace.Contains(sourceCode[i + 4]))
					//		goto ParseOperand;
					//	i += 2;
					//	DefineRulePart(thisRule, rulePart, current);
					//	rulePart = RulePart.Do;
					//	expectingOperandNext = true;
					//	continue;

				case 'd':
					if (sourceCode[i + 1] != 'o')
						goto ParseOperand;
					if (!whitespace.Contains(sourceCode[i + 2]))
						goto ParseOperand;
					i += 2;
					if (rulePart != RulePart.unknown)
						DefineRulePart(thisRule, rulePart, current);
					DefineRulePart(thisRule, RulePart.end, current);
					rulePart = RulePart.Do;
					expectingOperandNext = true;
					continue;
				case 'a':
					if (sourceCode[i + 1] != 's')
						goto ParseOperand;
					if (!whitespace.Contains(sourceCode[i + 2]))
						goto ParseOperand;
					i += 2;
					DefineRulePart(thisRule, rulePart, current);
					rulePart = RulePart.As;
					expectingOperandNext = true;
					continue;
					//case 'w':
					//	if (sourceCode[i + 1] != 'h' || sourceCode[i+2] != 'e' || sourceCode[i+3]!='n' || !whitespace.Contains(sourceCode[i + 4]))
					//		goto ParseOperand;
					//	i += 4;
					//	if (rulePart != RulePart.unknown)
					//		DefineRulePart(thisRule, rulePart, current);
					//	DefineRulePart(thisRule, RulePart.end, current);
					//	rulePart = RulePart.If;
					//	expectingOperandNext = true;
					//	continue;
				case 'i':
					if (sourceCode[i + 1] != 'f')
						goto ParseOperand;
					if (!whitespace.Contains(sourceCode[i + 2]))
						goto ParseOperand;
					i += 2;
					if (numBracesInside==0)
					{
						DefineRulePart(thisRule, rulePart, current);
						rulePart = RulePart.If;
					}
					expectingOperandNext = true;
					continue;

				case 'r':
					if (!sourceCode.Substring(i).StartsWith("rules "))
						goto ParseOperand;
					i += 5;
					if (thisPrecedenceRuling != null)
						throw new Exception("Please finish one precedence rule before starting another.");
					if (rulePart != RulePart.unknown)
						DefineRulePart(thisRule, rulePart, current);
					DefineRulePart(thisRule, RulePart.end, current);
					thisPrecedenceRuling = new PrecedenceRuling();
					thisRuleDescription = new RuleDescription();
					for (; i < len; i++)
					{
						switch (sourceCode[i])
						{
							case 'r':
								if (!sourceCode.Substring(i).StartsWith("reading "))
									throw new Exception("I was expecting that 'r' after 'rules' to be 'reading': "+sourceCode.Substring(i,20));
								i += 8;
								j = sourceCode.IndexOfAny(Whitespace, i);
								thisRuleDescription.ReadInTheBody = sourceCode.Substring(i, j - i).Trim();
								i = j - 1;
								break;
							case 'c':
								if (sourceCode.Substring(i).StartsWith("checking "))
								{
									i += 9;
									j = sourceCode.IndexOfAny(Whitespace, i); 
									thisRuleDescription.CheckedInTheCondition = sourceCode.Substring(i, j - i).Trim();
									i = j - 1;
								}
								else if (sourceCode.Substring(i).StartsWith("changing "))
								{
									i += 9;
									j = sourceCode.IndexOfAny(Whitespace, i);
									thisRuleDescription.ChangedByTheBody = sourceCode.Substring(i, j - i).Trim();
									i = j - 1;
								}
								else
									throw new Exception("I was expecting that 'c' after 'rules' to be 'checking' or 'changing': " + sourceCode.Substring(i, 20));
								break;
							case 'i':
								if (!sourceCode.Substring(i).StartsWith("in "))
									throw new Exception("I was expecting that 'i' to be 'in' as in 'rules in': " + sourceCode.Substring(i, 20));
								i += 3;
								j = sourceCode.IndexOf((thisPrecedenceRuling.First == null ? " precede rules " : "\n"), i);
								thisRuleDescription.UnderThisHeading = sourceCode.Substring(i, j - i).Trim();
								i = j - 1;
								break;
							case 'n':
								if (!sourceCode.Substring(i).StartsWith("named "))
									throw new Exception("I was expecting that 'n' to be 'named' as in 'rules named': " + sourceCode.Substring(i, Math.Min(30, len - i)));
								i += 6;
								j = sourceCode.IndexOfAny(Whitespace, i);
								thisRuleDescription.NamedLike = sourceCode.Substring(i, j - i).Trim();
								i = j - 1;
								break;
							case '\r':
							case '\t':
							case ' ':
								continue;
							case 'p':
								if (!sourceCode.Substring(i).StartsWith("precede rules "))
									throw new Exception("I was expecting 'precede rules' at this point instead of " + sourceCode.Substring(i, Math.Min(30, len - i)));
								i += 13;
								thisPrecedenceRuling.First = thisRuleDescription;
								thisRuleDescription = new RuleDescription();
								break;
							case '\n':
								linenumber++;
								thisPrecedenceRuling.Second = thisRuleDescription;
								thisRuleDescription = null;
								if (thisPrecedenceRuling.First == null)
									throw new Exception("This precedence rule is missing the first half of its definition.");
								if (thisPrecedenceRuling.Second == null)
									throw new Exception("This precedence rule is missing the second half of its definition.");
								Program.PrecedenceRulings.Add(thisPrecedenceRuling);
								thisPrecedenceRuling = null;
								goto RuleFinished;
						}
					}
					RuleFinished:
					continue;

				case '{':
					if (rulePart != RulePart.As)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "The { opening brace is only valid in the AS portion of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					numBracesInside++;
					if (numBracesInside >= 2) current = current.AppendOperator("{", numParensInside + numBracesInside);
					continue;
				case ';':
					if (numBracesInside == 0)
						throw new Exception("Misplaced ; semicolon outside of { braces } at position " + i + " line " + linenumber+": "+sourceCode.Substring(i,Math.Min(30,len-i)));
					current = current.AppendOperator(";", numParensInside + numBracesInside);
					break;
				case '}':
					if (rulePart != RulePart.As)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "The { opening brace is only valid in the AS portion of a rule, at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					numBracesInside--;
					if (numBracesInside >= 1) current = current.AppendOperand("}");
					expectingOperandNext = false;
					continue;

				case '"':
					j = sourceCode.IndexOf('"', i + 1);
					if (j == -1) throw new Exception("Unmatched \" at position " + i + " line " + linenumber);
					while (sourceCode[j - 1] == '\\')
						j = sourceCode.IndexOf('"', j + 1);
					var literalString = sourceCode.Substring(i + 1, j - i - 1).Replace("\\\"", "\"").Replace("\\n", "\n");
					current = current.AppendOperand(literalString, ValueTypes.text);
					i = j;
					expectingOperandNext = false;
					continue;

				default:
					ParseOperand:
					if (!expectingOperandNext)
						throw new Exception((thisRule.Atom == null ? "" : "In " + thisRule.Atom + ": ") + "Two operands in a row on line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
					j = sourceCode.IndexOfAny(Operators, i);
					if (j == -1)
						j = len;
					for (k = j; k >= i && whitespace.Contains(sourceCode[k - 1].ToString()); k--) // measures trailing whitespace 
						;
					current = current.AppendOperand(sourceCode.Substring(i, k - i));
					i = j - 1;
					expectingOperandNext = false;
					continue;
			}
			if (expectingOperandNext)
				throw new Exception("Two operators next to each other. Second operator is at position " + i + " line " + linenumber + ": " + sourceCode.Substring(i, Math.Min(30, len - i)));
			expectingOperandNext = true;
		}
		if (rulePart != RulePart.unknown)
			DefineRulePart(thisRule, rulePart, current);
		DefineRulePart(thisRule, RulePart.end, current);
		return current;
	}

	void DefineRulePart(Rule r, RulePart part, AbstractSyntaxTree tree)
	{
		switch (part)
		{
			case RulePart.Do:
				r.Atom = tree.ToString();
				break;
			case RulePart.As:
				r.Code = tree;
				break;
			case RulePart.If:
				r.Condition = (r.Condition == null) ? tree : new AbstractSyntaxTree("&") {Left = tree, Right = r.Condition};
				break;
			default:
				throw new Exception("Error: expected DO, AS, or IF here.");
			case RulePart.end:
				if (r.Atom == null && r.Condition == null && r.Code != null)
					throw new Exception("A rule requires either a name or a condition. This code part cannot stand alone: " + r.Code);
				if (r.Atom == null && r.Condition != null && r.Code == null)
					throw new Exception("A rule requires either a name or a value. This conditional part cannot stand alone: " + r.Condition);
				r.Location = Volume + Book + Chapter + Section;
				if (Volume.EndsWith(" private\r\n") || Book.EndsWith(" private\r\n") || Chapter.EndsWith(" private\r\n") || Section.EndsWith(" private\r\n"))
					r.IsPrivateToFile = Program.CurrentFileScope;
				Program.AllRules.Add(r);
				thisRule = new Rule();
				break;
		}
		current = null;
		rulePart = RulePart.unknown;
	}
}

public static class ExtensionMethods // so we can call these methods on NULL instances of AbstractSyntaxTree
{
	public static AbstractSyntaxTree AppendOperator(this AbstractSyntaxTree root, string operatorValue, int numParensInside, AbstractSyntaxTree rightChild = null)
	{
		var newOpNode = new AbstractSyntaxTree(operatorValue)
		{
			Precedence = Parser.PrecendenceRules[operatorValue[0]] + (numParensInside * Parser.OneLevelOfPrecendence),
		};
		if (root == null || root.IsOperand || root.Precedence >= newOpNode.Precedence)
		{
			newOpNode.Left = root;
			newOpNode.Right = rightChild;
			return newOpNode;
		}
		var current = root;
		while (current.Right != null && current.Right.Precedence < newOpNode.Precedence)
			current = current.Right;
		newOpNode.Left = current.Right;
		current.Right = newOpNode;
		return root;
	}

	public static AbstractSyntaxTree AppendOperand(this AbstractSyntaxTree root, string operandValue, ValueTypes vt = ValueTypes.code)
	{
		decimal d;
		bool bResult;
		if (decimal.TryParse(operandValue, out d))
			vt = ValueTypes.number;
		else if (bool.TryParse(operandValue, out bResult))
			vt = ValueTypes.boolean;
		else if (operandValue.StartsWith("\"") && operandValue.EndsWith("\""))
		{
			vt = ValueTypes.text;
			operandValue = operandValue.Substring(1, operandValue.Length - 2);
		}
		if (root == null)
			return new AbstractSyntaxTree(operandValue, vt);
		var current = root;
		while (current.Right != null)
			current = current.Right;
		current.Right = new AbstractSyntaxTree(operandValue, vt);
		return root;
	}
}

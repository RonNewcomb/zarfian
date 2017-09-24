using System;
using System.Collections.Generic;
using System.Linq;

class Interpreter
{
    readonly List<Rule> AllRules;
	readonly List<Node> OrderOfExecution;
	readonly Dictionary<RuleDescription, List<Rule>> DescriptionContainTheseRules;
	readonly Rule ThePlayersCommand;
	readonly Stack<List<Rule>> LocalVariables = new Stack<List<Rule>>();
	readonly AbstractSyntaxTree Yes = new AbstractSyntaxTree("true", ValueTypes.boolean);
	readonly AbstractSyntaxTree No = new AbstractSyntaxTree("false", ValueTypes.boolean);
	int RulesDebuggingCommand = 0;
	int RulebooksDebuggingCommand = 0;
	const string FillInThisBlank = "_";

	public Interpreter(List<Node> orderOfExecution, Dictionary<RuleDescription, List<Rule>> descriptionContainTheseRules, List<Rule> allRules)
	{
		OrderOfExecution = orderOfExecution;
		DescriptionContainTheseRules = descriptionContainTheseRules;
		AllRules = allRules;
		ThePlayersCommand = AllRules.Find(r => r.Atom == "ThePlayersCommand");
	}

	public void RunProgram()
    {
		Console.WriteLine("\n=== Start Program ==========\n");
		EvalCode(AllRules.Find(r => r.Atom == "ProgramStart").Code);
		var abeyedRules = new Stack<List<Rule>>();
		while (string.Compare(ThePlayersCommand.Code.Value, "quit", StringComparison.InvariantCultureIgnoreCase) != 0)
	    {
			abeyedRules.Clear();
		    foreach (Node groupBeginOrEnd in OrderOfExecution)
		    {
				RuleDescription description = groupBeginOrEnd.term.RuleDescription;
				if (groupBeginOrEnd.term.Begins == 1) // BEGIN
			    {
					if (RulebooksDebuggingCommand > 0) Console.WriteLine("[Rulebook \"" + description + "\" begins.]");
					abeyedRules.Push(DescriptionContainTheseRules.ContainsKey(description) ? DescriptionContainTheseRules[description] : new List<Rule>());
				}
			    else // END -- only run rules in this group which haven't been run already
				{
					List<Rule> previouslyScheduledRules = abeyedRules.Pop();
					// if group A wholly contains group B, then after group B runs its rules, do NOT re-run the B rules despite them also being in A
					foreach (Rule rule in previouslyScheduledRules)
						foreach (List<Rule> lineitem in abeyedRules)
							lineitem.Remove(rule);
					ApplyRules(previouslyScheduledRules, new AbstractSyntaxTree(null, ValueTypes.code));
					if (RulebooksDebuggingCommand > 0) Console.WriteLine("[Rulebook \"" + description + "\" ends.]");
				}
		    }
	    }
	    Console.WriteLine("\n=== End Program ===========\n");
    }

	void ApplyRules(List<Rule> rulebook, AbstractSyntaxTree suppliedArguments = null)
	{
		if (rulebook != null)
			foreach (Rule rule in rulebook)
				ApplyRule(rule, suppliedArguments);
	}

	void ApplyRule(Rule rule, AbstractSyntaxTree suppliedArguments = null)
	{
		if (ArgsCheck(rule, suppliedArguments))
		{
			if (rule.Condition == null || AsBoolean(EvalCode(rule.Condition)))
			{
				if (RulesDebuggingCommand > 0)
					Console.WriteLine("[Rule " + rule.Atom + " applies.]");
				if (rule.Code != null)
					ExecuteStatement(rule.Code);
			}
			else if (RulesDebuggingCommand >= 2)
				Console.WriteLine("[Rule " + rule.Atom + " does NOT apply.]");
		}
		else if (RulesDebuggingCommand >= 2)
			Console.WriteLine("[Rule " + rule.Atom + " does NOT apply.]");
	}

	bool ArgsCheck(Rule rule, AbstractSyntaxTree suppliedArguments)
	{
		if (!rule.Atom.Contains("(")) return true;
		if (suppliedArguments == null)
			return true;
		if (suppliedArguments.Value == null)
			return false;
		string args = rule.Atom.Substring(rule.Atom.IndexOf("(") + 1);
		args = args.Trim(')');
		string[] eacharg = args.Split(new[] {','});

		var supArgs = new List<AbstractSyntaxTree>();
		for (var subnode = suppliedArguments; subnode != null; subnode = subnode.Left)
		{
			var paramNode = (subnode.Value == ",") ? subnode.Right : subnode;
			supArgs.Insert(0,paramNode);
		}
		int i = -1;
		if (supArgs.Count < eacharg.Length) return false;
		foreach (string arg2 in eacharg)
		{
			i++;
			if (supArgs[i].Value == FillInThisBlank)
				continue;
			string arg = arg2.Trim();
			if (supArgs[i].ValueType == ValueTypes.text && arg.Trim('"') == supArgs[i].Value) 
				continue;
			if (supArgs[i].ValueType != ValueTypes.code && arg == supArgs[i].Value)
				continue;
			if (arg != supArgs[i].Value)
			{
				var parameter = AllRules.FirstOrDefault(r => r.Atom == supArgs[i].Value);
				if (parameter == null || parameter.Code == null) return false;
				var val = EvalCode(parameter.Code).ToString();
				if (arg != val) return false;
			}
		}
		return true;
	}

	void ExecuteStatement(AbstractSyntaxTree node)
	{
		if (node == null)
			return;
		else if (node.ValueType == ValueTypes.code && node.IsOperand)
			ApplyRules(AllRules.Where(r => r.Atom == node.Value).ToList()); // WhenPlayBegins;
		else if (node.ValueType == ValueTypes.code && node.Value == "(" && !node.Contains(FillInThisBlank))  // Heading(Foyer), Description(Foyer)....
		{
			Rule invokedRule = AllRules.Find(r => r.Atom == node.ToString());
			if (invokedRule != null)
				ApplyRule(invokedRule);
			else
				ApplyRules(AllRules.Where(r => r.Atom.StartsWith(node.Left.Value + "(")).ToList(), node.Right);
		}
		else if (node.ValueType == ValueTypes.code && node.Value == "(" && node.Contains(FillInThisBlank)) // understand(_,cloak) 
		{
			foreach(var item in ForEach_(node))
				ExecuteStatement(item);
		}
		else if (node.ValueType == ValueTypes.code)
			EvalCode(node);
		else if (node.ValueType == ValueTypes.text)
			Console.Write(node.Value);
		else if (node.ValueType == ValueTypes.number)
			Console.Write(node.Value);
		else if (node.ValueType == ValueTypes.boolean)
			Console.Write(node.Value == "true" ? "yes" : "no");
		else
			throw new Exception("We never defined:  do " + node.Value + "... ");
	}

	// if the passed-in node is a string value, number, or bool, it returns it. It does NOT auto-print it.
	AbstractSyntaxTree EvalCode(AbstractSyntaxTree node)
    {
        if (node == null) return null;
		switch (node.Value) // nodes which are either Operands or mustn't eval both sides immediately
		{
			case "?":
				AbstractSyntaxTree lhs = node.Left;
				Rule iteratorVar = new Rule { Atom = "each", Location = "local", IsPrivateToFile = "local" };
				LocalVariables.Push(new List<Rule>() { iteratorVar});
				AllRules.Add(iteratorVar); // todo remove
				if (lhs.Value == "(") 
					iteratorVar.Atom += lhs.Left.Value;
				else
					lhs = EvalCode(lhs);
				foreach (AbstractSyntaxTree code in ForEach_(lhs))
				{
					iteratorVar.Code = code; // set the "eachFoo" variable to the next value in the list
					ExecuteStatement(node.Right);
				}
				LocalVariables.Pop();
				AllRules.Remove(iteratorVar); // todo remove
				return null;

			case "}":
				LocalVariables.Pop();
				return null;

			case "Parse":
				CrappyHardCodedParser();
				return null;

			case null:
				return null;
		}
		if (node.IsOperand) return node;
		AbstractSyntaxTree left = EvalCode(node.Left);
		AbstractSyntaxTree right = EvalCode(node.Right);
	    switch (node.Value)  // nodes which will print a value or alter the ruleset
	    {
			case "{":
				ExecuteStatement(left);
				LocalVariables.Push(new List<Rule>());
				ExecuteStatement(right);
			    return null;

			case ";":
				ExecuteStatement(left);
				ExecuteStatement(right);
			    return null;

		    case ":=": 
			    var rule = AllRules.FirstOrDefault(r => r.Atom == left.ToString());
			    if (rule == null)
			    {
				    rule = new Rule {Atom = left.ToString(), Code = right};
				    AllRules.Add(rule);
			    }
			    if (right.Value == "ReadKeyboard")
					rule.Code = new AbstractSyntaxTree(Console.ReadLine(), ValueTypes.text);
				else if (right.Value == "ReadKey")
					rule.Code = new AbstractSyntaxTree(Console.ReadKey().KeyChar.ToString(), ValueTypes.text);
			    else if (right.Contains("_"))
					foreach(var item in ForEach_(right))
					    rule.Code = item;
			    else
				    rule.Code = right;
				return null;

			case "(": // we fill-in the values, but don't know if we're going to := assert it or ?{} test it
			    bool allBound;
			    return CloneTreeWithParamsBoundToArgs(node, out allBound);

			case ",":
				return node;

	    }
		// at this point, any items of the shape   foo(bar,blah)   are relations, not function-invocations. We test for existance and value.
		if (left.ValueType == ValueTypes.code)  left = TestRelation(left);
		if (right.ValueType == ValueTypes.code) right = TestRelation(right);
		// these produce a value. Statements (above) do not produce values.
		switch (node.Value)
        {
			case "<":  return Compare(left, right) < 0 ? Yes : No;
			case "<=": return Compare(left, right) <= 0 ? Yes : No;
            case "<>":
			case "!=": return Compare(left, right) != 0 ? Yes : No;
			case ">":  return Compare(left, right) > 0 ? Yes : No;
			case ">=": return Compare(left, right) >= 0 ? Yes : No;
			case "==": return Compare(left, right) == 0 ? Yes : No;
            case "&":
			case "&&": return AsBoolean(left) && AsBoolean(right) ? Yes : No;
            case "|":
			case "||": return AsBoolean(left) || AsBoolean(right) ? Yes : No;
			case "+":  return new AbstractSyntaxTree((decimal.Parse(left.Value) + decimal.Parse(right.Value)).ToString(), ValueTypes.number);
			case "-":  return new AbstractSyntaxTree((decimal.Parse(left.Value) - decimal.Parse(right.Value)).ToString(), ValueTypes.number);
			case "*":  return new AbstractSyntaxTree((decimal.Parse(left.Value) * decimal.Parse(right.Value)).ToString(), ValueTypes.number);
			case "/":  return new AbstractSyntaxTree((decimal.Parse(left.Value) / decimal.Parse(right.Value)).ToString(), ValueTypes.number);
		}
        return node;
    }

	bool AsBoolean(AbstractSyntaxTree node)
	{
		bool retval = false;
		if (bool.TryParse(node.Value, out retval)) return retval;
		if (node.ValueType == ValueTypes.number) return true; // a non-void rule was found
		if (node.ValueType == ValueTypes.text) return true;
		if (node.ValueType == ValueTypes.code && node.IsOperand) 
			return true;
		if (node.ValueType == ValueTypes.code && node.Value == "(")
			foreach (var item in ForEach_(node))
				return true;
		return false;
	}

	int Compare(AbstractSyntaxTree left, AbstractSyntaxTree right)
	{
		if (left.ValueType == ValueTypes.number && right.ValueType == ValueTypes.number)
		{
			decimal l = decimal.Parse(left.Value);
			decimal r = decimal.Parse(right.Value);
			return l.CompareTo(r);
		}
		return left.Value.CompareTo(right.Value);
	}

	// when node.Value == "(", and params are unbound OR just a true/false value
	// one param must be FillInThisBlank 
	// then it will act as a function call, searching all relations for the value that "fits" in there, and return all of them in turn. "Fill-in-the-blank mode"
	IEnumerable<AbstractSyntaxTree> ForEach_(AbstractSyntaxTree node)
	{
		if (node == null || node.Value == "false") yield break;
		if (node.Value == "true")
		{
			yield return node;
			yield break;
		}
		if (node.Right == null || node.Right.Value == null) yield break; // there are no parameters
		bool allBound;
		AbstractSyntaxTree retval = CloneTreeWithParamsBoundToArgs(node, out allBound);
		if (allBound) // then return YES/NO depending on the relation exists
		{
			AbstractSyntaxTree result = TestRelation(retval);
			if (result != null && result != No) 
				yield return result;
			yield break;
		}
		string relationToFind = retval.ToString();// now has the params replaced by their args
		var pieces = relationToFind.Split(new[] { FillInThisBlank[0] }, StringSplitOptions.RemoveEmptyEntries);
		foreach (Rule rule in AllRules.Where(r => r.Atom.StartsWith(pieces[0]) && r.Atom.EndsWith(pieces[1])))
		{
			string foundValue = (rule == null) ? "" : rule.Atom.Replace(pieces[0], "").Replace(pieces[1], "");
			yield return ((AbstractSyntaxTree)null).AppendOperand(foundValue);
		}
	}

	AbstractSyntaxTree CloneTreeWithParamsBoundToArgs(AbstractSyntaxTree node, out bool allBound)
	{
		var retval = new AbstractSyntaxTree(node);
		allBound = true;
		for (var subnode = retval.Right; subnode != null; subnode = subnode.Right)
		{
			var paramNode = (subnode.Value == ",") ? subnode.Left : subnode;
			if (paramNode.Value == FillInThisBlank)
			{
				if (!allBound) throw new Exception("Can't have two " + FillInThisBlank + " placeholders in " + node);
				allBound = false;
			}
			else if (paramNode.ValueType == ValueTypes.code) // values are already simply copied
			{
				var parameter = AllRules.FirstOrDefault(r => r.Atom == paramNode.Value);
				if (parameter != null && parameter.Code != null) paramNode.Value = EvalCode(parameter.Code).ToString();
			}
		}
		return retval;
	}

	// all parameters should be bound to arguments, but the relation-name needn't actually exist
	AbstractSyntaxTree TestRelation(AbstractSyntaxTree node)
	{
		if (node.ValueType != ValueTypes.code) 
			return node;
		string relationToFind = node.ToString();
		if (relationToFind == FillInThisBlank)
			return node;
		Rule rule = null;
		if (relationToFind.Contains(FillInThisBlank))
		{
			var pieces = relationToFind.Split(new[] {FillInThisBlank[0]}, StringSplitOptions.RemoveEmptyEntries);
			if (pieces.Length > 2) throw new Exception("Can't have two " + FillInThisBlank + " placeholders in " + relationToFind); // todo remove this restriction
			rule = AllRules.Find(r => r.Atom.StartsWith(pieces[0]) && r.Atom.EndsWith(pieces[1]));
		}
		else
			rule = AllRules.Find(r => r.Atom == relationToFind);
		return (rule != null) ? rule.Code ?? Yes : No;
	}

	void CrappyHardCodedParser()
	{
		string input = ThePlayersCommand.Code.Value;
		var words = input.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
		var verb = AllRules.Find(r => r.Atom == "verb");
		var noun = AllRules.Find(r => r.Atom == "noun");
		var prep = AllRules.Find(r => r.Atom == "prep");
		var noun2 = AllRules.Find(r => r.Atom == "noun2");
		verb.Code = new AbstractSyntaxTree("", ValueTypes.text);
		noun.Code = new AbstractSyntaxTree("", ValueTypes.text);
		prep.Code = new AbstractSyntaxTree("", ValueTypes.text);
		noun2.Code = new AbstractSyntaxTree("", ValueTypes.text);
		verb.Code.Value = (words.Length >= 1) ? words[0] : "";
		noun.Code.Value = (words.Length >= 2) ? words[1] : "";
		prep.Code.Value = (words.Length >= 3) ? words[2] : "";
		noun2.Code.Value = (words.Length >= 4) ? words[3] : "";
		Rule referredObject;
		referredObject = AllRules.Find(r => r.Atom.StartsWith("understand(\"" + noun.Code.Value + "\""));
		if (referredObject != null) noun.Code = new AbstractSyntaxTree(referredObject.Atom.Substring(referredObject.Atom.IndexOf(',') + 1).Trim(')', ' '), ValueTypes.code);
		referredObject = AllRules.Find(r => r.Atom.StartsWith("understand(\"" + noun2.Code.Value + "\""));
		if (referredObject != null) noun2.Code = new AbstractSyntaxTree(referredObject.Atom.Substring(referredObject.Atom.IndexOf(',') + 1).Trim(')', ' '), ValueTypes.code);

		// built-in debugging commands
		if (words.Length >= 1 && string.Compare(words[0], "rules", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			if (words.Length < 2)
				RulesDebuggingCommand = RulesDebuggingCommand == 0 ? 1 : 0;
			else
			{
				if (words[1] == "on")
					RulesDebuggingCommand = 1;
				else if (words[1] == "off")
					RulesDebuggingCommand = 0;
				else if (words[1] == "all")
					RulesDebuggingCommand = 2;
			}
			Console.WriteLine("[The RULES debugging command is now " + RulesDebuggingCommand + "]");
		}
		else if (words.Length >= 1 && string.Compare(words[0], "rulebooks", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			RulebooksDebuggingCommand = RulebooksDebuggingCommand == 0 ? 1 : 0;
			Console.WriteLine("[The RULEBOOKS debugging command is now " + RulebooksDebuggingCommand + "]");
		}
		else if (words.Length >= 1 && string.Compare(words[0], "vars", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			foreach (Rule rule in AllRules)
				if (rule.Code != null && rule.Code.IsOperand)
					Console.WriteLine(rule.Atom + ": " + rule.Code);
		}
		else if (words.Length >= 1 && string.Compare(words[0], "relations", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			foreach (Rule rule in AllRules)
				if (rule.Code == null && rule.Atom.Contains("("))
					Console.WriteLine(rule.Atom);
		}
		else if (words.Length >= 1 && string.Compare(words[0], "atoms", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			foreach (Rule rule in AllRules)
				if (rule.Code == null && !rule.Atom.Contains("("))
					Console.WriteLine(rule.Atom);
		}
	}
}
    
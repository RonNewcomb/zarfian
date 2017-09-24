using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    public static List<Rule> AllRules = new List<Rule>();
	public static List<PrecedenceRuling> PrecedenceRulings = new List<PrecedenceRuling>();
	public static string CurrentFileScope = "TheStandardRules.txt";
	public static List<RuleDescription> NumberedRuleDescriptions = new List<RuleDescription>();

    static void Main(string[] args)
    {
		if (args.Length == 0)
		{
			Console.WriteLine("Please pass the source file(s) on the command line.");
			return;
		}
	    var parser = new Parser();
		parser.Parse(File.ReadAllText(CurrentFileScope, Encoding.UTF8));
		foreach (string filename in args)
        {
			CurrentFileScope = filename;
			parser.Parse(File.ReadAllText(filename, Encoding.UTF8));
		}
        AllRules.RemoveAll(r => r.Atom == null);
        if (AllRules.All(r => r.Atom != "ProgramStart"))
            throw new Exception("Where to do first?  do ProgramStart as {...}.");
        if (AllRules.All(r => r.Atom != "StoryTitle"))
            throw new Exception("We lack a title: do StoryTitle as \"...\".");
        if (AllRules.All(r => r.Atom != "StoryAuthor"))
            throw new Exception("We lack an author: do StoryAuthor as \"...\".");

        // print rules
		//string headers = "";
		//foreach (Rule rule in AllRules)
		//{
		//    if (rule.Location != headers)
		//    {
		//        headers = rule.Location;
		//        Console.WriteLine("\n" + headers + "\n");
		//    }
		//    Console.WriteLine(rule.Atom + ((rule.IsPrivateToFile == null) ? "" : " (See " + rule.IsPrivateToFile + ")"));
		//    Console.WriteLine("body: " + rule.Code);
		//    Console.WriteLine("when: " + rule.Condition);
		//    Console.WriteLine();
		//}

		// two identical description-of-rules won't pass == test because the test is only reference-equality; fix that now.
		var ruledescriptions = new List<RuleDescription>();
		foreach (PrecedenceRuling prule in PrecedenceRulings)
		{
			foreach (RuleDescription ruledesc in ruledescriptions)
				if (ruledesc != prule.First && ruledesc.Equals(prule.First))
				{
					prule.First = ruledesc;
					goto foundFirst;
				}
			ruledescriptions.Add(prule.First);
		foundFirst:
			foreach (RuleDescription ruledesc in ruledescriptions)
				if (ruledesc != prule.Second && ruledesc.Equals(prule.Second))
				{
					prule.Second = ruledesc;
					goto foundSecond;
				}
			ruledescriptions.Add(prule.Second);
		foundSecond:
			;
		}

		// check (and print) precedence rules
	    var badPrecedenceRules = new List<PrecedenceRuling>();
		var descriptionContainTheseRules = new Dictionary<RuleDescription, List<Rule>>();
	    var rulesInDescriptions = new Dictionary<Rule, List<RuleDescription>>();
	    foreach (PrecedenceRuling prule in PrecedenceRulings)
	    {
			var rulesWhichQualify1 = new List<Rule>();
			foreach (Rule rule in AllRules)
				if (prule.First.Contains(rule))
				{
					rulesWhichQualify1.Add(rule);

					if (!descriptionContainTheseRules.ContainsKey(prule.First))
						descriptionContainTheseRules.Add(prule.First, new List<Rule>());
					if (!descriptionContainTheseRules[prule.First].Contains(rule))
						descriptionContainTheseRules[prule.First].Add(rule);

					if (!rulesInDescriptions.ContainsKey(rule))
						rulesInDescriptions.Add(rule, new List<RuleDescription>());
					if (!rulesInDescriptions[rule].Contains(prule.First))
						rulesInDescriptions[rule].Add(prule.First);
				}
		    var rulesWhichQualify2 = new List<Rule>();
			foreach (Rule rule in AllRules)
				if (prule.Second.Contains(rule))
				{ 
					rulesWhichQualify2.Add(rule);

					if (!descriptionContainTheseRules.ContainsKey(prule.Second))
						descriptionContainTheseRules.Add(prule.Second, new List<Rule>());
					if (!descriptionContainTheseRules[prule.Second].Contains(rule))
						descriptionContainTheseRules[prule.Second].Add(rule);

					if (!rulesInDescriptions.ContainsKey(rule))
						rulesInDescriptions.Add(rule, new List<RuleDescription>());
					if (!rulesInDescriptions[rule].Contains(prule.First))
						rulesInDescriptions[rule].Add(prule.First);
				}
			Console.WriteLine("The " + rulesWhichQualify1.Count + " " + prule.First + " PRECEDE the " + rulesWhichQualify2.Count + " " + prule.Second);
		    rulesWhichQualify1.RemoveAll(r => !rulesWhichQualify2.Contains(r));
		    if (rulesWhichQualify1.Count > 0)
		    {
				Console.WriteLine("ERROR: this precedence rule cannot be satisfied because the following rule(s) are in both groups:");
			    foreach (Rule rule in rulesWhichQualify1)
				    Console.WriteLine("  " + rule.Atom);
				badPrecedenceRules.Add(prule);
		    }
			Console.WriteLine();
	    }

	    // create a digraph from the precedence rules, rule-description groups
		var graph = new DirectedGraph { Nodes = new List<Node>(), Edges = new List<Edge>() };
		foreach (RuleDescription rd in ruledescriptions)
		{
			var beginNode = new Node(rd,1);
			var endNode = new Node(rd,2);
			graph.Nodes.Add(beginNode);
			graph.Nodes.Add(endNode);
			graph.Edges.Add(new Edge(beginNode, endNode));
		}
		graph.Edges.AddRange(PrecedenceRulings.ConvertAll(pr => new Edge(graph.Nodes.Find(n => n.term.RuleDescription.Equals(pr.First) && n.term.Begins == 2), graph.Nodes.Find(n => n.term.RuleDescription.Equals(pr.Second) && n.term.Begins == 1))));

		// add to the digraph any found Contains relationships
		Console.WriteLine("Corollaries:");
		for (int i = 0; i < ruledescriptions.Count; i++)
		{
			for (int j = 0; j < ruledescriptions.Count; j++)
			{
				if (i == j) continue;
				RuleDescription ldesc = ruledescriptions[i];
				RuleDescription rdesc = ruledescriptions[j];
				switch (ldesc.Compare(rdesc, descriptionContainTheseRules))
				{
					case Compares.equal:
						//Console.WriteLine(ldesc + " EQUALS " + rdesc);
						break;
					case Compares.distinct:
						//Console.WriteLine(ldesc + " IS UNRELATED TO " + rdesc);
						break;
					case Compares.subset:
						Console.WriteLine(ldesc + " IS IN " + rdesc);
						graph.Edges.Add(new Edge(graph.Nodes.Find(n => n.term.RuleDescription.Equals(rdesc) && n.term.Begins == 1), graph.Nodes.Find(n => n.term.RuleDescription.Equals(ldesc) && n.term.Begins == 1)));
						graph.Edges.Add(new Edge(graph.Nodes.Find(n => n.term.RuleDescription.Equals(ldesc) && n.term.Begins == 2), graph.Nodes.Find(n => n.term.RuleDescription.Equals(rdesc) && n.term.Begins == 2)));
						break;
					case Compares.superset:
						Console.WriteLine(ldesc + " CONTAINS " + rdesc);
						graph.Edges.Add(new Edge(graph.Nodes.Find(n => n.term.RuleDescription.Equals(ldesc) && n.term.Begins == 1), graph.Nodes.Find(n => n.term.RuleDescription.Equals(rdesc) && n.term.Begins == 1)));
						graph.Edges.Add(new Edge(graph.Nodes.Find(n => n.term.RuleDescription.Equals(rdesc) && n.term.Begins == 2), graph.Nodes.Find(n => n.term.RuleDescription.Equals(ldesc) && n.term.Begins == 2)));
						break;
					case Compares.overlaps:
						Console.WriteLine(ldesc + " OVERLAPS " + rdesc);
						break;
				}
			}
		}

		// debug print digraph
		//Console.WriteLine(graph);

		// find all rules that aren't sorted at all by PRECEDES rules
		Console.WriteLine("\nUnsorted rules, uninvoked rules:");
		foreach (Rule rule in AllRules)
			if (!rulesInDescriptions.ContainsKey(rule) && !rule.Location.Contains("Volume - the Standard Rules\r\n"))
			{
				int parensAt = rule.Atom.IndexOf('(');
				string atomWithoutParens = (parensAt > -1) ? rule.Atom.Substring(0, rule.Atom.IndexOf('(') + 1) : rule.Atom;
				bool isUsed = false;
				foreach (Rule otherRule in AllRules)
				{
					if (otherRule == rule) continue;
					if ((otherRule.Code != null && otherRule.Code.Contains(atomWithoutParens)) || (otherRule.Condition != null && otherRule.Condition.Contains(atomWithoutParens)))
					{
						isUsed = true;
						break;
					}
				}
				if (!isUsed)
					Console.WriteLine(rule);
			}

		// topsort the precedence rules to look for group-level conflicts
	    var sorted = new TopologicalSort(graph);
		Console.WriteLine(sorted);
		if (!sorted.Success || badPrecedenceRules.Count>0)
		{
			Console.ReadKey();
			return;
	    }

		// sort rules within a group by specificness of the Condition
	    foreach (KeyValuePair<RuleDescription,List<Rule>> description in descriptionContainTheseRules)
		    description.Value.Sort((a, b) => b.Condition.Specificness().CompareTo(a.Condition.Specificness()));

	    // run program
		new Interpreter(sorted.Sorted, descriptionContainTheseRules, AllRules).RunProgram();

        // print rules (and "variables")
		//foreach (Rule rule in AllRules)
		//    Console.WriteLine(rule.Atom + ": " + rule.Code);
        //Console.ReadKey();
    }

}


using System;
using System.Collections.Generic;
using System.Linq;

public class Term
{
	public RuleDescription RuleDescription;
	public int Begins;
	public Term(RuleDescription description, int begin) { RuleDescription = description; Begins = begin; }

	public override string ToString()
	{
		return (Begins == 1 ? "BEGIN " : Begins == 2 ? "END   " : "") + RuleDescription;
	}
}

public class Node
{
	public Term term;
	public Node(RuleDescription desc, int beginning) { term = new Term(desc, beginning); }
	// only for visit()
	public uint index = 0;
	public uint lowlink;
	public List<Edge> meAsIndependent = new List<Edge>();
};

public class Edge
{
	public Node independent, dependent;
	public Edge(Node from, Node to) { independent = from; dependent = to; from.meAsIndependent.Add(this); }
};

public class DirectedGraph
{
	public List<Node> Nodes;
	public List<Edge> Edges;

	public override string ToString()
	{
		string retval = "\n";
		for (int i = 0; i < Nodes.Count; i++)
			retval += i + ": " + Nodes[i].term + "\n";
		foreach (var edge in Edges)
			retval += edge.independent.term + " --> " + edge.dependent.term + "\n";
		return retval + "\n";
	}
}

public class TopologicalSort // depth-first, with Tarjan's algorithm
{
	// can be output
	public bool Success { get { return CircularLogicErrors.Count == 0; } }
	public List<Node> Sorted = new List<Node>(); // Empty list that will contain the sorted elements
	public List<List<Node>> CircularLogicErrors = new List<List<Node>>();

	public TopologicalSort(DirectedGraph graph) 
	{
		edges = graph.Edges; // pass to visit() by member field, because visit() is recursive

		// topological sort of the nodes, remembering cycles ("strongly connected groups")
		foreach (Node node in graph.Nodes)
			if (node.index == 0)
				visit(node);
	}

	// used by visit()
	protected List<Edge> edges;// = parameter
	protected Stack<Node> S = new Stack<Node>();
	protected uint indexCreator = 1;

	// recursive 
	protected void visit(Node v)
	{
		// init
		v.index = indexCreator;
		v.lowlink = indexCreator;
		indexCreator++;
		S.Push(v);

		// examine children
		//foreach (Edge edge in edges.Where(e => e.independent == v))
		foreach (Edge edge in v.meAsIndependent)
		{
			if (edge.dependent.index == 0)
			{
				visit(edge.dependent);
				edge.independent.lowlink = Math.Min(edge.independent.lowlink, edge.dependent.lowlink);
			}
			else if (S.Contains(edge.dependent))
				edge.independent.lowlink = Math.Min(edge.independent.lowlink, edge.dependent.index);
		}

		// insert parent into Sorted
		Sorted.Insert(0, v);

		// check for end of group
		if (v.lowlink != v.index) return;

		// possible cycle; definitely a group found. Check.
		var cycle = new List<Node>();
		//while (v != cycle.And(S.Pop()));
		Node popped;
		do
		{
			popped = S.Pop();
			if (popped != null)
				cycle.Add(popped);
		} while (v != popped);
		if (cycle.Count > 1)
			CircularLogicErrors.Add(cycle);
	}

	public override string ToString()
	{
		string retval = "";
		if (Success)
		{
			retval = "\nIn order:\n";
			retval += Sorted.Aggregate("", (s, n) => s + "\n" + n.term);
		}
		else if (CircularLogicErrors != null)
		{
			retval = "\nERROR: circular logic:\n";
			foreach (List<Node> cycle in CircularLogicErrors)
				retval += cycle.Aggregate("", (s, n) => s + "\n" + n.term) + "\n";
		}
		return retval;
	}

}

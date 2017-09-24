using System;
using System.Collections.Generic;

public class Rule
{
    public string Atom;
    public AbstractSyntaxTree Code;
    public AbstractSyntaxTree Condition;

    public string IsPrivateToFile;
    public string Location;

	public override string ToString()
	{
		return "Rule for " + (Atom ?? "");
	}
}

public class RuleDescription // like an Inform7 description-of-objects
{
	public string NamedLike; // for NamedLike, a * is a wildcard
	public string CheckedInTheCondition;
	public string ReadInTheBody;
	public string ChangedByTheBody;
	public string UnderThisHeading;
	private string[] NamePieces;

	public RuleDescription()
	{
	}

	public RuleDescription(RuleDescription rd2)
	{
		NamedLike = rd2.NamedLike;
		CheckedInTheCondition = rd2.CheckedInTheCondition;
		ReadInTheBody = rd2.ReadInTheBody;
		ChangedByTheBody = rd2.ChangedByTheBody;
		UnderThisHeading = rd2.UnderThisHeading;
	}

	public override bool Equals(object obj)
	{
		var rd2 = obj as RuleDescription;
		if (rd2==null) return base.Equals(obj); // standard is reference-equality, not contents equality
		if (NamedLike != rd2.NamedLike) return false;
		if (CheckedInTheCondition != rd2.CheckedInTheCondition) return false;
		if (ReadInTheBody != rd2.ReadInTheBody) return false;
		if (ChangedByTheBody != rd2.ChangedByTheBody) return false;
		if (UnderThisHeading != rd2.UnderThisHeading) return false;
		return true;
	}

	public bool Contains(Rule rule)
	{
		if (CheckedInTheCondition != null)
			if (rule.Condition == null || !rule.Condition.Contains(CheckedInTheCondition)) return false;
		if (ReadInTheBody != null)
			if (rule.Code == null || !rule.Code.Contains(ReadInTheBody)) return false;
		if (UnderThisHeading != null )
			if (rule.Location == null || !rule.Location.Contains(UnderThisHeading)) return false;
		if (NamedLike != null)
		{
			if (rule.Atom == null) return false;
			if (NamePieces == null)
				NamePieces = NamedLike.Split(new[] {'*'}, StringSplitOptions.RemoveEmptyEntries);
			int i = 0;
			foreach (string piece in NamePieces)
			{
				i = rule.Atom.IndexOf(piece, i);
				if (i == -1) return false;
				i += piece.Length;
			}
		}
		if (ChangedByTheBody != null)
			if (rule.Code == null || !rule.Code.IsAssignedTo(ChangedByTheBody)) return false;
		return true;
	}

	public override string ToString()
	{
		return "rules" +
			   (NamedLike != null ? " named " + NamedLike : "") +
			   (UnderThisHeading != null ? " under " + UnderThisHeading : "") +
		       ((CheckedInTheCondition ?? ReadInTheBody ?? ChangedByTheBody) == null
			       ? ""
			       : " in which" +
			         (CheckedInTheCondition != null ? " the condition contains " + CheckedInTheCondition : "") +
			         (ReadInTheBody != null ? " the body reads " + ReadInTheBody : "") +
			         (ChangedByTheBody != null ? " the body changes " + ChangedByTheBody : ""));
	}

	public Compares Compare(RuleDescription rd2, Dictionary<RuleDescription, List<Rule>> rulesPerDescription)
	{
		if (!rulesPerDescription.ContainsKey(this) || !rulesPerDescription.ContainsKey(rd2)) return Compares.distinct;
		var leftList = rulesPerDescription[this];
		var rightList = rulesPerDescription[rd2];

		if (leftList.Count == 0 || rightList.Count == 0) return Compares.distinct;

		bool leftHasRuleThatRightLacks = false;
		bool aRuleOnBothLists = false;
		foreach (Rule rule in leftList)
			if (!rightList.Contains(rule))
				leftHasRuleThatRightLacks = true;
			else
				aRuleOnBothLists = true;
		bool rightHasRuleThatLeftLacks = false;
		foreach (Rule rule in rightList)
			if (!leftList.Contains(rule))
			{
				rightHasRuleThatLeftLacks = true;
				break;
			}

		if (!aRuleOnBothLists) return Compares.distinct;
		if (!leftHasRuleThatRightLacks && !rightHasRuleThatLeftLacks) return Compares.equal;
		if (leftHasRuleThatRightLacks && !rightHasRuleThatLeftLacks) return Compares.superset;
		if (!leftHasRuleThatRightLacks && rightHasRuleThatLeftLacks) return Compares.subset;
		if (leftHasRuleThatRightLacks && rightHasRuleThatLeftLacks) return Compares.overlaps;
		return Compares.overlaps;
	}
}

public enum Compares
{
	superset, subset, equal, distinct, overlaps,
}

public class PrecedenceRuling
{
	public RuleDescription First;
	public RuleDescription Second;

	public override string ToString()
	{
		return First + " PRECEDE " + Second;
	}
}




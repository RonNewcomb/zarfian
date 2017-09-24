using System;

public enum ValueTypes { text, number, code, boolean }

public enum RulePart { unknown, Do, As, If, end }

public class AbstractSyntaxTree
{
	public string Value;
	public AbstractSyntaxTree Left, Right;
	public ValueTypes ValueType;
	public int Precedence;

	public bool IsOperand
	{
		get { return Left == null && Right == null; }
	}

	public AbstractSyntaxTree(string operandValue, ValueTypes vt = ValueTypes.code)
	{
		//if (operandValue == null)
		//	throw new ArgumentNullException("operandValue");
		Value = operandValue;
		Left = Right = null;
		Precedence = int.MaxValue;
		ValueType = vt;
	}

	public AbstractSyntaxTree(AbstractSyntaxTree original)
	{
		Value = original.Value;
		Precedence = original.Precedence;
		ValueType = original.ValueType;
		if (original.Left != null)
			Left = new AbstractSyntaxTree(original.Left);
		if (original.Right != null)
			Right = new AbstractSyntaxTree(original.Right);
	}

	public override string ToString()
	{
		if (Value == null)
			return "";
		if (ValueType == ValueTypes.text && Left == null && Right == null)
			return "\"" + Value + "\"";
		if (Value == "(" && Left != null)
			return Left.Value + "(" + ((Right != null) ? Right.ToString() : "") + ")";
		if (Value == "," && Left != null && Right != null)
			return Left + ", " + Right;
		return IsOperand ? Value : "(" + (Left == null ? "LNULL" : Left.ToString()) + " " + Value + " " + (Right == null ? "RNULL" : Right.ToString()) + ")";
	}

	public bool Contains(string atomWithoutParens)
	{
		if (Value == atomWithoutParens) return true;
		if (Left != null && Left.Contains(atomWithoutParens)) return true;
		if (Right != null && Right.Contains(atomWithoutParens)) return true;
		return false;
	}

	public bool IsAssignedTo(string p)
	{
		if (Value == ":=") return (Left != null && Left.Contains(p));
		if (Left != null && Left.IsAssignedTo(p)) return true;
		if (Right != null && Right.IsAssignedTo(p)) return true;
		return false;
	}
}

public static class ASTextensions
{
	public static int Specificness(this AbstractSyntaxTree This)
	{
		int retval = 0;
		if (This == null) return retval;
		if (This.Value == "&" || This.Value == "|" || This.Value == "&&" || This.Value == "||") retval++;
		if (This.Left != null) retval += This.Left.Specificness();
		if (This.Right != null) retval += This.Right.Specificness();
		return retval;
	}
}



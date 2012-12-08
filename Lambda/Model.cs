using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    public delegate string ExpressionDisplayDelegate(Expression expression, SymbolTable table);
    public delegate Expression ExpressionEvaluateDelegate(Expression expression);

    public class Expression
    { }

    public struct SymbolRef
    {
        public static bool operator ==(SymbolRef v1, SymbolRef v2)
        {
            return v1.Symbol == v2.Symbol && v1.Instance == v2.Instance;
        }

        public static bool operator !=(SymbolRef v1, SymbolRef v2)
        {
            return !(v1 == v2);
        }

        public int Symbol;
        public long Instance;

        public SymbolRef(int symbol)
            : this(symbol, 0)
        { }

        public SymbolRef(int symbol, long instance)
        {
            Symbol = symbol;
            Instance = instance;
        }

        public override bool Equals(object obj)
        {
            if (obj is SymbolRef)
                return this == (SymbolRef)obj;

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return Symbol ^ ((int)Instance * 3);
        }
    }

    public class BoundSymbolExpression : Expression
    {
        public SymbolRef Symbol;
    }

    public class FreeSymbolExpression : Expression
    {
        public string Name;
        public object Tag;
        public ExpressionDisplayDelegate Display;
    }

    public class AbstractionExpression : Expression
    {
        public SymbolRef Left;
        public Expression Right;
    }

    public class ApplicationExpression : Expression
    {
        public Expression Left;
        public Expression Right;
    }

    public class LazyExpression : Expression
    {
        public int Id;
    }

    public class BuiltinExpression : Expression
    {
        public Expression Left;
        public Expression Right;
        public ExpressionDisplayDelegate Display;
        public ExpressionEvaluateDelegate Evaluate;
    }
}

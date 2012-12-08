using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    [Flags]
    public enum EvaluatorFlags
    {
        Lazy = 0x1,
        NoBeta = 0x2,
        NoEta = 0x4,
        NoBuiltin = 0x8,
        ResolveOnly = NoBeta | NoEta | NoBuiltin
    }

    public static class Evaluator
    {
        private class EvaluatorState
        {
            private Func<Expression, Expression> _resolveFunction;
            private int _depthLimit;
            private EvaluatorFlags _flags;

            private int _nextLazyId;
            private Dictionary<int, Expression> _lazyExpressions = new Dictionary<int, Expression>();
            private HashSet<int> _evaluatedLazyExpressions = new HashSet<int>();

            public EvaluatorState(Func<Expression, Expression> resolveFunction, int depthLimit, EvaluatorFlags flags)
            {
                _resolveFunction = resolveFunction;
                _depthLimit = depthLimit;
                _flags = flags;
            }

            public Func<Expression, Expression> ResolveFunction
            {
                get { return _resolveFunction; }
            }

            public int DepthLimit
            {
                get { return _depthLimit; }
            }

            public EvaluatorFlags Flags
            {
                get { return _flags; }
            }

            public Expression RegisterLazyExpression(Expression expression)
            {
                int lazyId;

                if (expression is LazyExpression)
                    return expression;

                lazyId = _nextLazyId++;
                _lazyExpressions.Add(lazyId, expression);

                return new LazyExpression { Id = lazyId };
            }

            public Expression GetLazyExpression(LazyExpression expression)
            {
                return _lazyExpressions[expression.Id];
            }

            public Expression EvaluateLazyExpression(LazyExpression expression, int depthLimit, bool stopOnAbstraction)
            {
                if (_evaluatedLazyExpressions.Contains(expression.Id))
                    return _lazyExpressions[expression.Id];

                var result = Evaluate(_lazyExpressions[expression.Id], depthLimit, this, stopOnAbstraction);

                if (!stopOnAbstraction)
                {
                    _lazyExpressions[expression.Id] = result;
                    _evaluatedLazyExpressions.Add(expression.Id);
                }

                return result;
            }
        }

        private static long _instance = 1;

        public static long CreateNewInstance()
        {
            return _instance++;
        }

        public static Expression Evaluate(Expression expression, Func<Expression, Expression> resolveFunction = null, int depthLimit = 1000, EvaluatorFlags flags = 0)
        {
            return Evaluate(expression, depthLimit, new EvaluatorState(resolveFunction, depthLimit, flags));
        }

        private static Expression Evaluate(Expression expression, int depthLimit, EvaluatorState state, bool stopOnAbstraction = false)
        {
            if (depthLimit == 0)
                throw new Exception("Depth limit exceeded");

            depthLimit--;

            if (state.ResolveFunction != null)
                expression = state.ResolveFunction(expression);

            if (expression is AbstractionExpression)
            {
                var abstraction = (AbstractionExpression)expression;

                if (stopOnAbstraction)
                    return expression;

                var right = Evaluate(abstraction.Right, depthLimit, state);

                if ((state.Flags & EvaluatorFlags.NoEta) == 0)
                {
                    // Eta-conversion
                    if (right is ApplicationExpression)
                    {
                        var application = (ApplicationExpression)right;

                        if (application.Right is BoundSymbolExpression)
                        {
                            var boundSymbol = (BoundSymbolExpression)application.Right;

                            if (boundSymbol.Symbol == abstraction.Left && !ContainsBoundSymbol(application.Left, abstraction.Left))
                            {
                                return application.Left;
                            }
                        }
                    }
                }

                return new AbstractionExpression { Left = abstraction.Left, Right = right };
            }
            else if (expression is ApplicationExpression)
            {
                var application = (ApplicationExpression)expression;

                if ((state.Flags & EvaluatorFlags.NoBeta) == 0)
                {
                    var left = Evaluate(application.Left, depthLimit, state, true);

                    // Beta-reduction
                    if (left is AbstractionExpression)
                    {
                        var abstraction = (AbstractionExpression)left;
                        Expression right;

                        if ((state.Flags & EvaluatorFlags.Lazy) != 0)
                            right = state.RegisterLazyExpression(application.Right);
                        else
                            right = application.Right;

                        return Evaluate(Substitute(abstraction.Right, abstraction.Left, right), depthLimit, state, stopOnAbstraction);
                    }

                    return new ApplicationExpression { Left = left, Right = Evaluate(application.Right, depthLimit, state) };
                }
                else
                {
                    return new ApplicationExpression { Left = Evaluate(application.Left, depthLimit, state), Right = Evaluate(application.Right, depthLimit, state) };
                }
            }
            else if (expression is BuiltinExpression)
            {
                var builtin = (BuiltinExpression)expression;

                if ((state.Flags & EvaluatorFlags.NoBuiltin) != 0 || builtin.Evaluate == null)
                    return builtin;

                BuiltinExpression temp = new BuiltinExpression { Left = builtin.Left, Right = builtin.Right, Display = builtin.Display, Evaluate = builtin.Evaluate };

                if (temp.Left != null)
                    temp.Left = Evaluate(temp.Left, depthLimit, state, stopOnAbstraction);
                if (temp.Right != null)
                    temp.Right = Evaluate(temp.Right, depthLimit, state, stopOnAbstraction);

                return temp.Evaluate(temp);
            }
            else if (expression is LazyExpression)
            {
                return state.EvaluateLazyExpression((LazyExpression)expression, depthLimit, stopOnAbstraction);
            }
            else
            {
                return expression;
            }
        }

        public static Expression Substitute(Expression expression, SymbolRef symbol, Expression replacement)
        {
            if (expression is BoundSymbolExpression)
            {
                var boundSymbol = (BoundSymbolExpression)expression;

                if (boundSymbol.Symbol == symbol)
                    return replacement;
                else
                    return expression;
            }
            else if (expression is AbstractionExpression)
            {
                var abstraction = (AbstractionExpression)expression;

                if (abstraction.Left != symbol)
                    return new AbstractionExpression { Left = abstraction.Left, Right = Substitute(abstraction.Right, symbol, replacement) };
                else
                    return expression;
            }
            else if (expression is ApplicationExpression)
            {
                var application = (ApplicationExpression)expression;

                return new ApplicationExpression { Left = Substitute(application.Left, symbol, replacement), Right = Substitute(application.Right, symbol, replacement) };
            }
            else if (expression is BuiltinExpression)
            {
                var builtin = (BuiltinExpression)expression;

                return new BuiltinExpression { Left = Substitute(builtin.Left, symbol, replacement), Right = Substitute(builtin.Right, symbol, replacement), Display = builtin.Display, Evaluate = builtin.Evaluate };
            }
            else
            {
                return expression;
            }
        }

        public static bool ContainsBoundSymbol(Expression expression, SymbolRef symbol)
        {
            if (expression is BoundSymbolExpression)
            {
                var boundSymbol = (BoundSymbolExpression)expression;

                if (boundSymbol.Symbol == symbol)
                    return true;
                else
                    return false;
            }
            else if (expression is AbstractionExpression)
            {
                var abstraction = (AbstractionExpression)expression;

                if (abstraction.Left != symbol)
                    return ContainsBoundSymbol(abstraction.Right, symbol);
                else
                    return false;
            }
            else if (expression is ApplicationExpression)
            {
                var application = (ApplicationExpression)expression;

                if (ContainsBoundSymbol(application.Left, symbol))
                    return true;

                return ContainsBoundSymbol(application.Right, symbol);
            }
            else if (expression is BuiltinExpression)
            {
                var builtin = (BuiltinExpression)expression;

                if (ContainsBoundSymbol(builtin.Left, symbol))
                    return true;

                return ContainsBoundSymbol(builtin.Right, symbol);
            }
            else
            {
                return false;
            }
        }

        public static Expression ReplaceBoundSymbols(Expression expression, long newInstance)
        {
            if (expression is BoundSymbolExpression)
            {
                var boundSymbol = (BoundSymbolExpression)expression;

                return new BoundSymbolExpression { Symbol = new SymbolRef(boundSymbol.Symbol.Symbol, newInstance) };
            }
            else if (expression is AbstractionExpression)
            {
                var abstraction = (AbstractionExpression)expression;

                return new AbstractionExpression { Left = new SymbolRef(abstraction.Left.Symbol, newInstance), Right = ReplaceBoundSymbols(abstraction.Right, newInstance) };
            }
            else if (expression is ApplicationExpression)
            {
                var application = (ApplicationExpression)expression;

                return new ApplicationExpression { Left = ReplaceBoundSymbols(application.Left, newInstance), Right = ReplaceBoundSymbols(application.Right, newInstance) };
            }
            else if (expression is BuiltinExpression)
            {
                var builtin = (BuiltinExpression)expression;

                return new BuiltinExpression { Left = ReplaceBoundSymbols(builtin.Left, newInstance), Right = ReplaceBoundSymbols(builtin.Right, newInstance), Display = builtin.Display, Evaluate = builtin.Evaluate };
            }
            else
            {
                return expression;
            }
        }

        public static string ConvertToString(Expression expression, SymbolTable table)
        {
            if (expression is BoundSymbolExpression)
            {
                var boundSymbol = (BoundSymbolExpression)expression;

                return table.Lookup(boundSymbol.Symbol.Symbol);
            }
            else if (expression is FreeSymbolExpression)
            {
                var freeSymbol = (FreeSymbolExpression)expression;

                if (freeSymbol.Display != null)
                    return freeSymbol.Display(freeSymbol, table);

                return freeSymbol.Name;
            }
            else if (expression is AbstractionExpression)
            {
                var abstraction = (AbstractionExpression)expression;

                return "\\" + table.Lookup(abstraction.Left.Symbol) + "." + ConvertToString(abstraction.Right, table);
            }
            else if (expression is ApplicationExpression)
            {
                var application = (ApplicationExpression)expression;
                string left;
                string right;

                if (application.Left is AbstractionExpression)
                    left = "(" + ConvertToString(application.Left, table) + ")";
                else
                    left = ConvertToString(application.Left, table);

                if ((application.Right is AbstractionExpression) || (application.Right is ApplicationExpression))
                    right = "(" + ConvertToString(application.Right, table) + ")";
                else
                    right = ConvertToString(application.Right, table);

                return left + " " + right;
            }
            else if (expression is LazyExpression)
            {
                var lazy = (LazyExpression)expression;

                return "<#" + lazy.Id.ToString() + ">";
            }
            else if (expression is BuiltinExpression)
            {
                var builtin = (BuiltinExpression)expression;

                if (builtin.Display != null)
                    return builtin.Display(expression, table);

                string left = builtin.Left != null ? ConvertToString(builtin.Left, table) : "";
                string right = builtin.Right != null ? ConvertToString(builtin.Right, table) : "";

                if (right == "")
                    return "<BUILTIN>(" + left + ")";
                else
                    return "<BUILTIN>(" + left + ")(" + right + ")";
            }
            else
            {
                return "???";
            }
        }

        public static Expression ConvertToBuiltin(Expression expression, SymbolTable table)
        {
            // _Bool

            bool boolValue;

            if (RecognizeBool(expression, out boolValue))
                return table.BuiltinNames.CreateBool(boolValue);

            // _Integer

            int integerValue;

            if (RecognizeInteger(expression, out integerValue))
                return table.BuiltinNames.CreateInteger(integerValue);

            // _List

            BuiltinList listValue;

            if (RecognizeList(expression, out listValue))
                return table.BuiltinNames.CreateList(listValue);

            // _Pair

            BuiltinPair pairValue;

            if (RecognizePair(expression, out pairValue))
                return table.BuiltinNames.CreatePair(pairValue);

            // _Maybe

            Expression maybeValue;

            if (RecognizeMaybe(expression, out maybeValue))
                return table.BuiltinNames.CreateMaybe(maybeValue);

            return null;
        }

        public static bool RecognizeBool(Expression expression, out bool value)
        {
            // \x,y.x is True
            // \x,y.y is False

            value = false;

            if (expression is AbstractionExpression)
            {
                var abs1 = (AbstractionExpression)expression;

                if (abs1.Right is AbstractionExpression)
                {
                    var abs2 = (AbstractionExpression)abs1.Right;

                    if (abs2.Right is BoundSymbolExpression)
                    {
                        var bound = (BoundSymbolExpression)abs2.Right;

                        if (bound.Symbol == abs1.Left)
                        {
                            value = true;
                            return true;
                        }
                        else if (bound.Symbol == abs2.Left)
                        {
                            value = false;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static bool RecognizeInteger(Expression expression, out int value)
        {
            // \f,x.x is 0
            // \f,x.f x -> \f.f is 1
            // \f,x.f (f x) is 2
            // \f,x.f (f (f x)) is 3
            // ...

            value = 0;

            if (expression is AbstractionExpression)
            {
                var abs1 = (AbstractionExpression)expression;

                if (abs1.Right is BoundSymbolExpression)
                {
                    var bound = (BoundSymbolExpression)abs1.Right;

                    if (bound.Symbol == abs1.Left)
                    {
                        // 1 (identity)
                        value = 1;
                        return true;
                    }
                }
                else if (abs1.Right is AbstractionExpression)
                {
                    var abs2 = (AbstractionExpression)abs1.Right;
                    var right = abs2.Right;
                    var app = right as ApplicationExpression;
                    int count = 0;

                    while (app != null)
                    {
                        // Check for f on LHS
                        if (!(app.Left is BoundSymbolExpression))
                            return false;
                        if (((BoundSymbolExpression)app.Left).Symbol != abs1.Left)
                            return false;

                        right = app.Right;
                        app = right as ApplicationExpression;
                        count++;
                    }

                    // Check for x on RHS
                    if (!(right is BoundSymbolExpression))
                        return false;
                    if (((BoundSymbolExpression)right).Symbol != abs2.Left)
                        return false;

                    value = count;
                    return true;
                }
            }

            return false;
        }

        public static bool RecognizeList(Expression expression, out BuiltinList value)
        {
            BuiltinList head = null;
            BuiltinList tail = null;
            Expression current = expression;

            // \x,y,z.y is Null
            // \f.f <FIRST> <SECOND> is Pair

            value = null;

            while (true)
            {
                if (current is AbstractionExpression)
                {
                    var abs1 = (AbstractionExpression)current;

                    if (abs1.Right is ApplicationExpression)
                    {
                        var app1 = (ApplicationExpression)abs1.Right;

                        if (app1.Left is ApplicationExpression)
                        {
                            var app2 = (ApplicationExpression)app1.Left;

                            if (app2.Left is BoundSymbolExpression)
                            {
                                var bound = (BoundSymbolExpression)app2.Left;

                                if (bound.Symbol == abs1.Left)
                                {
                                    // Pair

                                    if (head == null)
                                    {
                                        head = new BuiltinList { First = app2.Right, Second = null };
                                        tail = head;
                                    }
                                    else
                                    {
                                        tail.Second = new BuiltinList { First = app2.Right, Second = null };
                                        tail = tail.Second;
                                    }

                                    current = app1.Right;
                                    continue;
                                }
                            }
                        }
                    }
                    else if (abs1.Right is AbstractionExpression)
                    {
                        var abs2 = (AbstractionExpression)abs1.Right;

                        if (abs2.Right is AbstractionExpression)
                        {
                            var abs3 = (AbstractionExpression)abs2.Right;

                            if (abs3.Right is BoundSymbolExpression)
                            {
                                var bound = (BoundSymbolExpression)abs3.Right;

                                if (bound.Symbol == abs2.Left)
                                {
                                    // Null
                                    value = head;
                                    return true;
                                }
                            }
                        }
                    }
                }

                return false;
            }
        }

        public static bool RecognizePair(Expression expression, out BuiltinPair value)
        {
            // \f.f <FIRST> <SECOND> is Pair

            value = null;

            if (expression is AbstractionExpression)
            {
                var abs = (AbstractionExpression)expression;

                if (abs.Right is ApplicationExpression)
                {
                    var app1 = (ApplicationExpression)abs.Right;

                    if (app1.Left is ApplicationExpression)
                    {
                        var app2 = (ApplicationExpression)app1.Left;

                        if (app2.Left is BoundSymbolExpression)
                        {
                            var bound = (BoundSymbolExpression)app2.Left;

                            if (bound.Symbol == abs.Left)
                            {
                                // Pair

                                value = new BuiltinPair { First = app2.Right, Second = app1.Right };
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public static bool RecognizeMaybe(Expression expression, out Expression value)
        {
            // \f.f <VALUE> is Maybe

            value = null;

            if (expression is AbstractionExpression)
            {
                var abs = (AbstractionExpression)expression;

                if (abs.Right is ApplicationExpression)
                {
                    var app = (ApplicationExpression)abs.Right;

                    if (app.Left is BoundSymbolExpression)
                    {
                        var bound = (BoundSymbolExpression)app.Left;

                        if (bound.Symbol == abs.Left)
                        {
                            // Maybe
                            value = app.Right;
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    public class BuiltinList
    {
        public Expression First;
        public BuiltinList Second;
    }

    public class BuiltinPair
    {
        public Expression First;
        public Expression Second;
    }

    public class BuiltinNames
    {
        private Dictionary<string, Expression> _names;

        public BuiltinNames(SymbolTable table)
        {
            int x = table.Register("x");
            int y = table.Register("y");

            _names = new Dictionary<string, Expression>();
            _names.Add("_True", CreateBool(true));
            _names.Add("_False", CreateBool(false));
            _names.Add("_Zero", CreateInteger(0));
            _names.Add("_Successor", new AbstractionExpression
            {
                Left = new SymbolRef(x),
                Right = new BuiltinExpression
                {
                    Left = new BoundSymbolExpression { Symbol = new SymbolRef(x) },
                    Right = null,
                    Evaluate = _SuccessorEvaluate
                }
            });
            _names.Add("_Null", new FreeSymbolExpression { Name = "_List", Tag = null, Display = _ListDisplay });
            _names.Add("_Nothing", new FreeSymbolExpression { Name = "_Maybe", Tag = null, Display = _MaybeDisplay });
        }

        public Expression CreateBool(bool value)
        {
            return new FreeSymbolExpression { Name = "_Bool", Tag = value, Display = _BoolDisplay };
        }

        private string _BoolDisplay(Expression expression, SymbolTable table)
        {
            var freeSymbol = (FreeSymbolExpression)expression;

            return "<" + freeSymbol.Name + ">" + ((bool)freeSymbol.Tag).ToString();
        }

        public Expression CreateInteger(int value)
        {
            return new FreeSymbolExpression { Name = "_Integer", Tag = value, Display = _IntegerDisplay };
        }

        private string _IntegerDisplay(Expression expression, SymbolTable table)
        {
            var freeSymbol = (FreeSymbolExpression)expression;

            return "<" + freeSymbol.Name + ">" + ((int)freeSymbol.Tag).ToString();
        }

        public Expression CreateList(IEnumerable<Expression> list)
        {
            BuiltinList head = null;
            BuiltinList tail = null;

            foreach (var item in list)
            {
                if (head == null)
                {
                    head = new BuiltinList { First = item, Second = null };
                    tail = head;
                }
                else
                {
                    tail.Second = new BuiltinList { First = item, Second = null };
                    tail = tail.Second;
                }
            }

            return CreateList(head);
        }

        public Expression CreateList(BuiltinList head)
        {
            return new FreeSymbolExpression { Name = "_List", Tag = head, Display = _ListDisplay };
        }

        private string _ListDisplay(Expression expression, SymbolTable table)
        {
            var freeSymbol = (FreeSymbolExpression)expression;
            BuiltinList list = (BuiltinList)freeSymbol.Tag;
            StringBuilder sb = new StringBuilder();

            sb.Append("<" + freeSymbol.Name + ">");

            if (list == null)
            {
                sb.Append("Null");
            }
            else
            {
                do
                {
                    var displayExpression = Evaluator.ConvertToBuiltin(list.First, table);

                    if (displayExpression == null)
                        displayExpression = list.First;

                    sb.Append("(" + Evaluator.ConvertToString(displayExpression, table) + ")");
                    list = list.Second;
                } while (list != null);
            }

            return sb.ToString();
        }

        public Expression CreatePair(BuiltinPair pair)
        {
            return new FreeSymbolExpression { Name = "_Pair", Tag = pair, Display = _PairDisplay };
        }

        private string _PairDisplay(Expression expression, SymbolTable table)
        {
            var freeSymbol = (FreeSymbolExpression)expression;
            BuiltinPair pair = (BuiltinPair)freeSymbol.Tag;
            StringBuilder sb = new StringBuilder();

            sb.Append("<" + freeSymbol.Name + ">");

            Expression displayExpression;

            // First

            displayExpression = Evaluator.ConvertToBuiltin(pair.First, table);

            if (displayExpression == null)
                displayExpression = pair.First;

            sb.Append("(" + Evaluator.ConvertToString(displayExpression, table) + ")");

            // Second

            displayExpression = Evaluator.ConvertToBuiltin(pair.Second, table);

            if (displayExpression == null)
                displayExpression = pair.Second;

            sb.Append("(" + Evaluator.ConvertToString(displayExpression, table) + ")");

            return sb.ToString();
        }

        public Expression CreateMaybe(Expression value)
        {
            return new FreeSymbolExpression { Name = "_Maybe", Tag = value, Display = _MaybeDisplay };
        }

        private string _MaybeDisplay(Expression expression, SymbolTable table)
        {
            var freeSymbol = (FreeSymbolExpression)expression;
            Expression value = (Expression)freeSymbol.Tag;
            StringBuilder sb = new StringBuilder();

            sb.Append("<" + freeSymbol.Name + ">");

            Expression displayExpression;

            if (value != null)
            {
                displayExpression = Evaluator.ConvertToBuiltin(value, table);

                if (displayExpression == null)
                    displayExpression = value;

                sb.Append("(" + Evaluator.ConvertToString(displayExpression, table) + ")");
            }
            else
            {
                sb.Append("Nothing");
            }

            return sb.ToString();
        }

        private Expression _SuccessorEvaluate(Expression expression)
        {
            var builtin = (BuiltinExpression)expression;

            if (builtin.Left is FreeSymbolExpression)
            {
                var left = (FreeSymbolExpression)builtin.Left;

                if (left.Name == "_Integer")
                    return new FreeSymbolExpression { Name = left.Name, Tag = (int)((FreeSymbolExpression)builtin.Left).Tag + 1, Display = left.Display };
            }

            return expression;
        }

        internal Expression Lookup(string name)
        {
            return _names[name];
        }

        public bool Lookup(string name, out Expression expression)
        {
            return _names.TryGetValue(name, out expression);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    public class SymbolTable
    {
        private SymbolTable _bottom;
        private SymbolTable _parent;
        private Dictionary<int, string> _boundSymbols;
        private Dictionary<string, int> _visibleSymbols = new Dictionary<string, int>();
        private int _nextId = 1;

        private BuiltinNames _builtinNames;

        public SymbolTable()
            : this(null)
        { }

        public SymbolTable(SymbolTable parent)
        {
            _parent = parent;

            if (parent != null)
            {
                _bottom = parent._bottom;
            }
            else
            {
                _bottom = this;
                _boundSymbols = new Dictionary<int, string>();
                _builtinNames = new BuiltinNames(this);
            }
        }

        public BuiltinNames BuiltinNames
        {
            get { return _builtinNames; }
        }

        public int Register(string name)
        {
            int id;

            if (_visibleSymbols.ContainsKey(name))
                throw new Exception("The name '" + name + "' is already used in this context");

            id = _bottom._nextId++;
            _bottom._boundSymbols.Add(id, name);

            _visibleSymbols.Add(name, id);

            return id;
        }

        public int Lookup(string name)
        {
            int symbol;

            if (_visibleSymbols.TryGetValue(name, out symbol))
                return symbol;

            if (_parent != null)
                return _parent.Lookup(name);
            else
                return 0;
        }

        public string Lookup(int id)
        {
            string name;

            if (_bottom._boundSymbols.TryGetValue(id, out name))
                return name;

            return null;
        }
    }
}

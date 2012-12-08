using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    class Program
    {
        public static SymbolTable Table = new SymbolTable();
        static Dictionary<string, Expression> Names = new Dictionary<string, Expression>();

        static void Main(string[] args)
        {
            while (true)
            {
                Console.Write("> ");

                string line = Console.ReadLine();

                if (line.StartsWith("let ", StringComparison.OrdinalIgnoreCase))
                {
                    ExecuteLetCommand(line);
                }
                else if (line.StartsWith(":load ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    try
                    {
                        string[] fileLines = System.IO.File.ReadAllLines(s[1]);

                        foreach (var fileLine in fileLines)
                        {
                            if (fileLine.StartsWith("let ", StringComparison.OrdinalIgnoreCase))
                                ExecuteLetCommand(fileLine);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                }
                else if (line.Equals(":names", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var pair in Names)
                        Console.WriteLine(pair.Key + " = " + Evaluator.ConvertToString(pair.Value, Table));
                }
                else if (line.StartsWith(":name ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (Names.ContainsKey(s[1]))
                        Console.WriteLine(s[1] + " = " + Evaluator.ConvertToString(Names[s[1]], Table));
                    else
                        Console.WriteLine("'" + s[1] + "' is not defined.");
                }
                else if (line.StartsWith(":undef ", StringComparison.OrdinalIgnoreCase))
                {
                    string[] s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (Names.ContainsKey(s[1]))
                        Names.Remove(s[1]);
                    else
                        Console.WriteLine("'" + s[1] + "' is not defined.");
                }
                else
                {
                    Expression expression;

                    try
                    {
                        expression = Parser.Parse(line, Table);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Parse error: " + ex.Message);
                        continue;
                    }

                    PrintSimplifiedExpression(expression, true);
                }
            }
        }

        static void PrintSimplifiedExpression(Expression expression, bool setLastValue = false)
        {
            bool success = false;
            System.Threading.Thread safeThread = new System.Threading.Thread(
                delegate()
                {
                    try
                    {
                        expression = Evaluator.Evaluate(expression, ResolveFunction, 2000, EvaluatorFlags.Lazy);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Evaluation error: " + ex.Message);
                    }
                },
                4 * 1024 * 1024
                );

            safeThread.Start();
            safeThread.Join();

            if (!success)
                return;

            Console.WriteLine(Evaluator.ConvertToString(expression, Table));

            try
            {
                var builtin = Evaluator.ConvertToBuiltin(expression, Table);

                if (builtin != null)
                    Console.WriteLine("-> " + Evaluator.ConvertToString(builtin, Table));
            }
            catch
            { }

            if (setLastValue)
                Names["%"] = expression;
        }

        static Expression ResolveFunction(Expression expression)
        {
            if (expression is FreeSymbolExpression)
            {
                var freeSymbol = (FreeSymbolExpression)expression;
                Expression replacement;

                if (Table.BuiltinNames.Lookup(freeSymbol.Name, out replacement))
                    return replacement;

                HashSet<string> trace = null;

                while (true)
                {
                    // Check for circular references.
                    if (trace != null && trace.Contains(freeSymbol.Name))
                        break;

                    if (!Names.TryGetValue(freeSymbol.Name, out replacement))
                        break;

                    expression = Evaluator.ReplaceBoundSymbols(replacement, Evaluator.CreateNewInstance());

                    if (trace == null)
                        trace = new HashSet<string>();

                    trace.Add(freeSymbol.Name);

                    if (expression is FreeSymbolExpression)
                        freeSymbol = (FreeSymbolExpression)expression;
                    else
                        break;
                }

                return expression;
            }
            else
            {
                return expression;
            }
        }

        static void ExecuteLetCommand(string line)
        {
            string[] s = line.Split(new char[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (s.Length < 2)
            {
                Console.WriteLine("Syntax: let NAME [:]= EXPRESSION");
                return;
            }

            string[] s2 = s[1].Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);

            if (s2.Length < 2)
            {
                Console.WriteLine("Syntax: let NAME [:]= EXPRESSION");
                return;
            }

            int evaluateLevel = s2[0].Count(c => c == ':');
            string lhs = s2[0].Replace(":", "").Trim();
            string rhs = s2[1].Trim();

            Expression expression;

            try
            {
                expression = Parser.Parse(rhs, Table);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Parse error: " + ex.Message);
                return;
            }

            if (evaluateLevel != 0)
            {
                try
                {
                    expression = Evaluator.Evaluate(expression, resolveFunction: ResolveFunction, flags: evaluateLevel > 1 ? 0 : EvaluatorFlags.ResolveOnly);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Evaluation error: " + ex.Message);
                    return;
                }
            }

            if (Names.ContainsKey(lhs))
                Names[lhs] = expression;
            else
                Names.Add(lhs, expression);

            Console.WriteLine(lhs + " = " + Evaluator.ConvertToString(expression, Table));
        }
    }
}

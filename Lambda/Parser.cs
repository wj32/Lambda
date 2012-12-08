using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lambda
{
    public static class Parser
    {
        private enum TokenType
        {
            None,
            Id,
            Symbol,
            Eof
        }

        private struct Token
        {
            public TokenType Type;
            public string Text;
        }

        private static IEnumerable<Token> Tokenize(string text)
        {
            TokenType runType = TokenType.None;
            int runStart = 0;
            int runLength = 0;

            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i]) || text[i] == '-' || text[i] == '_' || text[i] == '\'' || text[i] == '$' || text[i] == '%')
                {
                    if (runType != TokenType.Id)
                    {
                        runType = TokenType.Id;
                        runStart = i;
                        runLength = 0;
                    }

                    runLength++;
                }
                else if (text[i] == '(' || text[i] == ')' || text[i] == '\\' || text[i] == '.' || text[i] == ',')
                {
                    if (runType == TokenType.Id)
                    {
                        yield return new Token { Type = TokenType.Id, Text = text.Substring(runStart, runLength) };
                        runType = TokenType.None;
                    }

                    yield return new Token { Type = TokenType.Symbol, Text = text[i].ToString() };
                }
                else if (char.IsWhiteSpace(text[i]))
                {
                    if (runType == TokenType.Id)
                    {
                        yield return new Token { Type = TokenType.Id, Text = text.Substring(runStart, runLength) };
                        runType = TokenType.None;
                    }
                }
                else
                {
                    throw new Exception("invalid character '" + text[i] + "'");
                }
            }

            if (runType == TokenType.Id)
            {
                yield return new Token { Type = TokenType.Id, Text = text.Substring(runStart, runLength) };
            }

            yield return new Token { Type = TokenType.Eof, Text = null };
        }

        public static Expression Parse(string text, SymbolTable table)
        {
            var tokens = Tokenize(text);
            var enumerator = tokens.GetEnumerator();

            if (!enumerator.MoveNext())
                throw new Exception("token list must not be empty");

            var expression = ParseExpression(enumerator, table);

            if (enumerator.Current.Type != TokenType.Eof)
                throw new Exception("unexpected token '" + enumerator.Current.Text + "'");

            return expression;
        }

        private static List<string> ParseBindList(IEnumerator<Token> tokens)
        {
            var list = new List<string>();

            while (true)
            {
                if (tokens.Current.Type == TokenType.Id)
                {
                    list.Add(tokens.Current.Text);
                    tokens.MoveNext();

                    if (tokens.Current.Type == TokenType.Symbol && tokens.Current.Text == ",")
                    {
                        tokens.MoveNext();
                        continue;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    throw new Exception("identifier expected");
                }
            }

            return list;
        }

        private static Expression ParseExpression(IEnumerator<Token> tokens, SymbolTable table)
        {
            return ParseAbstractionExpression(tokens, table);
        }

        private static Expression ParseAbstractionExpression(IEnumerator<Token> tokens, SymbolTable table)
        {
            if (tokens.Current.Type == TokenType.Symbol && tokens.Current.Text == "\\")
            {
                tokens.MoveNext();
                var bindList = ParseBindList(tokens);

                if (tokens.Current.Type == TokenType.Symbol && tokens.Current.Text == ".")
                {
                    SymbolTable childTable = new SymbolTable(table);
                    var bindIdList = new List<int>(bindList.Count);

                    for (int i = 0; i < bindList.Count; i++)
                        bindIdList.Add(childTable.Register(bindList[i]));

                    tokens.MoveNext();
                    var rest = ParseAbstractionExpression(tokens, childTable);

                    Expression rhs = rest;

                    for (int i = 0; i < bindIdList.Count; i++)
                    {
                        rhs = new AbstractionExpression { Left = new SymbolRef(bindIdList[bindIdList.Count - i - 1]), Right = rhs };
                    }

                    return rhs;
                }
                else
                {
                    throw new Exception("'.' expected");
                }
            }
            else
            {
                return ParseApplicationExpression(tokens, table);
            }
        }

        private static Expression ParseApplicationExpression(IEnumerator<Token> tokens, SymbolTable table)
        {
            ApplicationExpression result = null;

            while (true)
            {
                var expression = ParsePrimaryExpression(tokens, table);

                if (expression == null)
                    break;

                if (result == null)
                    result = new ApplicationExpression { Left = expression, Right = null };
                else if (result.Right == null)
                    result.Right = expression;
                else
                    result = new ApplicationExpression { Left = result, Right = expression };
            }

            if (result == null)
                throw new Exception("expression expected");

            if (result.Right == null)
                return result.Left; // PrimaryExpression
            else
                return result;
        }

        private static Expression ParsePrimaryExpression(IEnumerator<Token> tokens, SymbolTable table)
        {
            if (tokens.Current.Type == TokenType.Symbol && tokens.Current.Text == "(")
            {
                tokens.MoveNext();
                var expression = ParseExpression(tokens, table);

                if (tokens.Current.Type == TokenType.Symbol && tokens.Current.Text == ")")
                {
                    tokens.MoveNext();
                    return expression;
                }
                else
                {
                    throw new Exception("')' expected");
                }
            }
            else if (tokens.Current.Type == TokenType.Id)
            {
                string name = tokens.Current.Text;
                int id = table.Lookup(name);
                tokens.MoveNext();

                if (id != 0)
                    return new BoundSymbolExpression { Symbol = new SymbolRef(id) };
                else
                    return new FreeSymbolExpression { Name = name };
            }
            else
            {
                return null;
            }
        }
    }
}

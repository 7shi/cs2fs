using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpParser
{
    public enum TokenType
    {
        None, Error, Space, NewLine, Name, Operator, Separator, Keyword,
        Int, UInt, Long, ULong, Float, Double, String, Char,
        Comment, Comment1, BeginBlock, EndBlock, Comma
    }

    public class Token
    {
        public string Text { get; private set; }
        public TokenType Type { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public Token(string name, TokenType type, int line, int column)
        {
            Text = name;
            Type = type;
            Line = line;
            Column = column;
        }
    }

    public class Lexer
    {
        private int column, line, position;
        public string Source { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }
        public int Position { get; private set; }
        public string Token { get; private set; }
        public TokenType Type { get; private set; }

        public static readonly string[] Operators =
        {
            ".", "(", ")", "[", "]", "++", "--", "->",
            "+", "-", "!", "~", "&", "*", "/", "%",
            "<<", ">>", "<", ">", "<=", ">=", "==", "!=",
            "^", "|", "&&", "||", "??", "?:", "=", "+=",
            "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=",
            ">>=", "=>"
        };

        public static readonly string[] Keywords =
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default", "delegate",
            "do", "double", "else", "enum", "event", "explicit", "extern", "false",
            "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit",
            "in", "int", "interface", "internal", "is", "lock", "long", "namespace",
            "new", "null", "object", "operator", "out", "override", "params", "private",
            "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        private static readonly Dictionary<char, string[]> OpDic = new Func<Dictionary<char, string[]>>(() =>
        {
            var dic = new Dictionary<char, List<string>>();
            foreach (var op in Operators)
            {
                List<string> list;
                if (!dic.TryGetValue(op[0], out list))
                {
                    list = new List<string>();
                    dic[op[0]] = list;
                }
                list.Add(op);
            }
            var keys = new List<char>(dic.Keys);
            keys.Sort();
            var ret = new Dictionary<char, string[]>();
            for (int i = 0; i < keys.Count; i++)
            {
                var key = keys[i];
                var list = dic[key];
                list.Sort((a, b) =>
                {
                    if (a.Length == b.Length)
                        return a.CompareTo(b);
                    else
                        return b.Length - a.Length;
                });
                ret[key] = list.ToArray();
            }
            return ret;
        })();

        private static readonly string OpHeads = new Func<string>(() =>
        {
            var keys = new List<char>(OpDic.Keys);
            keys.Sort();
            return string.Concat(keys);
        })();

        private static readonly Dictionary<string, int> KwStrs = new Func<Dictionary<string, int>>(() =>
        {
            var ret = new Dictionary<string, int>();
            foreach (var op in Keywords)
                ret[op] = 0;
            return ret;
        })();

        public Lexer(string source)
        {
            Source = source;
            column = 1;
            line = 1;
        }

        public Token[] ReadAllTokens()
        {
            var list = new List<Token>();
            while (Read())
                list.Add(new Token(Token, Type, Line, Column));
            if (Type == TokenType.Error)
                return null;
            else
                return list.ToArray();
        }

        public bool Read()
        {
            Column = column;
            Line = line;
            Position = position;

            if (Source == null || position >= Source.Length)
            {
                Token = "";
                Type = TokenType.None;
                return false;
            }

            var ch = Source[position];
            if (ch == ' ' || ch == '\t')
            {
                Token = ReadSpaces();
                Type = TokenType.Space;
                return true;
            }
            else if (ch == '\r')
            {
                position++;
                if (position < Source.Length)
                {
                    if (Source[position] == '\n') position++;
                }
                column = 1;
                line++;
                Token = Source.Substring(Position, position - Position);
                Type = TokenType.NewLine;
                return true;
            }
            else if (ch == '\n')
            {
                position++;
                column = 1;
                line++;
                Token = "\n";
                Type = TokenType.NewLine;
                return true;
            }
            else if (ch == ';')
            {
                position++;
                column++;
                Token = ";";
                Type = TokenType.Separator;
                return true;
            }
            else if (ch == '\'')
                return ReadChar();
            else if (ch == '"')
                return ReadString();
            else if (ch == '{')
            {
                position++;
                column++;
                Token = "{";
                Type = TokenType.BeginBlock;
                return true;
            }
            else if (ch == '}')
            {
                position++;
                column++;
                Token = "}";
                Type = TokenType.EndBlock;
                return true;
            }
            else if (ch == ',')
            {
                position++;
                column++;
                Token = ",";
                Type = TokenType.Comma;
                return true;
            }
            else if (ch == '/' && IsBeginComment())
                return ReadComment();
            else if (char.IsNumber(ch))
                return ReadNumber();
            else if (ch == '_' || char.IsLetter(ch))
            {
                do
                {
                    position++;
                    column++;
                } while (position < Source.Length && IsLetter(Source[position]));
                Token = Source.Substring(Position, position - Position);
                if (KwStrs.ContainsKey(Token))
                    Type = TokenType.Keyword;
                else
                    Type = TokenType.Name;
                return true;
            }
            else
            {
                var op = GetOperator();
                if (op != "")
                {
                    position += op.Length;
                    column += op.Length;
                    Token = op;
                    Type = TokenType.Operator;
                    return true;
                }
                else
                {
                    position++;
                    column++;
                    Error("invalid character");
                    return false;
                }
            }
        }

        public static bool IsLetter(char ch)
        {
            return ch == '_' || char.IsLetterOrDigit(ch);
        }

        public static bool IsNewLine(char ch)
        {
            return ch == '\r' || ch == '\n';
        }

        private bool IsBeginComment()
        {
            if (position + 1 < Source.Length)
            {
                var ch = Source[position + 1];
                return Source[position] == '/' && (ch == '/' || ch == '*');
            }
            else
            {
                return false;
            }
        }

        private bool IsEndComment()
        {
            if (position + 1 < Source.Length)
                return Source[position] == '*' && Source[position + 1] == '/';
            else
                return false;
        }

        private bool ReadComment()
        {
            position += 2;
            column += 2;
            if (Source[position - 1] == '/')
            {
                while (position < Source.Length && !IsNewLine(Source[position]))
                {
                    position++;
                    column++;
                }
                Type = TokenType.Comment1;
                Token = Source.Substring(Position, position - Position);
                return true;
            }
            else
            {
                while (position < Source.Length && !IsEndComment())
                {
                    position++;
                    column++;
                }
                if (position + 1 < Source.Length)
                {
                    position += 2;
                    column += 2;
                    Type = TokenType.Comment;
                    Token = Source.Substring(Position, position - Position);
                    return true;
                }
                else
                {
                    Error("unterminated comment");
                    return false;
                }
            }
        }

        private bool ReadString()
        {
            position++;
            column++;
            while (position < Source.Length && Source[position] != '"')
            {
                if (position + 1 < Source.Length && Source[position] == '\\')
                {
                    position += 2;
                    column += 2;
                }
                else
                {
                    position++;
                    column++;
                }
            }
            if (position < Source.Length && Source[position] == '"')
            {
                position++;
                column++;
                Token = Source.Substring(Position, position - Position);
                Type = TokenType.String;
                return true;
            }
            else
            {
                Error("unterminated string");
                return false;
            }
        }

        private bool ReadChar()
        {
            position++;
            column++;
            if (position < Source.Length)
            {
                if (Source[position] == '\\')
                {
                    position++;
                    column++;
                }
                if (position + 1 < Source.Length && Source[position + 1] == '\'')
                {
                    position += 2;
                    column += 2;
                    Token = Source.Substring(Position, position - Position);
                    Type = TokenType.Char;
                    return true;
                }
                else
                {
                    Error("unterminated character");
                    return false;
                }
            }
            else
            {
                Error("unterminated character");
                return false;
            }
        }

        private string GetOperator()
        {
            var ch = Source[position];
            if (!OpHeads.Contains(ch))
                return "";
            else
            {
                int max = Source.Length - position;
                var ret = "";
                foreach (var op in OpDic[ch])
                {
                    if (ret == "" && op.Length <= max && Source.Substring(position, op.Length) == op)
                        ret = op;
                }
                return ret;
            }
        }

        private string ReadSpaces()
        {
            var sb = new StringBuilder();
            while (position < Source.Length)
            {
                char ch = Source[position];
                if (ch == ' ')
                {
                    sb.Append(ch);
                    position++;
                    column++;
                }
                else if (ch == '\t')
                {
                    int len = 4 - ((column - 1) & 3);
                    sb.Append(new String(' ', len));
                    position++;
                    column += len;
                }
                else
                    break;
            }
            return sb.ToString();
        }

        private bool ReadNumber()
        {
            do
            {
                position++;
                column++;
            } while (position < Source.Length && char.IsNumber(Source[position]));
            if (position + 1 < Source.Length && Source[position] == '.' && char.IsNumber(Source[position + 1]))
                ReadFloat();
            else
            {
                if (position < Source.Length)
                {
                    var ch2 = char.ToLower(Source[position]);
                    if (ch2 == 'u')
                    {
                        position++;
                        column++;
                        if (position < Source.Length && char.ToLower(Source[position]) == 'l')
                        {
                            position++;
                            column++;
                            Type = TokenType.ULong;
                        }
                        else
                        {
                            Type = TokenType.UInt;
                        }
                    }
                    else if (ch2 == 'l')
                    {
                        position++;
                        column++;
                        Type = TokenType.Long;
                    }
                    else
                        Type = TokenType.Int;
                }
                else
                    Type = TokenType.Int;
            }
            Token = Source.Substring(Position, position - Position);
            return true;
        }

        private void ReadFloat()
        {
            position++;
            column++;
            do
            {
                position++;
                column++;
            } while (position < Source.Length && char.IsNumber(Source[position]));
            if (position < Source.Length)
            {
                var ch2 = char.ToLower(Source[position]);
                if (ch2 == 'f')
                {
                    position++;
                    column++;
                    Type = TokenType.Float;
                }
                else if (ch2 == 'd')
                {
                    position++;
                    column++;
                    Type = TokenType.Double;
                }
                else
                    Type = TokenType.Double;
            }
            else
                Type = TokenType.Double;
        }

        private void Error(string message)
        {
            Token = Source.Substring(Position, position - Position);
            Type = TokenType.Error;
            Debug.WriteLine(string.Format(
                "[{0},{1}] {2}: {3}",
                Line, Column, message, Token));
        }
    }
}

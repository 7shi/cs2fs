using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpParser
{
    public enum TokenType
    {
        None, Error, Space, NewLine, Any, Operator, Separator, Keyword,
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

        public string Align(int tab)
        {
            if (Type != TokenType.Space)
                return Text;
            else
            {
                var sw = new StringWriter();
                int column = Column;
                foreach (var ch in Text)
                {
                    if (ch == '\t')
                    {
                        int len = tab - ((column - 1) % tab);
                        sw.Write(new String(' ', len));
                        column += len;
                    }
                    else
                    {
                        sw.Write(ch);
                        column++;
                    }
                }
                sw.Close();
                return sw.ToString();
            }
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

        private char current;

        public static readonly string[] Operators =
        {
            ".", "(", ")", "[", "]", "++", "--", "->",
            "+", "-", "!", "~", "&", "*", "/", "%",
            "<<", ">>", "<", ">", "<=", ">=", "==", "!=",
            "^", "|", "&&", "||", "??", "?:", "=", "+=",
            "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=",
            ">>=", "=>", "?", ":"
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
            if (Source.Length > 0) current = Source[0];
        }

        private bool CanRead(int len)
        {
            return position + len <= Source.Length;
        }

        private void NextChar()
        {
            position++;
            column++;
            current = position < Source.Length ? Source[position] : '\0';
        }

        private void SetResult(TokenType type)
        {
            Token = Source.Substring(Position, position - Position);
            Type = type;
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

            if (current == ' ' || current == '\t')
            {
                while (CanRead(1) && IsSpace(current))
                    NextChar();
                SetResult(TokenType.Space);
                return true;
            }
            else if (current == '\r')
            {
                NextChar();
                if (CanRead(1))
                {
                    if (current == '\n') NextChar();
                }
                column = 1;
                line++;
                SetResult(TokenType.NewLine);
                return true;
            }
            else if (current == '\n')
            {
                NextChar();
                column = 1;
                line++;
                Token = "\n";
                Type = TokenType.NewLine;
                return true;
            }
            else if (current == ';')
            {
                NextChar();
                Token = ";";
                Type = TokenType.Separator;
                return true;
            }
            else if (current == '\'')
                return ReadChar();
            else if (current == '"')
                return ReadString();
            else if (current == '{')
            {
                NextChar();
                Token = "{";
                Type = TokenType.BeginBlock;
                return true;
            }
            else if (current == '}')
            {
                NextChar();
                Token = "}";
                Type = TokenType.EndBlock;
                return true;
            }
            else if (current == ',')
            {
                NextChar();
                Token = ",";
                Type = TokenType.Comma;
                return true;
            }
            else if (current == '/' && IsBeginComment())
                return ReadComment();
            else if (char.IsNumber(current))
                return ReadNumber();
            else if (IsFirstLetter(current))
            {
                NextChar();
                while (CanRead(1) && IsLetter(current))
                    NextChar();
                Token = Source.Substring(Position, position - Position);
                Type = KwStrs.ContainsKey(Token) ? TokenType.Keyword : TokenType.Any;
                return true;
            }
            else
            {
                var op = GetOperator();
                if (op != "")
                {
                    position += op.Length;
                    column += op.Length;
                    current = position < Source.Length ? Source[position] : '\0';
                    Token = op;
                    Type = TokenType.Operator;
                    return true;
                }
                else
                {
                    NextChar();
                    Error("invalid character");
                    return false;
                }
            }
        }

        public static bool IsSpace(char ch)
        {
            return ch == ' ' || ch == '\t';
        }

        public static bool IsFirstLetter(char ch)
        {
            return ch == '_' || ('A' <= ch && ch <= 'Z') || ('a' <= ch && ch <= 'z') || ch >= (char)128;
        }

        public static bool IsLetter(char ch)
        {
            return IsFirstLetter(ch) || char.IsNumber(ch);
        }

        public static bool IsNewLine(char ch)
        {
            return ch == '\r' || ch == '\n';
        }

        private bool IsBeginComment()
        {
            if (CanRead(2))
            {
                var ch = Source[position + 1];
                return current == '/' && (ch == '/' || ch == '*');
            }
            else
                return false;
        }

        private bool IsEndComment()
        {
            if (CanRead(2))
                return current == '*' && Source[position + 1] == '/';
            else
                return false;
        }

        private bool ReadComment()
        {
            NextChar();
            var ch = current;
            NextChar();
            if (ch == '/')
            {
                while (CanRead(1) && !IsNewLine(current))
                    NextChar();
                SetResult(TokenType.Comment1);
                return true;
            }
            else
            {
                while (CanRead(1) && !IsEndComment())
                    NextChar();
                if (position + 1 < Source.Length)
                {
                    NextChar();
                    NextChar();
                    SetResult(TokenType.Comment);
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
            NextChar();
            while (CanRead(1) && current != '"')
            {
                if (position + 1 < Source.Length && current == '\\')
                    NextChar();
                NextChar();
            }
            if (current == '"')
            {
                NextChar();
                SetResult(TokenType.String);
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
            NextChar();
            if (CanRead(1))
            {
                if (position + 1 < Source.Length && current == '\\')
                    NextChar();
                NextChar();
                if (current == '\'')
                {
                    NextChar();
                    SetResult(TokenType.Char);
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
            if (!OpHeads.Contains(current))
                return "";
            else
            {
                int max = Source.Length - position;
                var ret = "";
                foreach (var op in OpDic[current])
                {
                    if (ret == "" && op.Length <= max && Source.Substring(position, op.Length) == op)
                        ret = op;
                }
                return ret;
            }
        }

        private bool ReadNumber()
        {
            NextChar();
            while (CanRead(1) && char.IsNumber(current))
                NextChar();
            if (current == '.')
                ReadFloat();
            else
            {
                if (CanRead(1))
                {
                    var ch2 = char.ToLower(current);
                    if (ch2 == 'u')
                    {
                        NextChar();
                        if (CanRead(1) && char.ToLower(current) == 'l')
                        {
                            NextChar();
                            SetResult(TokenType.ULong);
                        }
                        else
                        {
                            SetResult(TokenType.UInt);
                        }
                    }
                    else if (ch2 == 'l')
                    {
                        NextChar();
                        SetResult(TokenType.Long);
                    }
                    else
                        SetResult(TokenType.Int);
                }
                else
                    SetResult(TokenType.Int);
            }
            return true;
        }

        private void ReadFloat()
        {
            NextChar();
            while (CanRead(1) && char.IsNumber(current))
                NextChar();
            if (CanRead(1))
            {
                var ch2 = char.ToLower(current);
                if (ch2 == 'f')
                {
                    NextChar();
                    SetResult(TokenType.Float);
                }
                else if (ch2 == 'd')
                {
                    NextChar();
                    SetResult(TokenType.Double);
                }
                else
                    SetResult(TokenType.Double);
            }
            else
                SetResult(TokenType.Double);
        }

        private void Error(string message)
        {
            SetResult(TokenType.Error);
            Debug.WriteLine(string.Format(
                "[{0},{1}] {2}: {3}", Line, Column, message, Token));
        }
    }
}

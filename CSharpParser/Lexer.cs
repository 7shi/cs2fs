using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpParser
{
    public class Lexer
    {
        private int clm, lin, pos;
        private char cur;
        private string src;

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

        public Lexer(string src)
        {
            this.src = src;
            clm = 1;
            lin = 1;
            if (src.Length > 0) cur = src[0];
        }

        private bool MoveNext()
        {
            if (pos < src.Length)
            {
                pos++;
                clm++;
                if (pos < src.Length)
                {
                    cur = src[pos];
                    return true;
                }
                else
                {
                    cur = '\0';
                    return false;
                }
            }
            else
            {
                cur = '\0';
                return false;
            }
        }

        private void SetResult(TokenType type)
        {
            Token = src.Substring(Position, pos - Position);
            Type = type;
        }

        public Token[] ReadAllTokens()
        {
            var list = new List<Token>();
            while (Read())
                list.Add(new Token(Token, Type, Line, Column));
            return list.ToArray();
        }

        public bool Read()
        {
            Column = clm;
            Line = lin;
            Position = pos;

            if (src == null || pos >= src.Length)
            {
                Token = "";
                Type = TokenType.None;
                return false;
            }
            else
            {
                if (cur == ' ' || cur == '\t')
                {
                    while (MoveNext() && IsSpace(cur)) ;
                    SetResult(TokenType.Space);
                }
                else if (cur == '\r')
                {
                    if (MoveNext())
                    {
                        if (cur == '\n') MoveNext();
                    }
                    clm = 1;
                    lin++;
                    SetResult(TokenType.NewLine);
                }
                else if (cur == '\n')
                {
                    MoveNext();
                    clm = 1;
                    lin++;
                    Token = "\n";
                    Type = TokenType.NewLine;
                }
                else if (cur == ';')
                {
                    MoveNext();
                    Token = ";";
                    Type = TokenType.Separator;
                }
                else if (cur == '\'')
                    ReadChar();
                else if (cur == '"')
                    ReadString();
                else if (cur == '{')
                {
                    MoveNext();
                    Token = "{";
                    Type = TokenType.BeginBlock;
                }
                else if (cur == '}')
                {
                    MoveNext();
                    Token = "}";
                    Type = TokenType.EndBlock;
                }
                else if (cur == ',')
                {
                    MoveNext();
                    Token = ",";
                    Type = TokenType.Comma;
                }
                else if (cur == '/' && IsBeginComment())
                    ReadComment();
                else if (char.IsNumber(cur))
                    ReadNumber();
                else if (IsFirstLetter(cur))
                {
                    while (MoveNext() && IsLetter(cur)) ;
                    Token = src.Substring(Position, pos - Position);
                    Type = KwStrs.ContainsKey(Token) ? TokenType.Keyword : TokenType.Any;
                }
                else
                {
                    var op = GetOperator();
                    if (op != "")
                    {
                        pos += op.Length;
                        clm += op.Length;
                        cur = pos < src.Length ? src[pos] : '\0';
                        Token = op;
                        Type = TokenType.Operator;
                    }
                    else
                        throw new Exception("invalid character");
                }
                return true;
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
            if (pos + 1 < src.Length)
            {
                var ch = src[pos + 1];
                return cur == '/' && (ch == '/' || ch == '*');
            }
            else
                return false;
        }

        private bool IsEndComment()
        {
            if (pos + 1 < src.Length)
                return cur == '*' && src[pos + 1] == '/';
            else
                return false;
        }

        private void ReadComment()
        {
            MoveNext();
            if (cur == '/')
            {
                while (MoveNext() && !IsNewLine(cur)) ;
                SetResult(TokenType.Comment1);
            }
            else
            {
                while (MoveNext() && !IsEndComment()) ;
                if (MoveNext() && MoveNext())
                    SetResult(TokenType.Comment);
                else
                    throw Abort("unterminated comment");
            }
        }

        private void ReadString()
        {
            while (MoveNext() && cur != '"')
            {
                if (cur == '\\') MoveNext();
            }
            if (cur == '"')
            {
                MoveNext();
                SetResult(TokenType.String);
            }
            else
                throw Abort("unterminated string");
        }

        private void ReadChar()
        {
            if (MoveNext())
            {
                if (cur == '\\') MoveNext();
                if (MoveNext() && cur == '\'')
                {
                    MoveNext();
                    SetResult(TokenType.Char);
                }
                else
                    throw Abort("unterminated character");
            }
            else
                throw Abort("unterminated character");
        }

        private string GetOperator()
        {
            if (!OpHeads.Contains(cur))
                return "";
            else
            {
                int max = src.Length - pos;
                var ret = "";
                foreach (var op in OpDic[cur])
                {
                    if (ret == "" && op.Length <= max && src.Substring(pos, op.Length) == op)
                        ret = op;
                }
                return ret;
            }
        }

        private void ReadNumber()
        {
            while (MoveNext() && char.IsNumber(cur)) ;
            if (cur == '.')
                ReadFloat();
            else
            {
                var ch2 = char.ToLower(cur);
                if (ch2 == 'u')
                {
                    if (MoveNext() && char.ToLower(cur) == 'l')
                    {
                        MoveNext();
                        SetResult(TokenType.ULong);
                    }
                    else
                    {
                        SetResult(TokenType.UInt);
                    }
                }
                else if (ch2 == 'l')
                {
                    MoveNext();
                    SetResult(TokenType.Long);
                }
                else
                    SetResult(TokenType.Int);
            }
        }

        private void ReadFloat()
        {
            while (MoveNext() && char.IsNumber(cur)) ;
            var ch2 = char.ToLower(cur);
            if (ch2 == 'f')
            {
                MoveNext();
                SetResult(TokenType.Float);
            }
            else if (ch2 == 'd')
            {
                MoveNext();
                SetResult(TokenType.Double);
            }
            else
                SetResult(TokenType.Double);
        }

        private Exception Abort(string message)
        {
            return new Exception(string.Format(
                "[{0},{1}] {2}: {3}", Line, Column, message, Token));
        }
    }
}

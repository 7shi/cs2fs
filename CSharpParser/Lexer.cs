using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpParser
{
    public class Lexer
    {
        private static string[] Operators;
        private static Dictionary<char, string[]> OpDic;
        private static string OpHeads;

        private int clm;
        private int lin;
        private int pos;
        private char cur;
        private string src;

        public int Line { get; private set; }
        public int Column { get; private set; }
        public int Position { get; private set; }
        public string Token { get; private set; }
        public TokenType Type { get; private set; }

        public Lexer(string src)
        {
            if (Lexer.Operators == null)
            {
                Lexer.Operators = new[]
                {
                    ".", "(", ")", "[", "]", "++", "--", "->",
                    "+", "-", "!", "~", "&", "*", "/", "%",
                    "<<", ">>", "<", ">", "<=", ">=", "==", "!=",
                    "^", "|", "&&", "||", "??", "?:", "=", "+=",
                    "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=",
                    ">>=", "=>", "?", ":"
                };

                var dic = new Dictionary<char, List<string>>();
                foreach (var op in Lexer.Operators)
                {
                    if (dic.ContainsKey(op[0]))
                        dic[op[0]].Add(op);
                    else
                    {
                        var list = new List<string>();
                        dic[op[0]] = list;
                        list.Add(op);
                    }
                }
                var keys = new List<char>(dic.Keys);
                keys.Sort();
                Lexer.OpHeads = String.Concat(keys);
                Lexer.OpDic = new Dictionary<char, string[]>();
                foreach (var i in Enumerable.Range(0, keys.Count))
                {
                    var key = keys[i];
                    var list = dic[key];
                    list.Sort(delegate(string a, string b)
                    {
                        if (a.Length == b.Length)
                            return a.CompareTo(b);
                        else
                            return b.Length - a.Length;
                    });
                    Lexer.OpDic[key] = list.ToArray();
                }
            }

            this.src = src;
            this.clm = 1;
            this.lin = 1;
            if (src.Length > 0) this.cur = src[0];
        }

        private void MoveNext()
        {
            if (this.pos < this.src.Length)
            {
                this.pos = this.pos + 1;
                this.clm = this.clm + 1;
                if (this.pos < this.src.Length)
                    this.cur = this.src[this.pos];
                else
                    this.cur = (char)0;
            }
            else
                this.cur = (char)0;
        }

        private void SetResult(TokenType t)
        {
            this.Token = this.src.Substring(this.Position, this.pos - this.Position);
            this.Type = t;
        }

        public Token[] ReadAllTokens()
        {
            var list = new List<Token>();
            while (this.Read())
                list.Add(new Token(this.Token, this.Type, this.Line, this.Column));
            return list.ToArray();
        }

        public bool Read()
        {
            this.Column = this.clm;
            this.Line = this.lin;
            this.Position = this.pos;

            if (this.src == null || this.pos >= this.src.Length)
            {
                this.Token = "";
                this.Type = TokenType.None;
                return false;
            }
            else
            {
                if (this.cur == ' ' || this.cur == '\t')
                {
                    while (Lexer.IsSpace(this.cur)) this.MoveNext();
                    this.SetResult(TokenType.Space);
                }
                else if (this.cur == '\r')
                {
                    this.MoveNext();
                    if (this.cur == '\n') this.MoveNext();
                    this.clm = 1;
                    this.lin = this.lin + 1;
                    this.SetResult(TokenType.NewLine);
                }
                else if (this.cur == '\n')
                {
                    this.MoveNext();
                    this.clm = 1;
                    this.lin = this.lin + 1;
                    this.Token = "\n";
                    this.Type = TokenType.NewLine;
                }
                else if (this.cur == ';')
                {
                    this.MoveNext();
                    this.Token = ";";
                    this.Type = TokenType.Separator;
                }
                else if (this.cur == '\'')
                    this.ReadChar();
                else if (this.cur == '"')
                    this.ReadString();
                else if (this.cur == '{')
                {
                    this.MoveNext();
                    this.Token = "{";
                    this.Type = TokenType.BeginBlock;
                }
                else if (this.cur == '}')
                {
                    this.MoveNext();
                    this.Token = "}";
                    this.Type = TokenType.EndBlock;
                }
                else if (this.cur == ',')
                {
                    this.MoveNext();
                    this.Token = ",";
                    this.Type = TokenType.Comma;
                }
                else if (this.cur == '/' && this.IsBeginComment())
                    this.ReadComment();
                else if (Char.IsNumber(this.cur))
                    this.ReadNumber();
                else if (Lexer.IsFirstLetter(this.cur))
                {
                    while (Lexer.IsLetter(this.cur)) this.MoveNext();
                    this.Token = this.src.Substring(this.Position, this.pos - this.Position);
                    this.Type = TokenType.Any;
                }
                else
                {
                    var op = this.GetOperator();
                    if (op != "")
                    {
                        this.pos = this.pos + op.Length;
                        this.clm = this.clm + op.Length;
                        if (this.pos < this.src.Length)
                            this.cur = this.src[this.pos];
                        else
                            this.cur = (char)0;
                        this.Token = op;
                        this.Type = TokenType.Operator;
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
            return Lexer.IsFirstLetter(ch) || Char.IsNumber(ch);
        }

        public static bool IsNewLine(char ch)
        {
            return ch == '\r' || ch == '\n';
        }

        private bool IsBeginComment()
        {
            if (this.pos + 1 < this.src.Length)
            {
                var ch = this.src[this.pos + 1];
                return this.cur == '/' && (ch == '/' || ch == '*');
            }
            else
                return false;
        }

        private bool IsEndComment()
        {
            if (this.pos + 1 < this.src.Length)
                return this.cur == '*' && this.src[this.pos + 1] == '/';
            else
                return false;
        }

        private void ReadComment()
        {
            this.MoveNext();
            if (this.cur == '/')
            {
                while (!(Lexer.IsNewLine(this.cur))) this.MoveNext();
                this.SetResult(TokenType.Comment1);
            }
            else
            {
                while (!(this.IsEndComment())) this.MoveNext();
                if (this.IsEndComment())
                {
                    this.MoveNext();
                    this.MoveNext();
                    this.SetResult(TokenType.Comment);
                }
                else
                    throw this.Abort("unterminated comment");
            }
        }

        private void ReadString()
        {
            this.MoveNext();
            while (this.cur != '"')
            {
                if (this.cur == '\\') this.MoveNext();
                this.MoveNext();
            }
            if (this.cur == '"')
            {
                this.MoveNext();
                this.SetResult(TokenType.String);
            }
            else
                throw this.Abort("unterminated string");
        }

        private void ReadChar()
        {
            this.MoveNext();
            if (this.cur == '\\') this.MoveNext();
            this.MoveNext();
            if (this.cur == '\'')
            {
                this.MoveNext();
                this.SetResult(TokenType.Char);
            }
            else
                throw this.Abort("unterminated character");
        }

        private string GetOperator()
        {
            if (!(Lexer.OpHeads.Contains(this.cur)))
                return "";
            else
            {
                var max = this.src.Length - this.pos;
                var ret = "";
                foreach (var op in Lexer.OpDic[this.cur])
                {
                    if (ret == "" && op.Length <= max && this.src.Substring(this.pos, op.Length) == op)
                        ret = op;
                }
                return ret;
            }
        }

        private void ReadNumber()
        {
            while (Char.IsNumber(this.cur)) this.MoveNext();
            if (this.cur == '.')
                this.ReadFloat();
            else
            {
                var ch2 = Char.ToLower(this.cur);
                if (ch2 == 'u')
                {
                    this.MoveNext();
                    if (Char.ToLower(this.cur) == 'l')
                    {
                        this.MoveNext();
                        this.SetResult(TokenType.ULong);
                    }
                    else
                    {
                        this.SetResult(TokenType.UInt);
                    }
                }
                else if (ch2 == 'l')
                {
                    this.MoveNext();
                    this.SetResult(TokenType.Long);
                }
                else
                    this.SetResult(TokenType.Int);
            }
        }

        private void ReadFloat()
        {
            while (Char.IsNumber(this.cur)) this.MoveNext();
            var ch2 = Char.ToLower(this.cur);
            if (ch2 == 'f')
            {
                this.MoveNext();
                this.SetResult(TokenType.Float);
            }
            else if (ch2 == 'd')
            {
                this.MoveNext();
                this.SetResult(TokenType.Double);
            }
            else
                this.SetResult(TokenType.Double);
        }

        private Exception Abort(string message)
        {
            return new Exception(String.Format(
                "[{0},{1}] {2}: {3}", this.Line, this.Column, message, this.Token));
        }
    }
}

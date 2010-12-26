using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CSharpParser
{
    public enum TokenType
    {
        None,
        Space,
        NewLine,
        Any,
        Operator,
        Separator,
        Int,
        UInt,
        Long,
        ULong,
        Float,
        Double,
        String,
        Char,
        Comment,
        Comment1,
        BeginBlock,
        EndBlock,
        Comma
    }

    public class Token
    {
        public string Text { get; private set; }
        public TokenType Type { get; private set; }
        public int Line { get; private set; }
        public int Column { get; private set; }

        public Token(string name, TokenType t, int line, int column)
        {
            this.Text = name;
            this.Type = t;
            this.Line = line;
            this.Column = column;
        }

        public string Align(int tab)
        {
            if (this.Type != TokenType.Space)
                return this.Text;
            else
            {
                var sw = new StringWriter();
                var column = this.Column;
                foreach (var ch in this.Text)
                {
                    if (ch == '\t')
                    {
                        var len = tab - ((column - 1) % tab);
                        sw.Write(new String(' ', len));
                        column = column + len;
                    }
                    else
                    {
                        sw.Write(ch);
                        column = column + 1;
                    }
                }
                sw.Close();
                return sw.ToString();
            }
        }

        public bool CanOmit
        {
            get
            {
                return this.Type == TokenType.Space
                     || this.Type == TokenType.NewLine
                     || this.Type == TokenType.Comment
                     || this.Type == TokenType.Comment1;
            }
        }

        public void Write(TextWriter tw)
        {
            tw.Write("[{0}, {1}] {2}: ", this.Line, this.Column, this.Type);
            switch (this.Type)
            {
                case TokenType.Space:
                    tw.WriteLine("{0}", this.Align(4).Length);
                    break;
                case TokenType.NewLine:
                    tw.WriteLine(this.Text.Replace("\r", "\\r").Replace("\n", "\\n"));
                    break;
                default:
                    tw.WriteLine(this.Text);
                    break;
            }

        }
    }
}

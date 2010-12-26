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
        Keyword,
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

        public bool CanOmit
        {
            get
            {
                return Type == TokenType.Space
                     || Type == TokenType.NewLine
                     || Type == TokenType.Comment
                     || Type == TokenType.Comment1;
            }
        }

        public void Write(TextWriter tw)
        {
            tw.Write("[{0}, {1}] {2}: ", Line, Column, Type);
            switch (Type)
            {
                case TokenType.Space:
                    tw.WriteLine("{0}", Align(4).Length);
                    break;
                case TokenType.NewLine:
                    tw.WriteLine(Text.Replace("\r", "\\r").Replace("\n", "\\n"));
                    break;
                default:
                    tw.WriteLine(Text);
                    break;
            }

        }
    }
}

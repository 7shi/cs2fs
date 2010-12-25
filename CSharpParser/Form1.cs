using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CSharpParser
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void convertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.Output = textBox2;
            textBox2.Clear();
            var csp = new Lexer(textBox1.Text);
            var tokens = csp.ReadAllTokens();
            if (tokens != null)
            {
                var sw = new StringWriter();
                foreach (var token in tokens)
                {
                    sw.Write("[{0}, {1}] {2}: ", token.Line, token.Column, token.Type);
                    switch (token.Type)
                    {
                        case TokenType.Space:
                            sw.WriteLine("{0}", token.Align(4).Length);
                            break;
                        case TokenType.NewLine:
                            sw.WriteLine(Debug.Escape(token.Text));
                            break;
                        default:
                            sw.WriteLine(token.Text);
                            break;
                    }
                }
                sw.Close();
                textBox2.Text = sw.ToString();
            }
        }
    }

    public static class Debug
    {
        public static TextBox Output;

        public static void Write(string format, params object[] args)
        {
            Output.AppendText(string.Format(format, args));
        }

        public static void WriteLine()
        {
            Output.AppendText(Environment.NewLine);
        }

        public static void WriteLine(string format, params object[] args)
        {
            Write(format, args);
            WriteLine();
        }

        public static string Escape(string s)
        {
            var sb = new StringBuilder();
            foreach (var ch in s)
            {
                switch (ch)
                {
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\");
                        break;
                    case '\0':
                        sb.Append("\\0");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }
            return sb.ToString();
        }
    }
}

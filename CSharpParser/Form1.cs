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

            var dir = Path.GetDirectoryName(Application.ExecutablePath);
            var sample = Path.Combine(dir, "Token.cs");
            try
            {
                textBox1.Text = File.ReadAllText(sample);
            }
            catch { }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void convertToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.Output = textBox2;
            textBox2.Clear();

#if !DEBUG
            try
#endif
            {
                var lex = new Lexer(textBox1.Text);
                var tokens = lex.ReadAllTokens();
                var conv = new Converter(tokens);
                conv.Convert();
            }
#if !DEBUG
            catch (Exception ex)
            {
                textBox2.AppendText("\r\n" + ex.Message + "\r\n");
            }
#endif
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBox2.TextLength > 0)
                Clipboard.SetText(textBox2.Text);
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

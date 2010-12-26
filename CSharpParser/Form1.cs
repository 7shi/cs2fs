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
            var sample = Path.Combine(dir, "Converter.cs");
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
            Debug.Stream = new StringWriter();
            textBox2.Clear();

#if DEBUG
            try
#endif
            {
                var lex = new Lexer(textBox1.Text);
                var tokens = lex.ReadAllTokens();
                var conv = new Converter(tokens);
                conv.Convert();
            }
#if DEBUG
            catch (Exception ex)
            {
                Debug.WriteLine();
                Debug.WriteLine(ex.Message);
            }
#endif
            Debug.Stream.Close();
            textBox2.AppendText(Debug.Stream.ToString());
        }

        private void copyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (textBox2.TextLength > 0)
                Clipboard.SetText(textBox2.Text);
        }

        private void pasteToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (Clipboard.ContainsText())
                textBox1.Text = Clipboard.GetText();
        }
    }

    public static class Debug
    {
        public static StringWriter Stream;

        public static void Write(string format, params object[] args)
        {
            Stream.Write(format, args);
        }

        public static void WriteLine()
        {
            Stream.WriteLine();
        }

        public static void WriteLine(string format, params object[] args)
        {
            Stream.WriteLine(format, args);
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

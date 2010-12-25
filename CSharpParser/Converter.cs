using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CSharpParser
{
    public class Converter
    {
        private Token[] tokens;
        private Token cur;
        private int pos;
        private string indent;

        public Converter(Token[] tokens)
        {
            this.tokens = tokens.Where(t => !t.CanOmit).ToArray();
            if (tokens.Length > 0) cur = tokens[0];
        }

        public bool MoveNext()
        {
            if (pos < tokens.Length)
            {
                pos++;
                cur = pos < tokens.Length ? tokens[pos] : null;
            }
            else
                cur = null;
            return cur != null;
        }

        public void Convert()
        {
            while (cur != null)
            {
                switch (cur.Text)
                {
                    case "using":
                        Debug.Write("open ");
                        while (MoveNext() && cur.Text != ";")
                            Debug.Write(cur.Text);
                        Debug.WriteLine();
                        MoveNext();
                        break;
                    case "namespace":
                        ReadNamespace();
                        break;
                    default:
                        MoveNext();
                        break;
                }
            }
        }

        public void ReadNamespace()
        {
            Debug.WriteLine();
            Debug.Write("namespace ");
            while (MoveNext() && cur.Text != "{")
                Debug.Write(cur.Text);
            Debug.WriteLine();
            MoveNext();
            while (cur != null && cur.Text != "}")
            {
                switch (cur.Text)
                {
                    case "class":
                        ReadClass();
                        break;
                    default:
                        MoveNext();
                        break;
                }
            }
            MoveNext();
        }

        public void ReadClass()
        {
            MoveNext();
            string name = cur.Text;
            MoveNext();
            Debug.WriteLine();
            Debug.WriteLine("type {0}() as this =", name);
            if (cur.Text == ":")
            {
                MoveNext();
                Debug.WriteLine("    inherit {0}()", cur.Text);
            }
            while (cur.Text != "{") MoveNext();
            while (cur != null && cur.Text != "}")
            {
                if (cur.Text == name)
                {
                    Debug.WriteLine("    do");
                    while (MoveNext() && cur.Text != "{") ;
                    indent = new string(' ', 8);
                    ReadMethod();
                }
                else
                    MoveNext();
            }
            MoveNext();
        }

        public void ReadMethod()
        {
            MoveNext();
            while (cur != null && cur.Text != "}")
            {
                switch (cur.Text)
                {
                    default:
                        ReadSentence();
                        break;
                }
            }
        }

        public void ReadSentence()
        {
            Debug.Write(indent);
            while (cur.Text != ";")
            {
                Debug.Write(cur.Text);
                MoveNext();
            }
            Debug.WriteLine();
            MoveNext();
        }
    }
}

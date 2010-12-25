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
        private List<string> usings;

        public Converter(Token[] tokens)
        {
            usings = new List<string>();
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
                        usings.Add(ReadUsing());
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

        public string ReadUsing()
        {
            var sw = new StringWriter();
            while (MoveNext() && cur.Text != ";")
                sw.Write(cur.Text);
            MoveNext();
            sw.Close();
            return sw.ToString();
        }

        public void ReadNamespace()
        {
            Debug.Write("namespace ");
            while (MoveNext() && cur.Text != "{")
                Debug.Write(cur.Text);
            Debug.WriteLine();
            if (usings.Count > 0)
            {
                Debug.WriteLine();
                foreach (var u in usings)
                    Debug.WriteLine("open {0}", u);
            }
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
            MoveNext();
            while (cur != null && cur.Text != "}")
                ReadMember("private", false);
            MoveNext();
        }

        public void ReadMember(string access, bool isStatic)
        {
            if (cur.Text == "static")
            {
                MoveNext();
                ReadMember(access, isStatic);
            }
            else if (IsAccess(cur.Text))
            {
                MoveNext();
                ReadMember(cur.Text, isStatic);
            }
            else
            {
                var decl = ReadDecl();
                if (cur.Text == "(")
                    ReadMethod(decl[0], decl[1], access, isStatic);
                else if (cur.Text == ";")
                    ReadField(decl[0], decl[1], access, isStatic);
                else
                    throw Abort("syntax error");
            }
        }

        public void ReadField(string name, string type, string access, bool isStatic)
        {
            MoveNext();
            Debug.Write("    [<DefaultValue>] ");
            if (isStatic) Debug.Write("static ");
            Debug.Write("val mutable ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} : {1}", name, type);
        }

        public void ReadMethod(string name, string type, string access, bool isStatic)
        {
            Debug.WriteLine();
            Debug.Write("    ");
            if (isStatic) Debug.Write("static ");
            if (type == null)
            {
                // constructor
                MoveNext();
                if (cur.Text == ")")
                {
                    // primary constructor
                    Debug.WriteLine("do");
                }
                else
                {
                    if (access == "private") Debug.Write("private ");
                    Debug.Write("new(");
                    ReadArgs();
                    Debug.WriteLine(") as this = {0}() then", name);
                }
            }
            else
            {
                Debug.Write("member ");
                if (access == "private") Debug.Write("private ");
                if (!isStatic) Debug.Write("this.");
                Debug.Write(name + "(");
                MoveNext();
                ReadArgs();
                if (type == "void")
                    Debug.WriteLine(") =");
                else
                    Debug.WriteLine(") : {0} =", type);
            }
            while (cur.Text != "{") MoveNext();
            indent = new string(' ', 8);
            ReadBlock();
        }

        public static string ConvType(string type)
        {
            switch (type)
            {
                case "uint":
                    return "uint32";
                case "short":
                    return "int16";
                case "ushort":
                    return "uint16";
                case "long":
                    return "int64";
                case "ulong":
                    return "uint64";
                default:
                    return type;
            }
        }

        public static string ConvOp(string op)
        {
            switch (op)
            {
                case "=":
                    return "<-";
                case "==":
                    return "=";
                case ">>":
                    return ">>>";
                case "<<":
                    return "<<<";
                case "&":
                    return "&&&";
                case "|":
                    return "|||";
                case "^":
                    return "^^^";
                default:
                    return op;
            }
        }

        public void ReadArgs()
        {
            while (cur.Text != ")")
            {
                ReadArg();
                if (cur.Text == ",")
                {
                    Debug.Write(", ");
                    MoveNext();
                }
            }
            MoveNext();
        }

        public string[] ReadDecl()
        {
            var list = new List<string>();
            while (cur.Text.Length > 1 || "();,".IndexOf(cur.Text) < 0)
            {
                list.Add(cur.Text);
                MoveNext();
            }
            if (list.Count < 1)
                throw Abort("missing type or name");
            var last = list.Count - 1;
            var name = list[last];
            list.RemoveAt(last);
            var type = list.Count > 0 ? ConvType(string.Concat(list)) : null;
            return new[] { name, type };
        }

        public void ReadArg()
        {
            var decl = ReadDecl();
            if (decl[1] == null)
                throw Abort("missing type or name");
            Debug.Write(decl[0] + " : " + decl[1]);
        }

        public void ReadBlock()
        {
            MoveNext();
            while (cur != null && cur.Text != "}")
            {
                switch (cur.Text)
                {
                    case "if":
                        ReadIf();
                        break;
                    default:
                        ReadSentence();
                        break;
                }
            }
            MoveNext();
        }

        private void ReadSentence()
        {
            Debug.Write(indent);
            ReadExpr();
            Debug.WriteLine();
        }

        public void ReadExpr()
        {
            while (cur.Text != ";" && cur.Text != ")")
            {
                if (cur.Text == "(")
                {
                    Debug.Write("(");
                    MoveNext();
                    ReadExpr();
                    Debug.Write(")");
                }
                else if (cur.Text == "." || cur.Type != TokenType.Operator)
                {
                    Debug.Write(cur.Text);
                    MoveNext();
                }
                else
                {
                    Debug.Write(" " + ConvOp(cur.Text) + " ");
                    MoveNext();
                }
            }
            MoveNext();
        }

        private void ReadBlockOrExpr()
        {
            if (cur.Text == "{")
                ReadBlock();
            else
                ReadSentence();
        }

        public void ReadIf()
        {
            MoveNext();
            Debug.Write(indent);
            Debug.Write("if ");
            ReadIfInternal();
        }

        private void ReadIfInternal()
        {
            if (cur.Text != "(") throw Abort("must be '('");
            MoveNext();
            ReadExpr();
            Debug.WriteLine(" then");
            var bak = indent;
            indent += "    ";
            ReadBlockOrExpr();
            if (cur.Text == "else")
            {
                MoveNext();
                Debug.Write(bak);
                if (cur.Text == "if")
                {
                    indent = bak;
                    MoveNext();
                    Debug.Write(indent);
                    Debug.Write("if ");
                    ReadIfInternal();
                }
                else
                {
                    Debug.WriteLine("else");
                    ReadBlockOrExpr();
                    indent = bak;
                }
            }
            else
                indent = bak;
        }

        private Exception Abort(string message)
        {
            return new Exception(string.Format(
                "[{0}, {1}] {2}: {3}", cur.Line, cur.Column, message, cur.Text));
        }

        public static bool IsAccess(string s)
        {
            return s == "public" || s == "protected" || s == "private";
        }
    }
}

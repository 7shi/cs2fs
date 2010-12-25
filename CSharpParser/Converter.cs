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

        private bool MoveNext()
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
                        throw Abort("syntax error");
                }
            }
        }

        private string ReadUsing()
        {
            var sw = new StringWriter();
            while (MoveNext() && cur.Text != ";")
                sw.Write(cur.Text);
            MoveNext();
            sw.Close();
            return sw.ToString();
        }

        private void ReadNamespace()
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
                if (IsAccess(cur.Text))
                {
                    var acc = cur.Text;
                    MoveNext();
                    if (cur.Text != "class")
                        throw Abort("class required");
                    ReadClass(acc);
                }
                else if (cur.Text == "class")
                    ReadClass("private");
            }
            MoveNext();
        }

        private void ReadClass(string access)
        {
            MoveNext();
            string name = cur.Text;
            MoveNext();
            Debug.WriteLine();
            Debug.Write("type ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} =", name);
            if (cur.Text == ":")
            {
                throw Abort("inherit not supported");
            }
            while (cur.Text != "{") MoveNext();
            MoveNext();
            while (cur != null && cur.Text != "}")
                ReadMember("private", false);
            MoveNext();
        }

        private void ReadMember(string access, bool isStatic)
        {
            if (cur.Text == "static")
            {
                MoveNext();
                ReadMember(access, isStatic);
            }
            else if (IsAccess(cur.Text))
            {
                var acc = cur.Text;
                MoveNext();
                ReadMember(acc, isStatic);
            }
            else
            {
                var decl = ReadDecl();
                switch (cur.Text)
                {
                    case "(":
                        ReadMethod(decl[0], decl[1], access, isStatic);
                        break;
                    case ";":
                        ReadField(decl[0], decl[1], access, isStatic);
                        break;
                    case "{":
                        ReadProperty(decl[0], decl[1], access, isStatic);
                        break;
                    case "=":
                        throw Abort("default value not supported");
                    default:
                        throw Abort("syntax error");
                }
            }
        }

        private void ReadProperty(string name, string type, string access, bool isStatic)
        {
            var autoField = false;
            MoveNext();
            while (cur.Text != "}")
            {
                var act = cur.Text;
                if (act == "get" || act == "set")
                {
                    MoveNext();
                    if (cur.Text == ";")
                    {
                        MoveNext();
                        if (!autoField)
                        {
                            MakeField("_" + name, type, "private", isStatic);
                            autoField = true;
                        }
                    }
                    Debug.Write("    ");
                    if (isStatic) Debug.Write("static ");
                    Debug.Write("member ");
                    if (access == "private") Debug.Write("private ");
                    if (!isStatic) Debug.Write("this.");
                    Debug.Write(name);
                    if (act == "get")
                    {
                        Debug.Write(" =");
                        if (autoField)
                            Debug.WriteLine(" _" + name);
                        else
                        {
                            if (pos + 1 < tokens.Length && tokens[pos + 1].Text == "return")
                            {
                                MoveNext();
                                MoveNext();
                                Debug.Write(" ");
                                ReadExpr();
                                Debug.WriteLine();
                                if (cur.Text != "}")
                                    throw Abort("block not closed");
                                MoveNext();
                            }
                            else
                            {
                                Debug.WriteLine();
                                indent = new string(' ', 8);
                                ReadBlock();
                            }
                        }
                    }
                    else
                    {
                        Debug.Write(" with set(value) =");
                        if (autoField)
                            Debug.WriteLine(" _" + name + " <- value");
                        else
                        {
                            Debug.WriteLine();
                            indent = new string(' ', 8);
                            ReadBlock();
                        }
                    }
                }
                else
                    throw Abort("syntax error");
            }
            MoveNext();
        }

        private void ReadField(string name, string type, string access, bool isStatic)
        {
            MoveNext();
            MakeField(name, type, access, isStatic);
        }

        private static void MakeField(string name, string type, string access, bool isStatic)
        {
            Debug.Write("    [<DefaultValue>] ");
            if (isStatic) Debug.Write("static ");
            Debug.Write("val mutable ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} : {1}", name, type);
        }

        private void ReadMethod(string name, string type, string access, bool isStatic)
        {
            Debug.WriteLine();
            Debug.Write("    ");
            if (isStatic) Debug.Write("static ");
            if (type == null)
            {
                // constructor
                MoveNext();
                if (access == "private") Debug.Write("private ");
                Debug.Write("new(");
                ReadArgs();
                Debug.Write(") ");
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
                    Debug.Write(") =");
                else
                    Debug.Write(") : {0} =", type);
            }
            if (cur.Text != "{") throw Abort("block required");
            if (pos + 1 < tokens.Length && tokens[pos + 1].Text == "}")
            {
                MoveNext();
                MoveNext();
                if (type == null)
                    Debug.WriteLine("= {{}}");
                else
                    Debug.WriteLine(" ()");
            }
            else
            {
                if (type == null)
                    Debug.WriteLine("as this = {{}} then");
                else
                    Debug.WriteLine();
                indent = new string(' ', 8);
                ReadBlock();
            }
        }

        private void ReadArgs()
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

        private string[] ReadDecl()
        {
            var list = new List<string>();
            while (cur.Text.Length > 1 || "(){};,=".IndexOf(cur.Text) < 0)
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

        private void ReadArg()
        {
            var decl = ReadDecl();
            if (decl[1] == null)
                throw Abort("missing type or name");
            Debug.Write(decl[0] + " : " + decl[1]);
        }

        private void ReadBlock()
        {
            if (cur.Text != "{") throw Abort("block required");
            MoveNext();
            if (cur.Text == "}")
            {
                Debug.Write(indent);
                Debug.WriteLine("()");
            }
            else
            {
                while (cur != null && cur.Text != "}")
                {
                    switch (cur.Text)
                    {
                        case "return":
                            MoveNext();
                            ReadSentence();
                            break;
                        case "if":
                            ReadIf();
                            break;
                        case "while":
                            ReadWhile();
                            break;
                        case "switch":
                            ReadSwitch();
                            break;
                        case "continue":
                        case "break":
                            throw Abort("not supported");
                        default:
                            ReadSentence();
                            break;
                    }
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

        private void ReadExpr()
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
                else if (cur.Text == ",")
                {
                    Debug.Write(", ");
                    MoveNext();
                }
                else if (cur.Text == "." || cur.Type != TokenType.Operator)
                {
                    Debug.Write("{0}", cur.Text);
                    MoveNext();
                }
                else if (cur.Text == "!")
                {
                    Debug.Write("not ");
                    MoveNext();
                }
                else if (cur.Text == "~")
                {
                    Debug.Write("~~~");
                    MoveNext();
                }
                else
                {
                    if (cur.Text == "++" || cur.Text == "--"
                        || (cur.Text.Length > 1 && cur.Text[cur.Text.Length - 1] == '='))
                        throw Abort("not supported");
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

        private void ReadIf()
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

        private void ReadWhile()
        {
            MoveNext();
            Debug.Write(indent);
            Debug.Write("while ");
            if (cur.Text != "(") throw Abort("must be '('");
            MoveNext();
            ReadExpr();
            Debug.WriteLine(" do");
            var bak = indent;
            indent += "    ";
            ReadBlockOrExpr();
            indent = bak;
        }

        private void ReadSwitch()
        {
            MoveNext();
            Debug.Write(indent);
            Debug.Write("match ");
            if (cur.Text != "(") throw Abort("must be '('");
            MoveNext();
            ReadExpr();
            Debug.WriteLine(" with");
            if (cur.Text != "{") throw Abort("must be '{'");
            MoveNext();
            while (cur.Text != "}")
            {
                if (cur.Text == "case")
                {
                    while (cur.Text == "case")
                    {
                        MoveNext();
                        Debug.Write(indent);
                        Debug.Write("| ");
                        Debug.Write(cur.Text);
                        MoveNext();
                        if (cur.Text != ":") throw Abort("must be ':'");
                        MoveNext();
                        if (cur.Text != "case")
                            Debug.Write(" ->");
                        else
                            Debug.WriteLine();
                    }
                    ReadCaseBlock();
                }
                else if (cur.Text == "default")
                {
                    MoveNext();
                    Debug.Write(indent);
                    Debug.Write("| _ ->");
                    if (cur.Text != ":") throw Abort("must be ':'");
                    MoveNext();
                    ReadCaseBlock();
                }
                else
                    throw Abort("syntax error");
            }
            MoveNext();
        }

        private void ReadCaseBlock()
        {
            if (cur.Text == "break")
            {
                Debug.WriteLine(" ()");
                MoveNext();
                if (cur.Text != ";") throw Abort("must be ';'");
                MoveNext();
            }
            else
            {
                Debug.WriteLine();
                var bak = indent;
                indent += "    ";
                while (cur.Text != "break" && cur.Text != "return")
                    ReadSentence();
                if (cur.Text == "break")
                {
                    MoveNext();
                    if (cur.Text != ";") throw Abort("must be ';'");
                    MoveNext();
                }
                else
                {
                    MoveNext();
                    ReadSentence();
                }
                indent = bak;
            }
        }

        private Exception Abort(string message)
        {
            return new Exception(string.Format(
                "[{0}, {1}] {2}: {3}", cur.Line, cur.Column, message, cur.Text));
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

        public static bool IsAccess(string s)
        {
            return s == "public" || s == "protected" || s == "private";
        }
    }
}

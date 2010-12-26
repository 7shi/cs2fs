using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CSharpParser
{
    public class TypeName
    {
        public string Type { get; private set; }
        public string Name { get; private set; }

        public TypeName(string t, string name)
        {
            this.Type = t;
            this.Name = name;
        }
    }

    public class Converter
    {
        private static string[] noop;
        private Token[] tokens;
        private Token cur;
        private int pos;
        private string indent;
        private List<string> usings;
        private bool isNew;

        public Converter(Token[] tokens)
        {
            if (noop == null)
            {
                noop = new[]
                {
                    "++", "--", "??", "?:","+=", "-=", "*=", "/=",
                    "%=", "&=", "|=", "^=", "<<=", ">>=", "=>", "?", ":"
                };
            }
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
                ReadNamespaceInternal("private");
            MoveNext();
        }

        private void ReadNamespaceInternal(string access)
        {
            if (IsAccess(cur.Text))
            {
                var acc = cur.Text;
                MoveNext();
                ReadNamespaceInternal(acc);
            }
            else if (cur.Text == "class")
                ReadClass(access);
            else if (cur.Text == "enum")
                ReadEnum(access);
            else
                throw Abort("not supported");
        }

        private void ReadEnum(string access)
        {
            MoveNext();
            string name = cur.Text;
            MoveNext();
            Debug.WriteLine();
            Debug.Write("type ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} =", name);
            if (cur.Text != "{") throw Abort("must be '{'");
            MoveNext();
            int v = 0;
            while (cur != null && cur.Text != "}")
            {
                var id = cur.Text;
                MoveNext();
                if (cur.Text == "=")
                {
                    MoveNext();
                    v = int.Parse(cur.Text);
                    MoveNext();
                }
                Debug.WriteLine("    | {0} = {1}", id, v);
                v = v + 1;
                if (cur.Text == ",") MoveNext();
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
            if (cur.Text != "{") throw Abort("must be '{'");
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
                ReadMember(access, true);
            }
            else if (IsAccess(cur.Text))
            {
                var acc = cur.Text;
                MoveNext();
                ReadMember(acc, isStatic);
            }
            else
            {
                var tn = ReadDecl(false);
                switch (cur.Text)
                {
                    case "(":
                        ReadMethod(tn.Name, tn.Type, access, isStatic);
                        break;
                    case ";":
                        ReadField(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "{":
                        ReadProperty(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "=":
                        throw Abort("default value not supported");
                    default:
                        throw Abort("syntax error");
                }
            }
        }

        private void ReadProperty(string name, string t, string access, bool isStatic)
        {
            Debug.WriteLine();
            var autoField = false;
            MoveNext();
            while (cur.Text != "}")
            {
                var acc = access;
                if (IsAccess(cur.Text))
                {
                    acc = cur.Text;
                    MoveNext();
                }
                if (cur.Text == "get" || cur.Text == "set")
                {
                    var act = cur.Text;
                    MoveNext();
                    if (cur.Text == ";")
                    {
                        MoveNext();
                        if (!autoField)
                        {
                            MakeField("_" + name, t, "private", isStatic);
                            autoField = true;
                        }
                    }
                    Debug.Write("    ");
                    if (isStatic) Debug.Write("static ");
                    Debug.Write("member ");
                    if (acc == "private") Debug.Write("private ");
                    if (!isStatic) Debug.Write("this.");
                    Debug.Write(name);
                    if (act == "get")
                    {
                        Debug.Write(" =");
                        if (autoField)
                            Debug.WriteLine(" this._" + name);
                        else
                        {
                            if (pos + 1 < tokens.Length && tokens[pos + 1].Text == "return")
                            {
                                MoveNext();
                                MoveNext();
                                Debug.Write(" ");
                                ReadExpr(false);
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
                            Debug.WriteLine(" this._" + name + " <- value");
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

        private void ReadField(string name, string t, string access, bool isStatic)
        {
            MoveNext();
            MakeField(name, t, access, isStatic);
        }

        private static void MakeField(string name, string t, string access, bool isStatic)
        {
            Debug.Write("    [<DefaultValue>] ");
            if (isStatic) Debug.Write("static ");
            Debug.Write("val mutable ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} : {1}", name, t);
        }

        private void ReadMethod(string name, string t, string access, bool isStatic)
        {
            Debug.WriteLine();
            Debug.Write("    ");
            if (isStatic) Debug.Write("static ");
            if (t == null)
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
                if (t == "void")
                    Debug.Write(") =");
                else
                    Debug.Write(") : {0} =", t);
            }
            if (cur.Text != "{") throw Abort("block required");
            if (pos + 1 < tokens.Length && tokens[pos + 1].Text == "}")
            {
                MoveNext();
                MoveNext();
                if (t == null)
                    Debug.WriteLine("= {{}}");
                else
                    Debug.WriteLine(" ()");
            }
            else
            {
                if (t == null)
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

        private TypeName ReadDecl(bool arg)
        {
            var list = new List<string>();
            var seps = "(){};=";
            if (arg) seps += ",";
            while (cur.Text.Length > 1 || seps.IndexOf(cur.Text) < 0)
            {
                list.Add(cur.Text);
                MoveNext();
            }
            if (list.Count < 1)
                throw Abort("missing type or name");
            var last = list.Count - 1;
            var name = list[last];
            list.RemoveAt(last);
            var t = list.Count > 0 ? ConvType(String.Concat(list)) : null;
            return new TypeName(t, name);
        }

        private void ReadArg()
        {
            var tn = ReadDecl(true);
            if (tn.Type == null)
                throw Abort("missing type or name");
            Debug.Write(tn.Name + " : " + tn.Type);
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
                    ReadSentence();
            }
            MoveNext();
        }

        private void ReadSentence()
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
                case "foreach":
                    ReadForEach();
                    break;
                case "continue":
                case "break":
                    throw Abort("not supported");
                case "throw":
                    MoveNext();
                    Debug.Write(indent);
                    Debug.Write("raise <| ");
                    ReadExpr(false);
                    Debug.WriteLine();
                    break;
                case "var":
                    ReadVar();
                    break;
                default:
                    Debug.Write(indent);
                    ReadExpr(false);
                    Debug.WriteLine();
                    break;
            }
        }

        private void ReadExpr(bool array)
        {
            var seps = ");:";
            if (array)
            {
                seps = ",}";
                if (seps.IndexOf(cur.Text) >= 0)
                    throw Abort("element required");
            }
            while (cur.Text.Length > 1 || seps.IndexOf(cur.Text) < 0)
            {
                var t = cur.Text;
                if (t == "(")
                {
                    isNew = false;
                    MoveNext();
                    Debug.Write("(");
                    ReadExpr(false);
                    Debug.Write(")");
                }
                else if (t == ",")
                {
                    MoveNext();
                    Debug.Write(", ");
                }
                else if (t == "new")
                {
                    MoveNext();
                    if (cur.Text == "[")
                        ReadArray();
                    else
                    {
                        Debug.Write("new ");
                        isNew = true;
                    }
                }
                else if (t == "delegate")
                    ReadDelegate();
                else if (t == "." || cur.Type != TokenType.Operator)
                {
                    MoveNext();
                    Debug.Write("{0}", t);
                }
                else if (t == "!")
                {
                    MoveNext();
                    Debug.Write("not ");
                }
                else if (t == "~")
                {
                    MoveNext();
                    Debug.Write("~~~");
                }
                else if (noop.Contains(t))
                    throw Abort("not supported");
                else if (isNew || t == "]")
                {
                    MoveNext();
                    Debug.Write(t);
                }
                else if (t == "[")
                {
                    MoveNext();
                    Debug.Write(".[");
                }
                else
                {
                    MoveNext();
                    Debug.Write(" " + ConvOp(t) + " ");
                }
            }
            if (!array) MoveNext();
        }

        private void ReadBlockOrExpr()
        {
            if (cur.Text == ";")
            {
                MoveNext();
                Debug.Write("()");
            }
            else if (cur.Text == "{")
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
            ReadExpr(false);
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
                    Debug.Write("elif ");
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
            ReadExpr(false);
            Debug.Write(" do");
            if (cur.Text == ";")
            {
                MoveNext();
                Debug.WriteLine(" ()");
            }
            else
            {
                Debug.WriteLine();
                var bak = indent;
                indent += "    ";
                ReadBlockOrExpr();
                indent = bak;
            }
        }

        private void ReadForEach()
        {
            MoveNext();
            Debug.Write(indent);
            if (cur.Text != "(") throw Abort("must be '('");
            MoveNext();
            if (cur.Text != "var") throw Abort("must be 'var'");
            MoveNext();
            Debug.Write("for {0} in ", cur.Text);
            MoveNext();
            if (cur.Text != "in") throw Abort("must be 'in'");
            MoveNext();
            ReadExpr(false);
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
            ReadExpr(false);
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
                        ReadExpr(false);
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
                while (cur.Text != "break" && cur.Text != "return" && cur.Text != "throw")
                    ReadSentence();
                if (cur.Text == "return")
                {
                    MoveNext();
                    ReadSentence();
                }
                else if (cur.Text == "throw")
                {
                    MoveNext();
                    Debug.Write(indent);
                    Debug.Write("raise <| ");
                    ReadExpr(false);
                    Debug.WriteLine();
                }
                else
                {
                    MoveNext();
                    if (cur.Text != ";") throw Abort("must be ';'");
                    MoveNext();
                }
                indent = bak;
            }
        }

        private void ReadVar()
        {
            MoveNext();
            if (cur.Type != TokenType.Any) throw Abort("name required");
            Debug.Write(indent);
            Debug.Write("let mutable {0} = ", cur.Text);
            MoveNext();
            if (cur.Text != "=") throw Abort("must be '='");
            MoveNext();
            ReadExpr(false);
            Debug.WriteLine();
        }

        private void ReadDelegate()
        {
            MoveNext();
            if (cur.Text != "(") throw Abort("argument required");
            MoveNext();
            Debug.Write("(fun");
            while (cur.Text != ")")
            {
                var tn = ReadDecl(true);
                Debug.Write(" ({0} : {1})", tn.Name, tn.Type);
                if (cur.Text == ",") MoveNext();
            }
            MoveNext();
            Debug.WriteLine(" ->");
            var bak = indent;
            indent = indent + "    ";
            ReadBlock();
            indent = bak;
            Debug.Write(indent);
            Debug.Write(")");
        }

        private void ReadArray()
        {
            MoveNext();
            if (cur.Text != "]") throw Abort("must be ']'");
            MoveNext();
            if (cur.Text != "{") throw Abort("must be '{'");
            MoveNext();
            Debug.Write("[| ");
            while (cur.Text != "}")
            {
                ReadExpr(true);
                if (cur.Text == ",")
                {
                    MoveNext();
                    if (cur.Text != "}") Debug.Write("; ");
                }
            }
            MoveNext();
            Debug.Write(" |]");
        }

        private Exception Abort(string message)
        {
            return new Exception(String.Format(
                "[{0}, {1}] {2}: {3}", cur.Line, cur.Column, message, cur.Text));
        }

        public static string ConvType(string t)
        {
            switch (t)
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
                    return t;
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
                case "!=":
                    return "<>";
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

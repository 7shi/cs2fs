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
        private Token last;
        private int pos;
        private string indent;
        private List<string> usings;
        private bool isNew;

        public Converter(Token[] tokens)
        {
            if (Converter.noop == null)
            {
                Converter.noop = new[]
                {
                    "++", "--", "??", "?:","+=", "-=", "*=", "/=",
                    "%=", "&=", "|=", "^=", "<<=", ">>=", "=>", "?", ":"
                };
            }
            this.usings = new List<string>();
            this.tokens = tokens.Where(delegate(Token t)
            {
                return !t.CanOmit;
            }).ToArray();
            this.last = new Token("", TokenType.None, 0, 0);
            if (this.tokens.Length > 0)
                this.cur = this.tokens[0];
            else
                this.cur = this.last;
        }

        private void MoveNext()
        {
            if (this.pos < this.tokens.Length)
            {
                this.pos = this.pos + 1;
                if (this.pos < this.tokens.Length)
                    this.cur = this.tokens[this.pos];
                else
                    this.cur = this.last;
            }
            else
                this.cur = this.last;
        }

        public void Convert()
        {
            while (this.cur != this.last)
            {
                switch (this.cur.Text)
                {
                    case "using":
                        this.usings.Add(this.ReadUsing());
                        break;
                    case "namespace":
                        this.ReadNamespace();
                        break;
                    default:
                        throw this.Abort("syntax error");
                }
            }
        }

        private string ReadUsing()
        {
            var sw = new StringWriter();
            this.MoveNext();
            while (this.cur.Text != ";")
            {
                sw.Write(this.cur.Text);
                this.MoveNext();
            }
            this.MoveNext();
            sw.Close();
            return sw.ToString();
        }

        private void ReadNamespace()
        {
            Debug.Write("namespace ");
            this.MoveNext();
            while (this.cur.Text != "{")
            {
                Debug.Write(this.cur.Text);
                this.MoveNext();
            }
            Debug.WriteLine();
            if (this.usings.Count > 0)
            {
                Debug.WriteLine();
                foreach (var u in this.usings)
                    Debug.WriteLine("open {0}", u);
            }
            this.MoveNext();
            while (this.cur != this.last && this.cur.Text != "}")
                this.ReadNamespaceInternal("private");
            this.MoveNext();
        }

        private void ReadNamespaceInternal(string access)
        {
            if (Converter.IsAccess(this.cur.Text))
            {
                var acc = this.cur.Text;
                this.MoveNext();
                this.ReadNamespaceInternal(acc);
            }
            else if (this.cur.Text == "class")
                this.ReadClass(access);
            else if (this.cur.Text == "enum")
                this.ReadEnum(access);
            else
                throw this.Abort("not supported");
        }

        private void ReadEnum(string access)
        {
            this.MoveNext();
            var name = this.cur.Text;
            this.MoveNext();
            Debug.WriteLine();
            Debug.Write("type ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} =", name);
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            var v = 0;
            while (this.cur != this.last && this.cur.Text != "}")
            {
                var id = this.cur.Text;
                this.MoveNext();
                if (this.cur.Text == "=")
                {
                    this.MoveNext();
                    v = Int32.Parse(this.cur.Text);
                    this.MoveNext();
                }
                Debug.WriteLine("    | {0} = {1}", id, v);
                v = v + 1;
                if (this.cur.Text == ",") this.MoveNext();
            }
            this.MoveNext();
        }

        private void ReadClass(string access)
        {
            this.MoveNext();
            var name = this.cur.Text;
            this.MoveNext();
            Debug.WriteLine();
            Debug.Write("type ");
            if (access == "private") Debug.Write("private ");
            Debug.WriteLine("{0} =", name);
            if (this.cur.Text == ":")
            {
                throw this.Abort("inherit not supported");
            }
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            while (this.cur != this.last && this.cur.Text != "}")
                this.ReadMember("private", false);
            this.MoveNext();
        }

        private void ReadMember(string access, bool isStatic)
        {
            if (this.cur.Text == "static")
            {
                this.MoveNext();
                this.ReadMember(access, true);
            }
            else if (Converter.IsAccess(this.cur.Text))
            {
                var acc = this.cur.Text;
                this.MoveNext();
                this.ReadMember(acc, isStatic);
            }
            else
            {
                var tn = this.ReadDecl(false);
                switch (this.cur.Text)
                {
                    case "(":
                        this.ReadMethod(tn.Name, tn.Type, access, isStatic);
                        break;
                    case ";":
                        this.ReadField(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "{":
                        this.ReadProperty(tn.Name, tn.Type, access, isStatic);
                        break;
                    case "=":
                        throw this.Abort("default value not supported");
                    default:
                        throw this.Abort("syntax error");
                }
            }
        }

        private void ReadProperty(string name, string t, string access, bool isStatic)
        {
            Debug.WriteLine();
            var autoField = false;
            this.MoveNext();
            while (this.cur.Text != "}")
            {
                var acc = access;
                if (Converter.IsAccess(this.cur.Text))
                {
                    acc = this.cur.Text;
                    this.MoveNext();
                }
                if (this.cur.Text == "get" || this.cur.Text == "set")
                {
                    var act = this.cur.Text;
                    this.MoveNext();
                    if (this.cur.Text == ";")
                    {
                        this.MoveNext();
                        if (!autoField)
                        {
                            this.MakeField("_" + name, t, "private", isStatic);
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
                            if (this.pos + 1 < this.tokens.Length && this.tokens[this.pos + 1].Text == "return")
                            {
                                this.MoveNext();
                                this.MoveNext();
                                Debug.Write(" ");
                                this.ReadExpr(false);
                                Debug.WriteLine();
                                if (this.cur.Text != "}")
                                    throw this.Abort("block not closed");
                                this.MoveNext();
                            }
                            else
                            {
                                Debug.WriteLine();
                                this.indent = new string(' ', 8);
                                this.ReadBlock();
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
                            this.indent = new string(' ', 8);
                            this.ReadBlock();
                        }
                    }
                }
                else
                    throw this.Abort("syntax error");
            }
            this.MoveNext();
        }

        private void ReadField(string name, string t, string access, bool isStatic)
        {
            this.MoveNext();
            this.MakeField(name, t, access, isStatic);
        }

        private void MakeField(string name, string t, string access, bool isStatic)
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
                this.MoveNext();
                if (access == "private") Debug.Write("private ");
                Debug.Write("new(");
                this.ReadArgs();
                Debug.Write(") ");
            }
            else
            {
                Debug.Write("member ");
                if (access == "private") Debug.Write("private ");
                if (!isStatic) Debug.Write("this.");
                Debug.Write(name + "(");
                this.MoveNext();
                this.ReadArgs();
                if (t == "void")
                    Debug.Write(") =");
                else
                    Debug.Write(") : {0} =", t);
            }
            if (this.cur.Text != "{") throw this.Abort("block required");
            if (this.pos + 1 < this.tokens.Length && this.tokens[this.pos + 1].Text == "}")
            {
                this.MoveNext();
                this.MoveNext();
                if (t == null)
                    Debug.WriteLine("= {0}", "{}");
                else
                    Debug.WriteLine(" ()");
            }
            else
            {
                if (t == null)
                    Debug.WriteLine("as this = {0} then", "{}");
                else
                    Debug.WriteLine();
                this.indent = new string(' ', 8);
                this.ReadBlock();
            }
        }

        private void ReadArgs()
        {
            while (this.cur.Text != ")")
            {
                this.ReadArg();
                if (this.cur.Text == ",")
                {
                    Debug.Write(", ");
                    this.MoveNext();
                }
            }
            this.MoveNext();
        }

        private TypeName ReadDecl(bool arg)
        {
            var list = new List<string>();
            var seps = "(){};=";
            if (arg) seps = seps + ",";
            while (this.cur.Text.Length > 1 || seps.IndexOf(this.cur.Text) < 0)
            {
                list.Add(this.cur.Text);
                this.MoveNext();
            }
            if (list.Count < 1)
                throw this.Abort("missing type or name");
            var last = list.Count - 1;
            var name = list[last];
            list.RemoveAt(last);
            if (list.Count > 0)
            {
                var t = Converter.ConvType(String.Concat(list.ToArray()));
                return new TypeName(t, name);
            }
            else
                return new TypeName(null, name);
        }

        private void ReadArg()
        {
            var tn = this.ReadDecl(true);
            if (tn.Type == null)
                throw this.Abort("missing type or name");
            Debug.Write(tn.Name + " : " + tn.Type);
        }

        private void ReadBlock()
        {
            if (this.cur.Text != "{") throw this.Abort("block required");
            this.MoveNext();
            if (this.cur.Text == "}")
            {
                Debug.Write(this.indent);
                Debug.WriteLine("()");
            }
            else
            {
                while (this.cur != this.last && this.cur.Text != "}")
                    this.ReadSentence();
            }
            this.MoveNext();
        }

        private void ReadSentence()
        {
            switch (this.cur.Text)
            {
                case "return":
                    this.MoveNext();
                    this.ReadSentence();
                    break;
                case "if":
                    this.ReadIf();
                    break;
                case "while":
                    this.ReadWhile();
                    break;
                case "switch":
                    this.ReadSwitch();
                    break;
                case "foreach":
                    this.ReadForEach();
                    break;
                case "continue":
                case "break":
                    throw this.Abort("not supported");
                case "throw":
                    this.MoveNext();
                    Debug.Write(this.indent);
                    Debug.Write("raise <| ");
                    this.ReadExpr(false);
                    Debug.WriteLine();
                    break;
                case "var":
                    this.ReadVar();
                    break;
                default:
                    Debug.Write(this.indent);
                    this.ReadExpr(false);
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
                if (seps.IndexOf(this.cur.Text) >= 0)
                    throw this.Abort("element required");
            }
            while (this.cur.Text.Length > 1 || seps.IndexOf(this.cur.Text) < 0)
            {
                var t = this.cur.Text;
                if (t == "(")
                {
                    this.isNew = false;
                    this.MoveNext();
                    Debug.Write("(");
                    this.ReadExpr(false);
                    Debug.Write(")");
                }
                else if (t == ",")
                {
                    this.MoveNext();
                    Debug.Write(", ");
                }
                else if (t == "new")
                {
                    this.MoveNext();
                    if (this.cur.Text == "[")
                        this.ReadArray();
                    else
                    {
                        Debug.Write("new ");
                        this.isNew = true;
                    }
                }
                else if (t == "delegate")
                    this.ReadDelegate();
                else if (t == "." || this.cur.Type != TokenType.Operator)
                {
                    this.MoveNext();
                    Debug.Write("{0}", t);
                }
                else if (t == "!")
                {
                    this.MoveNext();
                    Debug.Write("not ");
                }
                else if (t == "~")
                {
                    this.MoveNext();
                    Debug.Write("~~~");
                }
                else if (Converter.noop.Contains(t))
                    throw this.Abort("not supported");
                else if (this.isNew || t == "]")
                {
                    this.MoveNext();
                    Debug.Write(t);
                }
                else if (t == "[")
                {
                    this.MoveNext();
                    Debug.Write(".[");
                }
                else
                {
                    this.MoveNext();
                    Debug.Write(" " + Converter.ConvOp(t) + " ");
                }
            }
            if (!array) this.MoveNext();
        }

        private void ReadBlockOrExpr()
        {
            if (this.cur.Text == ";")
            {
                this.MoveNext();
                Debug.Write("()");
            }
            else if (this.cur.Text == "{")
                this.ReadBlock();
            else
                this.ReadSentence();
        }

        private void ReadIf()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("if ");
            this.ReadIfInternal();
        }

        private void ReadIfInternal()
        {
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(" then");
            var bak = this.indent;
            this.indent = this.indent + "    ";
            this.ReadBlockOrExpr();
            if (this.cur.Text == "else")
            {
                this.MoveNext();
                Debug.Write(bak);
                if (this.cur.Text == "if")
                {
                    this.indent = bak;
                    this.MoveNext();
                    Debug.Write("elif ");
                    this.ReadIfInternal();
                }
                else
                {
                    Debug.WriteLine("else");
                    this.ReadBlockOrExpr();
                    this.indent = bak;
                }
            }
            else
                this.indent = bak;
        }

        private void ReadWhile()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("while ");
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.Write(" do");
            if (this.cur.Text == ";")
            {
                this.MoveNext();
                Debug.WriteLine(" ()");
            }
            else
            {
                Debug.WriteLine();
                var bak = this.indent;
                this.indent = this.indent + "    ";
                this.ReadBlockOrExpr();
                this.indent = bak;
            }
        }

        private void ReadForEach()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            if (this.cur.Text != "var") throw this.Abort("must be 'var'");
            this.MoveNext();
            Debug.Write("for {0} in ", this.cur.Text);
            this.MoveNext();
            if (this.cur.Text != "in") throw this.Abort("must be 'in'");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(" do");
            var bak = this.indent;
            this.indent = this.indent + "    ";
            this.ReadBlockOrExpr();
            this.indent = bak;
        }

        private void ReadSwitch()
        {
            this.MoveNext();
            Debug.Write(this.indent);
            Debug.Write("match ");
            if (this.cur.Text != "(") throw this.Abort("must be '('");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine(" with");
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            while (this.cur.Text != "}")
            {
                if (this.cur.Text == "case")
                {
                    while (this.cur.Text == "case")
                    {
                        this.MoveNext();
                        Debug.Write(this.indent);
                        Debug.Write("| ");
                        this.ReadExpr(false);
                        if (this.cur.Text != "case")
                            Debug.Write(" ->");
                        else
                            Debug.WriteLine();
                    }
                    this.ReadCaseBlock();
                }
                else if (this.cur.Text == "default")
                {
                    this.MoveNext();
                    Debug.Write(this.indent);
                    Debug.Write("| _ ->");
                    if (this.cur.Text != ":") throw this.Abort("must be ':'");
                    this.MoveNext();
                    this.ReadCaseBlock();
                }
                else
                    throw this.Abort("syntax error");
            }
            this.MoveNext();
        }

        private void ReadCaseBlock()
        {
            if (this.cur.Text == "break")
            {
                Debug.WriteLine(" ()");
                this.MoveNext();
                if (this.cur.Text != ";") throw this.Abort("must be ';'");
                this.MoveNext();
            }
            else
            {
                Debug.WriteLine();
                var bak = this.indent;
                this.indent = this.indent + "    ";
                while (this.cur.Text != "break" && this.cur.Text != "return" && this.cur.Text != "throw")
                    this.ReadSentence();
                if (this.cur.Text == "return")
                {
                    this.MoveNext();
                    this.ReadSentence();
                }
                else if (this.cur.Text == "throw")
                {
                    this.MoveNext();
                    Debug.Write(this.indent);
                    Debug.Write("raise <| ");
                    this.ReadExpr(false);
                    Debug.WriteLine();
                }
                else
                {
                    this.MoveNext();
                    if (this.cur.Text != ";") throw this.Abort("must be ';'");
                    this.MoveNext();
                }
                this.indent = bak;
            }
        }

        private void ReadVar()
        {
            this.MoveNext();
            if (this.cur.Type != TokenType.Any) throw this.Abort("name required");
            Debug.Write(this.indent);
            Debug.Write("let mutable {0} = ", this.cur.Text);
            this.MoveNext();
            if (this.cur.Text != "=") throw this.Abort("must be '='");
            this.MoveNext();
            this.ReadExpr(false);
            Debug.WriteLine();
        }

        private void ReadDelegate()
        {
            this.MoveNext();
            if (this.cur.Text != "(") throw this.Abort("argument required");
            this.MoveNext();
            Debug.Write("(fun");
            while (this.cur.Text != ")")
            {
                var tn = this.ReadDecl(true);
                Debug.Write(" ({0} : {1})", tn.Name, tn.Type);
                if (this.cur.Text == ",") this.MoveNext();
            }
            this.MoveNext();
            Debug.WriteLine(" ->");
            var bak = this.indent;
            this.indent = this.indent + "    ";
            this.ReadBlock();
            this.indent = bak;
            Debug.Write(this.indent);
            Debug.Write(")");
        }

        private void ReadArray()
        {
            this.MoveNext();
            if (this.cur.Text != "]") throw this.Abort("must be ']'");
            this.MoveNext();
            if (this.cur.Text != "{") throw this.Abort("must be '{'");
            this.MoveNext();
            Debug.Write("[| ");
            while (this.cur.Text != "}")
            {
                this.ReadExpr(true);
                if (this.cur.Text == ",")
                {
                    this.MoveNext();
                    if (this.cur.Text != "}") Debug.Write("; ");
                }
            }
            this.MoveNext();
            Debug.Write(" |]");
        }

        private Exception Abort(string message)
        {
            return new Exception(String.Format(
                "[{0}, {1}] {2}: {3}", this.cur.Line, this.cur.Column, message, this.cur.Text));
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

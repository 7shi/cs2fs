using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TestApp
{
    public class Test
    {
        private int a;
        private int b;
        public int Value { get { return this.a; } }

        public Test(int a, int b)
        {
            this.a = a;
            this.b = b;
        }

        public void Test2()
        {
            if (this.a < this.b)
                Console.WriteLine("abc");
            else
                Console.WriteLine("def");
        }

        public int Add()
        {
            return this.a + this.b;
        }
    }
}

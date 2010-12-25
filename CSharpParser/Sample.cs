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
        public int Add { get { return this.a + this.b; } }

        private Test()
        {
        }

        public Test(int a, int b)
        {
            this.a = a;
            this.b = b;
        }

        public void Test2()
        {
            while (this.a < this.b)
            {
                Console.WriteLine("a = {0}, b = {1}", this.a, this.b);
                this.a = this.a + 1;
            }
        }
    }
}

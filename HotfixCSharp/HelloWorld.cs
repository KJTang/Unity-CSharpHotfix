using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HelloTest.Test {

    public class RewriteClass1
    {
        void RewriteFunc1() {
            this.m_i = 0;
            this.m_i = this.m_i + 1;
            this.RewriteFunc2();
        }

        public void RewriteFunc2() 
        {
            HelloWorldHelper.ShowMessage("RewriteClass1.RewriteFunc2: hotfixed");
        }

        public void RewriteFunc3(int i) {}

        public static void RewriteFunc4(HelloWorld instance) {}
    }



    public class RewriteClass2
    {
        public static void RewriteFunc4(HelloWorld instance) {}

        public void RewriteFunc5(int i) {}

        public static void RewriteFunc6(System.Int32 i) {}
    }
}
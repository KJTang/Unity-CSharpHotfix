﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HelloTest.Test {

    public class RewriteClass1
    {
        void RewriteFunc1() {
            this.RewriteFunc2();
        }

        public void RewriteFunc2() {}

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
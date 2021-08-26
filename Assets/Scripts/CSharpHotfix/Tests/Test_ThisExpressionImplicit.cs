using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_ThisExpressionImplicit
    {
        public int i;

        public string Func()
        {
            var j = i;          // assignment
            if (i == 1) { }     // if statement

            InvokeFunc(i);      // invocation
            return "hello";
        }

        public void InvokeFunc(int i)
        {
        }
    }
}
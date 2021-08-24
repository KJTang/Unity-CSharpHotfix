using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_ThisExpressionImplicit
    {
        public int i;

        public void Func()
        {
            var j = i;          // assignment
            if (i == 1) { }     // if statement

            InvokeFunc(i);      // invocation
        }

        public void InvokeFunc(int i)
        {
            Debug.Log("Test_ThisExpressionImplicit: hello");
        }
    }
}
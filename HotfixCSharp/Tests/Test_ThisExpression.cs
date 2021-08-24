using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_ThisExpression
    {
        public int i;

        public void Func()
        {
            this.InvokeFunc(this.i);
        }

        public void InvokeFunc(int i)
        {
            Debug.Log("Test_ThisExpression: hotfixed");
        }
    }
}
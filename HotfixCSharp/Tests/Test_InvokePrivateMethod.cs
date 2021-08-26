using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokePrivateMethod
    {
        public void Func()
        {
            InvokeFunc();
            InvokeParamFunc(0);
            if (InvokeReturnValueFunc())
                return;
        }

        private void InvokeFunc()
        {
            Debug.Log("Test_InvokePrivateMethod: hotfixed");
        }

        private int InvokeParamFunc(int i)
        {
            return i;
        }

        private bool InvokeReturnValueFunc()
        {
            return true;
        }
    }
}
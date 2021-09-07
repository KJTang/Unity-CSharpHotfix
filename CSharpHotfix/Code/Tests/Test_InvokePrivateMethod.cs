using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokePrivateMethod
    {
        public string Func()
        {
            InvokeFunc();
            InvokeParamFunc(0);
            if (InvokeReturnValueFunc())
                return "hotfixed";
            return "";
        }

        private void InvokeFunc()
        {
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
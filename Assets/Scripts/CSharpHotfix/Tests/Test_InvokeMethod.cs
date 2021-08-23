using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokeMethod
    {
        public void Func()
        {
            InvokeFunc();
        }

        public void InvokeFunc()
        {
            Debug.Log("Test_InvokeMethod: hello");
        }
    }
}
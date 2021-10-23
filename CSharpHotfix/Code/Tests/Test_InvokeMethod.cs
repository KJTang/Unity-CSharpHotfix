using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokeMethod
    {
        public string Func()
        {
            return InvokeFunc();
        }

        public string InvokeFunc()
        {
            return "hotfixed";
        }
    }
}
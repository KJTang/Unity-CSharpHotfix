using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_UnityMacro
    {
        public string Func()
        {
            #if UNITY_EDITOR
                return "hotfixed";
            #endif

            return "";
        }
    }
}
#define MACRO_TEST

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_UnityMacro
    {
        public string Func()
        {
            #if UNITY_EDITOR && MACRO_TEST
                return "hotfixed";
            #endif

            return "";
        }
    }
}
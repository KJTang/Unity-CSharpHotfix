using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_HotfixStaticMethod
    {
        public static void Func()
        {
            Debug.Log("Test_HotfixStaticMethod: hotfixed");
        }
    }
}
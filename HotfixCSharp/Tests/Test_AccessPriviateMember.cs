using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_AccessPriviateMember
    {
        private int fieldTest = 0;

        private bool PropTest { get; set; }

        private static int fieldStaticTest = 1;

        public void Func()
        {
            if (fieldTest >= 0 && CSharpHotfixTest.Test_AccessPriviateMember.fieldStaticTest >= 0)
                PropTest = true;
            Debug.Log("Test_AccessPriviateMember: hotfixed");
        }
    }
}
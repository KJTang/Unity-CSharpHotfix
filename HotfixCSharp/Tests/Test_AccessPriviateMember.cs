using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_AccessPriviateMember
    {
        private int fieldTest = 0;

        private bool PropTest { get; set; }

        private void MethodTest()
        {
            if (fieldTest >= 0)
                PropTest = true;
            Debug.Log("Test_AccessPriviateMember: hotfixed");
        }

        public void Func()
        {
            MethodTest();
        }
    }
}
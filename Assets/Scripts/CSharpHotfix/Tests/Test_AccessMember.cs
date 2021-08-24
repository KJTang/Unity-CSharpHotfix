using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_AccessMember
    {
        public int fieldTest = 0;

        public bool PropTest { get; set; }

        public void MethodTest()
        {
            if (fieldTest >= 0)
                PropTest = true;
            Debug.Log("Test_AccessMember: hello");
        }

        public void Func()
        {
            MethodTest();
        }
    }
}
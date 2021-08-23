using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class HelloWorld : MonoBehaviour
    {
        void Start()
        {
            CSharpHotfix.CSharpHotfixTestManager.RunTests();
        }
    }
}
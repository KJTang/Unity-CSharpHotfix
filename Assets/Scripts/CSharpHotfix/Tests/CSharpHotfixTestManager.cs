using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfix {

    public class CSharpHotfixTestManager
    {
        private static HashSet<string> enabledHotfixTests = new HashSet<string>()
        {
            "Test_HotfixMethod", 
            "Test_HotfixStaticMethod", 
            //"Test_InvokeMethod", 
        };

        public static bool EnableTest = true;


        public static bool IsTestFileEnabled(string fileName)
        {
            if (!EnableTest)
                return false;
            
            if (!enabledHotfixTests.Contains(fileName))
                return false;

            return true;
        }
    }
}
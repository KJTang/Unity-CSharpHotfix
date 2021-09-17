using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_AccessPriviateMember
    {
        private int fieldTest = 0;

        private bool PropTest { get; set; }

        private static int fieldStaticTest = 1;

        private List<int> listTest = new List<int>() {1, 2, 3};

        private Dictionary<string, List<int>> dictTest = new Dictionary<string, List<int>>();

        public string Func()
        {
            PropTest = true;

            if (fieldTest < 0 || CSharpHotfixTest.Test_AccessPriviateMember.fieldStaticTest < 0)
                PropTest = false;

            if (!listTest.Contains(1))
                PropTest = false;
            
            if (dictTest.ContainsKey("invalid"))
                PropTest = false;

            if (PropTest)
                return "hello";
            else
                return "hello";
        }
    }
}
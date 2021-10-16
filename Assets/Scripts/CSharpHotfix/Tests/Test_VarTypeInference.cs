using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_VarTypeInference
    {
        Dictionary<int, string> dict = new Dictionary<int, string>();

        public string Func()
        {
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            var result = "";
            foreach (var kv in dict)
            {
                if (kv.Key == 2)
                    result = kv.Value;
            }

            if (result != "two")
                return "invalid";
            return "hello";
        }
    }
}
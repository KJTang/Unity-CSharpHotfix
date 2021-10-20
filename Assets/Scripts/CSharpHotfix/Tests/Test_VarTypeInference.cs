using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest
{

    public class Test_VarTypeInference
    {
        Dictionary<int, string> dict = new Dictionary<int, string>();

        public string Func()
        {
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");

            var result1 = "";
            foreach (var kv in dict)
            {
                if (kv.Key == 2)
                    result1 = kv.Value;
            }

            var result2 = dict[2];

            if (result1 != "two" || result2 != "two")
                return "invalid";
            return "hello";
        }
    }
}
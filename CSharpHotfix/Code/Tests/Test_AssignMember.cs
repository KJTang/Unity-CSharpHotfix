using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_AssignMember
    {
        private int count = 0;

        public string Func()
        {
            count = 1;

            count += 2;

            count -= 3;

            count *= -1;

            count <<= 1;

            if (count == 2)
                return "hotfixed";
            return "invalid";
        }
    }
}
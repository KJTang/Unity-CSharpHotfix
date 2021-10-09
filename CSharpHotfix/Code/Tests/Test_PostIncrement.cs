using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest
{

    public class Test_PostIncrement
    {
        private int count = 0;

        public string Func()
        {
            // Increment/Decrement
            count = 0;
            count++;
            if (count != 1)
                return "invalid 1";

            count = 0;
            --count;
            if (count != -1)
                return "invalid 2";

            count = 0;
            if (count++ != 0)
                return "invalid 3";

            count = 0;
            if (++count != 1)
                return "invalid 4";

            count = 0;
            var t1 = Foo(count++);
            Debug.LogError("t1: " + t1 + " \t" + this.count);
            if (Foo(count++) != 1)
                return "invalid 5";
            
            count = 0;
            var t2 = Foo(count++);
            Debug.LogError("t2: " + t2 + " \t" + this.count);
            if (Foo(++count) != 1)
                return "invalid 6";


            return "hotfixed";
        }

        public int Foo(int i)
        {
            Debug.LogError("Foo: " + i + " \t" + (i != 1));
            return i;
        }
    }
}
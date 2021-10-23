using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokeMehtodWithOutParam
    {
        public string Func()
        {
            int len = 5;
            List<object> lst = new List<object>();

            var args = new object[2];
            args[0] = len;
            args[1] = lst;

            var method = typeof(Test_InvokeMehtodWithOutParam).GetMethod("Foo");
            method.Invoke(this, args);

            if (len == (args[1] as List<object>).Count)
                return "hotfixed";
            return "invalid";
        }

        public void Foo(int len, out List<object> lst)
        {
            lst = new List<object>(len);
            for (var i = 0; i != len; ++i)
                lst.Add(0);
        }
    }
}
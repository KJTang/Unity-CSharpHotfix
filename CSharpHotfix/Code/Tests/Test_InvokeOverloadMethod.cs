using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CSharpHotfixTest {

    public class Test_InvokeOverloadMethod
    {
        private int invokeCnt = 0;

        public string Func()
        {
            InvokeFunc();
            InvokeFunc(0);

            if (invokeCnt == 2)
                return "hotfixed";
            else
                return "invalid";
        }

        private void InvokeFunc()
        {
            invokeCnt = invokeCnt + 1;
        }

        private void InvokeFunc(int i)
        {
            invokeCnt = invokeCnt + 1;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HelloTest.Test {

    public class HelloWorld : MonoBehaviour
    {
        void Start()
        {
            //HelloWorldHelper.ShowHelloWorld();
            //Func(this);
            StaticFunc();
        }

        public void Func(object o)
        {
            HelloWorldHelper.ShowHelloWorld();
        }

        public int ParamsFunc(params object[] list)
        {
            var num1 = (System.Int32) list[0];
            var num2 = (System.Int32) list[1];
            return num1 + num2;
        }

        public static void StaticFunc()
        {
            HelloWorldHelper.ShowMessage("StaticFunc: normal");
        }
    }

    public class HelloWorldHelper
    {
        public static void ShowHelloWorld()
        {
            ShowMessage("Hello World" + ObjectMethodToInject(1, 2));
            VoidMethodToInject(1024);
        }

        public static void ShowMessage(string str)
        {
            Debug.Log(str);
        }

        public static string ObjectMethodToInject(int val1, int val2)
        {
            return val1.ToString() + val2.ToString();
        }

        public static void VoidMethodToInject(int val)
        {
            Debug.Log(val);
        }
    }

    
    public class RewriteClass1
    {
        public int m_i;

        void RewriteFunc1() {
            this.m_i = 0;
            this.m_i = this.m_i + 1;
            this.RewriteFunc2();
        }

        public void RewriteFunc2() {}

        public void RewriteFunc3(int i) {}

        public static void RewriteFunc4(HelloWorld instance) {}
    }



    public class RewriteClass2
    {
        public static void RewriteFunc4(HelloWorld instance) {}

        public void RewriteFunc5(int i) {}

        public static void RewriteFunc6(System.Int32 i) {}
    }
}
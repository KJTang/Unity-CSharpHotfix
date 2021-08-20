﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace HelloTest.Test {

    public class HelloWorld_Hotfix : MonoBehaviour
    {
        void Start()
        {
            HelloWorldHelper.ShowMessage("Start: hotfixed");
            //HelloTest.Test.HelloWorld.Func(this);

            var method = typeof(HelloWorld).GetMethod("Func");
            method.Invoke(this, new object[] {this});
        }

            //void Func(object o)
            //{
            //    HelloWorldHelper.ShowMessage("Func: hotfixed");
            //}

            //public int ParamsFunc(params object[] list)
            //{
            //    var num1 = (System.Int32)list[0];
            //    var num2 = (System.Int32)list[1];
            //    return num1 + num2;
            //}

        public static void StaticFunc()
        {
            HelloWorldHelper.ShowMessage("StaticFunc: hotfixed");
        }
    }

//public class HelloWorldHelper
//{
//    public static void ShowHelloWorld()
//    {
//        ShowMessage("Hello World" + ObjectMethodToInject(1, 2));
//        VoidMethodToInject(1024);
//    }

//    public static void ShowMessage(string str)
//    {
//        Debug.Log(str);
//    }

//    public static string ObjectMethodToInject(int val1, int val2)
//    {
//        return val1.ToString() + val2.ToString();
//    }

//    public static void VoidMethodToInject(int val)
//    {
//        Debug.Log(val);
//    }
//}

}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    void Start()
    {
        var num = 0;
        for (var i = 0; i != 3; ++i)
        {
            num += i;
        }

        var str = "hello";
        str = str + num.ToString();

        HelloWorldHelper.ShowMessage(str);
    }

    public static void TestFunc()
    {
        //AnotherHelper.ShowMessage("TestFunc");
        (new NewClassTest()).Func();
    }
}

public class AnotherHelper
{
    public static void ShowMessage(string str)
    {
        HelloWorldHelper.ShowMessage(str);
    }
}
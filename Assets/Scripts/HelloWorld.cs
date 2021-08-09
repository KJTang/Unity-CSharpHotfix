using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HelloTest.Test {

public class HelloWorld : MonoBehaviour
{
    void Start()
    {
        HelloWorldHelper.ShowHelloWorld();
    }
}

public class HelloWorldHelper
{
    public static void ShowHelloWorld()
    {
        ShowMessage("Hello World" + ToInject(1, 2));
    }

    public static void ShowMessage(string str)
    {
        Debug.Log(str);
    }

    public static bool Check(int val)
    {
        return true;
    }

    public static string ToInject(int val1, int val2)
    {
        return val1.ToString() + val2.ToString();
    }

    public static string InjectFunc(int val1, int val2)
    {
        return val1.ToString() + val2.ToString() + "_inject";
    }
}

}
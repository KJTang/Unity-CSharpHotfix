using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
        ShowMessage("Hello World");
    }

    public static void ShowMessage(string str)
    {
        Debug.Log(str);
    }

    public static bool Check(int val)
    {
        return true;
    }
}
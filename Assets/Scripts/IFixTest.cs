using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IFixTest : MonoBehaviour
{
    [IFix.Patch]
    void Start()
    {
        Debug.Log("#IFIX# Hello World");

        var obj = new TestNewClass();
        obj.Func();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}


[IFix.Interpret]
public class TestNewClass
{
    public void Func()
    {
        Debug.Log("#IFIX# TestNewClass: Func");
    }
}

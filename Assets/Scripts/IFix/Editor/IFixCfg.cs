using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using IFix;


[Configure]
public class IFixCfg 
{
    [IFix]
    static IEnumerable<Type> ToProcess
    {
        get
        {
            return (from type in Assembly.Load("Assembly-CSharp").GetTypes()
                    where !type.Name.Contains("<")
                    select type);
        }
    }
}

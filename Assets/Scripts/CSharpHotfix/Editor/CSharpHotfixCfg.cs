using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using UnityEngine;


namespace CSharpHotfix
{
    public class CSharpHotfixCfg 
    {
        private static bool UseIFixConfig = true;

        public static IEnumerable<Type> ToProcess
        {
            get
            {
                //if (UseIFixConfig)
                //{ 
                //    var propInfo = typeof(IFixCfg).GetProperty("ToProcess", BindingFlags.NonPublic | BindingFlags.Static);    
                //    return propInfo.GetValue(null) as IEnumerable<Type>;
                //}

                return (from type in Assembly.Load("Assembly-CSharp").GetTypes()
                        where !type.Name.Contains("<")
                        select type);
            }
        }
    }
}


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
        
        /// <summary>
        /// return types need to be hotfixed
        /// </summary>
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


        /// <summary>
        /// return mono path compatible mode needed
        /// </summary>
        /// <returns></returns>
        public static string GetMonoPath()
        {
            // Replace it to your mono which support higher version than .net framework 4.7
            //var monoPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Data/MonoBleedingEdge/bin/mono.exe");
            var monoPath = "C:/Program Files/Unity/2019.4.29f1/Editor/Data/MonoBleedingEdge/bin/mono.exe";
            return monoPath;
        }


        /// <summary>
        /// Assemblies to Inject
        /// </summary>
        public static string[] InjectAssemblies = new string[]
        {
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass"
        };
    }
}


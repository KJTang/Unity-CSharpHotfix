using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.Linq;
using System.IO;
using System.Diagnostics;
using UnityEngine;

namespace CSharpHotfix
{
    public class CSharpHotfixCfg 
    {
        /// <summary>
        /// return types need to be hotfixed
        /// </summary>
        public static IEnumerable<Type> ToProcess
        {
            get
            {
                // currently inject all types, you can change to types you need
                return (from type in Assembly.Load("Assembly-CSharp").GetTypes()
                        where !type.Name.Contains("<")
                        select type);
            }
        }


        /// <summary>
        /// mono path needed
        /// </summary>
        /// <returns></returns>
        public static string GetMonoPath()
        {
            // Replace it to your mono which support higher version than .net framework 4.7
            var monoPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName), "Data/MonoBleedingEdge/bin/mono.exe");
            //var monoPath = "C:/Program Files/Unity/2019.4.29f1/Editor/Data/MonoBleedingEdge/bin/mono.exe";
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

        
        /// <summary>
        /// filter namespace to avoid inject
        /// </summary>
        public static HashSet<string> InjectFilterNamespace = new HashSet<string>()
        {
            "CSharpHotfix", 
            "System", 
            // "UnityEngine", 
            // "UnityEditor", 
            "FlyingWormConsole3",       // my console plugin
        };
    }
}


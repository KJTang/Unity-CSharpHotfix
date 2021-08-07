using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using UnityEngine;
using UnityEditor;

namespace CSharpHotfix.Editor
{
    public class CSharpHotfixEditor 
    {

        [MenuItem("CSharpHotfix/Enable", false, 1)]
        public static void EnableHotifxMenu()
        {
            CSharpHotfixManager.IsHotfixEnabled = true;

            // when not playing, inject the dll
            if (!EditorApplication.isCompiling && !Application.isPlaying)
                TryInject();
        }
        
        [MenuItem("CSharpHotfix/Enable", true, 1)]
        public static bool EnableHotifxMenuValidate()
        {
            return !CSharpHotfixManager.IsHotfixEnabled;
        }

        [MenuItem("CSharpHotfix/Disable", false, 2)]
        public static void DisableHotifxMenu()
        {
            CSharpHotfixManager.IsHotfixEnabled = false;
        }
        
        [MenuItem("CSharpHotfix/Disable", true, 2)]
        public static bool DisableHotifxMenuValidate()
        {
            return CSharpHotfixManager.IsHotfixEnabled;
        }

        
        

        
        private static string[] injectAssemblys = new string[]
        {
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass"
        };

        private static HashSet<string> filterNamespace= new HashSet<string>()
        {
            "CSharpHotfix", 
            "System", 
            "UnityEngine", 
            "UnityEditor", 
        };

        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            Debug.Log("#CS_HOTFIX# OnInitialized: " + CSharpHotfixManager.IsHotfixEnabled);
            if (!CSharpHotfixManager.IsHotfixEnabled)
                return;
            TryInject();
        }

        public static void TryInject()
        {
            if (!CSharpHotfixManager.IsHotfixEnabled)
                return;

            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                UnityEngine.Debug.LogError("#CS_HOTFIX# TryInject: inject failed, compiling or playing, please re-enable it");
                CSharpHotfixManager.IsHotfixEnabled = false;
                return;
            }

            // inject
            CSharpHotfixManager.PrepareMethodId();
            foreach (var assembly in injectAssemblys)
            {
                InjectAssembly(assembly);
            }
            CSharpHotfixManager.PrintAllMethodId();
        }

        private static void InjectAssembly(string assemblyName)
        {
            var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
                .Select(type => type)
                .Where(type => 
                    type.Assembly.GetName().Name == assemblyName && !filterNamespace.Contains(type.Namespace))
                .ToList();

            foreach (var type in typeList)
            {
                Debug.LogFormat("#CS_HOTFIX# inject: assemblyName: {0} \ttype: {1}", assemblyName, type);

                var methodList = type.GetMethods();
                foreach (var method in methodList)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(method);
                    var methodId = CSharpHotfixManager.GetMethodId(signature);
                    // Debug.LogFormat("#CS_HOTFIX# method: {0}\tsignature: {1}\tmethodId: {2}", method, signature, methodId);
                }
            }

        }
    }

}

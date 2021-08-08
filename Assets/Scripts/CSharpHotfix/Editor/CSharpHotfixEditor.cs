using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using UnityEngine;
using UnityEditor;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
                CSharpHotfixInjector.TryInject();
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

        [MenuItem("CSharpHotfix/Hotfix", false, 2)]
        public static void TryHotfix()
        {
            CSharpHotfixInterpreter.ReloadHotfixFiles();
        }


        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            CSharpHotfixManager.Message("#CS_HOTFIX# CSharpHotfixEditor.OnInitialized: " + CSharpHotfixManager.IsHotfixEnabled);
            if (!CSharpHotfixManager.IsHotfixEnabled)
                return;
            CSharpHotfixInjector.TryInject();
        }

    }

}

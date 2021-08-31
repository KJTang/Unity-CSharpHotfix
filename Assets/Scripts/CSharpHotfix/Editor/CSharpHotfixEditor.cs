using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using UnityEditor;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CSharpHotfix.Editor
{
    [InitializeOnLoad]
    public static class CSharpHotfixEditor 
    {

        private const string kEnableBtnName = "CSharpHotfix/Enable";

        static CSharpHotfixEditor() 
        {
            var enabled = EditorPrefs.GetBool(kEnableBtnName, false);
            CSharpHotfixManager.IsHotfixEnabled = enabled;

            // called once after all inspector initilized    
            //EditorApplication.delayCall += () => {
            //    EnableHotfix(CSharpHotfixManager.IsHotfixEnabled);
            //};
        }
         
        [MenuItem(kEnableBtnName, false, 1)]
        private static void EnableHotifxMenuCheckMark() 
        {
            var enabled = !CSharpHotfixManager.IsHotfixEnabled;
            EnableHotfix(enabled);
        }

        [MenuItem(kEnableBtnName, true, 1)]
        public static bool EnableHotifxMenuValidate()
        {
            if (EditorApplication.isCompiling || Application.isPlaying)
                return false;
            return true;
        }

        private static void EnableHotfix(bool enabled)
        {
            var changed = enabled != CSharpHotfixManager.IsHotfixEnabled;
            Menu.SetChecked(kEnableBtnName, enabled);
            EditorPrefs.SetBool(kEnableBtnName, enabled);
            CSharpHotfixManager.IsHotfixEnabled = enabled;

            if (changed)
            {
                if (enabled)
                {
                    CSharpHotfixInjector.TryInject();
                }
                else
                {
                    ForceRecomiple();
                }
            }
        }

        private static void ForceRecomiple()
        {
            var assetsPath = Application.dataPath;
            var assetsUri = new System.Uri(assetsPath);
            var files = Directory.GetFiles(assetsPath, "CSharpHotfixManager.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            { 
                if (file.EndsWith("CSharpHotfixManager.cs"))
                {
                    // delete old assemblies
                    CSharpHotfixInjector.RevertInject();

                    // reimport to force compile
			        var relativeUri = assetsUri.MakeRelativeUri(new System.Uri(file));
			        var relativePath = System.Uri.UnescapeDataString(relativeUri.ToString());
                    AssetDatabase.ImportAsset(relativePath, ImportAssetOptions.ForceUpdate);
                    AssetDatabase.Refresh();
                    break;
                }
            }
        }

        [MenuItem("CSharpHotfix/Hotfix", false, 2)]
        public static void TryHotfix()
        {
            CSharpHotfixInterpreter.ReloadHotfixFiles();
        }


        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
            var enabled = EditorPrefs.GetBool(kEnableBtnName, false);
            CSharpHotfixManager.IsHotfixEnabled = false;

            CSharpHotfixManager.Message("#CS_HOTFIX# CSharpHotfixEditor.OnInitialized: is hotfix enabled: " + enabled);
            if (!enabled)
                return;

            EnableHotfix(true);
        }

    }

}

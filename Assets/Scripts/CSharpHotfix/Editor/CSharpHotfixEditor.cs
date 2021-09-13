using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using System.Text;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;

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
        
#if !CSHOFIX_COMPATIBLE_MODE
        [MenuItem("CSharpHotfix/Hotfix", false, 2)]
        public static void TryHotfix()
        {
            CSharpHotfixInterpreter.HotfixFromCodeFiles();
        }
#endif
        
#if CSHOFIX_COMPATIBLE_MODE
        [MenuItem("CSharpHotfix/Hotfix (Compatible Mode)", false, 2)]
        public static void TryHotfixCompatibleMode()
        {
            var monoPath = Path.Combine(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName),
                "Data/MonoBleedingEdge/bin/mono.exe");
            if (!File.Exists(monoPath))
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# TryHotfix (Compatible Mode): can not find mono: {0}", monoPath);
                return;
            }

            var toolPath = CSharpHotfixManager.GetAppRootPath() + "CSharpHotfix/Tools/CSharpHotfixTool/CSharpHotfixTool/bin/Debug/net472/CSharpHotfixTool.exe";
            if (!File.Exists(toolPath))
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# TryHotfix (Compatible Mode): can not find hotfix tool: {0}", toolPath);
                return;
            }

            var hotfixProc = new Process();
            hotfixProc.StartInfo.FileName = monoPath;
            hotfixProc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            hotfixProc.StartInfo.RedirectStandardOutput = true;
            hotfixProc.StartInfo.UseShellExecute = false;
            hotfixProc.StartInfo.CreateNoWindow = true;

            // arguments
            // proj path
            var projPath = CSharpHotfixManager.GetAppRootPath();

            // assemblies
            var assemblies = CSharpHotfixManager.GetAssemblies();
            var sb = new StringBuilder();
            foreach (var assembly in assemblies)
            {
                if (assembly.IsDynamic)
                    continue;
                sb.Append(assembly.Location);
                sb.Append(";");
            }
            var assembliesStr = sb.ToString();

            // defines
            var definitions = CSharpHotfixManager.GetMacroDefinitions();
            sb.Length = 0;
            foreach (var define in definitions)
            {
                sb.Append(define);
                sb.Append(";");
            }
            var definitionsStr = sb.ToString();

            hotfixProc.StartInfo.Arguments = "--debug " + 
                "\"" + toolPath + "\" " + 
                "\"" + projPath + "\" " + 
                "\"" + assembliesStr + "\" " + 
                "\"" + definitionsStr + "\" " + 
            "";
            
            //UnityEngine.Debug.LogError(hotfixProc.StartInfo.FileName);
            //UnityEngine.Debug.LogError(hotfixProc.StartInfo.Arguments);
            hotfixProc.Start();

            // get output
            StringBuilder exceptionInfo = null;
            while (!hotfixProc.StandardOutput.EndOfStream)
            {
                string line = hotfixProc.StandardOutput.ReadLine();
                if (exceptionInfo != null)
                {
                    exceptionInfo.AppendLine(line);
                }
                else
                {
                    if (line.StartsWith("Unhandled Exception:"))
                    {
                        exceptionInfo = new StringBuilder(line);
                    }
                    else
                    {
                        UnityEngine.Debug.Log("#CS_HOTFIX# compatible mode: " + line);
                    }
                }
            }
            hotfixProc.WaitForExit();
            if (exceptionInfo != null)
            {
                CSharpHotfixManager.Error(exceptionInfo.ToString());
            }

            var succ = exceptionInfo == null;
            if (!succ)
            {
                UnityEngine.Debug.LogError("hotfix (compatible mode) finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }

            CSharpHotfixInterpreter.HotfixFromAssembly();
        }
#endif
        
        
        [MenuItem("CSharpHotfix/Force Recompile", false, 3)]
        public static void ForceRecompileMenu()
        {
            ForceRecomiple();
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
        
        [MenuItem("CSharpHotfix/Gen Method Id", false, 4)]
        public static void GenMethodIdMenu()
        {
            CSharpHotfixInjector.GenMethodId();
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

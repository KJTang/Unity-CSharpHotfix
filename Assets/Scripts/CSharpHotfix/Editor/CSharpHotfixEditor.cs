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


        [MenuItem("CSharpHotfix/Inject (Compatible Mode)", false, 2)]
        public static void InjectMenu()
        {
            //CSharpHotfixInjector.TryInject(true);
            
            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Inject: cannot inject during playing or compiling");
                return;
            }
            
            var arguments = new List<string>();
            var succ = ExecuteCommand(true, arguments);
            if (!succ)
            {
                UnityEngine.Debug.LogError("Inject (compatible mode) finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }
        }
        
        [MenuItem("CSharpHotfix/Hotfix (Compatible Mode)", false, 3)]
        public static void TryHotfixCompatibleMode()
        {
            if (EditorApplication.isCompiling)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Hotfix: cannot hotfix during compiling");
                return;
            }

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

            // execute cmd
            var arguments = new List<string>();
            arguments.Add(assembliesStr);
            arguments.Add(definitionsStr);
            var succ = ExecuteCommand(false, arguments);

            if (!succ)
            {
                UnityEngine.Debug.LogError("Hotfix (compatible mode) finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }

            CSharpHotfixInterpreter.HotfixFromAssembly();
        }
        
        private static bool ExecuteCommand(bool isInjectMode, List<string> arguments)
        {
            var monoPath = CSharpHotfixCfg.GetMonoPath();
            if (!File.Exists(monoPath))
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# ExecuteCommand: can not find mono: {0}", monoPath);
                return false;
            }

            var toolPath = CSharpHotfixManager.GetAppRootPath() + "CSharpHotfix/Tools/CSharpHotfixTool/CSharpHotfixTool/bin/Debug/net472/CSharpHotfixTool.exe";
            if (!File.Exists(toolPath))
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# ExecuteCommand: can not find hotfix tool: {0}", toolPath);
                return false;
            }

            var hotfixProc = new Process();
            hotfixProc.StartInfo.FileName = monoPath;
            hotfixProc.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            hotfixProc.StartInfo.RedirectStandardOutput = true;
            hotfixProc.StartInfo.UseShellExecute = false;
            hotfixProc.StartInfo.CreateNoWindow = true;

            // arguments
            var projPath = CSharpHotfixManager.GetAppRootPath();
            var mode = isInjectMode ? "--inject" : "--hotfix";

            var argSb = new StringBuilder();
            argSb.Append("--debug ");
            argSb.Append("\"" + toolPath + "\" ");
            argSb.Append("\"" + mode + "\" ");
            argSb.Append("\"" + projPath + "\" ");
            foreach (var arg in arguments)
            {
                argSb.Append("\"" + arg + "\" ");
            }
            hotfixProc.StartInfo.Arguments = argSb.ToString();
            
            //UnityEngine.Debug.LogError(hotfixProc.StartInfo.FileName);
            //UnityEngine.Debug.LogError(hotfixProc.StartInfo.Arguments);
            hotfixProc.Start();

            // get output
            var succ = true;
            StringBuilder exceptionInfo = new StringBuilder();
            while (!hotfixProc.StandardOutput.EndOfStream)
            {
                var line = hotfixProc.StandardOutput.ReadLine();
                if (line.StartsWith("Exception:"))
                {
                    exceptionInfo.Length = 0;
                    while (!line.StartsWith("Exception End"))
                    {
                        exceptionInfo.Append(line);
                        line = hotfixProc.StandardOutput.ReadLine();
                    }
                    succ = false;
                    line = exceptionInfo.ToString();
                }
                UnityEngine.Debug.Log("#CS_HOTFIX# compatible mode: " + line);
            }
            hotfixProc.WaitForExit();

            if (!succ)
            {
                UnityEngine.Debug.LogError("#CS_HOTFIX# ExecuteCommand: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return false;
            }
            return true;
        }
        
        
        [MenuItem("CSharpHotfix/Force Recompile", false, 21)]
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
        
        [MenuItem("CSharpHotfix/Gen Method Id", false, 22)]
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

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

        static CSharpHotfixEditor() 
        {
        }


        [InitializeOnLoadMethod]
        private static void OnInitialized()
        {
        }


        [MenuItem("CSharpHotfix/Inject", false, 1)]
        public static void InjectMenu()
        {
            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Inject: cannot inject during playing or compiling");
                return;
            }

            // types to inject
            var sb = new StringBuilder();
            foreach (var assemblyName in CSharpHotfixCfg.InjectAssemblies)
            {
                var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
                    .Select(type => type)
                    .Where(type => 
                        type.Assembly.GetName().Name == assemblyName && 
                        (string.IsNullOrEmpty(type.Namespace) || !CSharpHotfixCfg.InjectFilterNamespace.Contains(type.Namespace.Split('.')[0])))
                    .ToList();

                // format: "assemblyA;typeA;typeB;typeC|assemblyB;typeD;typeE;"
                sb.Append(assemblyName);
                sb.Append(";");

                foreach (var type in typeList)
                {
                    sb.Append(type.FullName);
                    sb.Append(";");
                }

                sb.Append("|");
            }
            var injectTypesStr = sb.ToString();

            // search paths
            sb.Length = 0;
            foreach (var path in
                (from asm in AppDomain.CurrentDomain.GetAssemblies()
                    select System.IO.Path.GetDirectoryName(asm.ManifestModule.FullyQualifiedName)).Distinct())
            {
                sb.Append(path);
                sb.Append(";");
            }
            var searchPathStr = sb.ToString();

            // execute cmd
            var arguments = new List<string>();
            arguments.Add(injectTypesStr);
            arguments.Add(searchPathStr);
            var succ = ExecuteCommand(InjectMode.INJECT, arguments);
            if (!succ)
            {
                UnityEngine.Debug.LogError("Inject finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }
        }
        
        [MenuItem("CSharpHotfix/Hotfix", false, 2)]
        public static void TryHotfix()
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
            var succ = ExecuteCommand(InjectMode.HOTFIX, arguments);

            if (!succ)
            {
                UnityEngine.Debug.LogError("Hotfix finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }

            HotfixFromAssembly();
        }
        
        [MenuItem("CSharpHotfix/Force Recompile", false, 21)]
        public static void ForceRecompileMenu()
        {
            var assetsPath = Application.dataPath;
            var assetsUri = new System.Uri(assetsPath);
            var files = Directory.GetFiles(assetsPath, "CSharpHotfixManager.cs", SearchOption.AllDirectories);
            foreach (var file in files)
            { 
                if (file.EndsWith("CSharpHotfixManager.cs"))
                {
                    // delete old assemblies
                    RevertInject();

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
            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Inject: cannot gen method id during playing or compiling");
                return;
            }

            // types to inject
            var sb = new StringBuilder();
            foreach (var assemblyName in CSharpHotfixCfg.InjectAssemblies)
            {
                var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
                    .Select(type => type)
                    .Where(type => 
                        type.Assembly.GetName().Name == assemblyName && 
                        (string.IsNullOrEmpty(type.Namespace) || !CSharpHotfixCfg.InjectFilterNamespace.Contains(type.Namespace.Split('.')[0])))
                    .ToList();

                // format: "assemblyA;typeA;typeB;typeC|assemblyB;typeD;typeE;"
                sb.Append(assemblyName);
                sb.Append(";");

                foreach (var type in typeList)
                {
                    sb.Append(type.FullName);
                    sb.Append(";");
                }

                sb.Append("|");
            }
            var injectTypesStr = sb.ToString();

            // search paths
            sb.Length = 0;
            foreach (var path in
                (from asm in AppDomain.CurrentDomain.GetAssemblies()
                    select System.IO.Path.GetDirectoryName(asm.ManifestModule.FullyQualifiedName)).Distinct())
            {
                sb.Append(path);
                sb.Append(";");
            }
            var searchPathStr = sb.ToString();

            // execute cmd
            var arguments = new List<string>();
            arguments.Add(injectTypesStr);
            arguments.Add(searchPathStr);
            var succ = ExecuteCommand(InjectMode.GEN_METHOD_ID, arguments);
            if (!succ)
            {
                UnityEngine.Debug.LogError("Inject finsihed: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return;
            }
        }

        
        public static void RevertInject()
        {
            if (CSharpHotfixManager.IsHotfixEnabled)
                return;

            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# RevertInject: rervert failed, compiling or playing, please re-try after process finished");
                CSharpHotfixManager.IsHotfixEnabled = false;
                return;
            }

            foreach (var assemblyName in CSharpHotfixCfg.InjectAssemblies)
            {
                var assemblyPath = CSharpHotfixManager.GetAssemblyPath(assemblyName);
                CSharpHotfixManager.Message("#CS_HOTFIX# RevertAssembly: assemblyName: {0} \t{1}", assemblyName, assemblyPath);

                // dll
                try
                {
                    if (System.IO.File.Exists(assemblyPath))
                        System.IO.File.Delete(assemblyPath);
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# RevertInject: rervert failed: {0}", e);
                }
                
                // pdb
                try
                {
                    var assemblyPDBPath = assemblyPath.Replace(".dll", ".pdb");
                    if (System.IO.File.Exists(assemblyPDBPath))
                        System.IO.File.Delete(assemblyPDBPath);
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# RevertInject: rervert failed: {0}", e);
                }

                // hotfix dll
                try
                {
                    var hotfixPath = assemblyPath.Replace(".dll", ".hotfix.dll");
                    if (System.IO.File.Exists(hotfixPath))
                        System.IO.File.Delete(hotfixPath);
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# RevertInject: rervert failed: {0}", e);
                }
                
                // hotfix pdb
                try
                {
                    var hotfixPDBPath = assemblyPath.Replace(".pdb", ".hotfix.pdb");
                    if (System.IO.File.Exists(hotfixPDBPath))
                        System.IO.File.Delete(hotfixPDBPath);
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# RevertInject: rervert failed: {0}", e);
                }
            }
        }
        
        public static void HotfixFromAssembly()
        {
            if (!CSharpHotfixManager.IsMethodIdFileExist())
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# HotfixMethod: no method id file cache, please re-generate it");
                return;
            }
            CSharpHotfixManager.LoadMethodIdFromFile();
            CSharpHotfixManager.ClearReflectionData();

            // load assembly from file
            var hotfixStream = new MemoryStream();
            using (var fileStream = new FileStream(CSharpHotfixManager.GetHotfixAssemblyPath(), FileMode.Open, System.IO.FileAccess.Read)) 
            {
                byte[] bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, (int)fileStream.Length);
                hotfixStream.Write(bytes, 0, bytes.Length);
            }

            // save methodinfo
            CSharpHotfixManager.ClearMethodInfo();
            var hotfixAssembly = Assembly.Load(hotfixStream.GetBuffer(), null);
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var typeLst = hotfixAssembly.GetTypes();
            foreach (var type in typeLst)
            {
                var methodLst = type.GetMethods(bindingFlags);
                foreach (var methodInfo in methodLst)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(methodInfo);
                    var fixedSignature = CSharpHotfixManager.FixHotfixMethodSignature(signature);
                    var methodId = CSharpHotfixManager.GetMethodId(fixedSignature);
                    if (methodId <= 0) 
                        continue;

                    var state = CSharpHotfixManager.GetHotfixMethodStaticState(signature);
                    CSharpHotfixManager.SetMethodInfo(methodId, methodInfo, state == 2);
                    CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: {0} \t{1}", methodId, fixedSignature);
                }
            }

            // close stream
            hotfixStream.Close();
            
            // debug: 
            //CSharpHotfixManager.PrintAllMethodInfo();
            CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: hotfix finished");
        }
        
        public enum InjectMode
        {
            INJECT = 1, 
            HOTFIX = 2, 
            GEN_METHOD_ID = 3, 
        }

        private static bool ExecuteCommand(InjectMode injectMode, List<string> arguments)
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
            var mode = "";
            switch (injectMode)
            {
                case InjectMode.INJECT:
                    mode = "--inject";
                    break;
                case InjectMode.HOTFIX:
                    mode = "--hotfix";
                    break;
                case InjectMode.GEN_METHOD_ID:
                    mode = "--gen_method_id";
                    break;
            }

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
                UnityEngine.Debug.Log("#CS_HOTFIX# CSharpHotfixTool: " + line);
            }
            hotfixProc.WaitForExit();

            if (!succ)
            {
                UnityEngine.Debug.LogError("#CS_HOTFIX# ExecuteCommand: " + (!succ ? "<color=red>failed</color>" : "<color=green>succ</color>"));
                return false;
            }
            return true;
        }
        

    }

}

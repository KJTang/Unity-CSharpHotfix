using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using UnityEngine;
using UnityEditor;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CSharpHotfix
{
    public class CSharpHotfixInjector 
    {
        private static string[] injectAssemblys = new string[]
        {
            "Assembly-CSharp",
            "Assembly-CSharp-firstpass"
        };

        private static HashSet<string> filterNamespace= new HashSet<string>()
        {
            "CSharpHotfix", 
            "System", 
            // "UnityEngine", 
            // "UnityEditor", 
            "FlyingWormConsole3",       // my console plugin
        };

        public static void TryInject()
        {
            if (!CSharpHotfixManager.IsHotfixEnabled)
                return;

            if (EditorApplication.isCompiling || Application.isPlaying)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# TryInject: inject failed, compiling or playing, please re-enable it");
                CSharpHotfixManager.IsHotfixEnabled = false;
                return;
            }

            // inject
            CSharpHotfixManager.PrepareMethodId();
            foreach (var assembly in injectAssemblys)
            {
                InjectAssembly(assembly);
            }
            // CSharpHotfixManager.PrintAllMethodId();
            CSharpHotfixManager.SaveMethodIdToFile();
            CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: inject finished");
        }

        private static string assemblyDir;
        private static string GetAssemblyPath(string assemblyName)
        {
            if (assemblyDir == null)
            {
                assemblyDir = Application.dataPath;
                var pos = assemblyDir.IndexOf("Assets");
                if (pos >= 0)
                {
                    assemblyDir = assemblyDir.Remove(pos);
                }
                assemblyDir = assemblyDir + "Library/ScriptAssemblies/";
            }
            var assemblyPath = assemblyDir + assemblyName + ".dll";
            return assemblyPath;
        }
        
        private static void InjectAssembly(string assemblyName)
        {
            var assemblyPath = GetAssemblyPath(assemblyName);
            CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: assemblyName: {0} \t{1}", assemblyName, assemblyPath);
            if (!System.IO.File.Exists(assemblyPath))
            {
                CSharpHotfixManager.Warning("#CS_HOTFIX# InjectAssembly: assembly not exist: {0}", assemblyPath);
                return;
            }
            
            // get method list need inject
            var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
                .Select(type => type)
                .Where(type => 
                    type.Assembly.GetName().Name == assemblyName && 
                    (string.IsNullOrEmpty(type.Namespace) || !filterNamespace.Contains(type.Namespace.Split('.')[0])))
                .ToList();

            // generate method id for method need inject
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in typeList)
            {
                CSharpHotfixManager.Log("#CS_HOTFIX# InjectAssembly: reflection type: {0} \tnamespace: {1}", type, type.Namespace);

                var methodList = type.GetMethods(bindingFlags);
                foreach (var method in methodList)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(method);
                    var methodId = CSharpHotfixManager.GetMethodId(signature, true);    // true: will generate method id if not exist before

                    // Debug.LogFormat("#CS_HOTFIX# method: {0}\tsignature: {1}\tmethodId: {2}", method, signature, methodId);
                }
            }


            // read assembly
            AssemblyDefinition assembly = null;
            var readSymbols = true;
            try
            {
                // try read with symbols
                assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = true });
            }
            catch
            {
                // read with symbols failed, just don't read them
                CSharpHotfixManager.Warning("#CS_HOTFIX# InjectAssembly: read assembly with symbol failed: {0}", assemblyPath);
                try
                {
                    readSymbols = false;
                    assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = false });
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# InjectAssembly: read assembly failed: {0}", e);
                }
            }
            if (assembly == null)
                return;


            // inject il
            try
            {
                // modify method il
                ModuleDefinition module = assembly.MainModule;
                foreach (TypeDefinition type in module.Types) 
                {
                    CSharpHotfixManager.Log("#CS_HOTFIX# InjectAssembly: cecil type: {0}", type);

                    foreach (MethodDefinition method in type.Methods)
                    {
                        var signature = CSharpHotfixManager.GetMethodSignature(method);
                        var methodId = CSharpHotfixManager.GetMethodId(signature);
                        if (methodId <= 0)
                        {
                            CSharpHotfixManager.Log("#CS_HOTFIX# Cecil: cannot find method id: {0}", signature);
                            continue;
                        }

                        // TODO: insert il with method id
                    }
                }

                // modify assembly
                assembly.Write(assemblyPath + "_test.dll", new WriterParameters { WriteSymbols = readSymbols });
            }
            catch (Exception e)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# InjectAssembly: inject method failed: {0}", e);
            }
            finally
            {
                // clear symbol reader, incase lock file on windows
                if (assembly != null && assembly.MainModule.SymbolReader != null)
                {
                    assembly.MainModule.SymbolReader.Dispose();
                }
                assembly.Dispose();
            }
        }
    }

}

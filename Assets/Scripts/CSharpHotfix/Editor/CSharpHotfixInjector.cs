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
            "UnityEngine", 
            "UnityEditor", 
        };

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
            Debug.LogFormat("#CS_HOTFIX# InjectAssembly: assemblyName: {0}", assemblyName);
            // get method list need inject
            var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
                .Select(type => type)
                .Where(type => 
                    type.Assembly.GetName().Name == assemblyName && !filterNamespace.Contains(type.Namespace))
                .ToList();

            // generate method id for method need inject
            foreach (var type in typeList)
            {
                Debug.LogFormat("#CS_HOTFIX# InjectAssembly: reflection type: {0}", type);

                var methodList = type.GetMethods();
                foreach (var method in methodList)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(method);
                    var methodId = CSharpHotfixManager.GetMethodId(signature, true);    // true: will generate method id if not exist before

                    // Debug.LogFormat("#CS_HOTFIX# method: {0}\tsignature: {1}\tmethodId: {2}", method, signature, methodId);
                }
            }


            // read assembly
            var assemblyPath = GetAssemblyPath(assemblyName);
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
                Debug.LogWarningFormat("#CS_HOTFIX# InjectAssembly: read assembly with symbol failed: {0}", assemblyPath);
                try
                {
                    readSymbols = false;
                    assembly = AssemblyDefinition.ReadAssembly(assemblyPath, new ReaderParameters { ReadSymbols = false });
                }
                catch (Exception e)
                {
                    Debug.LogFormat("#CS_HOTFIX# InjectAssembly: read assembly failed: {0}", e);
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
                    Debug.LogFormat("#CS_HOTFIX# InjectAssembly: cecil type: {0}", type);

                    foreach (MethodDefinition method in type.Methods)
                    {
                        var signature = CSharpHotfixManager.GetMethodSignature(method);
                        var methodId = CSharpHotfixManager.GetMethodId(signature);
                        if (methodId < 0)
                        {
                            Debug.LogFormat("#CS_HOTFIX# Cecil: cannot find method id: {0}", signature);
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
                Debug.LogFormat("#CS_HOTFIX# InjectAssembly: inject method failed: {0}", e);
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

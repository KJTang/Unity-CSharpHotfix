using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CSharpHotfixTool
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
        
        public static bool IsFilteredNamespace(string ns)
        {
            if (string.IsNullOrEmpty(ns))
                return false;

            //var first = ns.Split('.')[0];
            //if (filterNamespace.Contains(first))
            //    return true;

            foreach (var filterNS in filterNamespace)
            {
                if (ns == filterNS)
                    return true;

                if (ns.Split('.')[0] == filterNS)
                    return true;
            }

            return false;
        }

        private const string injectedFlag = "CSharpHotfixInjectedFlag";

        public static void TryInject()
        {
            //if (EditorApplication.isCompiling || Application.isPlaying)
            //{
            //    CSharpHotfixManager.Error("#CS_HOTFIX# TryInject: inject failed, compiling or playing, please re-try after process finished");
            //    CSharpHotfixManager.IsHotfixEnabled = false;
            //    return;
            //}

            // inject
            CSharpHotfixManager.PrepareMethodId();
            var succ = true;
            foreach (var assembly in injectAssemblys)
            {
                if (!InjectAssembly(assembly))
                {
                    succ = false;
                    break;
                }
            }
            // CSharpHotfixManager.PrintAllMethodId();
            CSharpHotfixManager.SaveMethodIdToFile();

            if (succ)
                CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: inject finished");
            else
                CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: inject failed");
        }


        public static void GenMethodId()
        {
            //if (EditorApplication.isCompiling || Application.isPlaying)
            //{
            //    CSharpHotfixManager.Error("#CS_HOTFIX# GenMethodId: generate failed, compiling or playing, please re-try after process finished");
            //    CSharpHotfixManager.IsHotfixEnabled = false;
            //    return;
            //}

            // inject
            CSharpHotfixManager.PrepareMethodId();
            var succ = true;
            foreach (var assembly in injectAssemblys)
            {
                if (!InjectAssembly(assembly, true))
                {
                    succ = false;
                    break;
                }
            }
            // CSharpHotfixManager.PrintAllMethodId();
            CSharpHotfixManager.SaveMethodIdToFile();
            if (succ)
                CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: gen methoId finished");
            else
                CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: gen methoId failed");
        }

        
        private static bool InjectAssembly(string assemblyName, bool onlyGenMethodId = false)
        {
            var assemblyPath = CSharpHotfixManager.GetAssemblyPath(assemblyName);
            CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: assemblyName: {0} \t{1}", assemblyName, assemblyPath);
            if (!System.IO.File.Exists(assemblyPath))
            {
                CSharpHotfixManager.Warning("#CS_HOTFIX# InjectAssembly: assembly not exist: {0}", assemblyPath);
                return true;
            }
            var assemblyPDBPath = assemblyPath.Replace(".dll", ".pdb");
            var hotfixAssemblyPath = assemblyPath.Replace(".dll", ".hotfix.dll");
            var hotfixAssemblyPDBPath = assemblyPath.Replace(".pdb", ".hotfix.pdb");

            // get method list need inject
            //var typeList = CSharpHotfixCfg.ToProcess.Where(type => type is Type)
            //    .Select(type => type)
            //    .Where(type => 
            //        type.Assembly.GetName().Name == assemblyName && 
            //        !IsFilteredNamespace(type.Namespace))
            //    .ToList();
            var typeList = CSharpHotfixManager.GetTypesToInject();

            // record who can be injected, make injection faster
            var classCanBeInject = new HashSet<string>();
            var methodCanBeInject = new HashSet<string>();

            // generate method id for method need inject
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var type in typeList)
            {
                CSharpHotfixManager.Log("#CS_HOTFIX# InjectAssembly: reflection type: {0} \tnamespace: {1}", type, type.Namespace);
                if (!type.IsClass || type.IsGenericType)
                    continue;

                var canBeInject = false;
                var methodList = type.GetMethods(bindingFlags);
                foreach (var method in methodList)
                {
                    if (method.IsGenericMethod)
                        continue;

                    var isExtern = (method.GetMethodImplementationFlags() & System.Reflection.MethodImplAttributes.InternalCall) != 0;
                    if (isExtern)
                        continue;

                    if (method.IsSpecialName && (method.Name.StartsWith("get_") || method.Name.StartsWith("set_")))
                        continue;

                    var signature = CSharpHotfixManager.GetMethodSignature(method);
                    var methodId = CSharpHotfixManager.GetMethodId(signature, true);    // true: will generate method id if not exist before

                    canBeInject = true;
                    methodCanBeInject.Add(method.Name);
                    // Debug.LogFormat("#CS_HOTFIX# method: {0}\tsignature: {1}\tmethodId: {2}", method, signature, methodId);
                }

                if (canBeInject)
                {
                    classCanBeInject.Add(type.FullName);
                }
            }
            if (onlyGenMethodId)
                return true;

            // no need inject
            if (classCanBeInject.Count <= 0 && methodCanBeInject.Count <= 0)
                return true;


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
                return false;

            // init resolver search path
            var initResolverSucc = true;
            var resolver = assembly.MainModule.AssemblyResolver as BaseAssemblyResolver;
            foreach (var path in
                (from asm in AppDomain.CurrentDomain.GetAssemblies()
                    select System.IO.Path.GetDirectoryName(asm.ManifestModule.FullyQualifiedName)).Distinct())
            {
                try
                {
                    //UnityEngine.Debug.LogError("searchPath:" + path);
                    resolver.AddSearchDirectory(path);
                }
                catch (Exception e) 
                { 
                    initResolverSucc = false;
                    CSharpHotfixManager.Error("#CS_HOTFIX# InjectAssembly: init resolver failed: {0}", e);
                }
            }
            if (!initResolverSucc)
                return false;


            // check if injected before
            if (assembly.MainModule.Types.Any(t => t.Name == injectedFlag))
            {
                CSharpHotfixManager.Message("#CS_HOTFIX# InjectAssembly: already injected");
                return false;
            }

            // inject il
            var succ = true;
            try
            {
                // modify method il
                ModuleDefinition module = assembly.MainModule;
                foreach (TypeDefinition type in module.Types) 
                {
                    CSharpHotfixManager.Log("#CS_HOTFIX# InjectAssembly: cecil type: {0}", type);
                    if (!type.IsClass || !classCanBeInject.Contains(type.FullName))
                        continue;

                    foreach (MethodDefinition method in type.Methods)
                    {
                        if (!methodCanBeInject.Contains(method.Name))
                            continue; 

                        if (method.IsGetter || method.IsSetter || method.HasGenericParameters)
                            continue;

                        // example: extern methods
                        if (method.Body == null || method.Body.Instructions == null)
                        {
                            continue;
                        }

                        var signature = CSharpHotfixManager.GetMethodSignature(method);
                        var methodId = CSharpHotfixManager.GetMethodId(signature);
                        if (methodId <= 0)
                        {
                            // CSharpHotfixManager.Log("#CS_HOTFIX# Cecil: cannot find method id: {0}", signature);
                            continue;
                        }
                        InjectMethod(methodId, method, assembly);
                    }
                }

                // mark as injected
                var objType = assembly.MainModule.ImportReference(typeof(System.Object));
                var flagType = new TypeDefinition("CSharpHotfix", injectedFlag, Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, objType);
                assembly.MainModule.Types.Add(flagType);

                // modify assembly
                assembly.Write(hotfixAssemblyPath, new WriterParameters { WriteSymbols = readSymbols });
            }
            catch (Exception e)
            {
                succ = false;
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
            if (!succ)
                return false;

            // overwrite assembly
            var copySucc = true;
            try
            {
                System.IO.File.Copy(hotfixAssemblyPath, assemblyPath, true);
                System.IO.File.Copy(hotfixAssemblyPDBPath, assemblyPDBPath, true);
            }
            catch (Exception e)
            {
                copySucc = false;
                CSharpHotfixManager.Error("#CS_HOTFIX# InjectAssembly: override assembly failed: {0}", e);
            }
            return copySucc;
        }

        
        private static void InjectMethod(int methodId, MethodDefinition method, AssemblyDefinition assembly)
        {
            var hasMethodInfo = assembly.MainModule.ImportReference(typeof(CSharpHotfixManager).GetMethod("HasMethodInfo"));
            var voidMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfixManager).GetMethod("MethodReturnVoidWrapper"));
            var objMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfixManager).GetMethod("MethodReturnObjectWrapper"));

            var body = method.Body;
            var originIL = body.Instructions;
            var ilProcessor = body.GetILProcessor();
            var insertPoint = originIL[0];
            var endPoint = originIL[originIL.Count - 1];
            var ilList = new List<Instruction>();
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, methodId));
            ilList.Add(Instruction.Create(OpCodes.Call, hasMethodInfo));
            ilList.Add(Instruction.Create(OpCodes.Brfalse, insertPoint));
            InjectMethodArgument(methodId, method, ilList, assembly);
            if (method.ReturnType.FullName == "System.Void")
                ilList.Add(Instruction.Create(OpCodes.Call, voidMethodInject));
            else
                ilList.Add(Instruction.Create(OpCodes.Call, objMethodInject));
            if (method.ReturnType.IsValueType)  // unbox return value
            {
                var returnType = assembly.MainModule.ImportReference(method.ReturnType);
                ilList.Add(Instruction.Create(OpCodes.Unbox, returnType));
            }
            ilList.Add(Instruction.Create(OpCodes.Br, endPoint));

            // inject il
            for (var i = ilList.Count - 1; i >= 0; --i)
                ilProcessor.InsertBefore(originIL[0], ilList[i]);

            CSharpHotfixManager.Message("#CS_HOTFIX# InjectMethod: {0}", method.FullName);
        }

        private static void InjectMethodArgument(int methodId, MethodDefinition method, List<Instruction> ilList, AssemblyDefinition assembly)
        {
            var shift = 2;  // extra: methodId, instance

            //object[] arr = new object[argumentCount + shift]
            var argumentCount = method.Parameters.Count;
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, argumentCount + shift));  
            ilList.Add(Instruction.Create(OpCodes.Newarr, assembly.MainModule.ImportReference(typeof(object))));

            // methodId
            ilList.Add(Instruction.Create(OpCodes.Dup));
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, methodId));
            ilList.Add(Instruction.Create(OpCodes.Box, assembly.MainModule.ImportReference(typeof(System.Int32))));
            ilList.Add(Instruction.Create(OpCodes.Stelem_Ref));

            // instance
            ilList.Add(Instruction.Create(OpCodes.Dup));
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
            if (method.IsStatic)
                ilList.Add(Instruction.Create(OpCodes.Ldnull));
            else
                ilList.Add(Instruction.Create(OpCodes.Ldarg_0));
            ilList.Add(Instruction.Create(OpCodes.Stelem_Ref));

            // arguments
            for (int i = 0; i < argumentCount; ++i) 
            {
                var parameter = method.Parameters[i];

                // value = argument[i]
                ilList.Add(Instruction.Create(OpCodes.Dup));
                ilList.Add(Instruction.Create(OpCodes.Ldc_I4, i + shift));
                ilList.Add(Instruction.Create(OpCodes.Ldarg, parameter));

                // box
                TryBoxMethodArgument(parameter, ilList, assembly);

                // arr[i] = value;
                ilList.Add(Instruction.Create(OpCodes.Stelem_Ref));
            }

            // don't pop it, we will need it when call method wrapper
            // ilList.Add(Instruction.Create(OpCodes.Pop));
        }

        private static void TryBoxMethodArgument(ParameterDefinition param, List<Instruction> ilList, AssemblyDefinition assembly)
        {
            var paramType = param.ParameterType;
            if (paramType.IsValueType)
            {
                ilList.Add(Instruction.Create(OpCodes.Box, paramType));
            }
            else if (paramType.IsGenericParameter)
            {
                ilList.Add(Instruction.Create(OpCodes.Box, assembly.MainModule.ImportReference(paramType)));
            }
        }

    }

}

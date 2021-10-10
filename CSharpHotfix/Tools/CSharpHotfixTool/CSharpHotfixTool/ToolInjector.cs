using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace CSharpHotfixTool
{
    public class AssemblyDefinitionHandle
    {
        private const string injectedFlag = "CSharpHotfixInjectedFlag";

        private AssemblyDefinition assemblyDef = null;
        private bool assemblyReadSymbols = true;
        private string assemblyPath = null;
        private string hotfixAssemblyPath = null;

        public AssemblyDefinitionHandle() {}

        public AssemblyDefinition GetAssemblyDefinition()
        {
            return assemblyDef;
        }

        public AssemblyDefinition ReadAssembly(string path, string hotfixPath)
        {
            assemblyPath = null;
            hotfixAssemblyPath = null;
            assemblyDef = null;
            assemblyReadSymbols = true;
            ClearReference();

            // read assembly
            AssemblyDefinition assembly = null;
            var readSymbols = true;
            try
            {
                // try read with symbols
                assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = true });
            }
            catch
            {
                // read with symbols failed, just don't read them
                ToolManager.Warning("InjectAssembly: read assembly with symbol failed: {0}", path);
                try
                {
                    readSymbols = false;
                    assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { ReadSymbols = false });
                }
                catch (Exception e)
                {
                    ToolManager.Error("InjectAssembly: read assembly failed: {0}", e);
                }
            }
            if (assembly == null)
                return null;

            // init resolver search path
            var initResolverSucc = true;
            var resolver = assembly.MainModule.AssemblyResolver as BaseAssemblyResolver;
            foreach (var searchPath in ToolManager.GetInjectSearchPaths())
            {
                try
                {
                    //UnityEngine.Debug.LogError("searchPath:" + searchPath);
                    resolver.AddSearchDirectory(searchPath);
                }
                catch (Exception e) 
                { 
                    initResolverSucc = false;
                    ToolManager.Error("InjectAssembly: init resolver failed: {0}", e);
                }
            }
            if (!initResolverSucc)
                return null;
            
            assemblyPath = path;
            hotfixAssemblyPath = hotfixPath;
            assemblyDef = assembly;
            assemblyReadSymbols = readSymbols;
            ImportReference();  // import references
            return assembly;
        }

        public void DisposeAssembly()
        {
            assemblyReadSymbols = true;
            if (assemblyDef == null)
                return;
            
            // clear symbol reader, incase lock file on windows
            if (assemblyDef.MainModule.SymbolReader != null)
            {
                assemblyDef.MainModule.SymbolReader.Dispose();
            }
            assemblyDef.Dispose();
            assemblyDef = null;
        }

        public bool IsInjected()
        {
            if (assemblyDef == null)
                return false;

            var injected = assemblyDef.MainModule.Types.Any(t => t.Name == injectedFlag);
            return injected;
        }

        public void SetIsInjected()
        {
            if (assemblyDef == null)
                return;

            var objType = assemblyDef.MainModule.ImportReference(typeof(System.Object));
            var flagType = new TypeDefinition("CSharpHotfix", injectedFlag, Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public, objType);
            assemblyDef.MainModule.Types.Add(flagType);
        }

        public void Write()
        {
            if (assemblyDef == null || assemblyPath == null)
                return;

            assemblyDef.Write(hotfixAssemblyPath, new WriterParameters { WriteSymbols = assemblyReadSymbols });
        }

        private void ImportReference()
        {
            var mgrType = assemblyDef.MainModule.GetType("CSharpHotfix.CSharpHotfixManager");
            foreach (MethodDefinition method in mgrType.Methods)
            {
                if (method.Name == "HasMethodInfo")
                    mr_HasMethodInfo = assemblyDef.MainModule.ImportReference(method);
                else if (method.Name == "MethodReturnVoidWrapper")
                    mr_MethodReturnVoidWrapper = assemblyDef.MainModule.ImportReference(method);
                else if (method.Name == "MethodReturnObjectWrapper")
                    mr_MethodReturnObjectWrapper = assemblyDef.MainModule.ImportReference(method);
            }

            var objType = typeof(System.Object);
            tr_SystemObject = assemblyDef.MainModule.ImportReference(objType);
            
            var intType = typeof(System.Int32);
            tr_SystemInt = assemblyDef.MainModule.ImportReference(intType);
        }

        private void ClearReference()
        {
            mr_HasMethodInfo = null;
            mr_MethodReturnVoidWrapper = null;
            mr_MethodReturnObjectWrapper = null;

            tr_SystemObject = null;
            tr_SystemInt = null;
        }

        public MethodReference MR_HasMethodInfo
        {
            get { return mr_HasMethodInfo; }
        }
        private MethodReference mr_HasMethodInfo;

        public MethodReference MR_MethodReturnVoidWrapper
        {
            get { return mr_MethodReturnVoidWrapper; }
        }
        private MethodReference mr_MethodReturnVoidWrapper;
        
        public MethodReference MR_MethodReturnObjectWrapper
        {
            get { return mr_MethodReturnObjectWrapper; }
        }
        private MethodReference mr_MethodReturnObjectWrapper;
        
        public TypeReference TR_SystemObject
        {
            get { return tr_SystemObject; }
        }
        private TypeReference tr_SystemObject;
        
        public TypeReference TR_SystemInt
        {
            get { return tr_SystemInt; }
        }
        private TypeReference tr_SystemInt;
    }

    public class ToolInjector 
    {
        public static void TryInject()
        {
            // inject
            ToolManager.PrepareMethodId();
            var succ = true;
            foreach (var assembly in ToolManager.GetAssembliesToInject())
            {
                if (!InjectAssembly(assembly))
                {
                    succ = false;
                    break;
                }
            }
            // CSharpHotfixManager.PrintAllMethodId();
            ToolManager.SaveMethodIdToFile();

            if (succ)
                ToolManager.Message("InjectAssembly: inject finished");
            else
                ToolManager.Message("InjectAssembly: inject failed");
        }


        public static void GenMethodId()
        {
            // inject
            ToolManager.PrepareMethodId();
            var succ = true;
            foreach (var assembly in ToolManager.GetAssembliesToInject())
            {
                if (!InjectAssembly(assembly, true))
                {
                    succ = false;
                    break;
                }
            }
            // CSharpHotfixManager.PrintAllMethodId();
            ToolManager.SaveMethodIdToFile();
            if (succ)
                ToolManager.Message("InjectAssembly: gen methoId finished");
            else
                ToolManager.Message("InjectAssembly: gen methoId failed");
        }

        
        private static bool InjectAssembly(string assemblyName, bool onlyGenMethodId = false)
        {
            var assemblyPath = ToolManager.GetAssemblyPath(assemblyName);
            ToolManager.Message("InjectAssembly: assemblyName: {0} \t{1}", assemblyName, assemblyPath);
            if (!System.IO.File.Exists(assemblyPath))
            {
                ToolManager.Warning("InjectAssembly: assembly not exist: {0}", assemblyPath);
                return true;
            }
            var assemblyPDBPath = assemblyPath.Replace(".dll", ".pdb");
            var hotfixAssemblyPath = assemblyPath.Replace(".dll", ".hotfix.dll");
            var hotfixAssemblyPDBPath = assemblyPath.Replace(".pdb", ".hotfix.pdb");

            // get method list need inject
            var typeStrList = ToolManager.GetTypesToInject(assemblyName);
            if (typeStrList == null || typeStrList.Count <= 0)
            {
                ToolManager.Warning("InjectAssembly: assembly has nothing to inject: {0}", assemblyPath);
                return true;
            }
            var typeStrDict = new HashSet<string>();
            foreach (var typeStr in typeStrList)
            {
                if (string.IsNullOrEmpty(typeStr))
                    continue;
                typeStrDict.Add(typeStr);
            }

            // read assembly
            var assemblyHandle = new AssemblyDefinitionHandle();
            var assembly = assemblyHandle.ReadAssembly(assemblyPath, hotfixAssemblyPath);
            if (assembly == null)
                return false;

            // check if injected before
            if (!onlyGenMethodId && assemblyHandle.IsInjected())
            {
                ToolManager.Message("InjectAssembly: already injected");
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
                    ToolManager.Log("InjectAssembly: cecil type: {0}", type);
                    if (!type.IsClass || !typeStrDict.Contains(type.FullName))
                        continue;

                    foreach (MethodDefinition method in type.Methods)
                    {
                        if (method.IsGetter || method.IsSetter || method.HasGenericParameters)
                            continue;

                        // example: extern methods
                        if (method.Body == null || method.Body.Instructions == null)
                        {
                            continue;
                        }

                        var signature = ToolManager.GetMethodSignature(method);
                        var methodId = ToolManager.GetMethodId(signature, true);      // true: will generate method id if not exist before

                        // do inject
                        if (!onlyGenMethodId)
                        {
                            InjectMethod(methodId, method, assemblyHandle);
                        }
                    }
                }


                if (!onlyGenMethodId)
                {
                    // mark as injected
                    assemblyHandle.SetIsInjected();

                    // modify assembly
                    assemblyHandle.Write();
                }
            }
            catch (Exception e)
            {
                succ = false;
                ToolManager.Exception("InjectAssembly: inject method failed: {0}", e);
            }
            finally
            {
                assemblyHandle.DisposeAssembly();
            }
            if (!succ)
                return false;

            // overwrite assembly
            if (!onlyGenMethodId)
            {
                var copySucc = true;
                try
                {
                    System.IO.File.Copy(hotfixAssemblyPath, assemblyPath, true);
                    System.IO.File.Copy(hotfixAssemblyPDBPath, assemblyPDBPath, true);
                }
                catch (Exception e)
                {
                    copySucc = false;
                    ToolManager.Exception("InjectAssembly: override assembly failed: {0}", e);
                }
                return copySucc;
            }
            else
            {
                return true;
            }
        }
        
        private static void InjectMethod(int methodId, MethodDefinition method, AssemblyDefinitionHandle assemblyHandle)
        {
            var assembly = assemblyHandle.GetAssemblyDefinition();
            var body = method.Body;
            var originIL = body.Instructions;
            var ilProcessor = body.GetILProcessor();
            var insertPoint = originIL[0];
            var endPoint = originIL[originIL.Count - 1];
            var ilList = new List<Instruction>();
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, methodId));
            ilList.Add(Instruction.Create(OpCodes.Call, assemblyHandle.MR_HasMethodInfo));
            ilList.Add(Instruction.Create(OpCodes.Brfalse, insertPoint));
            InjectMethodArgument(methodId, method, ilList, assemblyHandle);
            if (method.ReturnType.FullName == "System.Void")
                ilList.Add(Instruction.Create(OpCodes.Call, assemblyHandle.MR_MethodReturnVoidWrapper));
            else
                ilList.Add(Instruction.Create(OpCodes.Call, assemblyHandle.MR_MethodReturnObjectWrapper));
            if (method.ReturnType.IsValueType)  // unbox return value
            {
                var returnType = assembly.MainModule.ImportReference(method.ReturnType);
                ilList.Add(Instruction.Create(OpCodes.Unbox, returnType));

                var typeName = returnType.FullName;
                var op = OpCodes.Ldind_U1;
                if (typeName == "System.Boolean")
                    op = OpCodes.Ldind_U1;
                else if (typeName == "System.Int16")
                    op = OpCodes.Ldind_I2;
                else if (typeName == "System.UInt16")
                    op = OpCodes.Ldind_U2;
                else if (typeName == "System.Int32")
                    op = OpCodes.Ldind_I4;
                else if (typeName == "System.UInt32")
                    op = OpCodes.Ldind_U4;
                else if (typeName == "System.Int64")
                    op = OpCodes.Ldind_I8;
                else if (typeName == "System.UInt64")
                    op = OpCodes.Ldind_I8;
                else if (typeName == "System.Single")
                    op = OpCodes.Ldind_R4;
                else if (typeName == "System.Double")
                    op = OpCodes.Ldind_R8;
                else
                    ToolManager.Assert(false, "unsupported return type: " + typeName
                        + " " + typeof(float).FullName + " " + typeof(double).FullName);
                ilList.Add(Instruction.Create(op));
            }
            ilList.Add(Instruction.Create(OpCodes.Br, endPoint));

            // inject il
            for (var i = ilList.Count - 1; i >= 0; --i)
                ilProcessor.InsertBefore(originIL[0], ilList[i]);

            ToolManager.Message("InjectMethod: {0}", method.FullName);
        }

        private static void InjectMethodArgument(int methodId, MethodDefinition method, List<Instruction> ilList, AssemblyDefinitionHandle assemblyHandle)
        {
            var assembly = assemblyHandle.GetAssemblyDefinition();
            var shift = 2;  // extra: methodId, instance

            //object[] arr = new object[argumentCount + shift]
            var argumentCount = method.Parameters.Count;
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, argumentCount + shift));  
            ilList.Add(Instruction.Create(OpCodes.Newarr, assemblyHandle.TR_SystemObject));

            // methodId
            ilList.Add(Instruction.Create(OpCodes.Dup));
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 0));
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, methodId));
            ilList.Add(Instruction.Create(OpCodes.Box, assemblyHandle.TR_SystemInt));
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

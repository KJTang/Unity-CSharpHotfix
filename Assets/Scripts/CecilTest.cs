using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;


[ExecuteInEditMode]
public class CecilTest : MonoBehaviour
{

    void Start()
    {
    }

    void OnEnable()
    {
        CecilTestFunc();
    }

    void Update()
    {
        
    }

    private static string GetDLLPath()
    {
        var dllPath = Application.dataPath;
        var pos = dllPath.IndexOf("Assets");
        if (pos >= 0)
        {
            dllPath = dllPath.Remove(pos);
        }
        dllPath = dllPath + "Library/ScriptAssemblies/Assembly-CSharp.dll";
        //Debug.Log("dllPath: " + dllPath);
        return dllPath;
    }

    void CecilTestFunc()
    {
        var dllPath = GetDLLPath();
        AssemblyDefinition assembly = null;
        
        try
        {
            // read assembly
            bool readSymbols = true;
            try
            {
                //尝试读取符号
                assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadSymbols = true });
            }
            catch
            {
                //如果读取不到符号则不读
                Debug.Log("#CECIL# Warning: read " + dllPath + " with symbol fail");
                readSymbols = false;
                assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadSymbols = false });
            }

            // module
            ModuleDefinition module = assembly.MainModule;
            foreach (TypeDefinition type in module.Types) {
                Debug.Log(type.FullName);
            }

            // method
	        TypeDefinition helloWorldHelper = module.Types.Single(t => t.Name == "HelloWorldHelper");

            // try inject void method
	        MethodDefinition voidMethodToInject = helloWorldHelper.Methods.Single(m => m.Name == "VoidMethodToInject");
            InjectMethod(voidMethodToInject, assembly);

            // try inject object method
	        MethodDefinition objectMethodToInject = helloWorldHelper.Methods.Single(m => m.Name == "ObjectMethodToInject");
            InjectMethod(objectMethodToInject, assembly);

            // method
	        TypeDefinition helloWorld = module.Types.Single(t => t.Name == "HelloWorld");
            foreach (MethodDefinition method in helloWorld.Methods)
            {
                CSharpHotfix.CSharpHotfixManager.Message("#CS_HOTFIX# helloworld method: {0}", method.Name);
            }
	        MethodDefinition startMethod = helloWorld.Methods.Single(m => m.Name == "Start");
            InjectMethod(startMethod, assembly);


            // modify
            assembly.Write(dllPath + "_test.dll", new WriterParameters { WriteSymbols = readSymbols });
        }
        catch (Exception e)
        {
            Debug.LogError("#CECIL# Unhandled Exception: " + e);
        }
        finally
        {
            //清理符号读取器
            //如果不清理，在window下会锁定文件
            if (assembly != null && assembly.MainModule.SymbolReader != null)
            {
                assembly.MainModule.SymbolReader.Dispose();
            }
        }
    }

    private static void InjectMethod(MethodDefinition method, AssemblyDefinition assembly)
    {
        var hasMethodInfo = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("HasMethodInfo"));
        var voidMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("MethodReturnVoidWrapper"));
        var objMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("MethodReturnObjectWrapper"));

        var body = method.Body;
        var msIls = body.Instructions;
        var ilProcessor = body.GetILProcessor();
        var insertPoint = msIls[0];
        var ilList = new List<Instruction>();
        ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
        ilList.Add(Instruction.Create(OpCodes.Call, hasMethodInfo));
        ilList.Add(Instruction.Create(OpCodes.Brfalse, insertPoint));
        MakeArrayOfArguments(method, ilList, assembly);
        if (method.ReturnType.FullName == "System.Void")
            ilList.Add(Instruction.Create(OpCodes.Call, voidMethodInject));
        else
            ilList.Add(Instruction.Create(OpCodes.Call, objMethodInject));
        ilList.Add(Instruction.Create(OpCodes.Ret));

        // inject il
        for (var i = ilList.Count - 1; i >= 0; --i)
            ilProcessor.InsertBefore(msIls[0], ilList[i]);

        CSharpHotfix.CSharpHotfixManager.Message("InjectMethod: {0}", method.Name);
    }

    private static void MakeArrayOfArguments(MethodDefinition method, List<Instruction> ilList, AssemblyDefinition assembly)
    {
        //实例函数第一个参数值为this(当前实例对象),所以要从1开始。
        var thisShift = method.IsStatic ? 0 : 1;

        var argumentCount = method.Parameters.Count;
        if (argumentCount > 0)
        {
            //object[] arr = new object[argumentCount]
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, argumentCount));
            ilList.Add(Instruction.Create(OpCodes.Newarr, assembly.MainModule.ImportReference(typeof(object))));

            for (int i = 0; i < argumentCount; ++i) 
            {
                var parameter = method.Parameters[i];

                // value = argument[i]
                ilList.Add(Instruction.Create(OpCodes.Dup));
                ilList.Add(Instruction.Create(OpCodes.Ldc_I4, i));
                TryLoadArgument(i + thisShift, ilList);

                // box
                TryBox(parameter, ilList, assembly);

                // arr[i] = value;
                ilList.Add(Instruction.Create(OpCodes.Stelem_Ref));
            }
        }
        else
        {
            ilList.Add(Instruction.Create(OpCodes.Ldnull));
        }
    }

    private static void TryLoadArgument(int argIdx, List<Instruction> ilList)
    {
        if (argIdx < 4)
        {
            switch (argIdx)
            {
                case 0: 
                    ilList.Add(Instruction.Create(OpCodes.Ldarg_0));
                    break;
                case 1: 
                    ilList.Add(Instruction.Create(OpCodes.Ldarg_1));
                    break;
                case 2: 
                    ilList.Add(Instruction.Create(OpCodes.Ldarg_2));
                    break;
                case 3: 
                    ilList.Add(Instruction.Create(OpCodes.Ldarg_3));
                    break;
            }
        }
        else if (argIdx < 256)
        {
            ilList.Add(Instruction.Create(OpCodes.Ldarg_S, argIdx));
        }
        else
        {
            ilList.Add(Instruction.Create(OpCodes.Ldarg, argIdx));
        }
    }

    private static void TryBox(ParameterDefinition param, List<Instruction> ilList, AssemblyDefinition assembly)
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


    private static MethodReference HotfixMethodIsHotfix
    {
        get
        {
            if (hotfixMethodIsHotfix == null)
            {
                var dllPath = GetDLLPath();  // "Assembly-CSharp"
                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
                foreach (TypeDefinition t in assembly.MainModule.Types) {
                    Debug.Log(t.FullName);
                }
                var method = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("HasMethodInfo"));
                //var type = assembly.MainModule.Types.Single(t => t.Name == "CSharpHotfixManager");
                //var method = type.Methods.Single(m => m.Name == "HasMethodInfo");
                hotfixMethodIsHotfix = method;
            }
            return hotfixMethodIsHotfix;
        }
    }
    private static MethodReference hotfixMethodIsHotfix;
        
        
    public static MethodReference HotfixMethodReturnVoid
    {
        get 
        {
            if (hotfixMethodReturnVoid == null)
            {
                var dllPath = GetDLLPath();  // "Assembly-CSharp"
                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
	            TypeDefinition helloWorld = assembly.MainModule.Types.Single(t => t.Name == "HelloWorldHelper");
	            MethodReference injectMethod = helloWorld.Methods.Single(m => m.Name == "VoidMethodInjectFunc");
                hotfixMethodReturnVoid = injectMethod;
                assembly.Dispose();
            }
            return hotfixMethodReturnVoid;
        }
    }
    private static MethodReference hotfixMethodReturnVoid;
        
    public static MethodReference HotfixMethodReturnObject
    {
        get 
        {
            if (hotfixMethodReturnObject == null)
            {
                var dllPath = GetDLLPath();  // "Assembly-CSharp"
                var assembly = AssemblyDefinition.ReadAssembly(dllPath);
	            TypeDefinition helloWorld = assembly.MainModule.Types.Single(t => t.Name == "HelloWorldHelper");
	            MethodReference injectMethod = helloWorld.Methods.Single(m => m.Name == "ObjectMethodInjectFunc");
                hotfixMethodReturnObject = injectMethod;
                assembly.Dispose();
            }
            return hotfixMethodReturnObject;
        }
    }
    private static MethodReference hotfixMethodReturnObject;
}


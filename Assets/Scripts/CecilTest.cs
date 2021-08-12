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
                if (!type.IsPublic)
                    continue;
                //Debug.Log(type.FullName);
            }

            // method
	        TypeDefinition helloWorld = module.Types.Single(t => t.Name == "HelloWorldHelper");
	        MethodDefinition voidMethodToInject = helloWorld.Methods.Single(m => m.Name == "VoidMethodToInject");
	        MethodDefinition objectMethodToInject = helloWorld.Methods.Single(m => m.Name == "ObjectMethodToInject");
            
            var hasMethodInfo = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("HasMethodInfo"));
            var voidMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("MethodReturnVoidWrapper"));
            var objMethodInject = assembly.MainModule.ImportReference(typeof(CSharpHotfix.CSharpHotfixManager).GetMethod("MethodReturnObjectWrapper"));


            // try insert void method
            var body = voidMethodToInject.Body;
            var msIls = body.Instructions;
            var ilProcessor = body.GetILProcessor();
            var insertPoint = msIls[0];
            var ilList = new List<Instruction>();
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
            ilList.Add(Instruction.Create(OpCodes.Call, hasMethodInfo));
            ilList.Add(Instruction.Create(OpCodes.Brfalse, insertPoint));
            ilList.Add(Instruction.Create(OpCodes.Ldarg_0));
            if (voidMethodToInject.ReturnType.FullName == "System.Void")
                ilList.Add(Instruction.Create(OpCodes.Call, voidMethodInject));
            else
                ilList.Add(Instruction.Create(OpCodes.Call, objMethodInject));
            ilList.Add(Instruction.Create(OpCodes.Ret));
            for (var i = ilList.Count - 1; i >= 0; --i)
                ilProcessor.InsertBefore(msIls[0], ilList[i]);

            // try insert object method
            body = objectMethodToInject.Body;
            msIls = body.Instructions;
            ilProcessor = body.GetILProcessor();
            insertPoint = msIls[0];
            ilList = new List<Instruction>();
            ilList.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
            ilList.Add(Instruction.Create(OpCodes.Call, hasMethodInfo));
            ilList.Add(Instruction.Create(OpCodes.Brfalse, insertPoint));
            ilList.Add(Instruction.Create(OpCodes.Ldarg_0));
            ilList.Add(Instruction.Create(OpCodes.Ldarg_1));
            if (objectMethodToInject.ReturnType.FullName == "System.Void")
                ilList.Add(Instruction.Create(OpCodes.Call, voidMethodInject));
            else
                ilList.Add(Instruction.Create(OpCodes.Call, objMethodInject));
            ilList.Add(Instruction.Create(OpCodes.Ret));
            for (var i = ilList.Count - 1; i >= 0; --i)
                ilProcessor.InsertBefore(msIls[0], ilList[i]);

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


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

    private string GetDLLPath()
    {
        var dllPath = Application.dataPath;
        var pos = dllPath.IndexOf("Assets");
        if (pos >= 0)
        {
            dllPath = dllPath.Remove(pos);
        }
        dllPath = dllPath + "Library/ScriptAssemblies/Assembly-CSharp.dll";
        Debug.Log("dllPath: " + dllPath);
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
	        MethodDefinition method = helloWorld.Methods.Single(m => m.Name == "ShowHelloWorld");
            Debug.Log("method: " + method);

	        MethodDefinition checkMethod = helloWorld.Methods.Single(m => m.Name == "Check");

            // try insert
            var body = method.Body;
            var msIls = body.Instructions;
            var ilProcessor = body.GetILProcessor();
            ilProcessor.InsertBefore(msIls[0], Instruction.Create(OpCodes.Ret));

            // modify
            assembly.Write(dllPath + "_test.dll", new WriterParameters { WriteSymbols = readSymbols });
        }
        catch (Exception e)
        {
            Debug.LogError("#CECIL# Unhandled Exception: " + e);
            return;
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
}


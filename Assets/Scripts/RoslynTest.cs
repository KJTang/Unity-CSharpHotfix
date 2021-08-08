using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using CSharpHotfix;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class RoslynTest : MonoBehaviour
{

    void Start()
    {
        RoslynTestFunc();
    }

    void Update()
    {
        
    }

    private string GetHotfixDirPath()
    {
        var dirPath = Application.dataPath;
        var pos = dirPath.IndexOf("Assets");
        if (pos >= 0)
        {
            dirPath = dirPath.Remove(pos);
        }
        dirPath = dirPath + "HotfixCSharp";
        return dirPath;
    }


    IEnumerable<Assembly> GetAllAssemblies()
    {
        var app = AppDomain.CurrentDomain;
        var allAssemblies = app.GetAssemblies();
        return allAssemblies;
    }

    IEnumerable<Assembly> GetReferencableAssemblies()
    {
        return GetAllAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location));
    }

    void Func(string filePath, string programText)
    {
        // syntax
        SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
        Debug.Log("#ROSLYN# program: \n" + tree.ToString());


        // semantic
        var references = new List<MetadataReference>();
        var assemblies = GetReferencableAssemblies();
        foreach (var assembly in assemblies)
        {
            //Debug.Log("#ROSLYN# assemblies: " + assembly);
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        var compilation = CSharpCompilation.Create("HelloWorld")
            .AddReferences(references)
            .AddSyntaxTrees(tree);
        

        // diagnostic
        SemanticModel model = compilation.GetSemanticModel(tree);
        var diagnostics = model.GetDiagnostics();
        var hasError = false;
        for (var i = 0; i != diagnostics.Length; ++i)
        {
            var diagnostic = diagnostics[i];
            var log = "#ROSLYN# diagnostics: level: " + diagnostic.WarningLevel + " \t" + diagnostic;
            if (diagnostic.WarningLevel == 0)
            {
                hasError = true;
                Debug.LogError(log);
            }
            else
            {
                Debug.LogWarning(log);
            }
        }
        if (hasError)
        {
            Debug.LogError("#ROSLYN# error occured when load file: " + filePath);
            return;
        }


        // Use the semantic model for symbol information:
        //SymbolInfo symbolInfo = model.GetSymbolInfo("Start");
        //Debug.Log("SymbolInfo: " + symbolInfo);

        // TODO: evaluate
        //var usingCollector = new UsingCollector();
        //usingCollector.Visit(root);

        //var methodCollector = new MethodCollector();
        //methodCollector.Visit(root);

        //var method = methodCollector.Methods[0];

        
        // test
        var method = compilation.GetSymbolsWithName(x => x == "TestFunc").Single();

        // 1. get source
        var methodRef = method.DeclaringSyntaxReferences.Single();
        var methodSource =  methodRef.SyntaxTree.GetText().GetSubText(methodRef.Span).ToString();
        Debug.Log("method: " + methodRef + " \n" + methodSource);

        // 2. compile in-memory as script
        var methodCompilation = CSharpCompilation.CreateScriptCompilation("Temp")
            .AddReferences(compilation.References)
            .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(methodSource, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script)));

        using (var dll = new MemoryStream())
        {
            var eimtRet = methodCompilation.Emit(dll);
            Debug.Log("emit: " + eimtRet.Success + " \t" + dll.Length);

            for (var i = 0; i != eimtRet.Diagnostics.Length; ++i)
            {
                var diagnostic = eimtRet.Diagnostics[i];
                var log = "emit diagnostics: level: " + diagnostic.WarningLevel + " \t" + diagnostic;
                if (diagnostic.WarningLevel == 0)
                {
                    Debug.LogError(log);
                }
                else
                {
                    Debug.LogWarning(log);
                }
            }

            // 3. load compiled assembly
            var assembly = Assembly.Load(dll.ToArray(), null);
            var methodBase = assembly.GetType("Script").GetMethod(method.Name, new Type[0]);

            // 4. get il or even execute
            var il = methodBase.GetMethodBody();
            Debug.Log("il: " + il);
            methodBase.Invoke(null, null);
        }
    }


    void RoslynTestFunc()
    {
        var dirPath = GetHotfixDirPath();
        var dirInfo = new DirectoryInfo(dirPath);
        var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);
        var treeLst = new List<SyntaxTree>();
        for (var i = 0; i != files.Length; ++i)
        {
            var fileInfo = files[i];
            Debug.Log("#ROSLYN# Load File: " + fileInfo.FullName);

            using (var streamReader = fileInfo.OpenText())
            {
                var programText = streamReader.ReadToEnd();
                //Func(fileInfo.FullName, programText);

                SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
                treeLst.Add(tree);
            }
        }
        
        // semantic
        var references = new List<MetadataReference>();
        var assemblies = GetReferencableAssemblies();
        foreach (var assembly in assemblies)
        {
            //Debug.Log("#ROSLYN# assemblies: " + assembly);
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        var compilation = CSharpCompilation.Create("HelloWorld")
            .AddReferences(references)
            .AddSyntaxTrees(treeLst);

        // diagnostic
        var diagnosticErrorCnt = 0;
        foreach (var tree in treeLst)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            var diagnostics = model.GetDiagnostics();
            var hasError = false;
            for (var i = 0; i != diagnostics.Length; ++i)
            {
                var diagnostic = diagnostics[i];
                var log = "#ROSLYN# diagnostics: level: " + diagnostic.WarningLevel + " \t" + diagnostic;
                if (diagnostic.WarningLevel == 0)
                {
                    hasError = true;
                    Debug.LogError(log);
                }
                else
                {
                    Debug.LogWarning(log);
                }
            }
            if (hasError)
            {
                Debug.LogErrorFormat("#ROSLYN# error occured when load file: {0}", tree.FilePath);
                diagnosticErrorCnt += 1;
            }
        }
        if (diagnosticErrorCnt > 0)
        {
            Debug.LogErrorFormat("#ROSLYN# has {0} error in hotfix files, please fix first", diagnosticErrorCnt);
            return;
        }

    }
}

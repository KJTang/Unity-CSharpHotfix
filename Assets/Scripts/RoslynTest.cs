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

public class RoslynTest : MonoBehaviour
{

    void Start()
    {
        LoadAllFiles();
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

    void LoadAllFiles()
    {
        var dirPath = GetHotfixDirPath();
        var dirInfo = new DirectoryInfo(dirPath);
        var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);
        for (var i = 0; i != files.Length; ++i)
        {
            var fileInfo = files[i];
            Debug.Log("#ROSLYN# Load File: " + fileInfo.FullName);
            using (var streamReader = fileInfo.OpenText())
            {
                var programText = streamReader.ReadToEnd();
                Func(fileInfo.FullName, programText);
            }
        }
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
        //CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
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


        // TODO: evaluate
    }
}

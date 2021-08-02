using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        Debug.Log("#ROSLYN# Hotfix Path: " + dirPath);

        var dirInfo = new DirectoryInfo(dirPath);
        var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);
        for (var i = 0; i != files.Length; ++i)
        {
            var fileInfo = files[i];
            Debug.Log("#ROSLYN# Load File: " + fileInfo.FullName);
            using (var streamReader = fileInfo.OpenText())
            {
                var programText = streamReader.ReadToEnd();
                Func(programText);
            }
        }
    }

    void Func(string programText)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
        Debug.Log("#ROSLYN# program: \n" + tree.ToString());


        //Debug.Log($"The tree is a {root.Kind()} node.");
        //Debug.Log($"The tree has {root.Members.Count} elements in it.");
        //Debug.Log($"The tree has {root.Usings.Count} using statements. They are:");
        //foreach (UsingDirectiveSyntax element in root.Usings)
        //    Debug.Log($"\t{element.Name}");

        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
    }
}

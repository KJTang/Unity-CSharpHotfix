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

    void LoadAllFiles()
    {
        var dirPath = Application.dataPath + "/Resources/HotfixCSharp/";
        var dirInfo = new DirectoryInfo(dirPath);
        var files = dirInfo.GetFiles("*.cs.txt", SearchOption.AllDirectories);
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
        Debug.Log("#ROSLYN# program: " + tree.ToString());


        //Debug.Log($"The tree is a {root.Kind()} node.");
        //Debug.Log($"The tree has {root.Members.Count} elements in it.");
        //Debug.Log($"The tree has {root.Usings.Count} using statements. They are:");
        //foreach (UsingDirectiveSyntax element in root.Usings)
        //    Debug.Log($"\t{element.Name}");

        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
    }
}

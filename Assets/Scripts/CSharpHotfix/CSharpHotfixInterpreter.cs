using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using UnityEngine;
using UnityEditor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;

namespace CSharpHotfix
{

    class CSharpHotfixMethodCollector : CSharpSyntaxWalker
    {
        public struct MethodData
        {
            public int methodId;
            public IMethodSymbol symbol;
            public MethodDeclarationSyntax syntaxNode;

            public MethodData(int methodId, IMethodSymbol symbol, MethodDeclarationSyntax syntaxNode)
            {
                this.methodId = methodId;
                this.symbol = symbol;
                this.syntaxNode = syntaxNode;
            }
        }
        
        public ICollection<MethodData> Methods 
        { 
            get { return methods; } 
        }
        private List<MethodData> methods = new List<MethodData>();

        private SemanticModel semanticModel;

        public CSharpHotfixMethodCollector(SemanticModel model)
        {
            semanticModel = model;
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (semanticModel == null)
                return;

            var symbol = semanticModel.GetDeclaredSymbol(node) as IMethodSymbol;
            if (symbol == null)
                return;
                
            var signature = CSharpHotfixManager.GetMethodSignature(symbol);
            var methodId = CSharpHotfixManager.GetMethodId(signature);
            CSharpHotfixManager.Log("#CS_HOTFIX VisitMethodDeclaration: methodId: {0} \tsignature {1}", methodId, signature);
            if (methodId <= 0)
                return;
            methods.Add(new MethodData(methodId, symbol, node));
        }



    }

    public class CSharpHotfixInterpreter 
    {
        private static string hotfixDirPath; 
        public static string GetHotfixDirPath()
        {
            if (hotfixDirPath == null)
            {
                hotfixDirPath = Application.dataPath;
                var pos = hotfixDirPath.IndexOf("Assets");
                if (pos >= 0)
                {
                    hotfixDirPath = hotfixDirPath.Remove(pos);
                }
                hotfixDirPath = hotfixDirPath + "HotfixCSharp";
            }
            return hotfixDirPath;
        }

        private static IEnumerable<Assembly> GetAllAssemblies()
        {
            var app = AppDomain.CurrentDomain;
            var allAssemblies = app.GetAssemblies();
            return allAssemblies;
        }

        private static IEnumerable<Assembly> GetReferencableAssemblies()
        {
            return GetAllAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location));
        }
        

        private static string hotfixAssemblyDir;
        private static string hotfixDllName = "CSHotfix_Assembly.dll";

        private static string GetHotfixAssemblyPath()
        {
            if (hotfixAssemblyDir == null)
            {
                hotfixAssemblyDir = Application.dataPath;
                var pos = hotfixAssemblyDir.IndexOf("Assets");
                if (pos >= 0)
                {
                    hotfixAssemblyDir = hotfixAssemblyDir.Remove(pos);
                }
                hotfixAssemblyDir = hotfixAssemblyDir + "Library/ScriptAssemblies/";
            }
            var hotfixAssemblyPath = hotfixAssemblyDir + hotfixDllName;
            return hotfixAssemblyPath;
        }

        public static void ReloadHotfixFiles()
        {
            if (!CSharpHotfixManager.IsMethodIdFileExist())
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Interpreter: no method id file cache, please re-generate it");
                return;
            }
            CSharpHotfixManager.LoadMethodIdFromFile();
            CSharpHotfixManager.ClearMethodInfo();
            
            // load hotfix files
            var dirPath = GetHotfixDirPath();
            var dirInfo = new DirectoryInfo(dirPath);
            var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);

            // parse & compile
            var treeLst = new List<SyntaxTree>();
            var fileLst = new List<string>();
            for (var i = 0; i != files.Length; ++i)
            {
                var fileInfo = files[i];
                CSharpHotfixManager.Message("#CS_HOTFIX# Load Hotfix File: " + fileInfo.FullName);

                using (var streamReader = fileInfo.OpenText())
                {
                    var programText = streamReader.ReadToEnd();
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
                    treeLst.Add(tree);
                    fileLst.Add(fileInfo.FullName);
                }
            }
            var hotfixStream = CompileHotfix(treeLst, fileLst);
            if (hotfixStream == null)
                return;

            // modify assembly
            //hotfixStream = PostprocessAssembly(hotfixStream);

            // get assembly
            //var assembly = Assembly.Load(hotfixStream.ToArray(), null);

            // save assembly to file (used to debug it)
            using (FileStream file = new FileStream(GetHotfixAssemblyPath(), FileMode.Create, System.IO.FileAccess.Write)) 
            {
                byte[] bytes = new byte[hotfixStream.Length];
                hotfixStream.Read(bytes, 0, (int)hotfixStream.Length);
                file.Write(bytes, 0, bytes.Length);
            }

            // save methodinfo
            var hotfixAssembly = Assembly.Load(hotfixStream.GetBuffer(), null);
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var typeLst = hotfixAssembly.GetTypes();
            foreach (var type in typeLst)
            {
                var methodLst = type.GetMethods(bindingFlags);
                foreach (var methodInfo in methodLst)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(methodInfo);
                    var methodId = CSharpHotfixManager.GetMethodId(signature);
                    if (methodId <= 0) 
                        continue;

                    CSharpHotfixManager.SetMethodInfo(methodId, methodInfo);
                    CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: {0} \t{1}", methodId, signature);
                }
            }

            // close stream
            hotfixStream.Close();

            // debug: 
            //CSharpHotfixManager.PrintAllMethodInfo();
            CSharpHotfixManager.Message("#CS_HOTFIX# Hotfix Finished");
       }

        private static MemoryStream CompileHotfix(List<SyntaxTree> treeLst, List<string> fileLst)
        {
            // create CSharpCompilation
            var references = new List<MetadataReference>();
            var assemblies = GetReferencableAssemblies();
            foreach (var assembly in assemblies)
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            var compilation = CSharpCompilation.Create(hotfixDllName)
                .AddReferences(references)
                .AddSyntaxTrees(treeLst)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            ;

            // diagnostic
            var diagnosticErrorCnt = 0;
            for (var idx = 0; idx != treeLst.Count; ++idx)
            {
                var tree = treeLst[idx];
                var filePath = fileLst[idx];

                SemanticModel model = compilation.GetSemanticModel(tree);
                var diagnostics = model.GetDiagnostics();
                var hasError = false;
                for (var i = 0; i != diagnostics.Length; ++i)
                {
                    var diagnostic = diagnostics[i];
                    var log = "#CS_HOTFIX# " + filePath + diagnostic;
                    if (diagnostic.WarningLevel == 0)
                    {
                        hasError = true;
                        CSharpHotfixManager.Error(log);
                    }
                    else
                    {
                        CSharpHotfixManager.Warning(log);
                    }
                }
                if (hasError)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# error occured when load file: {0}", filePath);
                    diagnosticErrorCnt += 1;
                }
            }
            if (diagnosticErrorCnt > 0)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# has {0} error in hotfix files, please fix first", diagnosticErrorCnt);
                return null;
            }
            
            // test
            //for (var idx = 0; idx != treeLst.Count; ++idx)
            //{
            //    var tree = treeLst[idx];
            //    var filePath = fileLst[idx];

            //    SemanticModel model = compilation.GetSemanticModel(tree);
            //    var methodCollector = new CSharpHotfixMethodCollector(model);
            //    methodCollector.Visit(tree.GetRoot());
            //    foreach (var methodData in methodCollector.Methods)
            //    {
            //        var methodId = methodData.methodId;
            //        if (methodId <= 0)
            //            continue;

            //        var methodSource = tree.GetText().GetSubText(methodData.syntaxNode.Span).ToString();
            //        Debug.LogErrorFormat("test {0} \n{1}", methodId, CSharpHotfixManager.GetMethodSignature(methodId), methodSource);
                    
            //        var methodCompilation = CSharpCompilation.CreateScriptCompilation("HotfixMethod_" + methodId)
            //            .AddReferences(compilation.References)
            //            .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(methodSource, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script)));
                    
            //        using (var dll = new MemoryStream())
            //        {
            //            var eimtRet = methodCompilation.Emit(dll);
            //            Debug.Log("emit: " + eimtRet.Success + " \t" + dll.Length);

            //            for (var i = 0; i != eimtRet.Diagnostics.Length; ++i)
            //            {
            //                var diagnostic = eimtRet.Diagnostics[i];
            //                var log = "emit diagnostics: level: " + diagnostic.WarningLevel + " \t" + diagnostic;
            //                if (diagnostic.WarningLevel == 0)
            //                {
            //                    Debug.LogError(log);
            //                }
            //                else
            //                {
            //                    Debug.LogWarning(log);
            //                }
            //            }

            //            var assembly = Assembly.Load(dll.ToArray(), null);
            //        }
            //    }
            //}

            // create assembly
            MemoryStream hotfixDllStream = null;
            try
            {
                hotfixDllStream = new MemoryStream();
                var eimtRet = compilation.Emit(hotfixDllStream);
                var hasError = false;
                for (var i = 0; i != eimtRet.Diagnostics.Length; ++i)
                {
                    var diagnostic = eimtRet.Diagnostics[i];
                    var log = "#CS_HOTFIX# compile hotfix error: " + diagnostic;
                    if (diagnostic.WarningLevel == 0)
                    {
                        hasError = true;
                        CSharpHotfixManager.Error(log);
                    }
                    else
                    {
                        CSharpHotfixManager.Warning(log);
                    }
                }
                if (hasError)
                {
                    hotfixDllStream.Close();
                    hotfixDllStream = null;
                    return hotfixDllStream;
                }

                hotfixDllStream.Seek(0, SeekOrigin.Begin);
            }
            catch (Exception e)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# HotfixAssembly: emit dll failed: {0}", e);
                if (hotfixDllStream != null)
                    hotfixDllStream.Close();
                hotfixDllStream = null;
            }

            return hotfixDllStream;
        }

        private static MemoryStream PostprocessAssembly(MemoryStream assemblyStream)
        {
            var assemblyDef = AssemblyDefinition.ReadAssembly(assemblyStream, new ReaderParameters { ReadSymbols = false });
            return assemblyStream;
        }
    }

}

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

namespace CSharpHotfix
{

    class CSharpHotfixMethodCollector : CSharpSyntaxWalker
    {
        public struct MethodData
        {
            public int methodId;
            public IMethodSymbol symbol;

            public MethodData(int methodId, IMethodSymbol symbol)
            {
                this.methodId = methodId;
                this.symbol = symbol;
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
            methods.Add(new MethodData(methodId, symbol));
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

        public static void ReloadHotfixFiles()
        {
            if (!CSharpHotfixManager.IsMethodIdFileExist())
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Interpreter: no method id file cache, please re-generate it");
                return;
            }
            CSharpHotfixManager.LoadMethodIdFromFile();
            
            // load hotfix files
            var dirPath = GetHotfixDirPath();
            var dirInfo = new DirectoryInfo(dirPath);
            var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);

            // parse 
            var treeLst = new List<SyntaxTree>();
            for (var i = 0; i != files.Length; ++i)
            {
                var fileInfo = files[i];
                CSharpHotfixManager.Log("#CS_HOTFIX# Load Hotfix File: " + fileInfo.FullName);

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
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            var compilation = CSharpCompilation.Create("CSharpHotfix_Compilation")
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
                    var log = "#CS_HOTFIX# diagnostics: " + tree.FilePath + " \t" + diagnostic;
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
                    CSharpHotfixManager.Error("#CS_HOTFIX# error occured when load file: {0}", tree.FilePath);
                    diagnosticErrorCnt += 1;
                }
            }
            if (diagnosticErrorCnt > 0)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# has {0} error in hotfix files, please fix first", diagnosticErrorCnt);
                return;
            }

            // get all method
            foreach (var tree in treeLst)
            {
                var methodCollector = new CSharpHotfixMethodCollector(compilation.GetSemanticModel(tree));
                methodCollector.Visit(tree.GetCompilationUnitRoot());

                foreach (var methodData in methodCollector.Methods)
                {
                    var methodId = methodData.methodId;
                    var symbol = methodData.symbol;
                    
                    foreach (var assembly in assemblies)
                    {
                        Debug.Log("assembly.Location: " + assembly.Location);
                    }
                    var methodInfo = CompileMethod(methodId, symbol, references, tree);
                    if (methodInfo != null)
                    {
                        CSharpHotfixManager.SetMethodInfo(methodId, methodInfo);
                    }

                    var result = methodInfo != null ? "<color=green>succ</color>" : "<color=red>fail</color>";
                    CSharpHotfixManager.Message("#CS_HOTFIX# method compile {0}: {1} \t {2} \t{3}", result, methodId, symbol.Name, CSharpHotfixManager.GetMethodSignature(methodId));
                }

            }
        }

        private static MethodInfo CompileMethod(int methodId, IMethodSymbol symbol, IEnumerable<MetadataReference> references, SyntaxTree tree)
        {
            // get source
            var methodRef = symbol.DeclaringSyntaxReferences.Single();
            var methodSource =  methodRef.SyntaxTree.GetText().GetSubText(methodRef.Span).ToString();
            var methodName = symbol.Name;
            
            // compile in-memory as script
            var signature = CSharpHotfixManager.GetMethodSignature(methodId);
            var methodCompilation = CSharpCompilation.CreateScriptCompilation(methodName)
                .AddReferences(references)
                .AddSyntaxTrees(SyntaxFactory.ParseSyntaxTree(methodSource, CSharpParseOptions.Default.WithKind(SourceCodeKind.Script)));

            //foreach (var reference in references)
            //{
            //    UnityEngine.Debug.Log("Reference: " + reference);
            //}

            MethodInfo methodInfo = null;
            using (var dll = new MemoryStream())
            {
                var eimtRet = methodCompilation.Emit(dll);
                var hasError = false;
                for (var i = 0; i != eimtRet.Diagnostics.Length; ++i)
                {
                    var diagnostic = eimtRet.Diagnostics[i];
                    var log = tree.FilePath + diagnostic;
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
                    return null;
                }

                // load compiled assembly
                var assembly = Assembly.Load(dll.ToArray(), null);
                methodInfo = assembly.GetType("Script").GetMethod(methodName, new Type[0]);
            }
            return methodInfo;
        }
    }

}

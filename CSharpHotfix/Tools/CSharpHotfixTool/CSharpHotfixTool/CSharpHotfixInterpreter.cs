using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpHotfixTool
{
    public class CSharpHotfixInterpreter 
    {
        private static string hotfixDirPath; 
        public static string GetHotfixDirPath()
        {
            if (hotfixDirPath == null)
            {
                hotfixDirPath = CSharpHotfixManager.GetAppRootPath() + "CSharpHotfix/Code";
            }
            return hotfixDirPath;
        }

        private static IEnumerable<Assembly> GetReferencableAssemblies()
        {
            var assemblies = CSharpHotfixManager.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location));
            return assemblies;
        }

        private static IEnumerable<MetadataReference> GetMetadataReferences()
        {
            var references = new List<MetadataReference>();
            var assemblies = GetReferencableAssemblies();
            foreach (var assembly in assemblies)
            {
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
            }
            return references;
        }
        

        private static string hotfixAssemblyDir;
        private static string hotfixDllName = "CSHotfix_Assembly.dll";
        
        private static string GetHotfixAssemblyPath()
        {
            if (hotfixAssemblyDir == null)
            {
                hotfixAssemblyDir = CSharpHotfixManager.GetAppRootPath() + "Library/ScriptAssemblies/";
            }
            var hotfixAssemblyPath = hotfixAssemblyDir + hotfixDllName;
            return hotfixAssemblyPath;
        }

        public static void ReloadHotfixFiles()
        {
            if (!CSharpHotfixManager.IsMethodIdFileExist())
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# HotfixMethod: no method id file cache, please re-generate it");
                return;
            }
            CSharpHotfixManager.LoadMethodIdFromFile();
            CSharpHotfixManager.ClearReflectionData();

            var treeLst = new List<SyntaxTree>();
            var fileLst = new List<string>();

            // parse 
            var parseResult = ParseHotfix(treeLst, fileLst);
            if (!parseResult)
                return;

            // rewrite
            var rewriteResult = RewriteHotfix(treeLst, fileLst);
            if (!rewriteResult)
                return;

            // compile
            var hotfixStream = CompileHotfix(treeLst, fileLst);
            if (hotfixStream == null)
                return;

            // modify assembly
            //hotfixStream = PostprocessAssembly(hotfixStream);

            // get assembly
            //var assembly = Assembly.Load(hotfixStream.ToArray(), null);

            Console.WriteLine("hotfix assembly: " + GetHotfixAssemblyPath());
            // save assembly to file (used to debug it)
            using (var fileStream = new FileStream(GetHotfixAssemblyPath(), FileMode.Create, System.IO.FileAccess.Write)) 
            {
                byte[] bytes = new byte[hotfixStream.Length];
                hotfixStream.Read(bytes, 0, (int)hotfixStream.Length);
                fileStream.Write(bytes, 0, bytes.Length);
            }

            // close stream
            hotfixStream.Close();

            // debug: 
            //CSharpHotfixManager.PrintAllMethodInfo();
            CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: hotfix finished");
        }
       
        private static bool ParseHotfix(List<SyntaxTree> treeLst, List<string> fileLst)
        {
            // load hotfix files
            var dirPath = GetHotfixDirPath();
            var dirInfo = new DirectoryInfo(dirPath);
            var files = dirInfo.GetFiles("*.cs", SearchOption.AllDirectories);
            
            // parse code
            for (var i = 0; i != files.Length; ++i)
            {
                var fileInfo = files[i];
                if (fileInfo.Directory.Name == "Tests")
                    continue;

                CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: load hotfix file: " + fileInfo.FullName);
                using (var streamReader = fileInfo.OpenText())
                {
                    var programText = streamReader.ReadToEnd();
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
                    treeLst.Add(tree);
                    fileLst.Add(fileInfo.FullName);
                }
            }

            // parse test code ( always hotfix them )
            //if (CSharpHotfixTestManager.EnableTest)
            {
                for (var i = 0; i != files.Length; ++i)
                {
                    var fileInfo = files[i];
                    if (fileInfo.Directory.Name != "Tests")
                        continue;

                    //if (!CSharpHotfixTestManager.IsTestFileEnabled(fileInfo.Name.Split('.')[0]))
                    //    continue;

                    CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: load test hotfix file: " + fileInfo.FullName);
                    using (var streamReader = fileInfo.OpenText())
                    {
                        var programText = streamReader.ReadToEnd();
                        SyntaxTree tree = CSharpSyntaxTree.ParseText(programText);
                        treeLst.Add(tree);
                        fileLst.Add(fileInfo.FullName);
                    }
                }
            }

            return true;
        }

        private static void RewriteSyntaxTree(List<SyntaxTree> treeLst, CSharpSyntaxRewriter rewriter)
        {
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                var oldNode = tree.GetRoot();
                var newNode = rewriter.Visit(oldNode);
                if (oldNode != newNode)
                {
                    tree = tree.WithRootAndOptions(newNode, tree.Options);
                    treeLst[i] = tree;
                }
            }
        }

        private static bool RewriteHotfix(List<SyntaxTree> treeLst, List<string> fileLst)
        {
            // first remove code not supported yet
            RewriteSyntaxTree(treeLst, new NotSupportNewClassRewriter());
            RewriteSyntaxTree(treeLst, new NotSupportPropertyRewriter());
            RewriteSyntaxTree(treeLst, new NotSupportFieldRewriter());

            // rewrite macro definitions 
            RewriteSyntaxTree(treeLst, new UnityMacroRewriter());

            // rewrite method declaration
            var classCollector = new HotfixClassCollector();
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                classCollector.Visit(tree.GetRoot());
            }

            var methodCollector = new HotfixMethodCollector();
            foreach (var classData in classCollector.HotfixClasses)
            {
                if (classData.isNew)
                    continue;
                methodCollector.Visit(classData.syntaxNode);
            }

            var methodDeclarationRewriter = new MethodDeclarationRewriter(methodCollector.HotfixMethods);
            RewriteSyntaxTree(treeLst, methodDeclarationRewriter);

            // rewrite class declaration
            classCollector.HotfixClasses.Clear();
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                classCollector.Visit(tree.GetRoot());
            }

            var classDeclarationRewriter = new ClassDeclarationRewriter(classCollector.HotfixClasses);
            RewriteSyntaxTree(treeLst, classDeclarationRewriter);


            // rewrite hotfix class member getter
            CSharpCompilation compilation = null;
            compilation = CSharpCompilation.Create(hotfixDllName)
                .AddReferences(GetMetadataReferences())
                .AddSyntaxTrees(treeLst)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            ;
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                var semanticModel = compilation.GetSemanticModel(tree, true);

                var oldNode = tree.GetRoot();
                var memberAccessRewriter = new GetMemberRewriter(semanticModel);
                var newNode = memberAccessRewriter.Visit(oldNode);
                if (oldNode != newNode)
                {
                    tree = tree.WithRootAndOptions(newNode, tree.Options);
                    treeLst[i] = tree;
                }
            }

            // rewrite hotfix class member setter
            compilation = CSharpCompilation.Create(hotfixDllName)
                .AddReferences(GetMetadataReferences())
                .AddSyntaxTrees(treeLst)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            ;
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                var semanticModel = compilation.GetSemanticModel(tree, true);

                var oldNode = tree.GetRoot();
                var memberAccessRewriter = new SetMemberRewriter(semanticModel);
                var newNode = memberAccessRewriter.Visit(oldNode);
                if (oldNode != newNode)
                {
                    tree = tree.WithRootAndOptions(newNode, tree.Options);
                    treeLst[i] = tree;
                }
            }

            // rewrite hotfix class method invocation
            compilation = CSharpCompilation.Create(hotfixDllName)
                .AddReferences(GetMetadataReferences())
                .AddSyntaxTrees(treeLst)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            ;
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                var semanticModel = compilation.GetSemanticModel(tree, true);

                var oldNode = tree.GetRoot();
                var invokeRewriter = new InvokeMemberRewriter(semanticModel);
                var newNode = invokeRewriter.Visit(oldNode);
                if (oldNode != newNode)
                {
                    tree = tree.WithRootAndOptions(newNode, tree.Options);
                    treeLst[i] = tree;
                }
            }

            // debug: output processed syntax tree, used to examine them
            for (var i = 0; i != treeLst.Count; ++i)
            {
                var tree = treeLst[i];
                var file = fileLst[i];
                using (var fileStream = new FileStream(file + ".hotfix", FileMode.Create, System.IO.FileAccess.Write)) 
                {
                    var bytes = new UTF8Encoding(true).GetBytes(tree.ToString());
                    fileStream.Write(bytes, 0, bytes.Length);
                    treeLst[i] = CSharpSyntaxTree.ParseText(tree.ToString());
                }
            }
            return true;
        }

        private static MemoryStream CompileHotfix(List<SyntaxTree> treeLst, List<string> fileLst)
        {
            // create CSharpCompilation
            // TODO: cause when assembly cannot be unload after loaded to AppDomain, 
            // we temporily create different assembly everytime here, 
            // incase new assembly will never load cause we have one same name assembly already loaded
            var unixTimestamp = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            var assemblyName = string.Format("{0}_{1}.dll", hotfixDllName, unixTimestamp);
            var compilation = CSharpCompilation.Create(assemblyName)
                .AddReferences(GetMetadataReferences())
                .AddSyntaxTrees(treeLst)
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            ;

            // diagnostic
            var diagnosticErrorCnt = 0;
            for (var idx = 0; idx != treeLst.Count; ++idx)
            {
                var tree = treeLst[idx];
                var filePath = fileLst[idx];

                var model = compilation.GetSemanticModel(tree);
                var diagnostics = model.GetDiagnostics();
                var hasError = false;
                for (var i = 0; i != diagnostics.Length; ++i)
                {
                    var diagnostic = diagnostics[i];
                    var log = "#CS_HOTFIX# " + filePath + ".hotfix" + diagnostic;
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
                    CSharpHotfixManager.Error("#CS_HOTFIX# Hotfix Method: error occured when load file: {0}", filePath);
                    diagnosticErrorCnt += 1;
                }
            }
            if (diagnosticErrorCnt > 0)
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# Hotfix Method: has {0} error in hotfix files, please fix first", diagnosticErrorCnt);
                return null;
            }
            
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
    }

}
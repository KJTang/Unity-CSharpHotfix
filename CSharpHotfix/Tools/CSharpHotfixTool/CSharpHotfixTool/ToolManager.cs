// #define CSHOTFIX_ENABLE_LOG

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Mono.Cecil;

namespace CSharpHotfixTool
{
    public class ToolManager 
    {
        
        private static string appRootPath;
        public static string GetAppRootPath()
        {
            // now we need manully set it
            return appRootPath;
        }

        public static void SetAppRootPath(string path)
        {
            appRootPath = path;
        }

        private static string assemblyDir;
        public static string GetAssemblyPath(string assemblyName)
        {
            if (assemblyDir == null)
            {
                assemblyDir = GetAppRootPath();
                assemblyDir = assemblyDir + "Library/ScriptAssemblies/";
            }
            var assemblyPath = assemblyDir + assemblyName + ".dll";
            return assemblyPath;
        }


        /// <summary>
        /// types to be injected
        /// </summary>
        private static Dictionary<string, List<string>> typesToInject;
        public static List<string> GetTypesToInject(string assemblyName)
        {
            if (typesToInject == null)
                return null;

            List<string> result;
            if (typesToInject.TryGetValue(assemblyName, out result))
            {
                return result;
            }
            return null;
        }
        
        public static IEnumerable<string> GetAssembliesToInject()
        {
            if (typesToInject == null)
                return null;

            return typesToInject.Keys.ToList();
        }

        public static void SetTypesToInject(string injectTypes)
        {
            typesToInject = new Dictionary<string, List<string>>();
            var strLst = injectTypes.Split('|');
            foreach (var str in strLst)
            {
                if (string.IsNullOrEmpty(str))
                    continue;

                var typeStrLst = str.Split(';');
                if (typeStrLst.Length <= 1)
                    continue;

                typesToInject.Add(typeStrLst[0], typeStrLst.ToList().GetRange(1, typeStrLst.Length - 1));
            }
        }


        /// <summary>
        /// dependencies search path of assemblies which to be injected
        /// </summary>
        private static List<string> injectSearchPaths = new List<string>();

        public static IEnumerable<string> GetInjectSearchPaths()
        {
            return injectSearchPaths;
        }

        public static void SetInjectSearchPaths(string paths)
        {
            injectSearchPaths = paths.Split(';').ToList();
        }
        
        
        /// <summary>
        /// assemblies needed when hotfix
        /// </summary>
        private static Assembly[] assemblies;
        public static Assembly[] GetAssemblies()
        {
            if (assemblies == null)
            {
                assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            }
            return assemblies;
        }

        public static void LoadAssemblies(string assemblyPathStr)
        {
            var assembiesPathLst = assemblyPathStr.Split(';');
            foreach (var assemblyPath in assembiesPathLst)
            {
                if (string.IsNullOrEmpty(assemblyPath))
                    continue;
                Assembly.LoadFile(assemblyPath);
            }
        }


        /// <summary>
        /// definitions
        /// </summary>
        private static HashSet<string> macroDefinitions;
        
        public static HashSet<string> GetMacroDefinitions() 
        {
            return macroDefinitions;
        }

        public static void SetMacroDefinitions(string definitionsStr)
        {
            macroDefinitions = new HashSet<string>();

            var definitions = definitionsStr.Split(';');
            foreach (var define in definitions)
            {
                macroDefinitions.Add(define);
            }
        }

#region log
        private static FileStream fileStream;
        public static void OpenLogFile()
        {
            var path = GetAppRootPath() + "/CSharpHotfix/log.txt";
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            fileStream = new FileStream(path, FileMode.OpenOrCreate);

            Console.OutputEncoding = Encoding.UTF8;
        }

        public static void CloseLogFile()
        {
            if (fileStream != null)
                fileStream.Close();
            fileStream = null;
        }


        [Conditional("CSHOTFIX_ENABLE_LOG")]
        public static void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
            //UnityEngine.Debug.LogFormat(message, args);

            if (fileStream != null)
            {
                var bytes = new UTF8Encoding(true).GetBytes(string.Format(message, args) + "\n");
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        public static void Warning(string message, params object[] args)
        {
            //UnityEngine.Debug.LogWarningFormat(message, args);
            
            //if (fileStream != null)
            //{
            //    var bytes = new UTF8Encoding(true).GetBytes(string.Format(message, args) + "\n");
            //    fileStream.Write(bytes, 0, bytes.Length);
            //}
        }

        public static void Error(string message, params object[] args)
        {
            Console.WriteLine(message, args);
            //UnityEngine.Debug.LogErrorFormat(message, args);
            
            if (fileStream != null)
            {
                var bytes = new UTF8Encoding(true).GetBytes(string.Format(message, args) + "\n");
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        public static void Message(string message, params object[] args)
        {
            Console.WriteLine(message, args);
            //UnityEngine.Debug.LogFormat(message, args);
            
            if (fileStream != null)
            {
                var bytes = new UTF8Encoding(true).GetBytes(string.Format(message, args) + "\n");
                fileStream.Write(bytes, 0, bytes.Length);
            }
        }

        public static void Exception(string message, Exception e)
        {
            ToolManager.Error("Exception: " + string.Format(message, e.ToString()));
            ToolManager.Error("Exception End");
        }

        public static void Exception(Exception e)
        {
            ToolManager.Exception("{0}", e);
        }

        public static void Assert(bool cond, string message)
        {
            if (cond == true)
                return;

            ToolManager.Error("Assert Failed: {0}", message);
            throw new Exception("assert failed");
        }

#endregion log



#region method signature
        private static StringBuilder methodSignatureBuilder = new StringBuilder();

        /// <summary>
        /// get method signature for System.Reflection.MethodInfo
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(MethodInfo method)
        {
            methodSignatureBuilder.Length = 0;

            // fullname
            var methodName = method.Name;
            var declaringType = method.DeclaringType;
            methodSignatureBuilder.Append(declaringType.FullName);
            methodSignatureBuilder.Append(".");
            methodSignatureBuilder.Append(methodName);
            methodSignatureBuilder.Append(";");
                
            // static
            var isStatic = method.IsStatic ? "Static" : "NonStatic";
            methodSignatureBuilder.Append(isStatic);
            methodSignatureBuilder.Append(";");

            // currently we don't care about it's virtual or not
            // var isVirtual = method.IsVirtual ? "Virtual" : "NonVirtual";
            // methodSignatureBuilder.Append(isVirtual);
            // methodSignatureBuilder.Append(";");
            
            // return type
            var returnType = method.ReturnType.ToString();
            methodSignatureBuilder.Append(returnType);
            methodSignatureBuilder.Append(";");

            // generic
            var genericArgs = method.GetGenericArguments();
            methodSignatureBuilder.Append(genericArgs.Length.ToString());
            methodSignatureBuilder.Append(";");

            // parameters
            var parameters = method.GetParameters();
            foreach (var param in parameters)
            {
                methodSignatureBuilder.Append(param.ParameterType);
                methodSignatureBuilder.Append(",");
            }
            methodSignatureBuilder.Append(";");

            return methodSignatureBuilder.ToString();
        }
        
        /// <summary>
        /// get method signature for Mono.Cecil.MethodDefinition
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(MethodDefinition method)
        {
            methodSignatureBuilder.Length = 0;

            // fullname
            var methodName = method.Name;
            var namespaceName = method.DeclaringType.FullName;
            methodName = namespaceName + "." + methodName;
            methodSignatureBuilder.Append(methodName);
            methodSignatureBuilder.Append(";");

            // static
            var isStatic = method.IsStatic ? "Static" : "NonStatic";
            methodSignatureBuilder.Append(isStatic);
            methodSignatureBuilder.Append(";");

            // return type
            var returnType = method.ReturnType.FullName;
            methodSignatureBuilder.Append(returnType);
            methodSignatureBuilder.Append(";");

            // generic
            var genericArgs = method.GenericParameters;
            methodSignatureBuilder.Append(genericArgs.Count.ToString());
            methodSignatureBuilder.Append(";");

            // parameters
            var parameters = method.Parameters;
            foreach (var param in parameters)
            {
                methodSignatureBuilder.Append(param.ParameterType.FullName);
                methodSignatureBuilder.Append(",");
            }
            methodSignatureBuilder.Append(";");

            return methodSignatureBuilder.ToString();
        }


        /// <summary>
        /// get method signature for Roslyn symbol
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(IMethodSymbol method)
        {
            methodSignatureBuilder.Length = 0;

            // fullname
            var methodName = GetSymbolFullName(method);
            methodSignatureBuilder.Append(methodName);
            methodSignatureBuilder.Append(";");

            // static
            var isStatic = method.IsStatic ? "Static" : "NonStatic";
            methodSignatureBuilder.Append(isStatic);
            methodSignatureBuilder.Append(";");

            // return type
            var returnType = GetSymbolFullName(method.ReturnType);
            methodSignatureBuilder.Append(returnType);
            methodSignatureBuilder.Append(";");

            // generic
            var genericArgs = method.TypeParameters;
            methodSignatureBuilder.Append(genericArgs.Length.ToString());
            methodSignatureBuilder.Append(";");

            // parameters
            var parameters = method.Parameters;
            foreach (var param in parameters)
            {
                methodSignatureBuilder.Append(GetSymbolFullName(param.Type));
                methodSignatureBuilder.Append(",");
            }
            methodSignatureBuilder.Append(";");

            return methodSignatureBuilder.ToString();
        }

        private static string GetSymbolFullName(ISymbol symbol)
        {
            var symbolName = symbol.Name;
            var parent = symbol.ContainingSymbol;
            while (parent != null)
            {
                if (parent is INamespaceSymbol && (parent as INamespaceSymbol).IsGlobalNamespace)
                    break;
                symbolName = parent.Name + "." + symbolName;
                parent = parent.ContainingSymbol;
            }
            return symbolName;
        }


        //public static string GetMethodSignature(MethodDeclarationSyntax method)
        //{
        //    methodSignatureBuilder.Length = 0;

        //    // fullname
        //    var methodName = method.Identifier.Text;
        //    var node = method.Parent;
        //    while (node != null)
        //    {
        //        var classNode = node as ClassDeclarationSyntax;
        //        var namespaceNode = node as NamespaceDeclarationSyntax;
        //        if (classNode != null)
        //        {
        //            methodName = classNode.Identifier.Text + "." + methodName;
        //        }
        //        else if (namespaceNode != null)
        //        {
        //            methodName = namespaceNode.Name.ToString() + "." + methodName;
        //        }
        //    }
        //    methodSignatureBuilder.Append(methodName);
        //    methodSignatureBuilder.Append(";");

        //    // TODO: 
        //    return methodSignatureBuilder.ToString();
        //}
        

        public static string FixHotfixMethodSignature(string signature)
        {
            // fix class name
            if (IsHotfixClass(signature))
                signature = signature.Replace(ToolRewriter.ClassNamePostfix, "");

            // fix static
            var oldStaticStr = "NonStatic";
            if (!signature.Contains(oldStaticStr))
                oldStaticStr = "Static";

            var staticState = GetHotfixMethodStaticState(signature);
            var newStaticStr = oldStaticStr;
            if (staticState > 0)
                newStaticStr = staticState == 1 ? "Static" : "NonStatic";
            if (oldStaticStr != newStaticStr)
                signature = signature.Replace(oldStaticStr, newStaticStr);

            // fix method name
            if (staticState == 1)
                signature = signature.Replace(ToolRewriter.StaticMethodNamePostfix, "");
            else if (staticState == 2)
                signature = signature.Replace(ToolRewriter.MethodNamePostfix, "");

            // fix parameter list
            if (staticState == 2)
            {
                var strLst = signature.Split(';');
                var oldParamStr = strLst[4];
                var newParamStr = oldParamStr;
                var pos = oldParamStr.IndexOf(',');
                if (pos > 0)
                {
                    newParamStr = oldParamStr.Substring(pos + 1);
                    signature = signature.Replace(oldParamStr, newParamStr);
                }
            }


            return signature;
        }

        public static bool IsHotfixClass(string signature)
        {
            return signature.Contains(ToolRewriter.ClassNamePostfix);
        }

        public static int GetHotfixMethodStaticState(string signature)
        {
            var state = 0;  // non hotfix
            if (signature.Contains(ToolRewriter.StaticMethodNamePostfix))
                state = 1;  // static
            else if (signature.Contains(ToolRewriter.MethodNamePostfix))
                state = 2;  // non static
            return state;
        }

#endregion

#region method id cache
        private static Dictionary<string, int> methodIdDict = new Dictionary<string, int>();
        private static Dictionary<int, string> methodIdReverseDict = new Dictionary<int, string>();
        private static int methodIdCounter = 1;

        public static void PrepareMethodId()
        {
            methodIdCounter = 1;
            methodIdDict.Clear();
            methodIdReverseDict.Clear();
        }

        public static int GetMethodId(string methodSignature, bool generate = false)
        {
            int methodId = 0;
            if (!methodIdDict.TryGetValue(methodSignature, out methodId) && generate) 
            {
                methodId = methodIdCounter++;
                methodIdDict.Add(methodSignature, methodId);
                methodIdReverseDict.Add(methodId, methodSignature);
            }
            return methodId;
        }

        public static string GetMethodSignature(int methodId)
        {
            string methodSignature = null;
            if (!methodIdReverseDict.TryGetValue(methodId, out methodSignature))
            {
                return null;
            }
            return methodSignature;
        }

        public static void PrintAllMethodId()
        {
            var list = methodIdDict.ToList();
            list.Sort((a, b) => a.Value.CompareTo(b.Value));
            ToolManager.Message("PrintAllMethodId: {0}", list.Count);
            foreach (var kv in list)
            {
                ToolManager.Message("MethodId: {0} \tSignature: {1}", kv.Value, kv.Key);
            }
        }
        
        private static string methodIdFilePath;
        public static string GetMethodIdFilePath()
        {
            if (methodIdFilePath == null)
            {
                methodIdFilePath = ToolManager.GetAppRootPath() + "CSharpHotfix/methodId.txt";
            }
            return methodIdFilePath;
        }

        public static bool IsMethodIdFileExist()
        {
            return File.Exists(GetMethodIdFilePath());
        }

        public static void SaveMethodIdToFile()
        {
            var fileInfo = new FileInfo(GetMethodIdFilePath());
            using (StreamWriter sw = fileInfo.CreateText())
            {
                var list = methodIdDict.ToList();
                list.Sort((a, b) => a.Value.CompareTo(b.Value));
                foreach (var kv in list)
                {
                    sw.WriteLine(string.Format("{0} {1}", kv.Value, kv.Key));
                }
            }	
        }

        public static void LoadMethodIdFromFile()
        {
            PrepareMethodId();

            var fileInfo = new FileInfo(GetMethodIdFilePath());
            using (StreamReader sr = fileInfo.OpenText())
            {
                var str = "";
                while ((str = sr.ReadLine()) != null)
                {
                    var strLst = str.Split(' ');
                    var methodId = Int32.Parse(strLst[0]);
                    var signature = strLst[1];
                    methodIdDict.Add(signature, methodId);
                    methodIdReverseDict.Add(methodId, signature);
                }
            }	
        }
        
        public static bool IsStatic(int methodId)
        {
            var signature = GetMethodSignature(methodId);
            if (string.IsNullOrEmpty(signature))
                return false;
            var strLst = signature.Split(';');
            return strLst[1] == "Static";
        }

        public static bool IsReturnTypeVoid(int methodId)
        {
            var signature = GetMethodSignature(methodId);
            if (string.IsNullOrEmpty(signature))
                return false;
            var strLst = signature.Split(';');
            return strLst[2] == "System.Void";
        }

#endregion
        
        //private static 
        public static Type ReflectionGetMemberType(string typeName, string memberName)
        {
            Type mgrType = null;
            foreach (var assembly in ToolManager.GetAssemblies())
            {
                var type = assembly.GetType("CSharpHotfix.CSharpHotfixManager");
                if (type != null)
                {
                    mgrType = type;
                    break;
                }
            }
            if (mgrType == null)
                return null;

            var method = mgrType.GetMethod("ReflectionGetMemberType");
            var parameters = new object[2] { typeName, memberName };
            return method.Invoke(null, parameters) as Type;
        }

        

    }

}

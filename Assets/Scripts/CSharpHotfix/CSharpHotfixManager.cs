#define UNITY_EDITOR    // debug
// #define CSHOTFIX_ENABLE_LOG

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.Assertions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpHotfix
{
    public class CSharpHotfixManager 
    {
        public static bool IsHotfixEnabled
        {
            get { return isHotfixEnabled; }
            set
            {
                isHotfixEnabled = value;
            }
        }
        private static bool isHotfixEnabled = false;

#region log
        [Conditional("CSHOTFIX_ENABLE_LOG")]
        public static void Log(string message, params object[] args)
        {
            UnityEngine.Debug.LogFormat(message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            UnityEngine.Debug.LogWarningFormat(message, args);
        }

        public static void Error(string message, params object[] args)
        {
            UnityEngine.Debug.LogErrorFormat(message, args);
        }

        public static void Message(string message, params object[] args)
        {
            UnityEngine.Debug.LogFormat(message, args);
        }

#endregion log

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
            var declaringType = method.DeclaringType;
            var methodName = declaringType.FullName + "." + method.Name;
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
            CSharpHotfixManager.Message("#CS_HOTFIX# PrintAllMethodId: {0}", list.Count);
            foreach (var kv in list)
            {
                CSharpHotfixManager.Message("#CS_HOTFIX# MethodId: {0} \tSignature: {1}", kv.Value, kv.Key);
            }
        }

        private static string methodIdFilePath;
        private static string GetMethodIdFilePath()
        {
            if (methodIdFilePath == null)
            {
                methodIdFilePath = Application.dataPath;
                var pos = methodIdFilePath.IndexOf("Assets");
                if (pos >= 0)
                {
                    methodIdFilePath = methodIdFilePath.Remove(pos);
                }
                methodIdFilePath = methodIdFilePath + "HotfixCSharp/methodId.txt";
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
        

#region method wrapper

        private static Dictionary<int, MethodInfo> methodInfoDict = new Dictionary<int, MethodInfo>();

        public static MethodInfo GetMethodInfo(int methodId)
        {
            MethodInfo methodInfo;
            if (methodInfoDict.TryGetValue(methodId, out methodInfo))
            {
                return methodInfo;
            }
            return null;
        }

        public static void SetMethodInfo(int methodId, MethodInfo methodInfo)
        {
            methodInfoDict.Add(methodId, methodInfo);
        }

        public static void ClearMethodInfo()
        {
            methodInfoDict.Clear();
        }

        public static bool HasMethodInfo(int methodId)
        {
            // DEBUG: 
            return true;

            return methodInfoDict.ContainsKey(methodId);
        }

        public static void MethodReturnVoidWrapper(object[] objList)
        {
            // DEBUG: 
            Message("MethodReturnVoidWrapper: " + objList);
            if (objList != null)
            { 
                for (var i = 0; i != objList.Length; ++i)
                {
                    Message("Void Param: " + i + " \t" + objList[i]);
                }
            }
            return;

            var methodId = (System.Int32) objList[0];
            var methodInfo = GetMethodInfo(methodId);
            Assert.IsNotNull(methodInfo);

            var len = objList.Length;
            var param = new object[len - 2];
            for (var i = 0; i != len; ++i)
                param[i] = objList[i + 2];

            var instance = objList[1];
            methodInfo.Invoke(instance, param);
        }

        public static object MethodReturnObjectWrapper(object[] objList)
        {
            // DEBUG: 
            Message("MethodReturnObjectWrapper: " + objList);
            if (objList != null)
            {
                for (var i = 0; i != objList.Length; ++i)
                {
                    Message("Object Param: " + i + " \t" + objList[i]);
                }
            }
            return "_inject";


            var methodId = (System.Int32) objList[0];
            var methodInfo = GetMethodInfo(methodId);
            Assert.IsNotNull(methodInfo);

            var len = objList.Length;
            var param = new object[len - 2];
            for (var i = 0; i != len; ++i)
                param[i] = objList[i + 2];

            var instance = objList[1];
            return methodInfo.Invoke(instance, param);
        }

#endregion

    }

}

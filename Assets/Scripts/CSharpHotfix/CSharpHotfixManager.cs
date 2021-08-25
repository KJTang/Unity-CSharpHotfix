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

        
        private static Assembly[] assemblies;
        public static Assembly[] GetAssemblies()
        {
            if (assemblies == null)
            {
                assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            }
            return assemblies;
        }

#region log
        [Conditional("CSHOTFIX_ENABLE_LOG")]
        public static void Log(string message, params object[] args)
        {
            UnityEngine.Debug.LogFormat(message, args);
        }

        public static void Warning(string message, params object[] args)
        {
            //UnityEngine.Debug.LogWarningFormat(message, args);
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
                signature = signature.Replace(CSharpHotfixRewriter.ClassNamePostfix, "");

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
                signature = signature.Replace(CSharpHotfixRewriter.StaticMethodNamePostfix, "");
            else if (staticState == 2)
                signature = signature.Replace(CSharpHotfixRewriter.MethodNamePostfix, "");

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
            return signature.Contains(CSharpHotfixRewriter.ClassNamePostfix);
        }

        public static int GetHotfixMethodStaticState(string signature)
        {
            var state = 0;  // non hotfix
            if (signature.Contains(CSharpHotfixRewriter.StaticMethodNamePostfix))
                state = 1;  // static
            else if (signature.Contains(CSharpHotfixRewriter.MethodNamePostfix))
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

        public class WrapMethodInfo
        {
            public MethodInfo methodInfo;
            public int paramOffset;
        }

        private static Dictionary<int, WrapMethodInfo> methodInfoDict = new Dictionary<int, WrapMethodInfo>();

        public static WrapMethodInfo GetMethodInfo(int methodId)
        {
            // UnityEngine.Debug.LogErrorFormat("GetMethodInfo: {0} \t{1}", methodId, methodInfoDict.ContainsKey(methodId));
            WrapMethodInfo methodInfo;
            if (methodInfoDict.TryGetValue(methodId, out methodInfo))
            {
                return methodInfo;
            }
            return null;
        }

        public static void SetMethodInfo(int methodId, MethodInfo methodInfo, bool isNonStaticMethod = false)
        {
            var methodData = new WrapMethodInfo();
            methodData.methodInfo = methodInfo;
            if (!isNonStaticMethod)
                methodData.paramOffset = 2; // methodId, instance
            else
                methodData.paramOffset = 1; // methodId

            methodInfoDict.Add(methodId, methodData);
        }

        public static void ClearMethodInfo()
        {
            methodInfoDict.Clear();
        }

        public static void PrintAllMethodInfo()
        {
            CSharpHotfixManager.Message("#CS_HOTFIX# PrintAllMethodInfo: {0}", methodInfoDict.Count);
            foreach (var kv in methodInfoDict)
            {
                CSharpHotfixManager.Message("#CS_HOTFIX# methodInfo: {0} \t{1}", kv.Key, kv.Value);
            }
        }

        public static bool HasMethodInfo(int methodId)
        {
            return methodInfoDict.ContainsKey(methodId);
        }

        public static void MethodReturnVoidWrapper(object[] objList)
        {
            var methodId = (System.Int32) objList[0];
            var methodInfo = GetMethodInfo(methodId);
            Assert.IsNotNull(methodInfo);

            var offset = methodInfo.paramOffset;
            var len = objList.Length - offset;
            var param = new object[len];
            for (var i = 0; i != len; ++i)
                param[i] = objList[i + offset];

            var instance = objList[1];
            methodInfo.methodInfo.Invoke(instance, param);
        }

        public static object MethodReturnObjectWrapper(object[] objList)
        {
            var methodId = (System.Int32) objList[0];
            var methodInfo = GetMethodInfo(methodId);
            Assert.IsNotNull(methodInfo);
            
            var offset = methodInfo.paramOffset;
            var len = objList.Length - offset;
            var param = new object[len];
            for (var i = 0; i != len; ++i)
                param[i] = objList[i + offset];

            var instance = objList[1];
            return methodInfo.methodInfo.Invoke(instance, param);
        }

        #endregion


#region reflection helper
        public static object ReflectionGet(string typeName, object instance, string memberName)
        {
            var reflectionData = GetReflectionData(typeName);
            Assert.IsNotNull(reflectionData, "cannot get reflection data for typeName: " + typeName);

            // try field
            FieldInfo field;
            if (reflectionData.fields.TryGetValue(memberName, out field))
            {
                return field.GetValue(instance);
            }

            // try property
            PropertyInfo prop;
            if (reflectionData.props.TryGetValue(memberName, out prop))
            {
                return prop.GetValue(instance);
            }

            // try delegate
            MethodInfo method;
            if (reflectionData.methods.TryGetValue(memberName, out method))
            {
                return Delegate.CreateDelegate(reflectionData.type, instance, method);
            }

            Assert.IsTrue(false, "not found member in typeName: " + typeName + " \t" + memberName);
            return null;
        }

        public static void ReflectionSet(string typeName, object instance, string memberName, object value)
        {
            var reflectionData = GetReflectionData(typeName);
            Assert.IsNotNull(reflectionData, "cannot get reflection data for typeName: " + typeName);

            // try field
            FieldInfo field;
            if (reflectionData.fields.TryGetValue(memberName, out field))
            {
                field.SetValue(instance, value);
                return;
            }

            // try property
            PropertyInfo prop;
            if (reflectionData.props.TryGetValue(memberName, out prop))
            {
                prop.SetValue(instance, value);
                return;
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
        }

        public static Type ReflectionGetMemberType(string typeName, string memberName)
        {
            var reflectionData = GetReflectionData(typeName);
            Assert.IsNotNull(reflectionData, "cannot get reflection data for typeName: " + typeName);

            // try field
            FieldInfo field;
            if (reflectionData.fields.TryGetValue(memberName, out field))
            {
                return field.FieldType;
            }

            // try property
            PropertyInfo prop;
            if (reflectionData.props.TryGetValue(memberName, out prop))
            {
                return prop.PropertyType;
            }

            // try delegate
            MethodInfo method;
            if (reflectionData.methods.TryGetValue(memberName, out method))
            {
                return method.ReturnType;
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
            return null;
        }

        public class ReflectionData
        {
            public Type type;
            public Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
            public Dictionary<string, PropertyInfo> props = new Dictionary<string, PropertyInfo>();
            public Dictionary<string, MethodInfo> methods = new Dictionary<string, MethodInfo>();

            public void Print()
            {
                UnityEngine.Debug.Log("ReflectionData: " + type);

                foreach (var kv in methods)
                {
                    var method = kv.Value;
                    UnityEngine.Debug.Log("method: " + method.Name);
                }

                foreach (var kv in fields)
                {
                    var field = kv.Value;
                    UnityEngine.Debug.Log("field: " + field.Name);
                }
            
                foreach (var kv in props)
                {
                    var property = kv.Value;
                    UnityEngine.Debug.Log("property: " + property.Name);
                }
            }

        }
        private static Dictionary<Type, ReflectionData> reflectionDatas = new Dictionary<Type, ReflectionData>();

        public static ReflectionData GetReflectionData(object instance)
        {
            var type = instance.GetType();
            return GetReflectionData(type);
        }

        public static ReflectionData GetReflectionData(string typeName)
        {
            Type targetType = null;
            foreach (var assembly in CSharpHotfixManager.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    targetType = type;
                    break;
                }
            }
            return GetReflectionData(targetType);
        }

        public static ReflectionData GetReflectionData(Type type)
        {
            if (type == null)
                return null;

            ReflectionData data;
            if (reflectionDatas.TryGetValue(type, out data) && data != null)
                return data;

            data = new ReflectionData();
            reflectionDatas[type] = data;
            
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var methods = type.GetMethods(bindingFlags);
            foreach (var method in methods)
            {
                data.methods.Add(method.Name, method);
            }

            var fields = type.GetFields(bindingFlags);
            foreach (var field in fields)
            {
                data.fields.Add(field.Name, field);
            }
            
            var properties = type.GetProperties(bindingFlags);
            foreach (var property in properties)
            {
                data.props.Add(property.Name, property);
            }

            return data;
        }

        public static void ClearReflectionData()
        {
            reflectionDatas.Clear();
        }

#endregion
    }

}

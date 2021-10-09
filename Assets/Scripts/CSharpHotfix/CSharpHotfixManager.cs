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

namespace CSharpHotfix
{
    public class CSharpHotfixManager 
    {

        private static string appRootPath;
        public static string GetAppRootPath()
        {
            if (string.IsNullOrEmpty(appRootPath))
            {
                var path = Application.dataPath;
                var pos = path.IndexOf("Assets");
                if (pos >= 0)
                {
                    path = path.Remove(pos);
                }
                appRootPath = path;
            }
            return appRootPath;
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

        private static Assembly[] assemblies;
        public static Assembly[] GetAssemblies()
        {
            if (assemblies == null)
            {
                assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            }
            return assemblies;
        }

        private static HashSet<string> macroDefinitions;
        
        public static HashSet<string> GetMacroDefinitions() 
        {
            if (macroDefinitions == null)
            {
                var definitions = new HashSet<string>();

                #if UNITY_EDITOR
                    var defineSymbols = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(UnityEditor.BuildTargetGroup.Standalone);
                    var symbolLst = defineSymbols.Split(';');
                    foreach (var symbol in symbolLst)
                        if (!string.IsNullOrEmpty(symbol))
                            definitions.Add(symbol);
                #endif


                // platform
                #if UNITY_EDITOR
                    definitions.Add("UNITY_EDITOR");
                #elif UNITY_IOS
                    definitions.Add("UNITY_IOS");
                #elif UNITY_ANDROID
                    definitions.Add("UNITY_ANDROID");
                #elif UNITY_STANDALONE
                    definitions.Add("UNITY_STANDALONE");

                // TODO: add other definitions here

                #endif

                macroDefinitions = definitions;

                //Debug.LogError("def cnt: " + macroDefinitions.Count);
                //foreach (var def in macroDefinitions)
                //{
                //    Debug.LogError("def: " + def);
                //}
            }
            return macroDefinitions;
        }

        
        private static string hotfixAssemblyDir;
        private static string hotfixDllName = "CSHotfix_Assembly.dll";
        public static string GetHotfixAssemblyPath()
        {
            if (hotfixAssemblyDir == null)
            {
                hotfixAssemblyDir = GetAppRootPath() + "Library/ScriptAssemblies/";
            }
            var hotfixAssemblyPath = hotfixAssemblyDir + hotfixDllName;
            return hotfixAssemblyPath;
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
                
        // same as CSharpHotfixRewriter.cs in CSharpHotfixTool Project
        // if need change, modify them in both files
        public static readonly string InstanceParamName = "__INST__";
        public static readonly string ClassNamePostfix = "__HOTFIX_CLS";
        public static readonly string MethodNamePostfix = "__HOTFIX_MTD";
        public static readonly string StaticMethodNamePostfix = "__HOTFIX_MTD_S";


        /// <summary>
        /// fix hotfixed method signature to non-hotfixed method signature, to make them can match
        /// </summary>
        /// <param name="signature"></param>
        /// <returns></returns>
        public static string FixHotfixMethodSignature(string signature)
        {
            // fix class name
            if (IsHotfixClass(signature))
                signature = signature.Replace(ClassNamePostfix, "");

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
                signature = signature.Replace(StaticMethodNamePostfix, "");
            else if (staticState == 2)
                signature = signature.Replace(MethodNamePostfix, "");

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
            return signature.Contains(ClassNamePostfix);
        }

        public static int GetHotfixMethodStaticState(string signature)
        {
            var state = 0;  // non hotfix
            if (signature.Contains(StaticMethodNamePostfix))
                state = 1;  // static
            else if (signature.Contains(MethodNamePostfix))
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
        public static string GetMethodIdFilePath()
        {
            if (methodIdFilePath == null)
            {
                methodIdFilePath = CSharpHotfixManager.GetAppRootPath() + "CSharpHotfix/methodId.txt";
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
                return prop.GetValue(instance, null);
            }

            // try delegate
            MethodInfoWrap methodWrap;
            if (reflectionData.methods.TryGetValue(memberName, out methodWrap))
            {
                // TODO: fix overload
                var method = methodWrap.Single();
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
                prop.SetValue(instance, value, null);
                return;
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
        }

        public static object ReflectionIncrement(string typeName, object instance, string memberName, bool isInc, bool isPre)
        {
            var oldValue = ReflectionGet(typeName, instance, memberName);
            var type = oldValue.GetType();
            if (!type.IsPrimitive)
                return oldValue;

            var newValue = Activator.CreateInstance(type);
            var addValue = isInc ? 1 : -1;
            if (type == typeof(System.Int16))
            {
                newValue = (System.Int16)oldValue + addValue;
            }
            else if (type == typeof(System.UInt16))
            {
                newValue = (System.UInt16)oldValue + addValue;
            }
            else if (type == typeof(System.Int32))
            {
                newValue = (System.Int32)oldValue + addValue;
            }
            else if (type == typeof(System.UInt32))
            {
                newValue = (System.UInt32)oldValue + addValue;
            }
            else if (type == typeof(System.Int64))
            {
                newValue = (System.Int64)oldValue + (System.Int64)addValue;
            }
            else if (type == typeof(System.UInt64))
            {
                newValue = (System.UInt64)oldValue + (System.UInt64)addValue;
            }
            else if (type == typeof(float))
            {
                newValue = (float)oldValue + addValue;
            }
            else if (type == typeof(double))
            {
                newValue = (double)oldValue + addValue;
            }
            else
            {
                Assert.IsTrue(false, "ReflectionIncrement: unsupport type: " + type.Name);
            }


            ReflectionSet(typeName, instance, memberName, newValue);
            UnityEngine.Debug.LogErrorFormat("#TEST# inc: {0}.{1} {2} isPre: {3} \tvalue: {4}/{5} final: {6} {7}", typeName, memberName, isInc ? "++" : "--", isPre, newValue, oldValue, (isPre ? newValue : oldValue), type.Name);
            return isPre ? newValue : oldValue;
        }

        public static void ReflectionReturnVoidInvoke(string typeName, object instance, string memberName, params object[] parameters)
        {
            var reflectionData = GetReflectionData(typeName);
            Assert.IsNotNull(reflectionData, "cannot get reflection data for typeName: " + typeName);

            // try method
            MethodInfoWrap methodWrap;
            if (reflectionData.methods.TryGetValue(memberName, out methodWrap))
            {
                methodWrap.Invoke(instance, parameters);
                return;
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
        }
        

        public static object ReflectionReturnObjectInvoke(string typeName, object instance, string memberName, params object[] parameters)
        {
            var reflectionData = GetReflectionData(typeName);
            Assert.IsNotNull(reflectionData, "cannot get reflection data for typeName: " + typeName);

            // try method
            MethodInfoWrap methodWrap;
            if (reflectionData.methods.TryGetValue(memberName, out methodWrap))
            {
                return methodWrap.Invoke(instance, parameters);
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
            return null;
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
            MethodInfoWrap methodWrap;
            if (reflectionData.methods.TryGetValue(memberName, out methodWrap))
            {
                return methodWrap.ReturnType;
            }

            Assert.IsTrue(false, "not found member in type: " + typeName + " \t" + memberName);
            return null;
        }

        /// <summary>
        /// wrap MethodInfo, to resolve overload methods
        /// </summary>
        public class MethodInfoWrap
        {
            public string Name
            {
                get { return Single().Name; }
            }

            public Type ReturnType
            {
                get { return Single().ReturnType; }
            }

            public int Count
            {
                get { return methodLst.Count; }
            }

            private List<MethodInfo> methodLst = new List<MethodInfo>();

            public void Add(MethodInfo method)
            {
                methodLst.Add(method);
            }

            public MethodInfo Single()
            {
                return methodLst[0];
            }

            public object Invoke(object instance, object[] parameters)
            {
                if (methodLst.Count == 1)
                {
                    return methodLst[0].Invoke(instance, parameters);
                }

                var paramLength = parameters == null ? 0 : parameters.Length;
                foreach (var method in methodLst)
                {
                    var paramInfos = method.GetParameters();
                    if (paramInfos.Length < paramLength)
                        continue;

                    var matched = true;
                    for (var i = 0; i != paramInfos.Length; ++i)
                    {
                        var paramInfo = paramInfos[i];
                        if (i >= paramLength)
                        {
                            if (!paramInfo.HasDefaultValue)
                            {
                                matched = false;
                                break;
                            }
                            continue;
                        }
                        
                        var paramInst = parameters[i];
                        if (!paramInfo.ParameterType.IsAssignableFrom(paramInst.GetType()))
                        {
                            matched = false;
                            break;
                        }
                    }
                    if (!matched)
                        continue;

                    // found method, invoke it
                    return method.Invoke(instance, parameters);
                }

                Assert.IsTrue(false, "invoke method failed");
                return null;
            }
        }

        public class ReflectionData
        {
            public Type type;
            public Dictionary<string, FieldInfo> fields = new Dictionary<string, FieldInfo>();
            public Dictionary<string, PropertyInfo> props = new Dictionary<string, PropertyInfo>();
            public Dictionary<string, MethodInfoWrap> methods = new Dictionary<string, MethodInfoWrap>();

            public void Print()
            {
                UnityEngine.Debug.Log("ReflectionData: " + type);

                foreach (var kv in methods)
                {
                    var methodWrap = kv.Value;
                    UnityEngine.Debug.Log("method: " + methodWrap.Name);
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
                MethodInfoWrap methodWrap; 
                if (!data.methods.TryGetValue(method.Name, out methodWrap))
                {
                    methodWrap = new MethodInfoWrap();
                    data.methods.Add(method.Name, methodWrap);
                }
                methodWrap.Add(method);
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

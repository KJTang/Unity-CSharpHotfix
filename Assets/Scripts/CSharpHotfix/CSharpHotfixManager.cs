#define UNITY_EDITOR    // debug

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Linq;
using UnityEngine;
using Mono.Cecil;
using Mono.Cecil.Cil;

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


        private static StringBuilder methodSignatureBuilder = new StringBuilder();

        /// <summary>
        /// get method signature for System.Reflection.MethodInfo
        /// </summary>
        /// <param name="method"></param>
        /// <returns></returns>
        public static string GetMethodSignature(MethodInfo method)
        {
            methodSignatureBuilder.Length = 0;

            var declaringType = method.DeclaringType;
            var methodName = declaringType.FullName + "." + method.Name;
            methodSignatureBuilder.Append(methodName);
            methodSignatureBuilder.Append(";");
            
            var isStatic = method.IsStatic ? "Static" : "NonStatic";
            methodSignatureBuilder.Append(isStatic);
            methodSignatureBuilder.Append(";");

            // currently we don't care about it's virtual or not
            // var isVirtual = method.IsVirtual ? "Virtual" : "NonVirtual";
            // methodSignatureBuilder.Append(isVirtual);
            // methodSignatureBuilder.Append(";");
            
            var returnType = method.ReturnType.ToString();
            methodSignatureBuilder.Append(returnType);
            methodSignatureBuilder.Append(";");

            var genericArgs = method.GetGenericArguments();
            methodSignatureBuilder.Append(genericArgs.Length.ToString());
            methodSignatureBuilder.Append(";");

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
            // TODO: 
            return "";
        }


#region method id cache
        private static Dictionary<string, int> methodIdDict = new Dictionary<string, int>();
        private static Dictionary<int, string> methodIdReverseDict = new Dictionary<int, string>();
        private static int methodIdCounter = 0;

        public static void PrepareMethodId()
        {
            methodIdCounter = 0;
            methodIdDict.Clear();
            methodIdReverseDict.Clear();
        }

        public static int GetMethodId(string methodSignature, bool generate = false)
        {
            int methodId = -1;
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
            Debug.LogFormat("#CS_HOTFIX# PrintAllMethodId: {0}", list.Count);
            foreach (var kv in list)
            {
                Debug.LogFormat("MethodId: {0} \tSignature: {1}", kv.Value, kv.Key);
            }
        }

        // TODO: 
        public static void SaveMethodIdToFile()
        {
            //
        }

        public static void LoadMethodIdFromFile()
        {
            //
        }
    }

#endregion

}

using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using System.Text;
using UnityEngine;
using UnityEditor;
using Mono.Cecil;

namespace CSharpHotfix
{
    public class CSharpHotfixInterpreter 
    {
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

        public static void HotfixFromAssembly()
        {
            if (!CSharpHotfixManager.IsMethodIdFileExist())
            {
                CSharpHotfixManager.Error("#CS_HOTFIX# HotfixMethod: no method id file cache, please re-generate it");
                return;
            }
            CSharpHotfixManager.LoadMethodIdFromFile();
            CSharpHotfixManager.ClearReflectionData();

            // load assembly from file
            var hotfixStream = new MemoryStream();
            using (var fileStream = new FileStream(GetHotfixAssemblyPath(), FileMode.Open, System.IO.FileAccess.Read)) 
            {
                byte[] bytes = new byte[fileStream.Length];
                fileStream.Read(bytes, 0, (int)fileStream.Length);
                hotfixStream.Write(bytes, 0, bytes.Length);
            }

            // save methodinfo
            CSharpHotfixManager.ClearMethodInfo();
            var hotfixAssembly = Assembly.Load(hotfixStream.GetBuffer(), null);
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var typeLst = hotfixAssembly.GetTypes();
            foreach (var type in typeLst)
            {
                var methodLst = type.GetMethods(bindingFlags);
                foreach (var methodInfo in methodLst)
                {
                    var signature = CSharpHotfixManager.GetMethodSignature(methodInfo);
                    var fixedSignature = CSharpHotfixManager.FixHotfixMethodSignature(signature);
                    var methodId = CSharpHotfixManager.GetMethodId(fixedSignature);
                    if (methodId <= 0) 
                        continue;

                    var state = CSharpHotfixManager.GetHotfixMethodStaticState(signature);
                    CSharpHotfixManager.SetMethodInfo(methodId, methodInfo, state == 2);
                    CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: {0} \t{1}", methodId, fixedSignature);
                }
            }

            // close stream
            hotfixStream.Close();
            
            // debug: 
            //CSharpHotfixManager.PrintAllMethodInfo();
            CSharpHotfixManager.Message("#CS_HOTFIX# HotfixMethod: hotfix (compatible mode) finished");
        }
    }

}

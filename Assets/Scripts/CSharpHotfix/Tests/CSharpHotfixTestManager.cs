using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace CSharpHotfix {

    public class CSharpHotfixTestManager
    {
        private static readonly HashSet<string> EnabledHotfixTests = new HashSet<string>()
        {
            "Test_HotfixMethod",
            "Test_HotfixStaticMethod",
            "Test_InvokeMethod",
            "Test_InvokePrivateMethod",
            "Test_ThisExpression",
            "Test_ThisExpressionImplicit",
            "Test_AccessMember",
            "Test_AccessPriviateMember",
        };

        public static bool EnableTest = true;


        public static bool IsTestFileEnabled(string fileName)
        {
            if (!EnableTest)
                return false;
            
            if (!EnabledHotfixTests.Contains(fileName))
                return false;

            return true;
        }

        public static void RunTests()
        {
            var allTestTypes = new List<Type>();
            foreach (var assembly in CSharpHotfixManager.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.Namespace == "CSharpHotfixTest" && EnabledHotfixTests.Contains(type.Name))
                        allTestTypes.Add(type);
                }
            }

            CSharpHotfixManager.Message("#CS_HOTFIX# Run Test: total: {0}", allTestTypes.Count);
            foreach (var type in allTestTypes)
            {
                CSharpHotfixManager.Message("#CS_HOTFIX# Run Test: {0}", type.Name);
                try
                {
                    var method = type.GetMethod("Func");
                    if (method.IsStatic)
                    {
                        method.Invoke(null, null);
                    }
                    else
                    {
                        var instance = Activator.CreateInstance(type);
                        method.Invoke(instance, null);
                    }
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# Run Test failed: {0} \t{1}", type.Name, e);
                }
            }
        }
    }
}
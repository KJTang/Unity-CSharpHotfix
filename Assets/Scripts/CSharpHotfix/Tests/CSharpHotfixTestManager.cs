using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;

namespace CSharpHotfix {

    public class CSharpHotfixTestManager
    {
        public static readonly HashSet<string> EnabledHotfixTests = new HashSet<string>()
        {
            "Test_HotfixMethod",
            "Test_HotfixStaticMethod",
            "Test_InvokeMethod",
            "Test_InvokePrivateMethod",
            "Test_InvokeOverloadMethod",
            "Test_InvokeMehtodWithOutParam",
            "Test_ThisExpression",
            "Test_ThisExpressionImplicit",
            "Test_AccessMember",
            "Test_AccessPriviateMember",
            "Test_UnityMacro",
            "Test_AssignMember",
            "Test_PostIncrement",
            "Test_InnerType",
            "Test_VarTypeInference",
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

            var succCnt = 0;
            foreach (var type in allTestTypes)
            {
                CSharpHotfixManager.Message("#CS_HOTFIX# Run Test: {0}", type.Name);
                string ret;
                try
                {
                    ret = "";

                    var method = type.GetMethod("Func");
                    if (method.IsStatic)
                    {
                        ret = (string) method.Invoke(null, null);
                    }
                    else
                    {
                        var instance = Activator.CreateInstance(type);
                        ret = (string) method.Invoke(instance, null);
                    }

                    if (ret != "hotfixed")
                        throw new Exception("method is not hotfix correctly, return value: " + ret);

                    succCnt = succCnt + 1;
                    CSharpHotfixManager.Error("#CS_HOTFIX# <color=green>succ</color>: {0}", type.Name);
                }
                catch (Exception e)
                {
                    CSharpHotfixManager.Error("#CS_HOTFIX# <color=red>failed</color>: {0} \t{1}", type.Name, e);
                }
            }
            CSharpHotfixManager.Message("#CS_HOTFIX# Run Test: total: {0} \t<color=green>succ: {1}</color> \t<color=red>failed: {2}</color>", allTestTypes.Count, succCnt, allTestTypes.Count - succCnt);
        }
    }
}
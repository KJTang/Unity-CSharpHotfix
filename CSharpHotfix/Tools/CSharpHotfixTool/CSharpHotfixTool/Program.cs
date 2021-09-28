using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpHotfixTool
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length <= 2)
            {
                CSharpHotfixManager.Error("args length invalid");
                return;
            }

            // tool mode
            var mode = args[0];

            // projPath
            var projPath = args[1];
            CSharpHotfixManager.SetAppRootPath(projPath);
            CSharpHotfixManager.OpenLogFile();
            CSharpHotfixManager.Message("ProjPath: " + projPath);

            // init arguments
            RunInProtectMode(() =>
            {
                switch (mode)
                {
                    case "--inject":
                    {
                        // inject types
                        var injectTypes = args[2];
                        CSharpHotfixManager.SetTypesToInject(injectTypes);
                        CSharpHotfixManager.Message("Set Inject Types Done");
                        break;
                    }
                    case "--hotfix":
                    {
                        // assemblies
                        var assembies = args[2];
                        CSharpHotfixManager.LoadAssemblies(assembies);
                        CSharpHotfixManager.Message("Load Assemblies Done");

                        // definitions
                        var definitions = args[3];
                        CSharpHotfixManager.SetMacroDefinitions(definitions);
                        CSharpHotfixManager.Message("Macro Deinitions: " + definitions);
                        break;
                    }
                    case "--gen_method_id":
                    {
                        // inject types
                        var injectTypes = args[2];
                        CSharpHotfixManager.SetTypesToInject(injectTypes);
                        CSharpHotfixManager.Message("Set Gen MethodId Types Done");
                        break;
                    }
                    default:
                    {
                        CSharpHotfixManager.Error("invalid tool mode: {0}", mode);
                        break;
                    }
                }
            });

            // do logic
            RunInProtectMode(() =>
            {
                switch (mode)
                {
                    case "--inject":
                    {
                        CSharpHotfixInjector.TryInject();
                        break;
                    }
                    case "--hotfix":
                    {
                        CSharpHotfixInterpreter.TryHotfix();
                        break;
                    }
                    case "--gen_method_id":
                    {
                        CSharpHotfixInjector.GenMethodId();
                        break;
                    }
                    default:
                    {
                        CSharpHotfixManager.Error("invalid tool mode: {0}", mode);
                        break;
                    }
                }
            });


            CSharpHotfixManager.CloseLogFile();
        }

        static void RunInProtectMode(System.Action func)
        {
            try
            {
                func();
            }
            catch (Exception e)
            {
                CSharpHotfixManager.Error("Exception: " + e.ToString());
                CSharpHotfixManager.Error("Exception End");
            }
            finally
            {
            }
        }
    }
}

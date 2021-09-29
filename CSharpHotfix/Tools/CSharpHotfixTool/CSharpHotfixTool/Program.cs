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
                ToolManager.Error("CSharpHotfixTool: args length invalid");
                return;
            }

            // tool mode
            var mode = args[0];

            // projPath
            var projPath = args[1];
            ToolManager.SetAppRootPath(projPath);
            ToolManager.OpenLogFile();
            ToolManager.Message("CSharpHotfixTool: ProjPath: " + projPath);

            // init arguments
            RunInProtectMode(() =>
            {
                switch (mode)
                {
                    case "--inject":
                    {
                        // inject types
                        var injectTypes = args[2];
                        ToolManager.SetTypesToInject(injectTypes);
                        ToolManager.Message("Set Inject Types Done");
                            
                        // search paths
                        var searchPaths = args[3];
                        ToolManager.SetInjectSearchPaths(searchPaths);
                        ToolManager.Message("Set Inject Search Paths Done");
                        break;
                    }
                    case "--hotfix":
                    {
                        // assemblies
                        var assembies = args[2];
                        ToolManager.LoadAssemblies(assembies);
                        ToolManager.Message("Load Assemblies Done");

                        // definitions
                        var definitions = args[3];
                        ToolManager.SetMacroDefinitions(definitions);
                        ToolManager.Message("Macro Deinitions: " + definitions);
                        break;
                    }
                    case "--gen_method_id":
                    {
                        // inject types
                        var injectTypes = args[2];
                        ToolManager.SetTypesToInject(injectTypes);
                        ToolManager.Message("Set GenMethodId Types Done");
                            
                        // search paths
                        var searchPaths = args[3];
                        ToolManager.SetInjectSearchPaths(searchPaths);
                        ToolManager.Message("Set GenMethodId Search Paths Done");
                        break;
                    }
                    default:
                    {
                        ToolManager.Error("invalid tool mode: {0}", mode);
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
                            ToolInjector.TryInject();
                            break;
                        }
                    case "--hotfix":
                        {
                            ToolInterpreter.TryHotfix();
                            break;
                        }
                    case "--gen_method_id":
                        {
                            ToolInjector.GenMethodId();
                            break;
                        }
                    default:
                        {
                            ToolManager.Error("invalid tool mode: {0}", mode);
                            break;
                        }
                }
            });

            ToolManager.CloseLogFile();
        }

        static void RunInProtectMode(System.Action func)
        {
            try
            {
                func();
            }
            catch (Exception e)
            {
                ToolManager.Exception(e);
            }
            finally
            {
            }
        }
    }
}

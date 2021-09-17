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
            // args: projPath, assemblies, definitions
            var argsLen = 3;
            if (args.Length < argsLen)
            {
                CSharpHotfixManager.Error("args length invalid");
                return;
            }

            // projPath
            CSharpHotfixManager.SetAppRootPath(args[0]);
            CSharpHotfixManager.Message("ProjPath: " + args[0]);

            // assemblies
            CSharpHotfixManager.LoadAssemblies(args[1]);
            CSharpHotfixManager.Message("Load Assemblies Done");

            // definitions
            CSharpHotfixManager.SetMacroDefinitions(args[2]);
            CSharpHotfixManager.Message("Macro Deinitions: " + args[2]);

            // do hotfix
            try
            {
                CSharpHotfixInterpreter.ReloadHotfixFiles();
            }
            catch (Exception e)
            {
                CSharpHotfixManager.Error("Exception: " + e.ToString());
                CSharpHotfixManager.Error("Exception End");
            }
        }
    }
}

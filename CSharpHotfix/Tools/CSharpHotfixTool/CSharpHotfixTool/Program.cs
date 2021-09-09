using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Symbols;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
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
                Console.WriteLine("args length invalid");
                return;
            }

            // projPath
            Console.WriteLine("ProjPath: " + args[0]);
            CSharpHotfixManager.SetAppRootPath(args[0]);

            // assemblies
            CSharpHotfixManager.LoadAssemblies(args[1]);

            // definitions
            CSharpHotfixManager.SetMacroDefinitions(args[2]);

            // do hotfix
            CSharpHotfixInterpreter.ReloadHotfixFiles();
        }
    }
}

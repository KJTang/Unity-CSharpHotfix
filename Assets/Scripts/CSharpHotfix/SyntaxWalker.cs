using System.Collections.Generic;
using UnityEngine;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpHotfix
{
    class UsingCollector : CSharpSyntaxWalker
    {
        public ICollection<UsingDirectiveSyntax> Usings { get; } = new List<UsingDirectiveSyntax>();

        public override void VisitUsingDirective(UsingDirectiveSyntax node)
        {
            Debug.Log($"\tVisitUsingDirective called with {node.Name}.");
            if (node.Name.ToString() != "System" &&
                !node.Name.ToString().StartsWith("System."))
            {
                Debug.Log($"\t\tSuccess. Adding {node.Name}.");
                this.Usings.Add(node);
            }
        }
    }

    
    class MethodCollector : CSharpSyntaxWalker
    {
        public ICollection<MethodDeclarationSyntax> Methods { get; } = new List<MethodDeclarationSyntax>();

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            //
            Debug.Log($"\tVisitMethodDeclaration called with {node.Identifier}.");
            this.Methods.Add(node);

            var blockSyntax = node.Body;
            if (blockSyntax != null)
            {
                Debug.Log("#ROSLYN# statements cnt: " + blockSyntax.Statements.Count);
                foreach (var statementSyntax in blockSyntax.Statements)
                {
                    //
                }
            }
        }
    }
}
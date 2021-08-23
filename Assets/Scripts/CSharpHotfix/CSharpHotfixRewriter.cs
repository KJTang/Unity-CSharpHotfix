using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using System.Text;
using UnityEngine;
using UnityEngine.Assertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpHotfix
{
    public struct HotfixClassData
    {
        public string className;
        public string fullName;
        public bool isNew;
        public ClassDeclarationSyntax syntaxNode;
    }

    public struct HotfixMethodData
    {
        public string methodName;
        public bool isNew;
        public bool isStatic;
        public MethodDeclarationSyntax syntaxNode;

    }

    public class HotfixClassCollector : CSharpSyntaxWalker
    {
        public ICollection<HotfixClassData> HotfixClasses 
        { 
            get { return hotfixClasses; } 
        }
        private List<HotfixClassData> hotfixClasses = new List<HotfixClassData>();

        public HotfixClassCollector() {}

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            //
            Debug.LogErrorFormat("ClassDeclarationSyntax: {0} \t{1} \t{2}", 
                node.Identifier.Text, 
                CSharpHotfixRewriter.GetSyntaxNodeFullName(node), 
                CSharpHotfixRewriter.IsHotfixClassNew(CSharpHotfixRewriter.GetSyntaxNodeFullName(node))
            );

            var classData = new HotfixClassData();
            classData.className = node.Identifier.Text;
            classData.fullName = CSharpHotfixRewriter.GetSyntaxNodeFullName(node);
            classData.isNew = CSharpHotfixRewriter.IsHotfixClassNew(classData.fullName);
            classData.syntaxNode = node;
            hotfixClasses.Add(classData);
        }
    }

    public class HotfixMethodCollector : CSharpSyntaxWalker
    {
        public ICollection<HotfixMethodData> HotfixMethods 
        { 
            get { return hotfixMethods; } 
        }
        private List<HotfixMethodData> hotfixMethods = new List<HotfixMethodData>();

        public HotfixMethodCollector() {}

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            //
            Debug.LogErrorFormat("MethodDeclarationSyntax: {0} \t{1} \t{2} \t{3}", 
                node.Identifier.Text, 
                CSharpHotfixRewriter.GetSyntaxNodeFullName(node), 
                CSharpHotfixRewriter.IsHotfixMethodNew(node), 
                node.Modifiers.Any(SyntaxKind.StaticKeyword)
            );

            var methodData = new HotfixMethodData();
            methodData.methodName = CSharpHotfixRewriter.GetSyntaxNodeFullName(node);
            methodData.isNew = CSharpHotfixRewriter.IsHotfixMethodNew(node);
            methodData.isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
            methodData.syntaxNode = node;
            hotfixMethods.Add(methodData);
        }
    }


    public class ClassDeclarationRewriter : CSharpSyntaxRewriter
    {
        public ClassDeclarationRewriter() {}

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var oldName = node.Identifier.Text;
            var newName = oldName + "_Hotfix";

            var oldNameToken = node.Identifier;
            var newNameToken = SyntaxFactory.Identifier(oldNameToken.LeadingTrivia, oldNameToken.Kind(), newName, newName, oldNameToken.TrailingTrivia);

            var newNode = node.WithIdentifier(newNameToken);
            return newNode;
        }
    }

    public class MethodDeclarationRewriter : CSharpSyntaxRewriter
    {
        private Dictionary<SyntaxNode, HotfixMethodData> methodNeedRewrite = new Dictionary<SyntaxNode, HotfixMethodData>();
        public MethodDeclarationRewriter(ICollection<HotfixMethodData> methodDataLst) 
        {
            foreach (var methodData in methodDataLst)
            {
                methodNeedRewrite.Add(methodData.syntaxNode, methodData);
            }
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!methodNeedRewrite.ContainsKey(node))
                return node;

            var methodData = methodNeedRewrite[node];
            if (methodData.isStatic)
                return node;
            
            // first child node
            SyntaxNode firstNode = null;
            foreach (var child in node.ChildNodes())
            {
                firstNode = child;
                break;
            }

            // rewrite return type
            TypeSyntax oldReturnType = null;
            if (firstNode != null && firstNode is TypeSyntax)
                oldReturnType = firstNode as TypeSyntax;
            TypeSyntax newReturnType = oldReturnType;

            // rewrite modifiers
            var modifiers = node.Modifiers;
            if (modifiers.Count > 0)
            {
                // already has modifier, use trivia in it
                var oldFirstToken = modifiers[0];
                var newFirstToken = SyntaxFactory.Token(CSharpHotfixRewriter.ZeroWhitespaceTrivia, oldFirstToken.Kind(), oldFirstToken.Text, oldFirstToken.ValueText, oldFirstToken.TrailingTrivia);
                var staticToken = SyntaxFactory.Token(oldFirstToken.LeadingTrivia, SyntaxKind.StaticKeyword, CSharpHotfixRewriter.OneWhitespaceTrivia);
                modifiers = modifiers.Replace(oldFirstToken, newFirstToken);
                modifiers = modifiers.Insert(0, staticToken);
            }
            else if (firstNode != null && firstNode is TypeSyntax)
            {
                // no modifier before, use trivia from first node
                var oldTypeNode = firstNode as TypeSyntax;
                var newTypeNode = oldTypeNode.WithLeadingTrivia(CSharpHotfixRewriter.ZeroWhitespaceTrivia);
                newReturnType = newTypeNode;

                var staticToken = SyntaxFactory.Token(oldTypeNode.GetLeadingTrivia(), SyntaxKind.StaticKeyword, CSharpHotfixRewriter.OneWhitespaceTrivia);
                modifiers = modifiers.Insert(0, staticToken);
            }
            else
            {
                var staticToken = SyntaxFactory.Token(SyntaxKind.StaticKeyword);
                modifiers = modifiers.Insert(0, staticToken);
            }

            // rewrite parameters
            var parameterList = node.ParameterList;
            var paramType = SyntaxFactory.IdentifierName((node.Parent as ClassDeclarationSyntax).Identifier.Text);
            var paramName = SyntaxFactory.Identifier(
                CSharpHotfixRewriter.OneWhitespaceTrivia, 
                SyntaxKind.Parameter, 
                "__INST__", 
                "__INST__", 
                SyntaxFactory.TriviaList()
            );
            var paramSyntax = SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), paramType, paramName, null);
            var parameters = parameterList.Parameters.Insert(0, paramSyntax);
            parameterList = parameterList.WithParameters(parameters);

            // TODO: rewrite 'this'

            // record node need replace
            if (oldReturnType != null && oldReturnType != newReturnType)
                node = node.ReplaceNode(oldReturnType, newReturnType);
            var newNode = node
                .WithModifiers(modifiers)
                .WithParameterList(parameterList);

            return newNode;
        }
    }

    public class CSharpHotfixRewriter
    {
        private static Assembly[] assemblies;
        public static Assembly[] GetAssemblies()
        {
            if (assemblies == null)
            {
                assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            }
            return assemblies;
        }


        public static SyntaxTriviaList ZeroWhitespaceTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, ""));
        public static SyntaxTriviaList OneWhitespaceTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " "));


        public static string GetSyntaxNodeFullName(SyntaxNode node)
        {
            string fullName = "";
            if (node is MethodDeclarationSyntax)
                fullName = (node as MethodDeclarationSyntax).Identifier.Text;
            else if (node is ClassDeclarationSyntax)
                fullName = (node as ClassDeclarationSyntax).Identifier.Text;
            else
                return fullName;

            node = node.Parent;
            while (node != null)
            {
                var classNode = node as ClassDeclarationSyntax;
                if (classNode != null)
                {
                    fullName = classNode.Identifier.Text + "." + fullName;
                    node = node.Parent;
                    continue;
                }

                var namespaceNode = node as NamespaceDeclarationSyntax;
                if (namespaceNode != null)
                {
                    fullName = namespaceNode.Name.ToString() + "." + fullName;
                    node = node.Parent;
                    continue;
                }

                break;
            }

            return fullName;
        }        

        /// <summary>
        /// check class is defined in origin assembly or not
        /// </summary>
        /// <param name="className">class fullname</param>
        /// <returns></returns>
        public static bool IsHotfixClassNew(string className)
        {
            foreach (var assembly in GetAssemblies())
            {
                var type = assembly.GetType(className);
                if (type != null)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// check method is defined in origin assembly or not
        /// </summary>
        /// <param name="method node"></param>
        /// <returns></returns>
        public static bool IsHotfixMethodNew(MethodDeclarationSyntax node)
        {
            Type classType = null;
            var className = CSharpHotfixRewriter.GetSyntaxNodeFullName(node.Parent);
            foreach (var assembly in GetAssemblies())
            {
                var type = assembly.GetType(className);
                if (type != null)
                {
                    classType = type;
                    break;
                }
            }
            Assert.IsNotNull(classType, "invalid class name: " + className);

            var methodName = node.Identifier.Text;
            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var methods = classType.GetMethods(bindingFlags);

            MethodInfo matched = null;
            var hasOverride = false;
            foreach (var methodInfo in methods)
            {
                if (IsMethodMatched(node, methodInfo))
                {
                    hasOverride = matched != null;
                    matched = methodInfo;
                    // break;
                }
            }
            Assert.IsFalse(hasOverride, "TODO: currently not support hotfix override method");
            if (matched != null)
                return false;

            return true;
        }

        public static bool IsMethodMatched(MethodDeclarationSyntax methodNode, MethodInfo methodInfo)
        {
            // Debug.LogFormat("Check Method: name: {0} \t{1}", methodInfo.Name, methodNode.Identifier.Text);
            if (methodInfo.Name != methodNode.Identifier.Text)
                return false;

            var methodInfoParam = methodInfo.GetParameters();
            var methodNodeParam = methodNode.ParameterList.Parameters;
            // Debug.LogFormat("Check Method: length: {0} \t{1}", methodInfoParam.Length, methodNodeParam.Count);
            if (methodInfoParam.Length != methodNodeParam.Count)
                return false;

            // TODO: not support override yet
            return true;

            // for (var i = 0; i != methodInfoParam.Length; ++i)
            // {
            //     var infoParam = methodInfoParam[i].ParameterType;
            //     var nodeParam = methodNodeParam[i].Type;
            //     Debug.LogFormat("Check Method: param: {0} \t{1} \t{2}", methodInfo.Name, infoParam, nodeParam);

            //     if (IsTypeMatched(infoParam, nodeParam))
            //         return true;
            // }
            // return false;
        }

        public static bool IsTypeMatched(Type reflectionType, TypeSyntax node)
        {
            // var typeNodeName = node.ToString();
            // foreach (var assembly in GetAssemblies())
            // {
            //     var syntaxType = assembly.GetType(typeNodeName);
            //     Debug.LogFormat("Check Type: {0} \tresult: {1} \t{2}", assembly.FullName, reflectionType, syntaxType != null ? syntaxType.ToString() : "null");
            //     if (syntaxType == reflectionType)
            //         return true;
            // }
            return false;
        }
    }
}
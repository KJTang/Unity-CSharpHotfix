﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.IO;
using System;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CSharpHotfixTool
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
            ////
            //Debug.LogErrorFormat("ClassDeclarationSyntax: {0} \t{1} \tisNew: {2}", 
            //    node.Identifier.Text, 
            //    CSharpHotfixRewriter.GetSyntaxNodeFullName(node), 
            //    CSharpHotfixRewriter.IsHotfixClassNew(CSharpHotfixRewriter.GetSyntaxNodeFullName(node))
            //);

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
            ////
            //Debug.LogErrorFormat("MethodDeclarationSyntax: {0} \t{1} \tisNew: {2} \tisStatic: {3}", 
            //    node.Identifier.Text, 
            //    CSharpHotfixRewriter.GetSyntaxNodeFullName(node), 
            //    CSharpHotfixRewriter.IsHotfixMethodNew(node), 
            //    node.Modifiers.Any(SyntaxKind.StaticKeyword)
            //);

            var methodData = new HotfixMethodData();
            methodData.methodName = CSharpHotfixRewriter.GetSyntaxNodeFullName(node);
            //methodData.isNew = CSharpHotfixRewriter.IsHotfixMethodNew(node);
            methodData.isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
            methodData.syntaxNode = node;
            hotfixMethods.Add(methodData);
        }
    }

    /// <summary>
    /// 'className' -> 'className__HOTFIX'
    /// </summary>
    public class ClassDeclarationRewriter : CSharpSyntaxRewriter
    {
        private Dictionary<SyntaxNode, HotfixClassData> classNeedRewrite = new Dictionary<SyntaxNode, HotfixClassData>();
        public ClassDeclarationRewriter(ICollection<HotfixClassData> classDataLst) 
        {
            foreach (var classData in classDataLst)
            {
                classNeedRewrite.Add(classData.syntaxNode, classData);
            }
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!classNeedRewrite.ContainsKey(node))
                return node;

            var methodData = classNeedRewrite[node];
            if (methodData.isNew)
                return node;

            var oldName = node.Identifier.Text;
            var newName = oldName + CSharpHotfixRewriter.ClassNamePostfix;

            var oldNameToken = node.Identifier;
            var newNameToken = SyntaxFactory.Identifier(oldNameToken.LeadingTrivia, oldNameToken.Kind(), newName, newName, oldNameToken.TrailingTrivia);

            var newNode = node.WithIdentifier(newNameToken);
            return newNode;
        }
    }

    
    /// <summary>
    /// 'methodName' -> 'methodName__HOTFIX'
    /// non-static method -> static method
    /// </summary>
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
            {
                var oldStaticName = node.Identifier.Text;
                var newStaticName = oldStaticName + CSharpHotfixRewriter.StaticMethodNamePostfix;
                var oldStaticNameToken = node.Identifier;
                var newStaticNameToken = SyntaxFactory.Identifier(oldStaticNameToken.LeadingTrivia, oldStaticNameToken.Kind(), newStaticName, newStaticName, oldStaticNameToken.TrailingTrivia);
                return node.WithIdentifier(newStaticNameToken);
            }
            
            // first child node
            SyntaxNode firstNode = null;
            foreach (var child in node.ChildNodes())
            {
                firstNode = child;
                break;
            }

            // rewrite modifiers
            TypeSyntax newReturnType = null;
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
                CSharpHotfixRewriter.InstanceParamName, 
                CSharpHotfixRewriter.InstanceParamName, 
                SyntaxFactory.TriviaList()
            );
            var paramSyntax = SyntaxFactory.Parameter(new SyntaxList<AttributeListSyntax>(), new SyntaxTokenList(), paramType, paramName, null);
            var parameters = parameterList.Parameters.Insert(0, paramSyntax);
            parameterList = parameterList.WithParameters(parameters);

            // rewrite return type
            if (newReturnType != null)
            {
                TypeSyntax oldReturnType = null;
                if (firstNode != null && firstNode is TypeSyntax)
                    oldReturnType = firstNode as TypeSyntax;
                if (oldReturnType != null && oldReturnType != newReturnType)
                    node = node.ReplaceNode(oldReturnType, newReturnType);
            }

            // rewrite implicit 'this'
            var implicitThisRewriter = new ImplicitThisRewriter(methodData.methodName);
            node = implicitThisRewriter.Visit(node) as MethodDeclarationSyntax;

            // rewrite 'this'
            var thisRewriter = new ThisExpressionRewriter();
            node = thisRewriter.Visit(node) as MethodDeclarationSyntax;

            // rewrite method name
            var oldName = node.Identifier.Text;
            var newName = oldName + CSharpHotfixRewriter.MethodNamePostfix;
            var oldNameToken = node.Identifier;
            var newNameToken = SyntaxFactory.Identifier(oldNameToken.LeadingTrivia, oldNameToken.Kind(), newName, newName, oldNameToken.TrailingTrivia);

            // record node need replace
            var newNode = node
                .WithIdentifier(newNameToken)
                .WithModifiers(modifiers)
                .WithParameterList(parameterList);

            return newNode;
        }
    }
    

    /// <summary>
    /// find all implicit 'this', make it explicit
    /// </summary>
    public class ImplicitThisRewriter : CSharpSyntaxRewriter
    {
        private HashSet<string> methodSet = new HashSet<string>();
        private HashSet<string> fieldSet = new HashSet<string>();
        private HashSet<string> propertySet = new HashSet<string>();
        private string methodName;

        public ImplicitThisRewriter(string methodName) 
        {
            var pos = methodName.LastIndexOf('.');
            if (pos < 0)
                return;

            var className = methodName.Substring(0, pos);
            Type classType = null;
            foreach (var assembly in CSharpHotfixManager.GetAssemblies())
            {
                var type = assembly.GetType(className);
                if (type != null)
                {
                    classType = type;
                    break;
                }
            }
            //Assert.IsNotNull(classType, "invalid class name: " + className);

            var bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var methods = classType.GetMethods(bindingFlags);
            foreach (var method in methods)
            {
                methodSet.Add(method.Name);
            }

            var fields = classType.GetFields(bindingFlags);
            foreach (var field in fields)
            {
                fieldSet.Add(field.Name);
            }
            
            var properties = classType.GetProperties(bindingFlags);
            foreach (var property in properties)
            {
                propertySet.Add(property.Name);
            }

            this.methodName = methodName;
        }

        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var brothers = node.Parent.ChildNodes();
            var enumerator = brothers.GetEnumerator();
            enumerator.MoveNext();
            if (node != enumerator.Current)     // must be the first node
                return node;

            var identifierName = node.Identifier.Text;
            if (!methodSet.Contains(identifierName) && !fieldSet.Contains(identifierName) && !propertySet.Contains(identifierName))
                return node;

            var memberAccessNode = SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ThisExpression(),
                SyntaxFactory.Token(SyntaxKind.DotToken),
                SyntaxFactory.IdentifierName(node.Identifier.Text)
            ).WithTriviaFrom(node);

            return memberAccessNode;
        }
    }

    /// <summary>
    /// 'this' -> '__INST__'
    /// </summary>
    public class ThisExpressionRewriter : CSharpSyntaxRewriter
    {
        public ThisExpressionRewriter() {}

        public override SyntaxNode VisitThisExpression(ThisExpressionSyntax node)
        {
            var oldThisToken = node.Token;
            var newThisToken = SyntaxFactory.Identifier(
                oldThisToken.LeadingTrivia, 
                SyntaxKind.Parameter, 
                CSharpHotfixRewriter.InstanceParamName, 
                CSharpHotfixRewriter.InstanceParamName, 
                oldThisToken.TrailingTrivia
            );

            var newNode = SyntaxFactory.IdentifierName(newThisToken);
            return newNode;
        }
    }


    /// <summary>
    /// use reflection to rewrite hotfix class getter
    /// </summary>
    public class GetMemberRewriter : CSharpSyntaxRewriter
    {
        private SemanticModel semanticModel;

        public GetMemberRewriter(SemanticModel semanticModel) 
        {
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (semanticModel == null)
                return node;
            
            var nameNode = node.Name;
            var expressionNode = node.Expression;

            var expressionSymbol = semanticModel.GetSymbolInfo(expressionNode);
            var nameSymbol = semanticModel.GetSymbolInfo(nameNode);

            // maybe need throw error, expression node should have symbol always
            if (expressionSymbol.Symbol == null) 
                return node;

            // name symbol is accessable, no need rewrite it
            if (nameSymbol.Symbol != null)
                return node;

            // ignore method invocation
            if (node.Parent is InvocationExpressionSyntax)
                return node;
            
            // getter can only be right value
            var isLeftValue = node.Parent is AssignmentExpressionSyntax;
            if (isLeftValue)
                return node;

            // CSharpHotfix.CSharpHotfixManager.ReflectionGet
            var getExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                    SyntaxFactory.IdentifierName("CSharpHotfix"), 
                    SyntaxFactory.IdentifierName("CSharpHotfixManager")
                ), 
                SyntaxFactory.IdentifierName("ReflectionGet")
            );

            var getArgs = SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>()
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(expressionSymbol.Symbol.ToString()))))
                .Add(expressionSymbol.Symbol.Kind == SymbolKind.NamedType ? 
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)) : 
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(expressionSymbol.Symbol.Name)
                ))
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(nameNode.Identifier.Text))))
            );

            ExpressionSyntax castExpr;
            var memberType = CSharpHotfixManager.ReflectionGetMemberType(expressionSymbol.Symbol.ToString(), nameNode.Identifier.Text);
            var typeStrLst = memberType.FullName.Split('.');
            if (typeStrLst.Length <= 1)
            {
                castExpr = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(memberType.Name), SyntaxFactory.InvocationExpression(getExpr, getArgs));
            }
            else
            {
                var qualifiedName = SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName(typeStrLst[0]), SyntaxFactory.IdentifierName(typeStrLst[1]));
                for (var i = 2; i < typeStrLst.Length; ++i)
                    qualifiedName = SyntaxFactory.QualifiedName(qualifiedName, SyntaxFactory.IdentifierName(typeStrLst[i]));
                castExpr = SyntaxFactory.CastExpression(qualifiedName, SyntaxFactory.InvocationExpression(getExpr, getArgs));
            }

            castExpr = castExpr.WithTriviaFrom(node);
            return castExpr;
        }
    }

    
    /// <summary>
    /// use reflection to rewrite hotfix class setter
    /// </summary>
    public class SetMemberRewriter : CSharpSyntaxRewriter
    {
        private SemanticModel semanticModel;

        public SetMemberRewriter(SemanticModel semanticModel) 
        {
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            if (semanticModel == null)
                return node;
            
            var leftNode = node.Left as MemberAccessExpressionSyntax;
            if (leftNode == null)
                return node;

            var nameNode = leftNode.Name;
            var expressionNode = leftNode.Expression;

            var expressionSymbol = semanticModel.GetSymbolInfo(expressionNode);
            var nameSymbol = semanticModel.GetSymbolInfo(nameNode);

            // maybe need throw error, expression node should have symbol always
            if (expressionSymbol.Symbol == null) 
                return node;

            // name symbol is accessable, no need rewrite it
            if (nameSymbol.Symbol != null)
                return node;

            // CSharpHotfix.CSharpHotfixManager.ReflectionSet
            var setExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, 
                    SyntaxFactory.IdentifierName("CSharpHotfix"), 
                    SyntaxFactory.IdentifierName("CSharpHotfixManager")
                ), 
                SyntaxFactory.IdentifierName("ReflectionSet")
            );

            var setArgs = SyntaxFactory.ArgumentList(new SeparatedSyntaxList<ArgumentSyntax>()
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(expressionSymbol.Symbol.ToString()))))
                .Add(expressionSymbol.Symbol.Kind == SymbolKind.NamedType ? 
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)) : 
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(expressionSymbol.Symbol.Name)
                ))
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(nameNode.Identifier.Text))))
                .Add(SyntaxFactory.Argument(node.Right))
            );

            var newNode = SyntaxFactory.InvocationExpression(setExpr, setArgs).WithTriviaFrom(node);
            return newNode;
        }
    }

    
    /// <summary>
    /// use reflection to rewrite hotfix class method invocations
    /// </summary>
    public class InvokeMemberRewriter : CSharpSyntaxRewriter
    {
        private SemanticModel semanticModel;

        public InvokeMemberRewriter(SemanticModel semanticModel) 
        {
            this.semanticModel = semanticModel;
        }

        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (semanticModel == null)
                return node;

            // if no expr node, we cannot get enough info to invoke it
            var exprNode = node.Expression as MemberAccessExpressionSyntax;
            if (exprNode == null)
                return node;

            var exprLeft = semanticModel.GetSymbolInfo(exprNode.Expression);
            var exprRight = semanticModel.GetSymbolInfo(exprNode.Name);

            // maybe need throw error, expression node should have symbol always
            if (exprLeft.Symbol == null)
                return node;

            // name symbol is accessable, no need rewrite it
            if (exprRight.Symbol != null)
                return node;


            // CSharpHotfix.CSharpHotfixManager.ReflectionInvokeXXX
            var memberType = CSharpHotfixManager.ReflectionGetMemberType(exprLeft.Symbol.ToString(), exprNode.Name.Identifier.Text);
            var returnVoid = memberType.FullName == "System.Void"; 
            MemberAccessExpressionSyntax invokeExpr;
            if (returnVoid)
            {
                invokeExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("CSharpHotfix"),
                        SyntaxFactory.IdentifierName("CSharpHotfixManager")
                    ),
                    SyntaxFactory.IdentifierName("ReflectionReturnVoidInvoke")
                );
            }
            else
            {
                invokeExpr = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("CSharpHotfix"),
                        SyntaxFactory.IdentifierName("CSharpHotfixManager")
                    ),
                    SyntaxFactory.IdentifierName("ReflectionReturnObjectInvoke")
                );
            }

            var newArgs = new SeparatedSyntaxList<ArgumentSyntax>()
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(exprLeft.Symbol.ToString()))))
                .Add(exprLeft.Symbol.Kind == SymbolKind.NamedType ?
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)) :
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(exprLeft.Symbol.Name))
                )
                .Add(SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(exprNode.Name.Identifier.Text))))
            ;
            var argsNode = node.ArgumentList;
            var invokeArgs = argsNode.WithArguments(argsNode.Arguments.InsertRange(0, newArgs));

            // no return value or no need return value
            if (returnVoid || node.Parent is ExpressionStatementSyntax)
            {
                var newNode = SyntaxFactory.InvocationExpression(invokeExpr, invokeArgs).WithTriviaFrom(node);
                return newNode;
            }

            // cast return value
            ExpressionSyntax castExpr;
            var typeStrLst = memberType.FullName.Split('.');
            if (typeStrLst.Length <= 1)
            {
                castExpr = SyntaxFactory.CastExpression(SyntaxFactory.IdentifierName(memberType.Name), SyntaxFactory.InvocationExpression(invokeExpr, invokeArgs));
            }
            else
            {
                var qualifiedName = SyntaxFactory.QualifiedName(SyntaxFactory.IdentifierName(typeStrLst[0]), SyntaxFactory.IdentifierName(typeStrLst[1]));
                for (var i = 2; i < typeStrLst.Length; ++i)
                    qualifiedName = SyntaxFactory.QualifiedName(qualifiedName, SyntaxFactory.IdentifierName(typeStrLst[i]));
                castExpr = SyntaxFactory.CastExpression(qualifiedName, SyntaxFactory.InvocationExpression(invokeExpr, invokeArgs));
            }

            castExpr = castExpr.WithTriviaFrom(node);
            return castExpr;
        }
    }

    /// <summary>
    /// rewrite '#define' value to 'true'
    /// </summary>
    public class UnityMacroRewriter : CSharpSyntaxRewriter
    {
        private HashSet<string> macroDefinitions;

        public UnityMacroRewriter() 
        {
            macroDefinitions = CSharpHotfixManager.GetMacroDefinitions();
        }

        public override SyntaxNode Visit(SyntaxNode node)
        {
            node = base.Visit(node);
            if (node == null)
                return node;

            if (node is CompilationUnitSyntax)
                return node;

            var trivia = node.GetLeadingTrivia().FirstOrDefault(t => (t.Kind() == SyntaxKind.IfDirectiveTrivia || t.Kind() == SyntaxKind.ElifDirectiveTrivia));
            if (trivia == default(SyntaxTrivia))
                return node;

            var triviaNode = trivia.GetStructure();
            if (triviaNode == null)     // maybe error, it should be structure trivia
                return node;

            var newTriviaNode = (new MacroIdentifierRewriter(macroDefinitions)).Visit(triviaNode) as StructuredTriviaSyntax;
            var newTrivia = SyntaxFactory.Trivia(newTriviaNode);

            var triviaList = node.GetLeadingTrivia();
            triviaList = triviaList.Replace(trivia, newTrivia);
            node = node.WithLeadingTrivia(triviaList);
            return node;
        }
        
    
        public class MacroIdentifierRewriter : CSharpSyntaxRewriter
        {
            private HashSet<string> definitions;
            public MacroIdentifierRewriter(HashSet<string> definitions)
            {
                this.definitions = definitions;
            }

            public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            {
                if (!definitions.Contains(node.Identifier.Text))
                    return node;
                return SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression).WithTriviaFrom(node);
            }
        }
    }

#region remover
    public class CSharpSyntaxRemover : CSharpSyntaxRewriter
    {
        private List<SyntaxNode> nodeToRemove = new List<SyntaxNode>();

        public CSharpSyntaxRemover() {}

        public override SyntaxNode VisitCompilationUnit(CompilationUnitSyntax node)
        {
            node = base.VisitCompilationUnit(node) as CompilationUnitSyntax;
            node = node.RemoveNodes(nodeToRemove, SyntaxRemoveOptions.KeepEndOfLine);
            return node;
        }

        public void MarkRemove(SyntaxNode node)
        {
            if (node != null)
                nodeToRemove.Add(node);
        }
    }
    
    /// <summary>
    /// currently don't support hotfix field, we'll remove it
    /// </summary>
    public class NotSupportFieldRewriter : CSharpSyntaxRemover
    {
        public NotSupportFieldRewriter() {}

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            MarkRemove(node);
            return node;
        }
    }
    
    /// <summary>
    /// currently don't support hotfix property, we'll remove it
    /// </summary>
    public class NotSupportPropertyRewriter : CSharpSyntaxRemover
    {
        public NotSupportPropertyRewriter() {}

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            MarkRemove(node);
            return node;
        }
    }
    
    
    /// <summary>
    /// currently don't support hotfix new class, we'll remove it
    /// </summary>
    public class NotSupportNewClassRewriter : CSharpSyntaxRemover
    {
        public NotSupportNewClassRewriter() {}

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var fullName = CSharpHotfixRewriter.GetSyntaxNodeFullName(node);
            var isNew = CSharpHotfixRewriter.IsHotfixClassNew(fullName);
            if (isNew)
                MarkRemove(node);
            return node;
        }
    }
#endregion


    public class CSharpHotfixRewriter
    {
        public static readonly SyntaxTriviaList ZeroWhitespaceTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, ""));
        public static readonly SyntaxTriviaList OneWhitespaceTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " "));
        public static readonly SyntaxTriviaList EndOfLineTrivia = SyntaxFactory.TriviaList(SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n"));
        public static readonly string InstanceParamName = "__INST__";
        public static readonly string ClassNamePostfix = "__HOTFIX_CLS";
        public static readonly string MethodNamePostfix = "__HOTFIX_MTD";
        public static readonly string StaticMethodNamePostfix = "__HOTFIX_MTD_S";


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
            foreach (var assembly in CSharpHotfixManager.GetAssemblies())
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
            foreach (var assembly in CSharpHotfixManager.GetAssemblies())
            {
                var type = assembly.GetType(className);
                if (type != null)
                {
                    classType = type;
                    break;
                }
            }
            //Assert.IsNotNull(classType, "invalid class name: " + className);

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
            //Assert.IsFalse(hasOverride, "TODO: currently not support hotfix override method");
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
            // foreach (var assembly in CSharpHotfixManager.GetAssemblies())
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
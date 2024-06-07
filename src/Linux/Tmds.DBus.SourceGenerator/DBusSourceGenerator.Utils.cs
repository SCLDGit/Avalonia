using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;


namespace Tmds.DBus.SourceGenerator
{
    public partial class DBusSourceGenerator
    {
        private static CompilationUnitSyntax MakeCompilationUnit(NamespaceDeclarationSyntax namespaceDeclaration) =>
            CompilationUnit()
                .AddUsings(
                    UsingDirective(IdentifierName("System")),
                    UsingDirective(IdentifierName("System.Collections.Generic")),
                    UsingDirective(IdentifierName("System.Linq")),
                    UsingDirective(IdentifierName("System.Runtime.InteropServices")),
                    UsingDirective(IdentifierName("System.Threading")),
                    UsingDirective(IdentifierName("System.Threading.Tasks")),
                    UsingDirective(IdentifierName("Microsoft.Win32.SafeHandles")),
                    UsingDirective(IdentifierName("Tmds.DBus.Protocol")))
                .AddMembers(namespaceDeclaration
                    .WithLeadingTrivia(
                        TriviaList(
                            Comment("// <auto-generated/>"),
                            Trivia(PragmaWarningDirectiveTrivia(Token(SyntaxKind.DisableKeyword), true)),
                            Trivia(NullableDirectiveTrivia(Token(SyntaxKind.EnableKeyword), true)))
                    )
                ).NormalizeWhitespace();

        private static FieldDeclarationSyntax MakePrivateStringConst(string identifier, string value, TypeSyntax type) =>
            FieldDeclaration(VariableDeclaration(type)
                    .AddVariables(VariableDeclarator(identifier)
                        .WithInitializer(EqualsValueClause(MakeLiteralExpression(value)))))
                .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ConstKeyword));

        private static FieldDeclarationSyntax MakePrivateReadOnlyField(string identifier, TypeSyntax type) =>
            FieldDeclaration(VariableDeclaration(type)
                    .AddVariables(VariableDeclarator(identifier)))
                .AddModifiers(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.ReadOnlyKeyword));

        private static PropertyDeclarationSyntax MakeGetOnlyProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        private static PropertyDeclarationSyntax MakeGetSetProperty(TypeSyntax type, string identifier, params SyntaxToken[] modifiers) =>
            PropertyDeclaration(type, identifier)
                .AddModifiers(modifiers)
                .AddAccessorListAccessors(
                    AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                        .WithSemicolonToken(Token(SyntaxKind.SemicolonToken)),
                    AccessorDeclaration(SyntaxKind.SetAccessorDeclaration).WithSemicolonToken(Token(SyntaxKind.SemicolonToken)));

        private static ExpressionStatementSyntax MakeAssignmentExpressionStatement(string left, string right) =>
            ExpressionStatement(MakeAssignmentExpression(IdentifierName(left), IdentifierName(right)));

        private static AssignmentExpressionSyntax MakeAssignmentExpression(ExpressionSyntax left, ExpressionSyntax right) =>
            AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right);

        private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string right) =>
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(left), IdentifierName(right));

        private static MemberAccessExpressionSyntax MakeMemberAccessExpression(string left, string middle, string right) =>
            MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, MakeMemberAccessExpression(left, middle), IdentifierName(right));

        private static LiteralExpressionSyntax MakeLiteralExpression(string literal) => LiteralExpression(SyntaxKind.StringLiteralExpression, Literal(literal));

        private static LiteralExpressionSyntax MakeLiteralExpression(int literal) => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(literal));

        private static SyntaxToken Utf8Literal(string value) => Token(TriviaList(ElasticMarker), SyntaxKind.Utf8StringLiteralToken, SymbolDisplay.FormatLiteral(value, true) + "u8", value, TriviaList(ElasticMarker));

        private string GetOrAddWriteMethod(DBusValue dBusValue) =>
            dBusValue.DBusType switch
            {
                DBusType.Byte => "WriteByte",
                DBusType.Bool => "WriteBool",
                DBusType.Int16 => "WriteInt16",
                DBusType.UInt16 => "WriteUInt16",
                DBusType.Int32 => "WriteInt32",
                DBusType.UInt32 => "WriteUInt32",
                DBusType.Int64 => "WriteInt64",
                DBusType.UInt64 => "WriteUInt64",
                DBusType.Double => "WriteDouble",
                DBusType.String => "WriteNullableString",
                DBusType.ObjectPath => "WriteObjectPathSafe",
                DBusType.Signature => "WriteSignature",
                DBusType.UnixFd => "WriteHandle",
                DBusType.Variant => "WriteVariant",
                DBusType.Array => GetOrAddWriteArrayMethod(dBusValue),
                DBusType.DictEntry => GetOrAddWriteDictionaryMethod(dBusValue),
                DBusType.Struct => GetOrAddWriteStructMethod(dBusValue),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusValue.DBusType), dBusValue.DBusType, null)
            };

        private string GetOrAddReadMethod(DBusValue dBusValue) =>
            dBusValue.DBusType switch
            {
                DBusType.Byte => "ReadByte",
                DBusType.Bool => "ReadBool",
                DBusType.Int16 => "ReadInt16",
                DBusType.UInt16 => "ReadUInt16",
                DBusType.Int32 => "ReadInt32",
                DBusType.UInt32 => "ReadUInt32",
                DBusType.Int64 => "ReadInt64",
                DBusType.UInt64 => "ReadUInt64",
                DBusType.Double => "ReadDouble",
                DBusType.String => "ReadString",
                DBusType.ObjectPath => "ReadObjectPath",
                DBusType.Signature => "ReadSignature",
                DBusType.UnixFd => "ReadHandle<SafeFileHandle>",
                DBusType.Variant => "ReadVariantValue",
                DBusType.Array => GetOrAddReadArrayMethod(dBusValue),
                DBusType.DictEntry => GetOrAddReadDictionaryMethod(dBusValue),
                DBusType.Struct => GetOrAddReadStructMethod(dBusValue),
                _ => throw new ArgumentOutOfRangeException(nameof(dBusValue.DBusType), dBusValue.DBusType, null)
            };

        private string GetOrAddReadArrayMethod(DBusValue dBusValue)
        {
            string identifier = $"ReadArray_{SanitizeSignature(dBusValue.Type!)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(GetDotnetType(dBusValue, AccessMode.Read), identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("reader"))
                            .WithType(IdentifierName("Reader"))
                            .AddModifiers(Token(SyntaxKind.ThisKeyword), Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(GenericName("List")
                                            .AddTypeArgumentListArguments(
                                                GetDotnetType(dBusValue.InnerDBusTypes![0], AccessMode.Read)))
                                        .AddVariables(
                                            VariableDeclarator("items")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        ImplicitObjectCreationExpression())))),
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayEnd"))
                                        .AddVariables(
                                            VariableDeclarator("headersEnd")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("reader", "ReadArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", Enum.GetName(typeof(DBusType), dBusValue.InnerDBusTypes[0].DBusType)!))))))),
                                WhileStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("reader", "HasNext"))
                                        .AddArgumentListArguments(
                                            Argument(IdentifierName("headersEnd"))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("items", "Add"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValue.InnerDBusTypes[0]))))))),
                                ReturnStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("items", "ToArray")))
                            )));

            return identifier;
        }

        private string GetOrAddReadDictionaryMethod(DBusValue dBusValue)
        {
            string identifier = $"ReadDictionary_{SanitizeSignature(dBusValue.Type!)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(GetDotnetType(dBusValue, AccessMode.Read), identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("reader"))
                            .WithType(IdentifierName("Reader"))
                            .AddModifiers(Token(SyntaxKind.ThisKeyword), Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(GetDotnetType(dBusValue, AccessMode.Read))
                                        .AddVariables(
                                            VariableDeclarator("items")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        ImplicitObjectCreationExpression())))),
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayEnd"))
                                        .AddVariables(
                                            VariableDeclarator("headersEnd")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("reader", "ReadArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", "Struct"))))))),
                                WhileStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("reader", "HasNext"))
                                        .AddArgumentListArguments(
                                            Argument(IdentifierName("headersEnd"))),
                                    ExpressionStatement(
                                        InvocationExpression(
                                                MakeMemberAccessExpression("items", "Add"))
                                            .AddArgumentListArguments(
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValue.InnerDBusTypes![0])))),
                                                Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValue.InnerDBusTypes[1]))))))),
                                ReturnStatement(
                                    IdentifierName("items")))));

            return identifier;
        }

        private string GetOrAddReadStructMethod(DBusValue dBusValue)
        {
            string identifier = $"ReadStruct_{SanitizeSignature(dBusValue.Type!)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(GetDotnetType(dBusValue, AccessMode.Read), identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("reader"))
                            .WithType(IdentifierName("Reader"))
                            .AddModifiers(Token(SyntaxKind.ThisKeyword), Token(SyntaxKind.RefKeyword)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                ExpressionStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("reader", "AlignStruct"))),
                                ReturnStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("ValueTuple", "Create"))
                                        .AddArgumentListArguments(
                                            dBusValue.InnerDBusTypes!.Select(
                                                x => Argument(
                                                    InvocationExpression(
                                                        MakeMemberAccessExpression("reader", GetOrAddReadMethod(x))))).ToArray())))));

            return identifier;
        }

        private string GetOrAddReadMessageMethod(DBusValue dBusValue, bool isVariant = false) => GetOrAddReadMessageMethod(new[] { dBusValue }, isVariant);

        private string GetOrAddReadMessageMethod(IReadOnlyList<DBusValue> dBusValues, bool isVariant = false)
        {
            string identifier = $"ReadMessage_{(isVariant ? "v_" : null)}{SanitizeSignature(ParseSignature(dBusValues)!)}";
            if (_readMethodExtensions.ContainsKey(identifier))
                return identifier;

            BlockSyntax block = Block()
                .AddStatements(
                    LocalDeclarationStatement(
                        VariableDeclaration(IdentifierName("Reader"))
                            .AddVariables(
                                VariableDeclarator("reader")
                                    .WithInitializer(
                                        EqualsValueClause(
                                            InvocationExpression(
                                                MakeMemberAccessExpression("message", "GetBodyReader")))))));

            if (isVariant)
            {
                block = block.AddStatements(
                    ExpressionStatement(
                        InvocationExpression(
                                MakeMemberAccessExpression("reader", "ReadSignature"))
                            .AddArgumentListArguments(
                                Argument(MakeLiteralExpression(dBusValues[0].Type!)))));
            }

            if (dBusValues.Count == 1)
            {
                block = block.AddStatements(
                    ReturnStatement(
                        InvocationExpression(
                            MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValues[0])))));
            }
            else
            {
                for (int i = 0; i < dBusValues.Count; i++)
                {
                    block = block.AddStatements(
                        LocalDeclarationStatement(
                            VariableDeclaration(GetDotnetType(dBusValues[i], AccessMode.Read))
                                .AddVariables(
                                    VariableDeclarator($"arg{i}")
                                        .WithInitializer(
                                            EqualsValueClause(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("reader", GetOrAddReadMethod(dBusValues[i]))))))));
                }

                block = block.AddStatements(
                    ReturnStatement(
                        TupleExpression(
                            SeparatedList(
                                dBusValues.Select(static (_, i) => Argument(IdentifierName($"arg{i}")))))));
            }

            _readMethodExtensions.Add(identifier,
                MethodDeclaration(ParseReturnType(dBusValues, AccessMode.Read)!, identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("message"))
                            .WithType(IdentifierName("Message")),
                        Parameter(Identifier("_"))
                            .WithType(NullableType(PredefinedType(Token(SyntaxKind.ObjectKeyword)))))
                    .WithBody(block));

            return identifier;
        }

        private string GetOrAddWriteArrayMethod(DBusValue dBusValue)
        {
            string identifier = $"WriteArray_{SanitizeSignature(dBusValue.Type!)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(
                        PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("writer"))
                            .WithType(IdentifierName("MessageWriter"))
                            .AddModifiers(Token(SyntaxKind.ThisKeyword), Token(SyntaxKind.RefKeyword)),
                        Parameter(Identifier("values"))
                            .WithType(
                                    GetDotnetType(dBusValue, AccessMode.Write, true)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayStart"))
                                        .AddVariables(
                                            VariableDeclarator("arrayStart")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("writer", "WriteArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", Enum.GetName(typeof(DBusType), dBusValue.InnerDBusTypes![0].DBusType)!))))))),
                                IfStatement(
                                    IsPatternExpression(
                                        IdentifierName("values"),
                                        UnaryPattern(
                                            ConstantPattern(
                                                LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                    ForEachStatement(
                                        GetDotnetType(dBusValue.InnerDBusTypes[0], AccessMode.Write, true),
                                        "value",
                                        IdentifierName("values"),
                                        ExpressionStatement(
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusValue.InnerDBusTypes[0])))
                                                .AddArgumentListArguments(
                                                    Argument(IdentifierName("value")))))),
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", "WriteArrayEnd"))
                                        .AddArgumentListArguments(
                                            Argument(IdentifierName("arrayStart")))))));

            return identifier;
        }

        private string GetOrAddWriteDictionaryMethod(DBusValue dBusValue)
        {
            string identifier = $"WriteDictionary_{SanitizeSignature(dBusValue.Type!)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(
                        Token(SyntaxKind.PublicKeyword),
                        Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(
                                Identifier("writer"))
                            .WithType(
                                IdentifierName("MessageWriter"))
                            .AddModifiers(
                                Token(SyntaxKind.ThisKeyword),
                                Token(SyntaxKind.RefKeyword)),
                        Parameter(
                                Identifier("values"))
                            .WithType(
                                    GetDotnetType(dBusValue, AccessMode.Write, true)))
                    .WithBody(
                        Block()
                            .AddStatements(
                                LocalDeclarationStatement(
                                    VariableDeclaration(IdentifierName("ArrayStart"))
                                        .AddVariables(
                                            VariableDeclarator("arrayStart")
                                                .WithInitializer(
                                                    EqualsValueClause(
                                                        InvocationExpression(
                                                                MakeMemberAccessExpression("writer", "WriteArrayStart"))
                                                            .AddArgumentListArguments(
                                                                Argument(
                                                                    MakeMemberAccessExpression("DBusType", "Struct"))))))),
                                IfStatement(
                                    IsPatternExpression(
                                        IdentifierName("values"),
                                        UnaryPattern(
                                            ConstantPattern(
                                                LiteralExpression(SyntaxKind.NullLiteralExpression)))),
                                    ForEachStatement(
                                        GenericName("KeyValuePair")
                                            .AddTypeArgumentListArguments(
                                                GetDotnetType(dBusValue.InnerDBusTypes![0], AccessMode.Write),
                                                GetDotnetType(dBusValue.InnerDBusTypes![1], AccessMode.Write, true)),
                                        "value",
                                        IdentifierName("values"),
                                        Block(
                                            ExpressionStatement(
                                                InvocationExpression(
                                                    MakeMemberAccessExpression("writer", "WriteStructureStart"))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusValue.InnerDBusTypes![0])))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression("value", "Key")))),
                                            ExpressionStatement(
                                                InvocationExpression(
                                                        MakeMemberAccessExpression("writer", GetOrAddWriteMethod(dBusValue.InnerDBusTypes![1])))
                                                    .AddArgumentListArguments(
                                                        Argument(
                                                            MakeMemberAccessExpression("value", "Value"))))))),
                                ExpressionStatement(
                                    InvocationExpression(
                                            MakeMemberAccessExpression("writer", "WriteArrayEnd"))
                                        .AddArgumentListArguments(
                                            Argument(
                                                IdentifierName("arrayStart")))))));

            return identifier;
        }

        private string GetOrAddWriteStructMethod(DBusValue dBusValue)
        {
            string identifier = $"WriteStruct_{SanitizeSignature(dBusValue.Type!)}";
            if (_writeMethodExtensions.ContainsKey(identifier))
                return identifier;

            _writeMethodExtensions.Add(identifier,
                MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), identifier)
                    .AddModifiers(Token(SyntaxKind.PublicKeyword), Token(SyntaxKind.StaticKeyword))
                    .AddParameterListParameters(
                        Parameter(Identifier("writer"))
                            .WithType(IdentifierName("MessageWriter"))
                            .AddModifiers(Token(SyntaxKind.ThisKeyword), Token(SyntaxKind.RefKeyword)),
                        Parameter(Identifier("value"))
                            .WithType(GetDotnetType(dBusValue, AccessMode.Write, true)))
                    .WithBody(
                        Block(
                                ExpressionStatement(
                                    InvocationExpression(
                                        MakeMemberAccessExpression("writer", "WriteStructureStart"))))
                            .AddStatements(
                                dBusValue.InnerDBusTypes!.Select(
                                        (x, i) => ExpressionStatement(
                                            InvocationExpression(
                                                    MakeMemberAccessExpression("writer", GetOrAddWriteMethod(x)))
                                                .AddArgumentListArguments(
                                                    Argument(
                                                        MakeMemberAccessExpression("value", $"Item{i + 1}")))))
                                    .Cast<StatementSyntax>()
                                    .ToArray())));

            return identifier;
        }
    }
}

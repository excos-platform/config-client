// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Excos.SourceGen;

[Generator]
public class PrivatePoolGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<SyntaxNode> typeDeclarations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Excos.Options.Utils.PrivatePoolAttribute",
                (_, _) => true,
                (context, _) => context.TargetNode);

        IncrementalValueProvider<(Compilation, ImmutableArray<SyntaxNode>)> compilationAndTypes =
            context.CompilationProvider.Combine(typeDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndTypes, static (spc, source) => HandleAnnotatedTypes(source.Item1, source.Item2, spc));
    }

    private static void HandleAnnotatedTypes(Compilation compilation, IEnumerable<SyntaxNode> nodes, SourceProductionContext context)
    {
        var optionsContextAttribute = compilation.GetTypeByMetadataName("Excos.Options.Utils.PrivatePoolAttribute");
        if (optionsContextAttribute == null)
        {
            return;
        }

        var typeDeclarations = nodes.OfType<TypeDeclarationSyntax>()
            .ToLookup(declaration => declaration.SyntaxTree)
            .SelectMany(declarations => declarations.Select(declaration => (symbol: compilation.GetSemanticModel(declarations.Key).GetDeclaredSymbol(declaration), declaration)))
            .Where(_ => _.symbol is INamedTypeSymbol)
            .Where(_ => _.symbol!.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, optionsContextAttribute)))
            .ToLookup(_ => _.symbol, _ => _.declaration, comparer: SymbolEqualityComparer.Default)
            .ToDictionary<IGrouping<ISymbol?, TypeDeclarationSyntax>, INamedTypeSymbol, List<TypeDeclarationSyntax>>(
                group => (INamedTypeSymbol)group.Key!, group => group.ToList(), comparer: SymbolEqualityComparer.Default);

        var list = new List<PrivatePooledType>();
        foreach (var type in GetPrivatePoolTypes(typeDeclarations))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            type.Diagnostics.ForEach(context.ReportDiagnostic);

            if (type.ShouldEmit)
            {
                list.Add(type);
            }
        }

        if (list.Count > 0)
        {
            var emitter = new Emitter();
            context.AddSource($"PrivatePool.g.cs", emitter.Emit(list.OrderBy(x => x.Namespace + "." + x.Name)));
        }
    }

    private const string Category = nameof(PrivatePooledType);

    public static DiagnosticDescriptor PooledTypeCannotBeStatic { get; } = new DiagnosticDescriptor(
        id: "EXCOSGEN000",
        title: nameof(PooledTypeCannotBeStatic),
        messageFormat: "Pooled type {0} cannot be static to make use of object pooling.",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static DiagnosticDescriptor PooledTypeMustBePartial { get; } = new DiagnosticDescriptor(
        id: "EXCOSGEN001",
        title: nameof(PooledTypeMustBePartial),
        messageFormat: "Pooled type {0} must be partial to inject pooling methods.",
        category: Category,
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static IEnumerable<PrivatePooledType> GetPrivatePoolTypes(Dictionary<INamedTypeSymbol, List<TypeDeclarationSyntax>> types) =>
        types
            .Select(type => new PrivatePooledType(type.Key, type.Value.ToImmutableArray()))
            .Select(CheckInstantiable)
            .Select(CheckPartial);

    private static PrivatePooledType CheckInstantiable(PrivatePooledType type)
    {
        if (type.Symbol.IsStatic)
        {
            type.Diagnostics.AddRange(
                type.Definitions
                    .SelectMany(def => def.Modifiers)
                    .Where(modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                    .Select(modifier => Diagnostic.Create(PooledTypeCannotBeStatic, modifier.GetLocation(), type.Name)));
        }

        return type;
    }

    private static PrivatePooledType CheckPartial(PrivatePooledType type)
    {
        if (!type.Definitions.Any(def => def.Modifiers.Any(static token => token.IsKind(SyntaxKind.PartialKeyword))))
        {
            type.Diagnostics.AddRange(
                type.Definitions.Select(def => Diagnostic.Create(PooledTypeMustBePartial, def.Identifier.GetLocation(), type.Name)));
        }

        return type;
    }

    private sealed class PrivatePooledType
    {
        public readonly List<Diagnostic> Diagnostics = [];
        public readonly INamedTypeSymbol Symbol;
        public readonly ImmutableArray<TypeDeclarationSyntax> Definitions;
        public readonly ImmutableArray<IMethodSymbol> Constructors;
        public bool HasClearMethod => Symbol.GetMembers("Clear").Any(m => !m.IsStatic && m is IMethodSymbol method && method.Parameters.Length == 0);
        public string Keyword => Definitions[0].Keyword.Text;
        public string? Namespace => Symbol.ContainingNamespace.IsGlobalNamespace ? null : Symbol.ContainingNamespace.ToString();
        public string Name => Symbol.TypeArguments.Length > 0 ? $"{Symbol.Name}<{string.Join(", ", Symbol.TypeArguments.Select(ta => ta.Name))}>" : Symbol.Name;

        public bool ShouldEmit => Diagnostics.TrueForAll(diag => diag.Severity != DiagnosticSeverity.Error);

        public string HintName => $"{Namespace}.{Name}";

        public PrivatePooledType(
            INamedTypeSymbol symbol,
            ImmutableArray<TypeDeclarationSyntax> definitions)
        {
            Symbol = symbol;
            Definitions = definitions;
            Constructors = symbol.InstanceConstructors;
        }
    }

    private sealed class Emitter
    {
        public string Emit(IEnumerable<PrivatePooledType> list)
        {
            var builder = new StringBuilder();
            foreach (var privatePoolType in list)
            {
                builder.AppendLine(FormatClass(privatePoolType).ToString(CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static string GeneratedCodeAttribute { get; } = $"global::System.CodeDom.Compiler.GeneratedCodeAttribute(" +
                       $"\"{typeof(Emitter).Assembly.GetName().Name}\", " +
                       $"\"{typeof(Emitter).Assembly.GetName().Version}\")";

        private static FormattableString FormatClass(PrivatePooledType privatePoolType) =>
        $@"{(!string.IsNullOrEmpty(privatePoolType.Namespace) ? $"namespace {privatePoolType.Namespace} {{" : string.Empty)}
        #nullable enable
        [{GeneratedCodeAttribute}]
        partial {privatePoolType.Keyword} {privatePoolType.Name}
        {{
            {EmitGetMethods(privatePoolType)}

            [{GeneratedCodeAttribute}]
            public static void Return({privatePoolType.Name} instance) =>
                global::Excos.Options.Utils.PrivateObjectPool<{privatePoolType.Name}>.Instance.Return(instance);
        }}
    {(!string.IsNullOrEmpty(privatePoolType.Namespace) ? "}" : string.Empty)}";

        private static string EmitGetMethods(PrivatePooledType privatePoolType) =>
        string.Join("\n", privatePoolType.Constructors.Select(ctor => FormattableString.Invariant(
        $@"
            [{GeneratedCodeAttribute}]
            public static {privatePoolType.Name} Get({EmitMethodParameters(ctor)})
            {{
                if (global::Excos.Options.Utils.PrivateObjectPool<{privatePoolType.Name}>.Instance.TryGet(out var instance) && instance != null)
                {{
                    {(privatePoolType.HasClearMethod ? "instance.Clear();" : string.Empty)}
                    {string.Join("\n", ctor.Parameters.Select(p => $"instance._{p.Name} = {p.Name};"))}
                }}
                else
                {{
                    instance = new {privatePoolType.Name}({string.Join(", ", ctor.Parameters.Select(p => p.Name))});
                }}

                return instance;
            }}
        "
        )));
        private static object EmitMethodParameters(IMethodSymbol ctor) =>
        string.Join(", ", ctor.Parameters.Select(p => $"{p.Type.ToDisplayString()} {p.Name}"));
    }
}

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Mediocr;

[Generator]
public class RequestHandlerGenerator : IIncrementalGenerator
{
    private const string RequestHandlerMetadataName = "Mediocr.Interfaces.IRequestHandler`2";
    private const string GeneratorName = "Mediocr.RequestHandlerGenerator";
    private const string GeneratorVersion = "1.0.0";
    private const string SourceFileName = "MediocRServiceCollectionExtensions.g.cs";
    private const string Namespace = "Mediocr.Interfaces";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all classes that might implement IRequestHandler
        var classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsCandidateClass(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null);

        // Combine with compilation
        var compilationAndClasses = context.CompilationProvider.Combine(classDeclarations.Collect());

        // Generate the registration code
        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsCandidateClass(SyntaxNode node)
    {
        return node is ClassDeclarationSyntax { BaseList: not null };
    }

    private static INamedTypeSymbol? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var symbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;

        if (symbol is null || symbol.IsAbstract || symbol.IsStatic)
            return null;

        // Filter out non-public types that can't be instantiated by DI
        if (symbol.DeclaredAccessibility != Accessibility.Public && 
            symbol.DeclaredAccessibility != Accessibility.Internal)
            return null;

        // Skip generic type definitions (e.g., MyHandler<> without concrete type arguments)
        if (symbol is { IsGenericType: true, IsUnboundGenericType: true })
            return null;

        // Skip open generic handlers (type parameters present) since DI can't instantiate them
        if (symbol is { IsGenericType: true } && symbol.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
            return null;

        // Validate has accessible constructor for DI
        return !HasAccessibleConstructor(symbol) ? null : symbol;
    }

    private static bool HasAccessibleConstructor(INamedTypeSymbol symbol)
    {
        return symbol.Constructors.Any(c => 
            !c.IsStatic && c.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal);
    }

    private static void Execute(
        Compilation compilation, 
        IEnumerable<INamedTypeSymbol?>? classes, 
        SourceProductionContext context)
    {
        if (classes is null)
            return;

        context.CancellationToken.ThrowIfCancellationRequested();

        var requestHandlerInterface = compilation.GetTypeByMetadataName(RequestHandlerMetadataName);

        if (requestHandlerInterface is null)
        {
            // Report diagnostic if the core interface is missing
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    id: "MEDIOCR001",
                    title: "IRequestHandler interface not found",
                    messageFormat: "The IRequestHandler<,> interface could not be found. Ensure Mediocr.Interfaces is properly referenced.",
                    category: "Mediocr.Generator",
                    defaultSeverity: DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                Location.None));
            return;
        }

        var handlers = new List<(INamedTypeSymbol HandlerType, ITypeSymbol InputType, ITypeSymbol OutputType)>();
        var handlerSignatures = new HashSet<string>();

        foreach (var classSymbol in classes)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            
            if (classSymbol is null) 
                continue;

            foreach (var iface in classSymbol.AllInterfaces)
            {
                if (!SymbolEqualityComparer.Default.Equals(iface.OriginalDefinition, requestHandlerInterface))
                    continue;
                
                // Validate type arguments
                if (iface.TypeArguments.Length != 2)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            id: "MEDIOCR002",
                            title: "Invalid IRequestHandler implementation",
                            messageFormat: "Handler '{0}' implements IRequestHandler with {1} type arguments instead of 2",
                            category: "Mediocr.Generator",
                            defaultSeverity: DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        GetBestLocation(classSymbol, iface),
                        classSymbol.Name,
                        iface.TypeArguments.Length));
                    continue;
                }

                var inputType = iface.TypeArguments[0];
                var outputType = iface.TypeArguments[1];
                
                // Check for duplicate handlers
                var signature = $"{inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{outputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
                if (!handlerSignatures.Add(signature))
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            id: "MEDIOCR003",
                            title: "Duplicate request handler",
                            messageFormat: "Multiple handlers found for request type '{0}' returning '{1}'. Handler: '{2}'",
                            category: "Mediocr.Generator",
                            defaultSeverity: DiagnosticSeverity.Warning,
                            isEnabledByDefault: true),
                        GetBestLocation(classSymbol, iface),
                        inputType.Name,
                        outputType.Name,
                        classSymbol.Name));
                }
                
                handlers.Add((classSymbol, inputType, outputType));
            }
        }

        if (handlers.Count == 0)
            return;

        // Sort handlers for deterministic output
        handlers.Sort((a, b) => string.Compare(
            a.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            b.HandlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            StringComparison.Ordinal));

        var source = GenerateExtensionClass(handlers);
        context.AddSource(SourceFileName, SourceText.From(source, Encoding.UTF8));
    }

    private static Location GetBestLocation(INamedTypeSymbol classSymbol, INamedTypeSymbol? interfaceSymbol = null)
    {
        // Try to get the interface implementation location first
        if (interfaceSymbol is null) return classSymbol.Locations.FirstOrDefault() ?? Location.None;
        
        var syntaxReferences = classSymbol.DeclaringSyntaxReferences;
        foreach (var syntax in syntaxReferences.Select(syntaxRef => syntaxRef.GetSyntax()))
        {
            if (syntax is not ClassDeclarationSyntax { BaseList: not null } classDecl) continue;
                
            foreach (var baseType in classDecl.BaseList.Types)
            {
                // Return the base list location for better error highlighting
                return baseType.GetLocation();
            }
        }

        // Fall back to the class location
        return classSymbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static string GenerateExtensionClass(
        List<(INamedTypeSymbol HandlerType, ITypeSymbol InputType, ITypeSymbol OutputType)> handlers)
    {
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine($"// Generated by {GeneratorName} v{GeneratorVersion}");
        sb.AppendLine($"// Generation time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"// Handlers discovered: {handlers.Count}");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.CodeDom.Compiler;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine();
        sb.AppendLine($"namespace {Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Extension methods for registering MediocR request handlers with dependency injection.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [GeneratedCode(\"{GeneratorName}\", \"{GeneratorVersion}\")]");
        sb.AppendLine("    public static class MediocRServiceCollectionExtensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Registers all {handlers.Count} discovered MediocR request handlers as scoped services.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection to add handlers to.</param>");
        sb.AppendLine("        /// <returns>The service collection for chaining.</returns>");
        sb.AppendLine("        public static IServiceCollection AddMediocrHandlers(this IServiceCollection services)");
        sb.AppendLine("        {");

        foreach (var (handlerType, inputType, outputType) in handlers)
        {
            var handlerFullName = handlerType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var inputFullName = inputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var outputFullName = outputType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sb.AppendLine($"            services.AddScoped<global::Mediocr.Interfaces.IRequestHandler<{inputFullName}, {outputFullName}>, {handlerFullName}>();");
        }

        sb.AppendLine();
        sb.AppendLine("            return services;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}

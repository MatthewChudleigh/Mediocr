using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Mediocr.Test;

public partial class RegexHelper
{
    [GeneratedRegex(@"Generation time:\s\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\sUTC")]
    public static partial Regex ReGenDateTime();
}

public static class GeneratorTestHelper
{
    public static GeneratorDriverRunResult RunGenerator(
        string sourceCode,
        RequestHandlerGenerator generator,
        params string[] additionalSources)
    {
        // Create compilation from source
        var compilation = CreateCompilation(sourceCode, additionalSources);
        
        // Create the generator driver
        var driver = CSharpGeneratorDriver.Create(generator);
        
        // Run the generator
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, 
            out var outputCompilation, 
            out var diagnostics);
        
        return driver.GetRunResult();
    }
    
    public static Compilation CreateCompilation(string source, params string[] additionalSources)
    {
        var sources = new List<string> { source };
        sources.AddRange(additionalSources);
        
        var syntaxTrees = sources.Select(s => 
            CSharpSyntaxTree.ParseText(s)).ToArray();
        
        // Get references to core assemblies
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Enumerable).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Console).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
            MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location),
            MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            MetadataReference.CreateFromFile(Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions").Location),
        };
        
        // Add reference to Mediocr.Interfaces
        references.Add(MetadataReference.CreateFromFile(typeof(Mediocr.Interfaces.IRequestHandler<,>).Assembly.Location));
        
        var compilation = CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
        return compilation;
    }
    
    public static string? GetGeneratedSource(this GeneratorDriverRunResult result, string hintName)
    {
        return result.GeneratedTrees
            .FirstOrDefault(t => t.FilePath.EndsWith(hintName))
            ?.GetText()
            .ToString();
    }
    
    public static ImmutableArray<GeneratedSourceResult> GetAllGeneratedSources(this GeneratorDriverRunResult result)
    {
        return result.Results[0].GeneratedSources;
    }
}
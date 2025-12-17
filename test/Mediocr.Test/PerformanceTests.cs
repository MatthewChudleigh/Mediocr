namespace Mediocr.Test;

using FluentAssertions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

/// <summary>
/// Performance tests to ensure the generator performs efficiently
/// </summary>
public class PerformanceTests(ITestOutputHelper output)
{
    private readonly RequestHandlerGenerator _generator = new();

    [Fact]
    public void Generator_WithManyHandlers_CompletesInReasonableTime()
    {
        // Arrange
        var source = GenerateManyHandlers(100);
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);
        stopwatch.Stop();
        
        // Assert
        result.GeneratedTrees.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000, "generation should complete in under 5 seconds");
        
        output.WriteLine($"Generated code for 100 handlers in {stopwatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public void Generator_WithDeeplyNestedNamespaces_PerformsWell()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10;

public class DeepRequest : IRequest<string> { }

public class DeepHandler : IRequestHandler<DeepRequest, string>
{
    public Task<string> Handle(DeepRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);
        stopwatch.Stop();
        
        // Assert
        result.GeneratedTrees.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        
        output.WriteLine($"Generated code for deeply nested namespace in {stopwatch.ElapsedMilliseconds}ms");
    }
    
    [Fact]
    public void Generator_WithComplexGenericTypes_PerformsWell()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Mediocr.Interfaces;

namespace TestApp;

public class ComplexRequest : IRequest<Dictionary<string, List<Tuple<int, string>>>> { }

public class ComplexHandler : IRequestHandler<ComplexRequest, Dictionary<string, List<Tuple<int, string>>>>
{
    public Task<Dictionary<string, List<Tuple<int, string>>>> Handle(ComplexRequest input, CancellationToken cancel)
        => Task.FromResult(new Dictionary<string, List<Tuple<int, string>>>());
}";

        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);
        stopwatch.Stop();
        
        // Assert
        result.GeneratedTrees.Should().HaveCount(1);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
        
        output.WriteLine($"Generated code for complex generic types in {stopwatch.ElapsedMilliseconds}ms");
    }
    
    [Theory]
    [InlineData(1, 1000)] // Warmup
    [InlineData(10, 100)]
    [InlineData(500, 100)]
    [InlineData(1000, 100)]
    public void Generator_ScalesLinearlyWithHandlerCount(int handlerCount, int expectedMs)
    {
        // Arrange
        var source = GenerateManyHandlers(handlerCount);
        var stopwatch = Stopwatch.StartNew();
        
        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);
        stopwatch.Stop();
        
        // Assert
        result.GeneratedTrees.Should().HaveCount(1);
        var msPerHandler = (double)stopwatch.ElapsedMilliseconds / handlerCount;
        
        output.WriteLine($"Generated {handlerCount} handlers in {stopwatch.ElapsedMilliseconds}ms ({msPerHandler:F2}ms per handler)");
        
        // Should be very fast per handler
        msPerHandler.Should().BeLessThan(expectedMs, "should process each handler quickly");
    }
    
    [Fact]
    public void Generator_GeneratedCodeSize_IsReasonable()
    {
        // Arrange
        var source = GenerateManyHandlers(100);
        
        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        // Assert
        var sizeInBytes = System.Text.Encoding.UTF8.GetByteCount(generatedSource!);
        var sizeInKB = sizeInBytes / 1024.0;
        
        output.WriteLine($"Generated code size for 100 handlers: {sizeInKB:F2} KB");
        
        // Should be reasonable size (less than 100KB for 100 handlers)
        sizeInKB.Should().BeLessThan(100);
    }
    
    private static string GenerateManyHandlers(int count)
    {
        var handlers = new System.Text.StringBuilder();
        handlers.AppendLine(@"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;
");

        for (int i = 0; i < count; i++)
        {
            handlers.AppendLine($@"
public class Request{i} : IRequest<string> {{ }}

public class Handler{i} : IRequestHandler<Request{i}, string>
{{
    public Task<string> Handle(Request{i} input, CancellationToken cancel)
        => Task.FromResult(""test{i}"");
}}
");
        }

        return handlers.ToString();
    }
}
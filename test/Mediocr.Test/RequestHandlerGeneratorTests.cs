using FluentAssertions;
using Microsoft.CodeAnalysis;
using System.Threading;

namespace Mediocr.Test;

public class RequestHandlerGeneratorTests
{
    private readonly RequestHandlerGenerator _generator = new();
    
    [Fact]
    public void Generator_WithNoHandlers_GeneratesNothing()
    {
        // Arrange
        var source = @"
namespace TestApp;

public class SomeClass
{
    public void DoSomething() { }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
    }
    
    [Fact]
    public void Generator_WithSingleHandler_GeneratesExtensionMethod()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string>
{
}

public class TestRequestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
    {
        return Task.FromResult(""Hello"");
    }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().HaveCount(1);
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("AddMediocrHandlers");
        generatedSource.Should().Contain("services.AddScoped");
        generatedSource.Should().Contain("TestRequest");
        generatedSource.Should().Contain("TestRequestHandler");
    }
    
    [Fact]
    public void Generator_WithMultipleHandlers_RegistersAll()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class Request1 : IRequest<string> { }
public class Request2 : IRequest<int> { }

public class Handler1 : IRequestHandler<Request1, string>
{
    public Task<string> Handle(Request1 input, CancellationToken cancel)
        => Task.FromResult(""test"");
}

public class Handler2 : IRequestHandler<Request2, int>
{
    public Task<int> Handle(Request2 input, CancellationToken cancel)
        => Task.FromResult(42);
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("Handler1");
        generatedSource.Should().Contain("Handler2");
        generatedSource.Should().Contain("Request1");
        generatedSource.Should().Contain("Request2");
        
        // Count the number of AddScoped calls
        var addScopedCount = CountOccurrences(generatedSource!, "AddScoped");
        addScopedCount.Should().Be(2);
    }
    
    [Fact]
    public void Generator_WithAbstractHandler_DoesNotRegister()
    {
        // Arrange
        const string source = """

                              using System.Threading;
                              using System.Threading.Tasks;
                              using Mediocr.Interfaces;

                              namespace TestApp;

                              public class TestRequest : IRequest<string> { }

                              public abstract class AbstractHandler : IRequestHandler<TestRequest, string>
                              {
                                  public abstract Task<string> Handle(TestRequest input, CancellationToken cancel);
                              }

                              public class ConcreteHandler : AbstractHandler
                              {
                                  public override Task<string> Handle(TestRequest input, CancellationToken cancel)
                                      => Task.FromResult("test");
                              }
                              """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("ConcreteHandler");
        generatedSource.Should().NotContain("AbstractHandler");
        
        var addScopedCount = CountOccurrences(generatedSource!, "AddScoped");
        addScopedCount.Should().Be(1);
    }
    
    [Fact]
    public void Generator_WithNestedClass_RegistersCorrectly()
    {
        // Arrange
        const string source = """

                              using System.Threading;
                              using System.Threading.Tasks;
                              using Mediocr.Interfaces;

                              namespace TestApp;

                              public class TestRequest : IRequest<string> { }

                              public class Outer
                              {
                                  public class NestedHandler : IRequestHandler<TestRequest, string>
                                  {
                                      public Task<string> Handle(TestRequest input, CancellationToken cancel)
                                          => Task.FromResult("nested");
                                  }
                              }
                              """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("Outer.NestedHandler");
    }
    
    [Fact]
    public void Generator_WithGenericHandler_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class GenericHandler<T> : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(typeof(T).Name);
}

public class ConcreteGenericHandler : GenericHandler<int>
{
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        // Only concrete types should be registered
        generatedSource.Should().Contain("ConcreteGenericHandler");
    }
    
    [Fact]
    public void Generator_WithComplexNamespaces_UsesFullyQualifiedNames()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace Company.Product.Feature;

public class MyRequest : IRequest<MyResponse> { }
public class MyResponse { }

public class MyHandler : IRequestHandler<MyRequest, MyResponse>
{
    public Task<MyResponse> Handle(MyRequest input, CancellationToken cancel)
        => Task.FromResult(new MyResponse());
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("global::Company.Product.Feature.MyRequest");
        generatedSource.Should().Contain("global::Company.Product.Feature.MyResponse");
        generatedSource.Should().Contain("global::Company.Product.Feature.MyHandler");
    }
    
    [Fact]
    public void Generator_WithMultipleInterfaceImplementations_RegistersAll()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class Request1 : IRequest<string> { }
public class Request2 : IRequest<int> { }

public class MultiHandler : IRequestHandler<Request1, string>, IRequestHandler<Request2, int>
{
    public Task<string> Handle(Request1 input, CancellationToken cancel)
        => Task.FromResult(""test"");
    
    public Task<int> Handle(Request2 input, CancellationToken cancel)
        => Task.FromResult(42);
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("MultiHandler");
        
        var addScopedCount = CountOccurrences(generatedSource!, "AddScoped");
        addScopedCount.Should().Be(2, "should register the handler twice for both interfaces");
    }
    
    [Fact]
    public void Generator_ProducesValidCSharpCode()
    {
        // Arrange
        const string source = """

                              using System.Threading;
                              using System.Threading.Tasks;
                              using Mediocr.Interfaces;

                              namespace TestApp;

                              public class TestRequest : IRequest<string> { }

                              public class TestHandler : IRequestHandler<TestRequest, string>
                              {
                                  public Task<string> Handle(TestRequest input, CancellationToken cancel)
                                      => Task.FromResult("test");
                              }
                              """;

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        // Try to compile the generated source
        var compilation = GeneratorTestHelper.CreateCompilation(generatedSource!, source);
        var diagnostics = compilation.GetDiagnostics();
        
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty("generated code should compile without errors");
    }
    
    [Fact]
    public void Generator_GeneratesAutoGeneratedHeader()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("// <auto-generated/>");
    }
    
    [Fact]
    public void Generator_GeneratesCorrectNamespace()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("namespace Mediocr.Interfaces");
    }
    
    [Fact]
    public void Generator_WithNoBaseList_IsIgnored()
    {
        // Arrange
        var source = @"
namespace TestApp;

public class ClassWithoutInterfaces
{
    public void DoSomething() { }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
    }
    
    [Fact]
    public void Generator_WithNonHandlerInterface_IsIgnored()
    {
        // Arrange
        var source = @"
using System;

namespace TestApp;

public interface IOtherInterface
{
    void DoSomething();
}

public class NonHandlerClass : IOtherInterface
{
    public void DoSomething() { }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
    }
    
    [Fact]
    public void Generator_ReturnsServiceCollection()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("return services;");
        generatedSource.Should().Contain("IServiceCollection");
    }
    
    [Fact]
    public void Generator_WithRecordType_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public record TestRequest(string Name) : IRequest<string>;

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(input.Name);
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("TestRequest");
        generatedSource.Should().Contain("TestHandler");
    }
    
    [Fact]
    public void Generator_NoDiagnosticErrors()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.Diagnostics.Should().BeEmpty("generator should not produce any diagnostics");
    }

    [Fact]
    public void Generator_Output_IsDeterministic()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class TestHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var firstRun = GeneratorTestHelper.RunGenerator(source, _generator);
        var firstGenerated = firstRun.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        Thread.Sleep(1100); // ensure timestamp would differ if non-deterministic
        var secondRun = GeneratorTestHelper.RunGenerator(source, _generator);
        var secondGenerated = secondRun.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");

        // Assert
        firstGenerated.Should().NotBeNull();
        secondGenerated.Should().NotBeNull();

        firstGenerated = RegexHelper.ReGenDateTime()
            .Replace(firstGenerated, "Generation time: yyyy-mm-dd HH:MM:SS UTC");
        secondGenerated = RegexHelper.ReGenDateTime()
            .Replace(secondGenerated, "Generation time: yyyy-mm-dd HH:MM:SS UTC");
        
        secondGenerated.Should().Be(firstGenerated, "generator output should be deterministic between runs");
    }

    [Fact]
    public void Generator_WithOpenGenericHandler_IsSkipped()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class OpenRequest<T> : IRequest<string> { }

public class OpenGenericHandler<T> : IRequestHandler<OpenRequest<T>, string>
{
    public Task<string> Handle(OpenRequest<T> input, CancellationToken cancel)
        => Task.FromResult(""open"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().BeEmpty("open generic handlers cannot be registered by DI without type arguments");
    }
    
    private static int CountOccurrences(string text, string pattern)
    {
        var count = 0;
        var index = 0;
        
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        
        return count;
    }
}

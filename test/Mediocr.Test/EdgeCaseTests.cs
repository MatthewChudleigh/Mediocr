using FluentAssertions;
using Microsoft.CodeAnalysis.CSharp;

namespace Mediocr.Test;

/// <summary>
/// Tests for edge cases and boundary conditions
/// </summary>
public class EdgeCaseTests
{
    private readonly RequestHandlerGenerator _generator = new();
    
    [Fact]
    public void Generator_WithVeryLongNamespaces_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace Company.Product.Feature.SubFeature.Domain.Commands.Users.Create;

public class CreateUserCommand : IRequest<CreateUserResult> { }
public class CreateUserResult { }

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    public Task<CreateUserResult> Handle(CreateUserCommand input, CancellationToken cancel)
        => Task.FromResult(new CreateUserResult());
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        generatedSource.Should().Contain("Company.Product.Feature.SubFeature.Domain.Commands.Users.Create");
    }
    
    [Fact]
    public void Generator_WithSpecialCharactersInNames_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class Request_With_Underscores : IRequest<string> { }

public class Handler_With_Underscores : IRequestHandler<Request_With_Underscores, string>
{
    public Task<string> Handle(Request_With_Underscores input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("Request_With_Underscores");
        generatedSource.Should().Contain("Handler_With_Underscores");
    }
    
    [Fact]
    public void Generator_WithGenericResponseTypes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Mediocr.Interfaces;

namespace TestApp;

public class GetListRequest : IRequest<List<string>> { }

public class GetListHandler : IRequestHandler<GetListRequest, List<string>>
{
    public Task<List<string>> Handle(GetListRequest input, CancellationToken cancel)
        => Task.FromResult(new List<string>());
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("List<string>");
    }
    
    [Fact]
    public void Generator_WithNullableResponseTypes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class GetUserRequest : IRequest<string?> { }

public class GetUserHandler : IRequestHandler<GetUserRequest, string?>
{
    public Task<string?> Handle(GetUserRequest input, CancellationToken cancel)
        => Task.FromResult<string?>(null);
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        // Note: nullable reference types are erased at runtime, so the generated code won't show ?
    }
    
    [Fact]
    public void Generator_WithValueTupleResponseTypes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class GetTupleRequest : IRequest<(int Id, string Name)> { }

public class GetTupleHandler : IRequestHandler<GetTupleRequest, (int Id, string Name)>
{
    public Task<(int Id, string Name)> Handle(GetTupleRequest input, CancellationToken cancel)
        => Task.FromResult((1, ""test""));
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        // Value tuples can be represented as either (int, string) or System.ValueTuple<int, string>
        // depending on the SymbolDisplayFormat - check that it contains the handler
        generatedSource.Should().Contain("GetTupleHandler");
        generatedSource.Should().Contain("GetTupleRequest");
        // The tuple syntax should be preserved
        (generatedSource!.Contains("(int Id, string Name)") || generatedSource.Contains("System.ValueTuple<")).Should().BeTrue();
    }
    
    [Fact]
    public void Generator_WithArrayResponseTypes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class GetArrayRequest : IRequest<string[]> { }

public class GetArrayHandler : IRequestHandler<GetArrayRequest, string[]>
{
    public Task<string[]> Handle(GetArrayRequest input, CancellationToken cancel)
        => Task.FromResult(new string[0]);
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        // Check for any diagnostics that might indicate why generation failed
        result.Diagnostics.Should().BeEmpty("generator should not produce diagnostics");
        
        result.GeneratedTrees.Should().HaveCount(1, "generator should produce output for array types");
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        // Arrays are represented as T[] in the fully qualified format
        generatedSource.Should().Contain("GetArrayHandler");
        generatedSource.Should().Contain("GetArrayRequest");
    }
    
    [Fact]
    public void Generator_WithInternalHandler_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

internal class InternalHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        // Internal handlers can be registered when DI is in the same assembly
        generatedSource.Should().Contain("InternalHandler");
    }
    
    [Fact]
    public void Generator_WithPrivateNestedHandler_DoesNotRegister()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public class Outer
{
    private class PrivateNestedHandler : IRequestHandler<TestRequest, string>
    {
        public Task<string> Handle(TestRequest input, CancellationToken cancel)
            => Task.FromResult(""test"");
    }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        // Private nested classes can't be registered in DI, but the generator might still find them
        // This behavior depends on how you want to handle it
        result.GeneratedTrees.Should().BeEmpty();
    }
    
    [Fact]
    public void Generator_WithSealedHandler_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class TestRequest : IRequest<string> { }

public sealed class SealedHandler : IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("SealedHandler");
    }
    
    [Fact]
    public void Generator_WithStaticClass_IsIgnored()
    {
        // Arrange
        var source = @"
namespace TestApp;

public static class StaticClass
{
    public static void DoSomething() { }
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        result.GeneratedTrees.Should().BeEmpty();
    }
    
    [Fact]
    public void Generator_WithEmptyNamespace_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

public class GlobalRequest : IRequest<string> { }

public class GlobalHandler : IRequestHandler<GlobalRequest, string>
{
    public Task<string> Handle(GlobalRequest input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().NotBeNull();
        // Global namespace types should still work
        generatedSource.Should().Contain("GlobalHandler");
    }
    
    [Fact]
    public void Generator_WithUnicodeCharactersInNames_HandlesCorrectly()
    {
        // Arrange - C# allows Unicode in identifiers
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using Mediocr.Interfaces;

namespace TestApp;

public class Request日本語 : IRequest<string> { }

public class Handler日本語 : IRequestHandler<Request日本語, string>
{
    public Task<string> Handle(Request日本語 input, CancellationToken cancel)
        => Task.FromResult(""test"");
}";

        // Act
        var result = GeneratorTestHelper.RunGenerator(source, _generator);

        // Assert
        var generatedSource = result.GetGeneratedSource("MediocRServiceCollectionExtensions.g.cs");
        
        generatedSource.Should().Contain("Request日本語");
        generatedSource.Should().Contain("Handler日本語");
    }
}
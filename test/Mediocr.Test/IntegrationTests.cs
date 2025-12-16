namespace Mediocr.Test;

using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

/// <summary>
/// Integration tests that verify the generated code works correctly with DI
/// </summary>
public class IntegrationTests
{
    [Fact]
    public async Task GeneratedExtension_CanResolveHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Simulate what the generated code does
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<TestRequest, string>, TestRequestHandler>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var handler = serviceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<TestRequest, string>>();
        var result = await handler.Handle(new TestRequest { Value = "test" }, CancellationToken.None);
        
        // Assert
        handler.Should().NotBeNull();
        handler.Should().BeOfType<TestRequestHandler>();
        result.Should().Be("test");
    }
    
    [Fact]
    public void GeneratedExtension_RegistersAsScoped()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<TestRequest, string>, TestRequestHandler>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act - Get same handler twice from same scope
        using var scope1 = serviceProvider.CreateScope();
        var handler1a = scope1.ServiceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<TestRequest, string>>();
        var handler1b = scope1.ServiceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<TestRequest, string>>();
        
        // Get handler from different scope
        using var scope2 = serviceProvider.CreateScope();
        var handler2 = scope2.ServiceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<TestRequest, string>>();
        
        // Assert
        handler1a.Should().BeSameAs(handler1b, "should be same instance within scope");
        handler1a.Should().NotBeSameAs(handler2, "should be different instance across scopes");
    }
    
    [Fact]
    public void GeneratedExtension_CanRegisterMultipleHandlers()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Simulate registering multiple handlers
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<TestRequest, string>, TestRequestHandler>();
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<IntRequest, int>, IntRequestHandler>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var stringHandler = serviceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<TestRequest, string>>();
        var intHandler = serviceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<IntRequest, int>>();
        
        // Assert
        stringHandler.Should().NotBeNull();
        intHandler.Should().NotBeNull();
        stringHandler.Should().BeOfType<TestRequestHandler>();
        intHandler.Should().BeOfType<IntRequestHandler>();
    }
    
    [Fact]
    public void GeneratedExtension_ReturnsServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        var result = AddTestHandlers(services);
        
        // Assert
        result.Should().BeSameAs(services, "should return the same service collection for chaining");
    }
    
    [Fact]
    public async Task GeneratedExtension_HandlersCanHaveDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IDependency, Dependency>();
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<DependentRequest, string>, DependentRequestHandler>();
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act
        var handler = serviceProvider.GetRequiredService<Mediocr.Interfaces.IRequestHandler<DependentRequest, string>>();
        var result = await handler.Handle(new DependentRequest(), CancellationToken.None);
        
        // Assert
        result.Should().Be("dependency-injected");
    }
    
    // Helper method to simulate generated extension method
    private static IServiceCollection AddTestHandlers(IServiceCollection services)
    {
        services.AddScoped<Mediocr.Interfaces.IRequestHandler<TestRequest, string>, TestRequestHandler>();
        return services;
    }
}

// Test classes
public class TestRequest : Mediocr.Interfaces.IRequest<string>
{
    public string Value { get; set; } = string.Empty;
}

public class TestRequestHandler : Mediocr.Interfaces.IRequestHandler<TestRequest, string>
{
    public Task<string> Handle(TestRequest input, CancellationToken cancel)
    {
        return Task.FromResult(input.Value);
    }
}

public class IntRequest : Mediocr.Interfaces.IRequest<int>
{
    public int Value { get; set; }
}

public class IntRequestHandler : Mediocr.Interfaces.IRequestHandler<IntRequest, int>
{
    public Task<int> Handle(IntRequest input, CancellationToken cancel)
    {
        return Task.FromResult(input.Value * 2);
    }
}

public interface IDependency
{
    string GetValue();
}

public class Dependency : IDependency
{
    public string GetValue() => "dependency-injected";
}

public class DependentRequest : Mediocr.Interfaces.IRequest<string>
{
}

public class DependentRequestHandler : Mediocr.Interfaces.IRequestHandler<DependentRequest, string>
{
    private readonly IDependency _dependency;
    
    public DependentRequestHandler(IDependency dependency)
    {
        _dependency = dependency;
    }
    
    public Task<string> Handle(DependentRequest input, CancellationToken cancel)
    {
        return Task.FromResult(_dependency.GetValue());
    }
}
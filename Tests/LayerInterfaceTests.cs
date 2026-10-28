using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ValoresReceitas.Api.Controllers;
using ValoresReceitas.Api.Models;
using ValoresReceitas.Api.Services;
using ValoresReceitas.Api.Services.Interfaces;
using Xunit;

namespace ValoresReceitas.Api.Tests;

public sealed class LayerInterfaceTests
{
    private static readonly string[] LayerNamespaces =
    [
        "ValoresReceitas.Api.Controllers",
        "ValoresReceitas.Api.Services",
        "ValoresReceitas.Api.Repositories",
    ];

    private static IEnumerable<Type> Implementations => typeof(DataController).Assembly.GetTypes()
        .Where(type => type.IsClass && !type.IsAbstract && type.IsPublic &&
            LayerNamespaces.Contains(type.Namespace) && !typeof(Exception).IsAssignableFrom(type));

    private static Type Contract(Type implementation) =>
        implementation.Assembly.GetType($"{implementation.Namespace}.Interfaces.I{implementation.Name}", throwOnError: true)!;

    [Fact]
    public void EveryLayerImplementationHasACompleteInterfaceInItsOwnFile()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "ValoresReceitas.Api.csproj")))
            root = root.Parent;
        Assert.NotNull(root);
        var implementations = Implementations.ToList();
        Assert.NotEmpty(implementations);
        foreach (var implementation in implementations)
        {
            var contract = Contract(implementation);
            Assert.True(contract.IsInterface);
            Assert.True(contract.IsAssignableFrom(implementation), implementation.FullName);
            var layer = implementation.Namespace!.Split('.').Last();
            var path = Path.Combine(root.FullName, layer, "interfaces", $"{contract.Name}.cs");
            Assert.True(File.Exists(path), path);
            Assert.Single(System.Text.RegularExpressions.Regex.Matches(
                File.ReadAllText(path), @"\bpublic\s+interface\s+\w+"));
            var contracts = implementation.GetInterfaces().SelectMany(type =>
                implementation.GetInterfaceMap(type).TargetMethods).ToHashSet();
            Assert.All(implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
                method => Assert.Contains(method, contracts));
        }
    }

    [Fact]
    public void ConstructorsDependOnLayerInterfacesRatherThanConcreteImplementations()
    {
        foreach (var parameter in Implementations.SelectMany(type => type.GetConstructors())
            .SelectMany(constructor => constructor.GetParameters())
            .Where(parameter => LayerNamespaces.Contains(parameter.ParameterType.Namespace) ||
                LayerNamespaces.Any(layer => parameter.ParameterType.Namespace == $"{layer}.Interfaces")))
        {
            Assert.True(parameter.ParameterType.IsInterface, parameter.ToString());
            Assert.EndsWith(".Interfaces", parameter.ParameterType.Namespace);
        }
    }

    [Fact]
    public async Task AllLayerContractsResolveWithTheirExpectedLifetimes()
    {
        await using var host = await ApiTestHost.CreateAsync();
        var firstScope = new Dictionary<Type, object>();
        await host.WithServicesAsync(provider =>
        {
            foreach (var implementation in Implementations)
            {
                var contract = Contract(implementation);
                var instance = provider.GetRequiredService(contract);
                Assert.IsType(implementation, instance);
                firstScope.Add(contract, instance);
                if (implementation == typeof(EmailService))
                    Assert.NotSame(instance, provider.GetRequiredService(contract));
                else
                    Assert.Same(instance, provider.GetRequiredService(contract));
            }
            return Task.CompletedTask;
        });
        await host.WithServicesAsync(provider =>
        {
            foreach (var (contract, instance) in firstScope)
                Assert.NotSame(instance, provider.GetRequiredService(contract));
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void IdentityEmailSenderUsesTheApplicationEmailContract()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddApplicationServices();
        using var provider = services.BuildServiceProvider();
        var sender = provider.GetRequiredService<IEmailSender<AccountUser>>();
        Assert.IsType<EmailService>(sender);
        Assert.IsAssignableFrom<IEmailService>(sender);
        Assert.NotSame(sender, provider.GetRequiredService<IEmailSender<AccountUser>>());
    }
}

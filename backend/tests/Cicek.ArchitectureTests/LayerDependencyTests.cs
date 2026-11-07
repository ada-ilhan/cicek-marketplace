using System.Reflection;

namespace Cicek.ArchitectureTests;

/// <summary>
/// Enforces ADR-0001/0009: dependency direction and that Oracle/EF Core types stay inside Infrastructure.
/// Uses the assembly reference metadata the compiler actually emitted.
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly Assembly SharedKernel = typeof(Cicek.SharedKernel.Results.Error).Assembly;
    private static readonly Assembly Domain = typeof(Cicek.Domain.Identity.RefreshSession).Assembly;
    private static readonly Assembly Application = typeof(Cicek.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(Cicek.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(Program).Assembly;

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void SharedKernel_references_only_the_base_class_library()
    {
        Assert.All(References(SharedKernel), name => Assert.True(IsBcl(name), $"SharedKernel must not reference {name}"));
    }

    [Fact]
    public void Domain_references_only_SharedKernel_and_the_base_class_library()
    {
        Assert.All(References(Domain), name =>
            Assert.True(IsBcl(name) || name == SharedKernel.GetName().Name, $"Domain must not reference {name}"));
    }

    [Theory]
    [InlineData("Oracle.")]
    [InlineData("Microsoft.EntityFrameworkCore.Relational")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Cicek.Infrastructure")]
    [InlineData("Cicek.Api")]
    // EF Core abstractions (Microsoft.EntityFrameworkCore) are allowed by ADR-0003; Relational and Oracle are not.
    public void Application_does_not_reference_infrastructure_or_web_frameworks(string forbiddenPrefix)
    {
        Assert.DoesNotContain(References(Application), name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void Api_does_not_use_Oracle_types_directly()
    {
        Assert.DoesNotContain(References(Api), name => name.StartsWith("Oracle.", StringComparison.Ordinal));
    }

    [Fact]
    public void Inner_layers_do_not_reference_outer_layers()
    {
        Assert.DoesNotContain(References(Domain), name => name is "Cicek.Application" or "Cicek.Infrastructure" or "Cicek.Api");
        Assert.DoesNotContain(References(Infrastructure), name => name == "Cicek.Api");
    }

    [Fact]
    public void Money_and_domain_types_never_use_binary_floating_point()
    {
        var offenders =
            from assembly in new[] { SharedKernel, Domain, Application }
            from type in assembly.GetTypes()
            from member in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .Select(p => (Name: p.Name, Type: p.PropertyType))
                .Concat(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .Select(f => (Name: f.Name, Type: f.FieldType)))
            let underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type
            where underlying == typeof(double) || underlying == typeof(float)
            select $"{type.FullName}.{member.Name}";

        Assert.Empty(offenders);
    }

    private static bool IsBcl(string name) =>
        name is "System.Runtime" or "netstandard" or "mscorlib" || name.StartsWith("System.", StringComparison.Ordinal);
}

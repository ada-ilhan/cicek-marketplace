using Cicek.Application.Auth;
using Cicek.Application.Carts;
using Cicek.Application.Catalog;
using Cicek.Application.Common;
using Cicek.Application.Ordering;
using Cicek.Application.Shops;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Cicek.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ImageUrlPolicy>();
        services.AddValidatorsFromAssemblyContaining<RegisterCustomerCommandValidator>(ServiceLifetime.Singleton);

        // Use-case handlers (ADR-0003): every concrete "*Handler" class is a scoped service.
        foreach (var handler in typeof(DependencyInjection).Assembly.GetTypes()
                     .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Name.EndsWith("Handler", StringComparison.Ordinal)))
        {
            services.AddScoped(handler);
        }

        services.AddScoped<AuthSessionIssuer>();
        services.AddScoped<ShopAccess>();
        services.AddScoped<Media.MediaReferenceValidator>();
        services.AddScoped<CartPricer>();
        services.AddScoped<OrderReader>();
        services.AddScoped<OrderTransitionRunner>();
        services.AddScoped<MarketCalendar>();

        return services;
    }

    /// <summary>All use-case handler types (exposed for tests).</summary>
    public static IEnumerable<Type> HandlerTypes() =>
        typeof(DependencyInjection).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Name.EndsWith("Handler", StringComparison.Ordinal));
}

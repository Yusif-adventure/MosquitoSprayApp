using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace SmartMosquitoControl.Extensions;

public static class StaticAssetsExtensions
{
    // No-op placeholder to keep Program.cs compileable and allow future customization
    public static WebApplication MapStaticAssets(this WebApplication app)
    {
        // Default static files middleware is already registered in Program.cs
        return app;
    }

    // No-op endpoint extension to allow fluent chaining in Program.cs
    public static TBuilder WithStaticAssets<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        return builder;
    }
}

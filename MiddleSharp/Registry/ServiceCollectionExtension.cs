using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using MiddleSharp.Pipeline;

namespace MiddleSharp.Registry;

/// <summary>
/// Provides extension methods for adding and managing middleware and chained services in the Microsoft.Extensions.DependencyInjection service collection.
/// This static class serves as an entry point to integrate middleware pipelines into the DI container.
/// </summary>
public static class ServiceCollectionExtension
{
    /// <summary>
    /// A static list of pipeline middlewares used for global middleware configuration.
    /// </summary>
    /// <remarks>
    /// The <c>Pipeline</c> variable is a shared collection of <c>PipelineMiddleware</c> delegates,
    /// which are added when global middleware is registered through
    /// <see cref="ServiceCollectionExtension.UseGlobalMiddleware"/>. Each middleware represents a piece
    /// of functionality that can be executed in a middleware pipeline during method invocation.
    /// This collection is used when building a chain of middlewares within the pipeline.
    /// </remarks>
    private static readonly List<PipelineMiddleware> Pipeline = [];

    /// <summary>
    /// Adds global middleware to a shared pipeline, allowing its execution
    /// for all registered services.
    /// </summary>
    /// <param name="services">The service collection to which the middleware is registered.</param>
    /// <param name="middleware">The middleware delegates to be executed during request processing.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection UseGlobalMiddleware(this IServiceCollection services,
        PipelineMiddleware middleware)
    {
        ArgumentNullException.ThrowIfNull(middleware);
        Pipeline.Add(middleware);
        return services;
    }

    /// <summary>
    /// Adds the specified service with a pipeline of middleware to the <see cref="IServiceCollection"/>.
    /// This allows chaining multiple middleware components to enhance or transform the behavior of the service dynamically.
    /// </summary>
    /// <typeparam name="TService">The service type to be registered.</typeparam>
    /// <typeparam name="TImplementation">The implementation type of the service.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the service will be added.</param>
    /// <param name="configure">
    /// An optional delegate to configure the pipeline. The delegate can be used to add additional middleware
    /// to the service pipeline.
    /// </param>
    /// <returns>
    /// The modified <see cref="IServiceCollection"/> to support method chaining.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown when the dispatcher for the service cannot be created.</exception>
    public static IServiceCollection AddChained<TService, TImplementation>(this IServiceCollection services,
        Action<ChainPipeline>? configure = null) where TService : class where TImplementation : class, TService
    {
        services.AddTransient<TImplementation>();
        services.AddTransient<TService>(sp =>
        {
            var decorated = sp.GetRequiredService<TImplementation>();
            var pipeline = new ChainPipeline();

            foreach (var middleware in Pipeline)
                pipeline.Use(middleware);

            configure?.Invoke(pipeline);

            var proxy = DispatchProxy.Create<TService, Dispatcher<TService>>();
            var dispatcher = proxy as Dispatcher<TService>;
            
            if (dispatcher == null)
                throw new InvalidOperationException("Dispatcher<TService> not found");
            
            dispatcher.Configure(decorated, sp, pipeline);
            return proxy;
        });
        
        return services;
    }
}
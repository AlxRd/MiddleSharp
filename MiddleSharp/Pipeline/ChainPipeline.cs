using MiddleSharp.Context;

namespace MiddleSharp.Pipeline;

/// <summary>
/// Represents a delegate defining the execution of a single member in a middleware pipeline for processing.
/// </summary>
/// <param name="context">The context of the current middleware operation, containing the target object, method, arguments, and other contextual information required for processing.</param>
/// <returns>A task representing the asynchronous operation of the pipeline member.</returns>
public delegate Task PipelineMember(ChainContext context);

/// <summary>
/// Represents a middleware component within a chain-processing pipeline.
/// Pipeline middleware is a delegate that takes a <see cref="ChainContext"/> object,
/// processes some logic, and then calls the next middleware in the pipeline.
/// </summary>
/// <param name="context">
/// Represents the method invocation context, containing information such as the target service,
/// method to be called, arguments, and a service provider.
/// </param>
/// <param name="next">
/// The delegate to invoke the next middleware in the pipeline.
/// The next middleware can be invoked after or during the current middleware logic.
/// It allows chaining of multiple middleware components in a sequence.
/// </param>
/// <returns>
/// A <see cref="Task"/> representing the asynchronous operation of the middleware processing.
/// </returns>
public delegate Task PipelineMiddleware(ChainContext context, PipelineMember next);

/// <summary>
/// Represents a configurable pipeline for chaining middleware that can process,
/// modify, or execute in the context of a service method invocation.
/// </summary>
/// <remarks>
/// The <c>ChainPipeline</c> class allows constructing a sequence of middleware components,
/// where each middleware can perform its own processing. Middleware is executed in the order
/// it is added and is passed a <c>ChainContext</c> along with the next middleware in the chain.
/// Each middleware can modify the <c>ChainContext</c>, handle exceptions,
/// or impact the execution of the pipeline. The final built pipeline delegates the service
/// method execution and result handling.
/// Middleware must conform to the <c>PipelineMiddleware</c> delegate structure.
/// The pipeline execution is finalized using the <see cref="Build"/> method.
/// </remarks>
/// <example>
/// <para>
/// This class is commonly used in scenarios where dynamic behaviors need to be injected
/// at runtime for service methods, such as logging, authentication, or any cross-cutting concerns.
/// </para>
/// <para>
/// The pipeline is particularly useful in combination with dependency injection frameworks,
/// allowing for configurable and extendable middleware composition.
/// </para>
/// </example>
public class ChainPipeline
{
    /// <summary>
    /// A collection of middleware delegates that form the processing pipeline.
    /// Each middleware in the collection processes the context and optionally
    /// invokes the next middleware in the pipeline.
    /// </summary>
    private readonly List<PipelineMiddleware> _middlewares = [];

    /// Adds middleware to the pipeline.
    /// <param name="middleware">
    /// The middleware to be added, which takes a <see cref="ChainContext"/> and a next <see cref="PipelineMember"/> as its parameters.
    /// </param>
    /// <return>
    /// Returns the current instance of <see cref="ChainPipeline"/> to allow for method chaining.
    /// </return>
    public ChainPipeline Use(PipelineMiddleware middleware)
    {
        _middlewares.Add(middleware);
        return this;
    }

    /// Builds and returns a delegate representing the full middleware pipeline.
    /// This delegate invokes each middleware in the pipeline in reverse order,
    /// starting from the last middleware added and ending with the default processing logic.
    /// The returned delegate processes a ChainContext, handling method invocation,
    /// setting the return value, or capturing exceptions as needed.
    /// <returns>
    /// A PipelineMember delegate that when invoked, processes the ChainContext
    /// through the configured middleware pipeline.
    /// </returns>
    public PipelineMember Build()
    {
        PipelineMember next = async context =>
        {
            try
            {
                context.ReturnValue = context.Method.Invoke(context.Service, context.Arguments);

                if (context.ReturnValue is Task task)
                {
                    await task;
                    if (context.Method.ReturnType.IsGenericType)
                        context.ReturnValue = task.GetType().GetProperty("Result")?.GetValue(task);
                }
            }
            catch (Exception e)
            {
                context.Exception = e.InnerException ?? e;
            }
        };

        foreach (var middleware in _middlewares.AsEnumerable().Reverse())
        {
            var current = next;
            next = context => middleware(context, current);
        }
        
        return next;
    }
}
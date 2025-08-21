using System.Reflection;

namespace MiddleSharp.Context;

/// <summary>
/// Encapsulates the context for a method invocation in a middleware pipeline.
/// Provides information about the target object, method being invoked,
/// arguments passed, and other contextual data for middleware processing.
/// </summary>
public class ChainContext(object service, MethodInfo method, object?[] arguments, IServiceProvider serviceProvider)
{
    /// <summary>
    /// Gets the service instance associated with the current <see cref="ChainContext"/>.
    /// This property provides access to the object on which the method, represented by the
    /// <see cref="Method"/> property, is invoked during execution of the pipeline.
    /// </summary>
    public object Service { get; } = service;

    /// <summary>
    /// Gets the <see cref="MethodInfo"/> instance representing the method invoked within the chain context.
    /// </summary>
    /// <remarks>
    /// This property provides metadata about the method being executed, including its name, return type, parameters, and other details.
    /// It is particularly useful in scenarios where you need to log, monitor, or manipulate the behavior of the method invocation.
    /// </remarks>
    public MethodInfo Method { get; } = method;

    /// <summary>
    /// Gets the array of arguments passed to the method being invoked within the pipeline context.
    /// Represents the input parameters for the method execution.
    /// </summary>
    public object?[] Arguments { get; } = arguments;

    /// <summary>
    /// Gets or sets the return value of the invoked method within the context of the middleware pipeline.
    /// </summary>
    /// <remarks>
    /// This property holds the result of the method invocation. It is populated after the method
    /// specified in the <see cref="Method"/> property of <see cref="ChainContext"/> is executed.
    /// If the method returns a Task, this property will store the result of the Task upon completion.
    /// The property can be modified during the pipeline execution if needed.
    /// </remarks>
    public object? ReturnValue { get; set; }

    /// <summary>
    /// Gets or sets the exception that occurred during the execution of middleware or pipeline operations.
    /// </summary>
    /// <remarks>
    /// This property is primarily used to capture and handle exceptions encountered in the pipeline or middleware chain.
    /// Setting this property allows the propagation of the captured exception, which can later be re-thrown or handled appropriately.
    /// If no exception occurred, this property remains null.
    /// </remarks>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Provides access to the <see cref="IServiceProvider"/> within the execution context.
    /// </summary>
    /// <remarks>
    /// The <see cref="ServiceProvider"/> property is used to resolve services and dependencies
    /// that are required during the execution of a middleware or any component of the pipeline.
    /// </remarks>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}
using System.Reflection;
using System.Runtime.ExceptionServices;
using MiddleSharp.Context;
using MiddleSharp.Pipeline;

namespace MiddleSharp;

/// <summary>
/// Represents a dynamic proxy dispatcher that intercepts method calls on a given decorated instance,
/// executes a middleware pipeline, and handles synchronous and asynchronous method invocations.
/// </summary>
/// <typeparam name="T">The type of the interface being proxied. Must be a class.</typeparam>
public class Dispatcher<T> : DispatchProxy where T : class
{
    /// <summary>
    /// Represents the instance of the proxied class that is decorated by the dispatcher.
    /// This variable holds the implementation of the target class provided during
    /// the configuration of the dispatcher and is used for method invocation through the pipeline.
    /// </summary>
    private T _decorated = null!;

    /// <summary>
    /// Represents an instance of <see cref="IServiceProvider"/> used to resolve required services and dependencies.
    /// This variable is used during the execution of the method invocation pipeline to provide dependency injection capabilities.
    /// </summary>
    private IServiceProvider _serviceProvider = null!;

    /// <summary>
    /// Represents the pipeline associated with the dispatcher. The pipeline is responsible for managing
    /// and executing the chain of middleware and executing the final method target in a specified context.
    /// </summary>
    /// <remarks>
    /// This pipeline is implemented using the <see cref="ChainPipeline"/> class, which allows middleware
    /// to be defined and executed in sequence. It is built dynamically and plays a critical role in
    /// the invocation process of the dispatcher.
    /// </remarks>
    /// <seealso cref="ChainPipeline"/>
    private ChainPipeline _pipeline = null!;

    /// <summary>
    /// Configures the dispatcher with the necessary parts to intercept method calls
    /// and execute a middleware pipeline.
    /// </summary>
    /// <param name="decorated">The wrapped instance of the service being decorated.</param>
    /// <param name="serviceProvider">The service provider used for resolving dependencies.</param>
    /// <param name="pipeline">The chain pipeline that contains a sequence of middleware components.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="decorated"/>, <paramref name="serviceProvider"/>, or <paramref name="pipeline"/> is null.
    /// </exception>
    public void Configure(T decorated, IServiceProvider serviceProvider, ChainPipeline pipeline)
    {
        _decorated = decorated ?? throw new ArgumentNullException(nameof(decorated));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
    }

    /// <summary>
    /// Invokes the specified method on the decorated service instance, executing through a middleware pipeline configured in the dispatcher.
    /// </summary>
    /// <param name="targetMethod">The method to invoke on the decorated service instance.</param>
    /// <param name="args">An array of arguments to pass to the invoked method, or null if no arguments are provided.</param>
    /// <returns>
    /// The result of the invoked method. If the method is asynchronous, it returns a Task or Task <see cref="TResult"/> instance.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the dispatcher has not been properly configured before invocation.</exception>
    /// <exception cref="ArgumentNullException">Thrown if the provided target method is null.</exception>
    /// <exception cref="ExceptionDispatchInfo">Throws any exception captured during the middleware pipeline execution.</exception>
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (_decorated == null || _serviceProvider == null || _pipeline == null)
            throw new InvalidOperationException("Dispatcher is not configured");

        ArgumentNullException.ThrowIfNull(targetMethod);

        var context = new ChainContext(_decorated, targetMethod, args ?? [], _serviceProvider);

        var pipeline = _pipeline.Build();

        if (typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
            return InvokeAsyncMethod(targetMethod, context, pipeline);

        pipeline(context).GetAwaiter().GetResult();

        if (context.Exception == null) return context.ReturnValue;

        ExceptionDispatchInfo.Capture(context.Exception).Throw();
        return null;
    }

    /// <summary>
    /// Invokes an asynchronous method, processes its execution pipeline, and handles its return type.
    /// </summary>
    /// <param name="targetMethod">The reflection <see cref="MethodInfo"/> instance representing the method to be invoked.</param>
    /// <param name="context">The execution context encapsulated in a <see cref="ChainContext"/> object.</param>
    /// <param name="pipeline">A delegate representing the pipeline to be executed for processing the <paramref name="context"/>.</param>
    /// <returns>
    /// An object representing the result of the invoked asynchronous method.
    /// This could be a <see cref="Task"/> or <see cref="Task{TResult}"/>, depending on the method's original return type.
    /// </returns>
    private object InvokeAsyncMethod(MethodInfo targetMethod, ChainContext context, PipelineMember pipeline)
    {
        var isGenericTask = targetMethod.ReturnType.IsGenericType &&
                            targetMethod.ReturnType.GetGenericTypeDefinition() == typeof(Task<>) &&
                            targetMethod.ReturnType.GetGenericArguments().Length > 0;
        
        var pipelineTask = pipeline(context);

        if (!isGenericTask) return CreateNonGenericResultTask(pipelineTask, context);
        
        var resultType = targetMethod.ReturnType.GetGenericArguments()[0];

        var createResultTaskMethod = typeof(Dispatcher<T>)
            .GetMethod(nameof(CreateResultTask), BindingFlags.NonPublic | BindingFlags.Static);
            
        if (createResultTaskMethod == null)
            throw new InvalidOperationException("CreateResultTask method not found");
        
        var genericMethod = createResultTaskMethod.MakeGenericMethod(resultType);
            
        return genericMethod.Invoke(this, [pipelineTask, context])!;

    }

    /// <summary>
    /// Creates a task that produces a result of the specified type after executing the provided pipeline task
    /// and updating the provided context.
    /// </summary>
    /// <typeparam name="TResult">The type of the result produced by the task.</typeparam>
    /// <param name="pipelineTask">
    /// The task that represents the pipeline processing. It is awaited to ensure the pipeline completes
    /// before generating the result.
    /// </param>
    /// <param name="context">
    /// The execution context that contains information about the current pipeline execution, including arguments,
    /// return values, services, and any exceptions thrown during processing.
    /// </param>
    /// <returns>
    /// A task that produces a result of type <typeparamref name="TResult"/>. If an exception occurs during
    /// pipeline execution, it will propagate when the task is awaited.
    /// </returns>
    private static async Task<TResult> CreateResultTask<TResult>(Task pipelineTask, ChainContext context)
    {
        await pipelineTask.ConfigureAwait(false);
        
        if (context.Exception != null)
            throw context.Exception;
        
        return (TResult)context.ReturnValue!;
    }

    /// <summary>
    /// Creates a non-generic task that completes once the provided pipeline task finishes.
    /// If an exception is captured in the context, it will be thrown after the task completes.
    /// </summary>
    /// <param name="pipelineTask">The task representing the pipeline execution.</param>
    /// <param name="context">The execution context containing the target method, arguments, and any resulting data or exceptions.</param>
    /// <returns>A task that completes when the pipeline execution finishes asynchronously.</returns>
    private static async Task CreateNonGenericResultTask(Task pipelineTask, ChainContext context)
    {
        await pipelineTask.ConfigureAwait(false);
        
        if (context.Exception != null)
            throw context.Exception;
    }
}

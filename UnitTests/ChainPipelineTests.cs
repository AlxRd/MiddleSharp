using System.Globalization;
using System.Reflection;
using MiddleSharp.Context;
using MiddleSharp.Pipeline;

namespace UnitTests;

[TestFixture]
public class ChainPipelineTests
{
    [Test]
    public void Use_AddsMiddlewareToPipeline_ReturnsSameInstance()
    {
        // Arrange
        var pipeline = new ChainPipeline();

        // Act
        var result = pipeline.Use(Middleware);

        // Assert
        Assert.That(result, Is.SameAs(pipeline));
        return;

        Task Middleware(ChainContext context, PipelineMember next) => next(context);
    }

    [Test]
    public async Task Build_WithNoMiddleware_ExecutesMethod()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        var context = CreateContext();

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(context.ReturnValue, Is.EqualTo("Test"));
            Assert.That(context.Exception, Is.Null);
        });
    }

    [Test]
    public async Task Build_WithMiddleware_ExecutesMiddlewares()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        var executionOrder = 0;
        var firstMiddleware = 0;
        var secondMiddleware = 0;

        pipeline.Use((context, next) =>
        {
            firstMiddleware = ++executionOrder;
            return next(context);
        });

        pipeline.Use((context, next) =>
        {
            secondMiddleware = ++executionOrder;
            return next(context);
        });

        var context = CreateContext();

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(firstMiddleware, Is.EqualTo(1));
            Assert.That(secondMiddleware, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Build_WhenMethodThrowsException_CapturesException()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        var exception = new InvalidOperationException("Test exception");
        var context = CreateContext(shouldThrow: true, exceptionToThrow: exception);

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        // Assert
        Assert.That(context.Exception, Is.SameAs(exception));
    }

    [Test]
    public async Task Build_WithAsyncMethod_AwaitsTasks()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        var context = CreateAsyncContext();

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        // Assert
        Assert.That(context.ReturnValue, Is.EqualTo("AsyncTest"));
    }

    [Test]
    public async Task Build_WithMiddlewareModifyingContext_AppliesChanges()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        var context = CreateContext();

        pipeline.Use(async (ctx, next) =>
        {
            await next(ctx);
            ctx.ReturnValue = "Modified";
        });

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        // Assert
        Assert.That(context.ReturnValue, Is.EqualTo("Modified"));
    }

    [Test]
    public async Task Build_WithMiddlewareShortCircuiting_DoesNotInvokeMethod()
    {
        // Arrange
        var pipeline = new ChainPipeline();
        bool methodInvoked = false;

        pipeline.Use((ctx, _) =>
        {
            ctx.ReturnValue = "ShortCircuited";
            return Task.CompletedTask;
        });
        
        var service = new TestService();
        var customMethod = new TestMethod(() => { methodInvoked = true; return "Test"; });
        var context = new ChainContext(service, customMethod, [], null!);

        // Act
        var pipelineMember = pipeline.Build();
        await pipelineMember(context);

        Assert.Multiple(() =>
        {
            // Assert
            Assert.That(context.ReturnValue, Is.EqualTo("ShortCircuited"));
            Assert.That(methodInvoked, Is.False);
        });
    }

    private ChainContext CreateContext(bool shouldThrow = false, Exception exceptionToThrow = null!)
    {
        var service = new TestService { ShouldThrow = shouldThrow, ExceptionToThrow = exceptionToThrow };
        var method = typeof(TestService).GetMethod("GetValue");

        return new ChainContext(service, method!, [], null!);
    }

    private ChainContext CreateAsyncContext()
    {
        var service = new TestService();
        var method = typeof(TestService).GetMethod("GetValueAsync");

        return new ChainContext(service, method!, [], null!);
    }

    private class TestService
    {
        public bool ShouldThrow { get; init; }
        public Exception ExceptionToThrow { get; init; } = null!;

        public string GetValue()
        {
            return ShouldThrow ? throw ExceptionToThrow : "Test";
        }

        public async Task<string> GetValueAsync()
        {
            await Task.Delay(10);
            return "AsyncTest";
        }
    }

    private class TestMethod(Func<string> implementation) : MethodInfo
    {
        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        {
            return implementation();
        }

        public override MethodInfo GetBaseDefinition()
        {
            return this;
        }

        public override MethodAttributes Attributes => throw new NotImplementedException();
        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();
        public override Type DeclaringType => typeof(TestService);
        public override string Name => "GetValue";
        public override Type ReflectedType => throw new NotImplementedException();
        public override ParameterInfo[] GetParameters() => [];
        public override object[] GetCustomAttributes(bool inherit) => [];
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => [];
        public override bool IsDefined(Type attributeType, bool inherit) => false;
        public override ICustomAttributeProvider ReturnTypeCustomAttributes => throw new NotImplementedException();
        public override Type ReturnType => typeof(string);
        public override MethodImplAttributes GetMethodImplementationFlags() => throw new NotImplementedException();
    }
}
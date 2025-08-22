using System.Reflection;
using MiddleSharp;
using MiddleSharp.Pipeline;
using Moq;

namespace UnitTests;

[TestFixture]
public class DispatcherTests
{
    private ITestService _dispatcher;
    private Mock<ITestService> _decorated;
    private Mock<IServiceProvider> _serviceProvider;
    private PipelineMember _pipelineDelegate;

    [SetUp]
    public void Setup()
    {
        _decorated = new Mock<ITestService>();
        _serviceProvider = new Mock<IServiceProvider>();
        _pipelineDelegate = context => Task.CompletedTask;

        var stubPipeline = new TestChainPipeline(_pipelineDelegate);

        _dispatcher = DispatchProxy.Create<ITestService, Dispatcher<ITestService>>();
        (_dispatcher as Dispatcher<ITestService>).Configure(_decorated.Object, _serviceProvider.Object, stubPipeline);
    }

    private class TestChainPipeline(PipelineMember pipelineToReturn) : ChainPipeline
    {
        public new PipelineMember Build()
        {
            return pipelineToReturn;
        }
    }

    [Test]
    public async Task Invoke_WithNonGenericTaskMethod_ExecutesPipelineAndReturnsTask()
    {
        // Arrange
        _decorated.Setup(s => s.DoWorkAsync()).Returns(Task.CompletedTask);

        // Act
        var result = _dispatcher.DoWorkAsync();

        // Assert
        Assert.That(result, Is.AssignableTo<Task>());
        await result;
    }

    [Test]
    public async Task Invoke_WithGenericTaskMethod_ExecutesPipelineAndReturnsGenericTask()
    {
        // Arrange
        const string expectedResult = "test data";
        _decorated.Setup(s => s.GetDataAsync()).ReturnsAsync(expectedResult);

        // Act
        var result = await _dispatcher.GetDataAsync();

        // Assert
        Assert.That(result, Is.EqualTo(expectedResult));
    }

    [Test]
    public void Invoke_WhenDispatcherNotConfigured_ThrowsInvalidOperationException()
    {
        // Arrange
        var unconfiguredDispatcher = DispatchProxy.Create<ITestService, Dispatcher<ITestService>>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => unconfiguredDispatcher.DoWorkAsync());
    }

    public interface ITestService
    {
        Task DoWorkAsync();
        Task<string> GetDataAsync();
    }
}
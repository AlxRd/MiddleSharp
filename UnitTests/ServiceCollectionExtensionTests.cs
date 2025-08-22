using Microsoft.Extensions.DependencyInjection;
using MiddleSharp.Context;
using MiddleSharp.Pipeline;
using MiddleSharp.Registry;

namespace UnitTests;

[TestFixture]
public class ServiceCollectionExtensionTests
{
    private IServiceCollection _serviceCollection;
        
        [SetUp]
        public void Setup()
        {
            _serviceCollection = new ServiceCollection();
        }

        [Test]
        public void UseGlobalMiddleware_AddsMiddlewareToGlobalPipeline()
        {
            // Act
            var result = _serviceCollection.UseGlobalMiddleware(Middleware);
            
            // Assert
            Assert.That(result, Is.SameAs(_serviceCollection));
            return;

            // Arrange
            Task Middleware(ChainContext context, PipelineMember next) => next(context);
        }
        
        [Test]
        public void UseGlobalMiddleware_WithNullMiddleware_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => _serviceCollection.UseGlobalMiddleware(null!));
        }
        
        [Test]
        public void AddChained_RegistersServiceAndImplementation()
        {
            // Act
            _serviceCollection.AddChained<ITestService, TestService>();
            
            // Assert
            var provider = _serviceCollection.BuildServiceProvider();
            var service = provider.GetService<ITestService>();
            Assert.That(service, Is.Not.Null);
            Assert.That(service, Is.InstanceOf<ITestService>());
        }
        
        [Test]
        public void AddChained_WithConfigureAction_AppliesConfiguration()
        {
            // Arrange
            var configureInvoked = false;
            
            // Act
            _serviceCollection.AddChained<ITestService, TestService>(_ => 
            {
                configureInvoked = true;
            });
            
            // Assert
            Assert.That(configureInvoked, Is.True);
        }
        
        [Test]
        public void AddChained_AppliesGlobalMiddlewares()
        {
            // Arrange
            var middlewareExecuted = false;
            _serviceCollection.UseGlobalMiddleware((context, next) => 
            {
                middlewareExecuted = true;
                return next(context);
            });
            
            // Act
            _serviceCollection.AddChained<ITestService, TestService>();
            
            // Assert
            var provider = _serviceCollection.BuildServiceProvider();
            var service = provider.GetService<ITestService>();
            service!.DoSomething();
            Assert.That(middlewareExecuted, Is.True);
        }

        [Test]
        public void AddChained_ReturnsServiceCollection_ForMethodChaining()
        {
            // Act
            var result = _serviceCollection.AddChained<ITestService, TestService>();
            
            // Assert
            Assert.That(result, Is.SameAs(_serviceCollection));
        }

        private interface ITestService
        {
            void DoSomething();
        }

        private class TestService : ITestService
        {
            public void DoSomething() { }
        }
}
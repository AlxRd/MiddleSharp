# MiddleSharp

This is a dynamic middleware system for C# services using dependency injection and proxies. It allows intercepting method calls, executing middleware pipelines and handling synchronous and asynchronous invocations.

## Description

MiddleSharp provides a flexible way to add middleware to services registered in `IServiceCollection`. It uses dynamic proxies to intercept methods and execute a chain of middlewares before and after the actual method invocation.

## Characteristics

- Methods interception using dynamic proxies.
- Configurable middleware pipeline.
- Support for synchronous and asynchronous methods (`Task` and `Task<T>`).
- Integration with the .NET dependency injection system.
- Global and per-service middleware.

## Installation

Install MiddleSharp through NuGet:

```bash
dotnet add package MiddleSharp
```

## Usage

### Middleware registration

For global middleware, use the `UseGlobalMiddleware` extension method:
```csharp
services.UseGlobalMiddleware(Middleware.Invoke);
```

For per-service middleware, use the `AddChained` extension method:

```csharp
services.AddChained<IService, Service>(pipeline =>
{
    pipeline.Use(Middleware.Invoke);
});
```

## Main Components

- `Dispatcher<T>`: Dynamic proxy to run the middleware pipeline.
- `ChainPipeline`: Allows building and chain middlewares.
- `ChainContext`: Execution context for each method invocation.
- `ServiceCollectionExtension`: Extensions to register services and middlewares.

## Licence

MIT

---

For additional details, please refer to the source code documentation.
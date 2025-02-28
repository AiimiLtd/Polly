# Fallback resilience strategy

## About

- **Options**: [`FallbackStrategyOptions<T>`](xref:Polly.Fallback.FallbackStrategyOptions`1)
- **Extensions**: `AddFallback`
- **Strategy Type**: Reactive

---

## Usage

<!-- snippet: fallback -->
```cs
// Add a fallback/substitute value if an operation fails.
new ResiliencePipelineBuilder<UserAvatar>()
    .AddFallback(new FallbackStrategyOptions<UserAvatar>
    {
        ShouldHandle = new PredicateBuilder<UserAvatar>()
            .Handle<SomeExceptionType>()
            .HandleResult(r => r is null),
        FallbackAction = args => Outcome.FromResultAsValueTask(UserAvatar.Blank)
    });

// Use a dynamically generated value if an operation fails.
new ResiliencePipelineBuilder<UserAvatar>()
    .AddFallback(new FallbackStrategyOptions<UserAvatar>
    {
        ShouldHandle = new PredicateBuilder<UserAvatar>()
            .Handle<SomeExceptionType>()
            .HandleResult(r => r is null),
        FallbackAction = args =>
        {
            var avatar = UserAvatar.GetRandomAvatar();
            return Outcome.FromResultAsValueTask(avatar);
        }
    });

// Use a default or dynamically generated value, and execute an additional action if the fallback is triggered.
new ResiliencePipelineBuilder<UserAvatar>()
    .AddFallback(new FallbackStrategyOptions<UserAvatar>
    {
        ShouldHandle = new PredicateBuilder<UserAvatar>()
            .Handle<SomeExceptionType>()
            .HandleResult(r => r is null),
        FallbackAction = args =>
        {
            var avatar = UserAvatar.GetRandomAvatar();
            return Outcome.FromResultAsValueTask(UserAvatar.Blank);
        },
        OnFallback = args =>
        {
            // Add extra logic to be executed when the fallback is triggered, such as logging.
            return default; // Returns an empty ValueTask
        }
    });
```
<!-- endSnippet -->

## Defaults

| Property         | Default Value                                                              | Description                                                                                 |
| ---------------- | -------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `ShouldHandle`   | Predicate that handles all exceptions except `OperationCanceledException`. | Predicate that determines what results and exceptions are handled by the fallback strategy. |
| `FallbackAction` | `Null`, **Required**                                                       | Fallback action to be executed.                                                             |
| `OnFallback`     | `null`                                                                     | Event that is raised when fallback happens.                                                 |

## Diagrams

### Happy path sequence diagram

```mermaid
sequenceDiagram
    actor C as Caller
    participant P as Pipeline
    participant F as Fallback
    participant D as DecoratedUserCallback

    C->>P: Calls ExecuteAsync
    P->>F: Calls ExecuteCore
    F->>+D: Invokes
    D->>-F: Returns result
    F->>P: Returns result
    P->>C: Returns result
```

### Unhappy path sequence diagram

```mermaid
sequenceDiagram
    actor C as Caller
    participant P as Pipeline
    participant F as Fallback
    participant FA as FallbackAction
    participant D as DecoratedUserCallback

    C->>P: Calls ExecuteAsync
    P->>F: Calls ExecuteCore
    F->>+D: Invokes
    D->>-F: Fails
    F->>+FA: Invokes
    FA-->>FA: Calculates substitute result
    FA->>-F: Returns <br/>substituted result
    F->>P: Returns <br/>substituted result
    P->>C: Returns <br/>substituted result
```

## Patterns

### Fallback after retries

When designing resilient systems, a common pattern is to use a fallback after multiple failed retry attempts. This approach is especially relevant when a fallback strategy can provide a sensible default value.

<!-- snippet: fallback-after-retries -->
```cs
// Define a common predicates re-used by both fallback and retries
var predicateBuilder = new PredicateBuilder<HttpResponseMessage>()
    .Handle<HttpRequestException>()
    .HandleResult(r => r.StatusCode == HttpStatusCode.InternalServerError);

var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddFallback(new()
    {
        ShouldHandle = predicateBuilder,
        FallbackAction = args =>
        {
            // Try to resolve the fallback response
            HttpResponseMessage fallbackResponse = ResolveFallbackResponse(args.Outcome);

            return Outcome.FromResultAsValueTask(fallbackResponse);
        }
    })
    .AddRetry(new()
    {
        ShouldHandle = predicateBuilder,
        MaxRetryAttempts = 3,
    })
    .Build();

// Demonstrative execution that always produces invalid result
pipeline.Execute(() => new HttpResponseMessage(HttpStatusCode.InternalServerError));
```
<!-- endSnippet -->

Here's a breakdown of the behavior when the callback produces either an `HttpStatusCode.InternalServerError` or an `HttpRequestException`:

- The fallback strategy initiates by executing the provided callback, then immediately passes the execution to the retry strategy.
- The retry strategy starts execution, makes 3 retry attempts and yields the outcome that represents an error.
- The fallback strategy resumes execution, assesses the outcome generated by the callback, and if necessary, supplies the fallback value.
- The fallback strategy completes its execution.

> [!NOTE]
> The preceding example also demonstrates how to re-use `ResiliencePipelineBuilder<HttpResponseMessage>` across multiple strategies.

## Anti-patterns

Over the years, many developers have used Polly in various ways. Some of these recurring patterns may not be ideal. This section highlights the recommended practices and ones to avoid.

### 1 - Using fallback to replace thrown exception

❌ DON'T

Throw custom exceptions from the `OnFallback` delegate:

<!-- snippet: fallback-anti-pattern-1 -->
```cs
var fallback = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddFallback(new()
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>().Handle<HttpRequestException>(),
        FallbackAction = args => Outcome.FromResultAsValueTask(new HttpResponseMessage()),
        OnFallback = args => throw new CustomNetworkException("Replace thrown exception", args.Outcome.Exception!)
    })
    .Build();
```
<!-- endSnippet -->

**Reasoning**:

Throwing an exception from a user-defined delegate can disrupt the normal control flow.

✅ DO

Use `ExecuteOutcomeAsync` and then evaluate the `Exception`:

<!-- snippet: fallback-pattern-1 -->
```cs
var outcome = await WhateverPipeline.ExecuteOutcomeAsync(Action, context, "state");
if (outcome.Exception is HttpRequestException requestException)
{
    throw new CustomNetworkException("Replace thrown exception", requestException);
}
```
<!-- endSnippet -->

**Reasoning**:

This method lets you execute the strategy or pipeline smoothly, without unexpected interruptions. If you repeatedly find yourself writing this exception "remapping" logic, consider marking the method you wish to decorate as `private` and expose the "remapping" logic publicly.

<!-- snippet: fallback-pattern-1-ext -->
```cs
public static async ValueTask<HttpResponseMessage> Action()
{
    var context = ResilienceContextPool.Shared.Get();
    var outcome = await WhateverPipeline.ExecuteOutcomeAsync<HttpResponseMessage, string>(
        async (ctx, state) =>
        {
            var result = await ActionCore();
            return Outcome.FromResult(result);
        }, context, "state");

    if (outcome.Exception is HttpRequestException requestException)
    {
        throw new CustomNetworkException("Replace thrown exception", requestException);
    }

    ResilienceContextPool.Shared.Return(context);
    return outcome.Result!;
}

private static ValueTask<HttpResponseMessage> ActionCore()
{
    // The core logic
    return ValueTask.FromResult(new HttpResponseMessage());
}
```
<!-- endSnippet -->

### 2 - Using retry for fallback

Suppose you have a primary and a secondary endpoint. If the primary fails, you want to call the secondary.

❌ DON'T

Use retry for fallback:

<!-- snippet: fallback-anti-pattern-2 -->
```cs
var fallback = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(new()
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(res => res.StatusCode == HttpStatusCode.RequestTimeout),
        MaxRetryAttempts = 1,
        OnRetry = async args =>
        {
            args.Context.Properties.Set(fallbackKey, await CallSecondary(args.Context.CancellationToken));
        }
    })
    .Build();

var context = ResilienceContextPool.Shared.Get();
var outcome = await fallback.ExecuteOutcomeAsync<HttpResponseMessage, string>(
    async (ctx, state) =>
    {
        var result = await CallPrimary(ctx.CancellationToken);
        return Outcome.FromResult(result);
    }, context, "none");

var result = outcome.Result is not null
    ? outcome.Result
    : context.Properties.GetValue(fallbackKey, default);

ResilienceContextPool.Shared.Return(context);

return result;
```
<!-- endSnippet -->

**Reasoning**:

A retry strategy by default executes the same operation up to `N` times, where `N` equals the initial attempt plus `MaxRetryAttempts`. In this case, that means **2** times. Here, the fallback is introduced as a side effect rather than a replacement.

✅ DO

Use fallback to call the secondary:

<!-- snippet: fallback-pattern-2 -->
```cs
var fallback = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddFallback(new()
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(res => res.StatusCode == HttpStatusCode.RequestTimeout),
        OnFallback = async args => await CallSecondary(args.Context.CancellationToken)
    })
    .Build();

return await fallback.ExecuteAsync(CallPrimary, CancellationToken.None);
```
<!-- endSnippet -->

**Reasoning**:

- The target code is executed only once.
- The fallback value is returned directly, eliminating the need for additional code like `Context` or `ExecuteOutcomeAsync()`.

### 3 - Nesting `ExecuteAsync` calls

Combining multiple strategies can be achieved in various ways. However, deeply nesting `ExecuteAsync` calls can lead to what's commonly referred to as _`Execute` Hell_.

> [!NOTE]
> While this isn't strictly tied to the Fallback mechanism, it's frequently observed when Fallback is the outermost layer.

❌ DON'T

Nest `ExecuteAsync` calls:

<!-- snippet: fallback-anti-pattern-3 -->
```cs
var result = await fallback.ExecuteAsync(async (CancellationToken outerCT) =>
{
    return await timeout.ExecuteAsync(static async (CancellationToken innerCT) =>
    {
        return await CallExternalSystem(innerCT);
    }, outerCT);
}, CancellationToken.None);

return result;
```
<!-- endSnippet -->

**Reasoning**:

This is akin to JavaScript's [callback hell](http://callbackhell.com/) or _[the pyramid of doom](https://en.wikipedia.org/wiki/Pyramid_of_doom_(programming))_. It's easy to mistakenly reference the wrong `CancellationToken` parameter.

✅ DO

Use `ResiliencePipelineBuilder` to chain strategies:

<!-- snippet: fallback-pattern-3 -->
```cs
var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddPipeline(timeout)
    .AddPipeline(fallback)
    .Build();

return await pipeline.ExecuteAsync(CallExternalSystem, CancellationToken.None);
```
<!-- endSnippet -->

**Reasoning**:

In this approach, we leverage the escalation mechanism provided by Polly rather than creating our own through nesting. `CancellationToken` values are automatically propagated between the strategies for you.

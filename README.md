# Ozakboy.Core.Abstractions

Zero-dependency .NET building blocks for systems that need exact decimal arithmetic and explicit error handling.

English | [繁體中文](README_zh-TW.md)

```
dotnet add package Ozakboy.Core.Abstractions
```

Requires .NET 10. Nothing outside the BCL.

---

## What is in it

Four types, each solving a problem that actually bit us.

| Type | Problem it solves |
| --- | --- |
| `Result` / `Result<T>` | Expected failures should not travel as exceptions |
| `Money` | Amounts in different currencies get added, and the type system says nothing |
| `Precision` | Decimal step alignment, and string serialisation for APIs |
| `RetryPolicy` | Describing a retry strategy and computing backoff intervals |

Written for an automated trading system, but nothing about trading leaked in. Any project can use it.

---

## Result: getting expected failures out of exceptions

Network timeouts, rate limiting, arguments that fail validation — these happen every day. They are not exceptional. Modelling them as exceptions forces callers to write control flow inside `try/catch`, and turns "forgot to handle it" into a runtime surprise.

```csharp
Result<Order> Submit(OrderRequest request)
{
    if (request.Quantity <= 0)
    {
        return Error.Validation("order.quantity_invalid", "Quantity must be positive");
    }

    return new Order(request);   // implicit conversion; just return the value
}
```

On the reading side:

```csharp
var result = Submit(request);

if (result.TryGetValue(out var order))
{
    Console.WriteLine($"Submitted {order.Id}");
}
else
{
    // The compiler knows Error is not null here
    Console.WriteLine(result.Error.Message);
}
```

`IsSuccess` and `IsFailure` both carry `MemberNotNullWhen`, so after a check the compiler knows whether `Error` is null. No null-forgiving operator needed.

Chains short-circuit on failure, and the intermediate delegates never run:

```csharp
var report = LoadAccount(id)
    .Ensure(a => a.IsActive, Error.Conflict("account.inactive", "Account is disabled"))
    .Map(a => a.Balance)
    .Then(BuildReport);
```

**`default(Result<T>)` is a failure** carrying `Error.Uninitialized`. That is deliberate: an unassigned result mistaken for success hides for a long time.

Genuine defects should still throw. `Result` is not a replacement for exceptions.

### Async chains

The combinators above take synchronous delegates, so a chain breaks the moment one step is asynchronous. The `*Async` extensions accept both a `Task<Result<T>>` receiver and asynchronous continuations, so the chain survives:

```csharp
var name = await LoadAccountAsync(id)
    .EnsureAsync(a => a.IsActive, Error.Conflict("account.inactive", "Account is disabled"))
    .ThenAsync(FetchProfileAsync)     // Func<Account, Task<Result<Profile>>>
    .MapAsync(p => p.DisplayName)
    .MatchAsync(n => n, e => e.Code);
```

Failures short-circuit here too: no delegate after the first failure is invoked. Every `await` inside uses `ConfigureAwait(false)`.

### Crossing an exception boundary

Some signatures leave no room for a `Result` — `DelegatingHandler.SendAsync` must return `Task<HttpResponseMessage>`, `BackgroundService.ExecuteAsync` must return `Task`. `ResultException` is the one carrier for those:

```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
{
    var result = await _pipeline.SendAsync(request, ct);

    return result.TryGetValue(out var response)
        ? response
        : throw result.Error.ToException();
}
```

```csharp
catch (ResultException ex)
{
    logger.LogWarning("{Code}: {Message}", ex.Error.Code, ex.Error.Message);
    if (ex.Error.IsTransient) { /* ... */ }
}
```

It derives from `InvalidOperationException`, which is also what `ThrowIfFailure()` and `GetValueOrThrow()` throw — so existing `catch (InvalidOperationException)` handlers keep working, and only the code that wants the `Error` back needs to change.

---

## Money: making the currency part of the type

A trading system carries quote-currency balances (USDT) and base-currency quantities (BTC) side by side. Both are `decimal`, and nothing stops you adding them.

```csharp
var balance = new Money(1000m, "USDT");
var position = new Money(0.05m, "BTC");

var wrong = balance + position;   // CurrencyMismatchException
```

Same-currency arithmetic works as you would expect:

```csharp
var fee = balance * 0.0004m;
var net = balance - fee;

Console.WriteLine(net);           // 999.6 USDT
```

`default(Money)` is a currency-less zero that adds to anything, which makes it a convenient accumulator seed:

```csharp
var total = fills.Aggregate(default(Money), (sum, f) => sum + f.Amount);
```

One asymmetry is intentional: `Equals` returns `false` across currencies without throwing, while `CompareTo` throws. Equality must be safe for any input; ordering must not silently mix currencies.

---

## Precision: step alignment and string serialisation

Exchanges define a minimum increment for price and quantity on every instrument, and reject anything that is not aligned to it.

```csharp
Precision.FloorToStep(0.123456m, 0.001m);    // 0.123
Precision.CeilingToStep(0.123456m, 0.001m);  // 0.124
Precision.IsAlignedToStep(0.123m, 0.001m);   // true
```

**Always use `FloorToStep` for quantities.** Rounding up makes the real position larger than the size risk management calculated, quietly increasing exposure.

Serialisation is the other frequent trap. APIs take prices as strings, and `decimal` keeps its trailing zeros:

```csharp
(1.2300m).ToString();              // "1.2300"
Precision.ToPlainString(1.2300m);  // "1.23"
Precision.ToPlainString(0.00000001m);  // "0.00000001", never 1E-08
```

Exponent notation or redundant zeros usually come back as an opaque parameter error that gives no hint of the real cause.

`TryParsePlain` and `ParsePlain` are the inverse, with a guaranteed round trip — and always `InvariantCulture`, which is the part that is easy to leave out and only breaks under a different locale:

```csharp
Precision.TryParsePlain("0.00000001", out var qty);   // true
Precision.TryParsePlain("1E-08", out _);              // false, by design
Precision.ParsePlain("nope");                          // Result<decimal> failure, ErrorCategory.Validation
```

---

## RetryPolicy: describes the strategy, does not run the loop

```csharp
var policy = new RetryPolicy
{
    MaxAttempts = 5,
    BaseDelay = TimeSpan.FromMilliseconds(200),
    MaxDelay = TimeSpan.FromSeconds(30),
    Strategy = BackoffStrategy.Exponential,
    JitterRatio = 0.2,
};

for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
{
    var result = await CallAsync();
    if (result.IsSuccess || !policy.ShouldRetry(attempt, result.Error))
    {
        return result;
    }

    await Task.Delay(policy.GetDelay(attempt));
}
```

It never retries anything and never touches I/O, so one policy applies across transports and the interval arithmetic stays testable without time — `GetDelay(attempt, jitterSample)` takes a fixed jitter sample and gives a fully deterministic result.

Only transient categories (`Timeout`, `Network`, `RateLimited`, `Unavailable`) are considered worth retrying by `ShouldRetry`. `ErrorCategory.Exhausted` is deliberately not one of them: it means the operation used to work but the retry or reconnect budget is spent, so the caller needs a fresh object rather than another attempt.

When transience is not the right test — one 429 carries a `Retry-After` and is worth retrying, another means the IP is banned — set `RetryPredicate`. It **replaces** the `IsTransient` check outright rather than adding to it, so there is only ever one place to look when asking why something was or was not retried:

```csharp
var policy = RetryPolicy.Default with
{
    RetryPredicate = e => e.TryGetInt64("retryAfterMs", out var ms) && ms < 5000,
};
```

`MaxAttempts` still always applies. `RetryPredicate` is excluded from equality, because two lambdas with identical source are never equal and would make two identical policies compare as different.

**Do not retry non-idempotent operations.** A timeout does not mean the request failed to arrive, and resending blindly duplicates the side effect. Use `RetryPolicy.NoRetry` there, and confirm the outcome through an idempotency key plus a follow-up query.

---

## License

MIT

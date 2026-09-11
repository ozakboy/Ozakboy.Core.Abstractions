# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-09-11

Additions driven by two real consumers: `Ozakboy.Http` (an HttpClientFactory pipeline) and `Ozakboy.WebSockets`
(a long-running WebSocket client). Two of the six came from the packages hitting the same wall **independently**,
which is why they belong down here rather than in either package. Nothing was removed or changed; every existing
test still passes.

### Added

- `ResultException` — one exception type for carrying an `Error` across a boundary that cannot return a
  `Result`. **Both consumers arrived at this independently**: `DelegatingHandler.SendAsync` must return
  `Task<HttpResponseMessage>` and `BackgroundService.ExecuteAsync` must return `Task`, so `Ozakboy.Http` had
  already defined its own `HttpPipelineException` to get an `Error` through. With every package inventing its
  own carrier, a consumer ends up catching N exception types to recover an `Error`, each with its own retrieval
  convention that has to be looked up. It derives from `InvalidOperationException`, which is what makes this
  additive: `Result.ThrowIfFailure()` and `Result<T>.GetValueOrThrow()` now throw it instead, and every existing
  `catch (InvalidOperationException)` keeps working unchanged while callers that want the `Error` back catch
  `ResultException`. `Error.ToException()` is the shorthand.
- `ErrorCategory.Exhausted` (= 13, not transient) — the operation used to work, but the retry or reconnect budget
  is spent and the object's lifetime is over. **The second thing both consumers hit independently.**
  `Ozakboy.WebSockets` had to express "out of reconnect attempts" as `Unavailable`, the closest of the thirteen
  existing categories — but `Unavailable` is transient, so `IsTransient` answered wrongly for that error and the
  client was pushed into branching on error codes instead. Once anyone does that, `IsTransient` has stopped being
  the single source of truth for "is this worth retrying", which was the whole point of the property.
  Distinct from `Validation` (the input is wrong) and `NotSupported` (this implementation never could).
  `Error.Exhausted(code, message)` matches the existing factories.
- `ResultExtensions` — asynchronous combinators over `Task<Result<T>>` and `Task<Result>`: `MapAsync`,
  `ThenAsync`, `EnsureAsync`, `MatchAsync`, `ToResultAsync`, each in synchronous and asynchronous delegate forms,
  plus `ThenAsync`/`MapAsync` on a synchronous `Result<T>` so a pipeline that starts synchronously can join
  without a `Task.FromResult` wrapper. From `Ozakboy.Http`: the combinators on `Result<T>` take synchronous
  delegates, so a chain breaks the moment one step is asynchronous — you `await`, inspect, and forward the failure
  by hand. Three fallible steps came out twice as long as they needed to be, and every hand-written forward was
  another chance to miss one. Failures short-circuit; no delegate after the first failure runs. Every `await` uses
  `ConfigureAwait(false)`, which is mandatory under a host with a `SynchronizationContext` — a WPF application sits
  above this package, and without it any synchronous wait up the stack deadlocks.
- `RetryPolicy.RetryPredicate` — an optional `Func<Error, bool>` that **entirely replaces** the `IsTransient`
  check rather than narrowing or widening it. From `Ozakboy.Http`: one 429 carries a `Retry-After` and is worth
  retrying, another means the IP is banned and is not. That judgement could only live in the handler, so the
  policy object had stopped being the single source of truth for the retry decision. Replacement rather than AND
  or OR is deliberate — a mixed rule turns "why was this error not retried" into a question needing two places
  checked. `MaxAttempts` still always applies: the predicate decides whether an error is worth retrying, not
  whether attempts remain. It is excluded from `Equals`/`GetHashCode` for the same reason `Error` excludes
  `Exception`: two lambdas with identical source are different instances and never compare equal, which would make
  two policies with identical settings compare as different.
- `Error.WithData` and the typed readers `TryGetData`, `TryGetDecimal`, `TryGetInt64`, `TryGetBoolean` — from
  `Ozakboy.Http`: `Data` could only be supplied wholesale at construction, so attaching a status code and a body
  meant building a `Dictionary` and then `with`-ing it in. Worse, values are `string`, so numbers had to be
  formatted with `InvariantCulture` on the way in and parsed back on the way out — exactly the "parse the number
  out of a string" that 0.2.0 set out to avoid, just moved one level down. Values are still stored as text,
  because `Data` has to serialise wholesale into logs and databases; what changed is that both ends are typed, so
  nobody has to remember `InvariantCulture`. `Data` still takes no part in equality, so `WithData` never changes
  how an error compares.
- `Precision.TryParsePlain` and `Precision.ParsePlain` — the missing inverse of `ToPlainString`, with a documented
  round trip: `TryParsePlain(ToPlainString(x), out var y)` always succeeds and yields `y == x`. From
  `Ozakboy.Http`: every consumer was writing its own `decimal.Parse(s, NumberStyles.Any, InvariantCulture)` and
  `InvariantCulture` is easy to leave out — under zh-TW the decimal separator is still `.`, so the omission fails
  silently and only breaks under a different locale, by which time the symptom is a long way from the cause.
  Only plain notation is accepted: a sign, a decimal point, and surrounding whitespace. **Exponent notation and
  group separators are rejected**, because this is the inverse of a method whose entire purpose is to never emit
  an exponent — receiving `1E-05` means the upstream serialiser is misconfigured, and absorbing it quietly leaves
  the same problem to resurface somewhere we do not control, such as a signed request body. The rejection is
  recoverable rather than fatal: `TryParsePlain` returns `false` and `ParsePlain` returns a `Validation` failure
  naming the offending text, and a caller that genuinely needs tolerance can still reach for `decimal.Parse` with
  `NumberStyles.Float` as a visible decision.

### Changed

- `Result.ThrowIfFailure()` and `Result<T>.GetValueOrThrow()` throw `ResultException` instead of
  `InvalidOperationException`. This is source- and binary-compatible: `ResultException` derives from
  `InvalidOperationException`, and the message and `InnerException` are unchanged. Only a test asserting the
  *exact* runtime type would notice.

## [0.2.1] - 2026-09-11

### Added

- Package icon. The shared ozakboy brand mark now shows on nuget.org and in IDE package managers.

No code changed in this release. NuGet package metadata cannot be altered on an already-published
version, so refreshing the icon requires publishing a new one.

## [0.2.0] - 2026-09-11

Additions driven by the first real consumer of this package. Everything here came from writing an exchange
abstraction layer on top of `0.1.0` and hitting the same friction repeatedly — no API was removed or changed,
so this is additive only.

### Added

- `Result.ToFailure<TOut>()` and `Result<T>.ToFailure<TOut>()` — forward an existing failure unchanged as a
  failed result of a different value type. Without it, propagating an inner failure outward reads as
  `Result.Failure<Order>(inner.Error!)`: `Error` is nullable, so a null-forgiving operator is required, and that
  `!` appeared often enough in forwarding code that it stopped being a useful signal of "this was checked".
  Calling it on a successful result throws, because there is no error to forward and reaching that line means a
  check was skipped.
- `ErrorCategory.NotSupported` — the operation is fine, this particular implementation just cannot do it.
  Distinct from `Validation` (bad input) and from any transient category. The motivating case is an interface
  with implementations of unequal capability: a simulated exchange used for backtesting cannot change margin
  mode, and none of the existing categories described that honestly.
- `Error.Data` — optional structured data alongside the human-readable message, excluded from equality like
  `Exception`. It spares callers from parsing numbers back out of message strings: a "quantity below minimum"
  failure can carry the actual quantity and the minimum, so the caller can decide whether to round up without
  running a regular expression over prose that is going to get rewritten.
- `Precision.TryFloorToStep`, `TryCeilingToStep`, and `TryRoundToStep` — non-throwing variants that return
  `false` for an invalid step. Exchange trading rules arrive over the wire and should not be assumed valid;
  callers that were going to wrap the outcome in a failure value anyway had to guard the step separately first.

## [0.1.0] - 2026-09-11

First release. The API is still settling, hence the `0.x` version.

### Added

- `Result` and `Result<T>` — explicit outcome types for operations whose failures are expected rather than
  exceptional. Both are `readonly struct`, so the success path allocates nothing. `IsSuccess` and `IsFailure`
  carry `MemberNotNullWhen` contracts, letting the compiler prove whether `Error` is null after a check.
  A `default` value is a failure carrying `Error.Uninitialized`, so an unassigned result is never mistaken
  for success. Composition through `Map`, `Then`, `Ensure`, and `Match`, all short-circuiting on failure.
- `Error` and `ErrorCategory` — a failure with a machine-readable code, a message, and a category.
  `IsTransient` marks the categories worth retrying (`Timeout`, `Network`, `RateLimited`, `Unavailable`).
  Equality deliberately excludes the captured `Exception`, which has no value semantics.
- `Money` and `CurrencyMismatchException` — a `decimal` amount tagged with its currency. Cross-currency
  arithmetic throws instead of producing a meaningless number. `default(Money)` is a currency-less zero that
  adds to any currency, making it usable as an accumulator seed. `Equals` is safe across currencies while
  `CompareTo` throws, because ordering must not mix currencies.
- `Precision` — decimal step alignment (`FloorToStep`, `CeilingToStep`, `RoundToStep`, `IsAlignedToStep`),
  scale inspection (`GetScale`, `GetSignificantScale`, `Normalize`), and `ToPlainString` for API
  serialisation with no exponent notation and no trailing zeros.
- `RetryPolicy` and `BackoffStrategy` — a description of a retry strategy plus backoff interval arithmetic.
  It never runs a retry loop and never touches I/O, so one policy applies across transports.
  `GetDelay(attempt, jitterSample)` takes an explicit jitter sample for deterministic tests.
  `RetryPolicy.NoRetry` is provided for non-idempotent operations.

### Notes

- Targets `net10.0` and has no NuGet dependencies. SourceLink comes from the SDK rather than a package
  reference, which keeps the dependency graph empty.
- Every price, quantity, and monetary amount is `decimal`. Binary floating point is not used anywhere.

[Unreleased]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/releases/tag/v0.1.0

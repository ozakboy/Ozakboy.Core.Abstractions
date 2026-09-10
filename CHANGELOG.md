# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/releases/tag/v0.1.0

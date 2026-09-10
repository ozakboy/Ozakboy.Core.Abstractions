# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/ozakboy/Ozakboy.Core.Abstractions/releases/tag/v0.1.0

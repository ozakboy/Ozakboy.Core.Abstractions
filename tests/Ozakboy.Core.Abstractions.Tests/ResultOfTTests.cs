namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="Result{T}"/>(帶回傳值)的單元測試。
/// 泛型邊界容易出事,因此參考型別、值型別與可為 null 的型別各測一輪。
/// </summary>
[TestClass]
public sealed class ResultOfTTests
{
    [TestMethod]
    public void DefaultValueIsFailureCarryingUninitializedErrorForValueType()
    {
        Result<int> uninitialized = default;

        Assert.IsFalse(uninitialized.IsSuccess);
        Assert.IsTrue(uninitialized.IsFailure);
        Assert.AreEqual(Error.Uninitialized, uninitialized.Error);
    }

    [TestMethod]
    public void DefaultValueIsFailureCarryingUninitializedErrorForReferenceType()
    {
        Result<string> uninitialized = default;

        Assert.IsTrue(uninitialized.IsFailure);
        Assert.AreEqual(Error.Uninitialized, uninitialized.Error);
        Assert.IsNull(uninitialized.GetValueOrDefault());
    }

    [TestMethod]
    public void DefaultValueIsFailureCarryingUninitializedErrorForNullableValueType()
    {
        Result<int?> uninitialized = default;

        Assert.IsTrue(uninitialized.IsFailure);
        Assert.AreEqual(Error.Uninitialized, uninitialized.Error);
    }

    [TestMethod]
    public void DefaultValueInsideArrayIsAlsoFailure()
    {
        var slots = new Result<decimal>[2];

        Assert.IsTrue(slots[0].IsFailure);
        Assert.AreEqual(Error.Uninitialized, slots[1].Error);
    }

    [TestMethod]
    public void DefaultValueDoesNotYieldValue()
    {
        Result<int> uninitialized = default;

        Assert.IsFalse(uninitialized.TryGetValue(out var value));
        Assert.AreEqual(0, value);
    }

    [TestMethod]
    public void SuccessHasNoErrorAndYieldsValue()
    {
        var result = Result.Success(42);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.IsNull(result.Error);
        Assert.IsTrue(result.TryGetValue(out var value));
        Assert.AreEqual(42, value);
    }

    [TestMethod]
    public void SuccessWorksForReferenceType()
    {
        var result = Result.Success("BTCUSDT");

        Assert.IsTrue(result.TryGetValue(out var value));
        Assert.AreEqual("BTCUSDT", value);
    }

    [TestMethod]
    public void SuccessWorksForNullableValueTypeCarryingNull()
    {
        var result = Result.Success<int?>(null);

        Assert.IsTrue(result.IsSuccess);
        Assert.IsTrue(result.TryGetValue(out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void FailureHasErrorAndNoValue()
    {
        var error = Error.NotFound("symbol.missing", "找不到商品");

        var result = Result.Failure<string>(error);

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
        Assert.IsFalse(result.TryGetValue(out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void GetValueOrDefaultReturnsFallbackOnFailure()
    {
        var failed = Result.Failure<decimal>(Error.Timeout("http.timeout", "逾時"));
        var succeeded = Result.Success(1.5m);

        Assert.AreEqual(-1m, failed.GetValueOrDefault(-1m));
        Assert.AreEqual(1.5m, succeeded.GetValueOrDefault(-1m));
    }

    [TestMethod]
    public void GetValueOrDefaultWithoutFallbackReturnsTypeDefault()
    {
        var failedValueType = Result.Failure<int>(Error.Timeout("http.timeout", "逾時"));
        var failedReferenceType = Result.Failure<string>(Error.Timeout("http.timeout", "逾時"));

        Assert.AreEqual(0, failedValueType.GetValueOrDefault());
        Assert.IsNull(failedReferenceType.GetValueOrDefault());
        Assert.AreEqual("BTCUSDT", Result.Success("BTCUSDT").GetValueOrDefault());
    }

    [TestMethod]
    public void MatchRunsSuccessBranchOnSuccess()
    {
        var failureBranchRan = false;

        var text = Result.Success(7).Match(
            onSuccess: v => $"值 {v}",
            onFailure: _ =>
            {
                failureBranchRan = true;
                return "bad";
            });

        Assert.AreEqual("值 7", text);
        Assert.IsFalse(failureBranchRan, "成功時不應執行失敗分支");
    }

    [TestMethod]
    public void MatchRunsFailureBranchOnFailure()
    {
        var successBranchRan = false;

        var text = Result.Failure<int>(Error.NotFound("symbol.missing", "找不到商品")).Match(
            onSuccess: _ =>
            {
                successBranchRan = true;
                return "ok";
            },
            onFailure: e => e.Code);

        Assert.AreEqual("symbol.missing", text);
        Assert.IsFalse(successBranchRan, "失敗時不應執行成功分支");
    }

    [TestMethod]
    public void MatchThrowsWhenDelegatesAreNull()
    {
        var result = Result.Success(1);

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Match<string>(null!, _ => "x"); });
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Match(_ => "x", null!); });
    }

    [TestMethod]
    public void MapTransformsValueOnSuccess()
    {
        var result = Result.Success(21).Map(v => v * 2);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public void MapCanChangeTypeToReferenceType()
    {
        var result = Result.Success(1.2300m).Map(Precision.ToPlainString);

        Assert.AreEqual("1.23", result.GetValueOrDefault(string.Empty));
    }

    [TestMethod]
    public void MapDoesNotRunTransformOnFailureAndPropagatesOriginalError()
    {
        var transformRan = false;
        var original = Error.Timeout("http.timeout", "逾時");

        var result = Result.Failure<int>(original).Map(v =>
        {
            transformRan = true;
            return v * 2;
        });

        Assert.IsFalse(transformRan, "失敗時不應執行轉換函式");
        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(original, result.Error);
    }

    [TestMethod]
    public void MapDoesNotRunTransformOnDefaultValue()
    {
        var transformRan = false;
        Result<int> uninitialized = default;

        var result = uninitialized.Map(v =>
        {
            transformRan = true;
            return v * 2;
        });

        Assert.IsFalse(transformRan);
        Assert.AreEqual(Error.Uninitialized, result.Error);
    }

    [TestMethod]
    public void MapThrowsWhenTransformIsNull()
    {
        var result = Result.Success(1);

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Map<int>(null!); });
    }

    [TestMethod]
    public void ThenChainsNextOnSuccess()
    {
        var result = Result.Success(10).Then(v => Result.Success(v.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("10", result.GetValueOrDefault(string.Empty));
    }

    [TestMethod]
    public void ThenCanReturnFailure()
    {
        var result = Result.Success(10).Then(_ => Result.Failure<string>(Error.Conflict("state.invalid", "狀態不允許")));

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("state.invalid", result.Error!.Code);
    }

    [TestMethod]
    public void ThenDoesNotRunNextOnFailureAndPropagatesOriginalError()
    {
        var nextRan = false;
        var original = Error.Network("net.down", "斷線");

        var result = Result.Failure<int>(original).Then(_ =>
        {
            nextRan = true;
            return Result.Success("unreachable");
        });

        Assert.IsFalse(nextRan, "失敗時不應執行後續操作");
        Assert.AreSame(original, result.Error);
    }

    [TestMethod]
    public void ThenThrowsWhenNextIsNull()
    {
        var result = Result.Success(1);

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Then<int>(null!); });
    }

    [TestMethod]
    public void EnsureKeepsResultWhenPredicateHolds()
    {
        var original = Result.Success(5m);

        var ensured = original.Ensure(v => v > 0m, Error.Validation("qty.non_positive", "數量必須為正"));

        Assert.IsTrue(ensured.IsSuccess);
        Assert.AreEqual(original, ensured);
    }

    [TestMethod]
    public void EnsureTurnsIntoSuppliedFailureWhenPredicateDoesNotHold()
    {
        var guard = Error.Validation("qty.non_positive", "數量必須為正");

        var ensured = Result.Success(0m).Ensure(v => v > 0m, guard);

        Assert.IsTrue(ensured.IsFailure);
        Assert.AreSame(guard, ensured.Error);
    }

    [TestMethod]
    public void EnsureDoesNotRunPredicateOnFailureAndPropagatesOriginalError()
    {
        var predicateRan = false;
        var original = Error.Timeout("http.timeout", "逾時");

        var ensured = Result.Failure<int>(original).Ensure(
            v =>
            {
                predicateRan = true;
                return v > 0;
            },
            Error.Validation("qty.non_positive", "數量必須為正"));

        Assert.IsFalse(predicateRan, "失敗時不應執行條件判斷");
        Assert.AreSame(original, ensured.Error);
    }

    [TestMethod]
    public void EnsureThrowsWhenArgumentsAreNull()
    {
        var result = Result.Success(1);

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Ensure(null!, Error.Validation("c", "m")); });
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = result.Ensure(v => v > 0, null!); });
    }

    [TestMethod]
    public void ToResultDiscardsValueOnSuccess()
    {
        var result = Result.Success(42).ToResult();

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(Result.Success(), result);
    }

    [TestMethod]
    public void ToResultKeepsErrorOnFailure()
    {
        var error = Error.NotFound("symbol.missing", "找不到商品");

        var result = Result.Failure<int>(error).ToResult();

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
    }

    [TestMethod]
    public void GetValueOrThrowReturnsValueOnSuccess()
    {
        Assert.AreEqual(42, Result.Success(42).GetValueOrThrow());
    }

    [TestMethod]
    public void GetValueOrThrowThrowsInvalidOperationExceptionOnFailure()
    {
        var error = Error.Validation("qty.too_small", "數量太小");
        var result = Result.Failure<int>(error);

        var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => { _ = result.GetValueOrThrow(); });

        Assert.AreEqual(error.ToString(), thrown.Message);
        Assert.IsNull(thrown.InnerException);
    }

    [TestMethod]
    public void GetValueOrThrowKeepsOriginalExceptionAsInnerException()
    {
        var source = new TimeoutException("底層逾時");
        var result = Result.Failure<int>(Error.FromException(source, "http.timeout", ErrorCategory.Timeout));

        var thrown = Assert.ThrowsExactly<InvalidOperationException>(() => { _ = result.GetValueOrThrow(); });

        Assert.AreSame(source, thrown.InnerException);
    }

    [TestMethod]
    public void ImplicitConversionFromValueProducesSuccessForValueType()
    {
        Result<int> result = 42;

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public void ImplicitConversionFromValueProducesSuccessForReferenceType()
    {
        Result<string> result = "BTCUSDT";

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("BTCUSDT", result.GetValueOrDefault(string.Empty));
    }

    [TestMethod]
    public void ImplicitConversionFromValueProducesSuccessForNullableValueType()
    {
        Result<int?> result = 42;

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public void ImplicitConversionFromErrorProducesFailure()
    {
        var error = Error.Conflict("state.invalid", "狀態不允許");

        Result<int> result = error;

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
    }

    [TestMethod]
    public void EqualityComparesSuccessValues()
    {
        Assert.AreEqual(Result.Success(42), Result.Success(42));
        Assert.AreNotEqual(Result.Success(42), Result.Success(43));
        Assert.IsTrue(Result.Success("a") == Result.Success("a"));
        Assert.IsTrue(Result.Success("a") != Result.Success("b"));
    }

    [TestMethod]
    public void EqualityComparesFailureErrors()
    {
        var left = Result.Failure<int>(new Error("code", "message", ErrorCategory.Network));
        var right = Result.Failure<int>(new Error("code", "message", ErrorCategory.Network));
        var other = Result.Failure<int>(new Error("code", "message", ErrorCategory.Timeout));

        Assert.AreEqual(left, right);
        Assert.AreNotEqual(left, other);
    }

    [TestMethod]
    public void SuccessAndFailureAreNotEqual()
    {
        Assert.AreNotEqual(Result.Success(0), Result.Failure<int>(Error.Uninitialized));
    }

    [TestMethod]
    public void EqualsAgainstOtherTypeReturnsFalse()
    {
        // 注意:這裡必須明確轉型為 object。因為有 Error 到 Result<T> 的隱含轉換,
        // 裸寫 Equals(null) 會被解析成 Equals(Result<T>) 並在轉換時擲出例外。
        Assert.IsFalse(Result.Success(1).Equals("not a result"));
        Assert.IsFalse(Result.Success(1).Equals((object?)null));
    }

    [TestMethod]
    public void BoxedEqualityWorks()
    {
        object boxed = Result.Success(42);

        Assert.IsTrue(Result.Success(42).Equals(boxed));
        Assert.IsFalse(Result.Success(43).Equals(boxed));
    }

    [TestMethod]
    public void ImplicitConversionFromNullErrorThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Result<int> result = (Error)null!;
            _ = result;
        });
    }

    [TestMethod]
    public void GetHashCodeIsStableForEqualResults()
    {
        Assert.AreEqual(Result.Success(42).GetHashCode(), Result.Success(42).GetHashCode());
        Assert.AreEqual(
            Result.Failure<int>(new Error("code", "message")).GetHashCode(),
            Result.Failure<int>(new Error("code", "message")).GetHashCode());
    }

    [TestMethod]
    public void ToStringWrapsValueOnSuccess()
    {
        Assert.AreEqual("Success(42)", Result.Success(42).ToString());
    }

    [TestMethod]
    public void ToStringUsesErrorTextOnFailure()
    {
        var error = Error.Validation("qty.too_small", "數量太小");

        Assert.AreEqual(error.ToString(), Result.Failure<int>(error).ToString());
    }

    [TestMethod]
    public void ChainedPipelineStopsAtTheFirstFailure()
    {
        // 串接情境的整合驗證:第一個失敗之後的每一步都不應該被執行。
        var mapRan = false;
        var thenRan = false;
        var guard = Error.Validation("qty.non_positive", "數量必須為正");

        var result = Result.Success(0m)
            .Ensure(v => v > 0m, guard)
            .Map(v =>
            {
                mapRan = true;
                return v * 2m;
            })
            .Then(v =>
            {
                thenRan = true;
                return Result.Success(v);
            });

        Assert.IsFalse(mapRan);
        Assert.IsFalse(thenRan);
        Assert.AreSame(guard, result.Error);
    }
}

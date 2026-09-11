namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="Result"/>(無回傳值)的單元測試。
/// </summary>
[TestClass]
public sealed class ResultTests
{
    [TestMethod]
    public void DefaultValueIsFailureCarryingUninitializedError()
    {
        // 刻意的設計:未經賦值的結果不可以被當成成功。
        Result uninitialized = default;

        Assert.IsFalse(uninitialized.IsSuccess);
        Assert.IsTrue(uninitialized.IsFailure);
        Assert.IsNotNull(uninitialized.Error);
        Assert.AreEqual(Error.Uninitialized, uninitialized.Error);
    }

    [TestMethod]
    public void DefaultValueInsideArrayIsAlsoFailure()
    {
        // 陣列預設元素、未指派欄位都會走到同一條路。
        var slots = new Result[2];

        Assert.IsTrue(slots[0].IsFailure);
        Assert.AreEqual(Error.Uninitialized, slots[1].Error);
    }

    [TestMethod]
    public void SuccessHasNoError()
    {
        var result = Result.Success();

        Assert.IsTrue(result.IsSuccess);
        Assert.IsFalse(result.IsFailure);
        Assert.IsNull(result.Error);
    }

    [TestMethod]
    public void FailureFromErrorKeepsTheError()
    {
        var error = Error.Validation("qty.too_small", "數量太小");

        var result = Result.Failure(error);

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
    }

    [TestMethod]
    public void FailureFromErrorThrowsWhenErrorIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Result.Failure((Error)null!); });
    }

    [TestMethod]
    public void FailureFromCodeAndMessageBuildsTheError()
    {
        var result = Result.Failure("http.timeout", "請求逾時", ErrorCategory.Timeout);

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual("http.timeout", result.Error.Code);
        Assert.AreEqual("請求逾時", result.Error.Message);
        Assert.AreEqual(ErrorCategory.Timeout, result.Error.Category);
    }

    [TestMethod]
    public void FailureFromCodeAndMessageDefaultsToUnexpected()
    {
        var result = Result.Failure("some.code", "some message");

        Assert.IsTrue(result.IsFailure);
        Assert.AreEqual(ErrorCategory.Unexpected, result.Error.Category);
    }

    [TestMethod]
    public void FailureFromCodeAndMessageValidatesArguments()
    {
        Assert.ThrowsExactly<ArgumentException>(() => { _ = Result.Failure(string.Empty, "message"); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = Result.Failure("code", "  "); });
    }

    [TestMethod]
    public void FromErrorProducesTheSameFailureAsFailure()
    {
        var error = Error.Network("net.down", "斷線");

        var viaFromError = Result.FromError(error);

        Assert.IsTrue(viaFromError.IsFailure);
        Assert.AreEqual(Result.Failure(error), viaFromError);
    }

    [TestMethod]
    public void ImplicitConversionFromErrorProducesFailure()
    {
        var error = Error.Conflict("state.invalid", "狀態不允許");

        Result result = error;

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
    }

    [TestMethod]
    public void MatchRunsSuccessBranchOnSuccess()
    {
        var failureBranchRan = false;

        var text = Result.Success().Match(
            onSuccess: () => "ok",
            onFailure: _ =>
            {
                failureBranchRan = true;
                return "bad";
            });

        Assert.AreEqual("ok", text);
        Assert.IsFalse(failureBranchRan, "成功時不應執行失敗分支");
    }

    [TestMethod]
    public void MatchRunsFailureBranchOnFailure()
    {
        var successBranchRan = false;
        var error = Error.NotFound("symbol.missing", "找不到商品");

        var text = Result.Failure(error).Match(
            onSuccess: () =>
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
        var success = Result.Success();

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = success.Match<string>(null!, _ => "x"); });
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = success.Match(() => "x", null!); });
    }

    [TestMethod]
    public void ThenRunsNextOnSuccess()
    {
        var ran = false;

        var result = Result.Success().Then(() =>
        {
            ran = true;
            return Result.Failure("next.failed", "後續步驟失敗");
        });

        Assert.IsTrue(ran);
        Assert.AreEqual("next.failed", result.Error!.Code);
    }

    [TestMethod]
    public void ThenSkipsNextOnFailureAndPropagatesOriginalError()
    {
        var ran = false;
        var original = Error.Timeout("http.timeout", "逾時");

        var result = Result.Failure(original).Then(() =>
        {
            ran = true;
            return Result.Success();
        });

        Assert.IsFalse(ran, "失敗時不應執行後續操作");
        Assert.AreSame(original, result.Error);
    }

    [TestMethod]
    public void ThenSkipsNextOnDefaultResult()
    {
        var ran = false;
        Result uninitialized = default;

        var result = uninitialized.Then(() =>
        {
            ran = true;
            return Result.Success();
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(Error.Uninitialized, result.Error);
    }

    [TestMethod]
    public void ThenThrowsWhenNextIsNull()
    {
        var success = Result.Success();

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = success.Then(null!); });
    }

    [TestMethod]
    public void ThrowIfFailureDoesNothingOnSuccess()
    {
        Result.Success().ThrowIfFailure();
    }

    [TestMethod]
    public void ThrowIfFailureThrowsWithErrorTextOnFailure()
    {
        // 擲出的型別自 0.3.0 起是 ResultException(仍然衍生自 InvalidOperationException),
        // 因此這裡用 ThrowsExactly 就必須指名衍生型別。訊息與內層例外的契約不變。
        var error = Error.Validation("qty.too_small", "數量太小");
        var result = Result.Failure(error);

        var thrown = Assert.ThrowsExactly<ResultException>(result.ThrowIfFailure);

        Assert.AreEqual(error.ToString(), thrown.Message);
        Assert.IsNull(thrown.InnerException);
    }

    [TestMethod]
    public void ThrowIfFailureKeepsOriginalExceptionAsInnerException()
    {
        var source = new TimeoutException("底層逾時");
        var result = Result.Failure(Error.FromException(source, "http.timeout", ErrorCategory.Timeout));

        var thrown = Assert.ThrowsExactly<ResultException>(result.ThrowIfFailure);

        Assert.AreSame(source, thrown.InnerException);
    }

    [TestMethod]
    public void ThrowIfFailureCarriesTheErrorOnTheException()
    {
        var error = Error.Validation("qty.too_small", "數量太小");

        var thrown = Assert.ThrowsExactly<ResultException>(Result.Failure(error).ThrowIfFailure);

        Assert.AreSame(error, thrown.Error);
    }

    [TestMethod]
    public void ThrowIfFailureIsStillCatchableAsInvalidOperationException()
    {
        // 相容性保證:0.2.1 之前擲的是 InvalidOperationException,既有呼叫端不能因為升版而漏接。
        var caught = false;

        try
        {
            Result.Failure("qty.too_small", "數量太小").ThrowIfFailure();
        }
        catch (InvalidOperationException)
        {
            caught = true;
        }

        Assert.IsTrue(caught, "既有的 catch (InvalidOperationException) 必須仍然攔得到");
    }

    [TestMethod]
    public void EqualitySucceedsForTwoSuccesses()
    {
        Assert.AreEqual(Result.Success(), Result.Success());
        Assert.IsTrue(Result.Success() == Result.Success());
        Assert.IsFalse(Result.Success() != Result.Success());
    }

    [TestMethod]
    public void EqualityComparesErrorsByValue()
    {
        var left = Result.Failure("code", "message", ErrorCategory.Network);
        var right = Result.Failure("code", "message", ErrorCategory.Network);
        var other = Result.Failure("code", "message", ErrorCategory.Timeout);

        Assert.AreEqual(left, right);
        Assert.IsTrue(left != other);
    }

    [TestMethod]
    public void SuccessAndFailureAreNotEqual()
    {
        Assert.AreNotEqual(Result.Success(), Result.Failure("code", "message"));
    }

    [TestMethod]
    public void EqualsAgainstOtherTypeReturnsFalse()
    {
        // 注意:這裡必須明確轉型為 object。因為有 Error 到 Result 的隱含轉換,
        // 裸寫 Equals(null) 會被解析成 Equals(Result) 並在轉換時擲出例外。
        Assert.IsFalse(Result.Success().Equals("not a result"));
        Assert.IsFalse(Result.Success().Equals((object?)null));
    }

    [TestMethod]
    public void BoxedEqualityWorks()
    {
        object boxed = Result.Success();

        Assert.IsTrue(Result.Success().Equals(boxed));
        Assert.IsFalse(Result.Failure("code", "message").Equals(boxed));
    }

    [TestMethod]
    public void ImplicitConversionFromNullErrorThrows()
    {
        // 隱含轉換運算子讓 null 也能被轉成 Result,轉換當下才擲出例外。
        Assert.ThrowsExactly<ArgumentNullException>(() =>
        {
            Result result = (Error)null!;
            _ = result;
        });
    }

    [TestMethod]
    public void GetHashCodeIsStableForEqualResults()
    {
        var left = Result.Failure("code", "message", ErrorCategory.Network);
        var right = Result.Failure("code", "message", ErrorCategory.Network);

        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreEqual(Result.Success().GetHashCode(), Result.Success().GetHashCode());
    }

    [TestMethod]
    public void ToStringSaysSuccessOnSuccess()
    {
        Assert.AreEqual("Success", Result.Success().ToString());
    }

    [TestMethod]
    public void ToStringUsesErrorTextOnFailure()
    {
        var error = Error.Validation("qty.too_small", "數量太小");

        Assert.AreEqual(error.ToString(), Result.Failure(error).ToString());
    }

    [TestMethod]
    public void ToStringOnDefaultValueUsesUninitializedError()
    {
        Result uninitialized = default;

        Assert.AreEqual(Error.Uninitialized.ToString(), uninitialized.ToString());
    }

    [TestMethod]
    public void SuccessOfTBridgesToGenericResult()
    {
        var result = Result.Success(42);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public void FailureOfTBridgesToGenericResult()
    {
        var error = Error.NotFound("symbol.missing", "找不到商品");

        var result = Result.Failure<decimal>(error);

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(error, result.Error);
    }

    [TestMethod]
    public void FailureOfTThrowsWhenErrorIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Result.Failure<int>(null!); });
    }

    [TestMethod]
    public void ToFailureForwardsTheSameErrorInstanceToAnotherValueType()
    {
        var error = Error.Timeout("http.timeout", "請求逾時");
        var result = Result.Failure(error);

        var forwarded = result.ToFailure<int>();

        Assert.IsTrue(forwarded.IsFailure);
        Assert.AreSame(error, forwarded.Error);
    }

    [TestMethod]
    public void ToFailurePreservesEveryFieldOfTheError()
    {
        var source = new TimeoutException("底層逾時");
        var error = Error.FromException(source, "http.timeout", ErrorCategory.Timeout) with
        {
            Data = new Dictionary<string, string> { ["retryAfterMs"] = "500" },
        };
        var result = Result.Failure(error);

        var forwarded = result.ToFailure<string>();

        Assert.AreEqual(error.Code, forwarded.Error!.Code);
        Assert.AreEqual(error.Message, forwarded.Error.Message);
        Assert.AreEqual(error.Category, forwarded.Error.Category);
        Assert.AreSame(error.Exception, forwarded.Error.Exception);
        Assert.AreSame(error.Data, forwarded.Error.Data);
    }

    [TestMethod]
    public void ToFailureThrowsInvalidOperationExceptionOnSuccess()
    {
        var success = Result.Success();

        Assert.ThrowsExactly<InvalidOperationException>(() => { _ = success.ToFailure<int>(); });
    }

    [TestMethod]
    public void ToFailureOnDefaultValueForwardsUninitializedError()
    {
        Result uninitialized = default;

        var forwarded = uninitialized.ToFailure<int>();

        Assert.IsTrue(forwarded.IsFailure);
        Assert.AreEqual(Error.Uninitialized, forwarded.Error);
    }
}

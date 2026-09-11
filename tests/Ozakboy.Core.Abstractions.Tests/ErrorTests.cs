namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="Error"/> 的單元測試。
/// </summary>
[TestClass]
public sealed class ErrorTests
{
    [TestMethod]
    public void ConstructorKeepsCodeMessageAndCategory()
    {
        var error = new Error("order.rejected", "委託被拒絕", ErrorCategory.Conflict);

        Assert.AreEqual("order.rejected", error.Code);
        Assert.AreEqual("委託被拒絕", error.Message);
        Assert.AreEqual(ErrorCategory.Conflict, error.Category);
        Assert.IsNull(error.Exception);
    }

    [TestMethod]
    public void ConstructorDefaultsCategoryToUnexpected()
    {
        var error = new Error("some.code", "some message");

        Assert.AreEqual(ErrorCategory.Unexpected, error.Category);
    }

    [TestMethod]
    public void ConstructorThrowsWhenCodeIsBlank()
    {
        // 空字串與純空白都視為未提供代碼。
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Error(string.Empty, "message"); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Error("   ", "message"); });
    }

    [TestMethod]
    public void ConstructorThrowsWhenCodeIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = new Error(null!, "message"); });
    }

    [TestMethod]
    public void ConstructorThrowsWhenMessageIsBlank()
    {
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Error("code", string.Empty); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Error("code", "\t "); });
    }

    [TestMethod]
    public void ConstructorThrowsWhenMessageIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = new Error("code", null!); });
    }

    [TestMethod]
    public void UninitializedIsInternalCategory()
    {
        Assert.AreEqual("core.uninitialized_result", Error.Uninitialized.Code);
        Assert.AreEqual(ErrorCategory.Internal, Error.Uninitialized.Category);
        Assert.IsFalse(Error.Uninitialized.IsTransient);
    }

    [TestMethod]
    public void UninitializedIsTheSameInstanceEveryTime()
    {
        // 靜態屬性應該只建立一次,避免每次讀取都配置新物件。
        Assert.AreSame(Error.Uninitialized, Error.Uninitialized);
    }

    [TestMethod]
    public void ValidationFactoryProducesValidationCategory()
    {
        var error = Error.Validation("qty.too_small", "數量低於最小下單量");

        Assert.AreEqual("qty.too_small", error.Code);
        Assert.AreEqual("數量低於最小下單量", error.Message);
        Assert.AreEqual(ErrorCategory.Validation, error.Category);
        Assert.IsFalse(error.IsTransient);
    }

    [TestMethod]
    public void NotFoundFactoryProducesNotFoundCategory()
    {
        var error = Error.NotFound("symbol.missing", "找不到商品");

        Assert.AreEqual(ErrorCategory.NotFound, error.Category);
        Assert.IsFalse(error.IsTransient);
    }

    [TestMethod]
    public void ConflictFactoryProducesConflictCategory()
    {
        var error = Error.Conflict("order.duplicate", "重複的委託");

        Assert.AreEqual(ErrorCategory.Conflict, error.Category);
        Assert.IsFalse(error.IsTransient);
    }

    [TestMethod]
    public void TimeoutFactoryProducesTransientTimeout()
    {
        var error = Error.Timeout("http.timeout", "請求逾時");

        Assert.AreEqual(ErrorCategory.Timeout, error.Category);
        Assert.IsTrue(error.IsTransient);
    }

    [TestMethod]
    public void NetworkFactoryProducesTransientNetwork()
    {
        var error = Error.Network("http.connection_reset", "連線中斷");

        Assert.AreEqual(ErrorCategory.Network, error.Category);
        Assert.IsTrue(error.IsTransient);
    }

    [TestMethod]
    public void RateLimitedFactoryProducesTransientRateLimited()
    {
        var error = Error.RateLimited("exchange.rate_limit", "被限流");

        Assert.AreEqual(ErrorCategory.RateLimited, error.Category);
        Assert.IsTrue(error.IsTransient);
    }

    [TestMethod]
    public void InternalFactoryProducesNonTransientInternal()
    {
        var error = Error.Internal("core.invariant_broken", "不變條件被打破");

        Assert.AreEqual(ErrorCategory.Internal, error.Category);
        Assert.IsFalse(error.IsTransient);
    }

    [TestMethod]
    public void FromExceptionKeepsOriginalExceptionAndUsesTypeNameAsDefaultCode()
    {
        var source = new InvalidOperationException("狀態不允許");

        var error = Error.FromException(source);

        Assert.AreEqual(nameof(InvalidOperationException), error.Code);
        Assert.AreEqual("狀態不允許", error.Message);
        Assert.AreEqual(ErrorCategory.Unexpected, error.Category);
        Assert.AreSame(source, error.Exception);
    }

    [TestMethod]
    public void FromExceptionHonoursExplicitCodeAndCategory()
    {
        var source = new TimeoutException("太久了");

        var error = Error.FromException(source, "http.timeout", ErrorCategory.Timeout);

        Assert.AreEqual("http.timeout", error.Code);
        Assert.AreEqual(ErrorCategory.Timeout, error.Category);
        Assert.IsTrue(error.IsTransient);
        Assert.AreSame(source, error.Exception);
    }

    [TestMethod]
    public void FromExceptionThrowsWhenExceptionIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = Error.FromException(null!); });
    }

    [TestMethod]
    public void EqualityIgnoresException()
    {
        // 這是刻意的設計:例外實例沒有值語義,兩個內容相同的例外並不相等,
        // 若列入比較會讓兩個語意相同的錯誤被判定為不同。
        var withoutException = new Error("http.timeout", "逾時", ErrorCategory.Timeout);
        var withException = Error.FromException(new TimeoutException("逾時"), "http.timeout", ErrorCategory.Timeout);
        var withAnotherException = Error.FromException(new TimeoutException("逾時"), "http.timeout", ErrorCategory.Timeout);

        Assert.AreEqual(withoutException, withException);
        Assert.AreEqual(withException, withAnotherException);
        Assert.AreNotSame(withException.Exception, withAnotherException.Exception);
    }

    [TestMethod]
    public void EqualityComparesCodeMessageAndCategory()
    {
        var baseline = new Error("code", "message", ErrorCategory.Validation);

        Assert.AreEqual(baseline, new Error("code", "message", ErrorCategory.Validation));
        Assert.AreNotEqual(baseline, new Error("other", "message", ErrorCategory.Validation));
        Assert.AreNotEqual(baseline, new Error("code", "other", ErrorCategory.Validation));
        Assert.AreNotEqual(baseline, new Error("code", "message", ErrorCategory.NotFound));
    }

    [TestMethod]
    public void EqualityIsCaseSensitive()
    {
        Assert.AreNotEqual(new Error("code", "message"), new Error("CODE", "message"));
        Assert.AreNotEqual(new Error("code", "message"), new Error("code", "MESSAGE"));
    }

    [TestMethod]
    public void EqualsReturnsFalseForNull()
    {
        var error = new Error("code", "message");

        Assert.IsFalse(error.Equals(null));
        Assert.IsFalse(error.Equals((object?)null));
    }

    [TestMethod]
    public void OperatorEqualityMatchesEquals()
    {
        var left = new Error("code", "message", ErrorCategory.Network);
        var right = new Error("code", "message", ErrorCategory.Network);
        var different = new Error("code", "message", ErrorCategory.Timeout);

        Assert.IsTrue(left == right);
        Assert.IsFalse(left != right);
        Assert.IsTrue(left != different);
    }

    [TestMethod]
    public void GetHashCodeIsConsistentWithEquality()
    {
        var withException = Error.FromException(new TimeoutException("逾時"), "http.timeout", ErrorCategory.Timeout);
        var withoutException = new Error("http.timeout", "逾時", ErrorCategory.Timeout);

        Assert.AreEqual(withoutException.GetHashCode(), withException.GetHashCode());
    }

    [TestMethod]
    public void GetHashCodeDiffersForDifferentCategory()
    {
        var left = new Error("code", "message", ErrorCategory.Timeout);
        var right = new Error("code", "message", ErrorCategory.Network);

        Assert.AreNotEqual(left.GetHashCode(), right.GetHashCode());
    }

    [TestMethod]
    public void ToStringUsesCodeColonMessageForm()
    {
        var error = new Error("order.rejected", "委託被拒絕", ErrorCategory.Conflict);

        Assert.AreEqual("order.rejected: 委託被拒絕", error.ToString());
    }

    [TestMethod]
    public void WithExpressionCanAttachExceptionWithoutChangingEquality()
    {
        var original = new Error("code", "message", ErrorCategory.Network);

        var enriched = original with { Exception = new HttpRequestExceptionStandIn() };

        Assert.AreEqual(original, enriched);
        Assert.IsNotNull(enriched.Exception);
        Assert.IsNull(original.Exception);
    }

    [TestMethod]
    public void DataDefaultsToNull()
    {
        var error = new Error("qty.too_small", "數量太小");

        Assert.IsNull(error.Data);
    }

    [TestMethod]
    public void WithExpressionCanSetDataAndReadItBack()
    {
        var original = new Error("qty.too_small", "數量太小", ErrorCategory.Validation);
        var data = new Dictionary<string, string>
        {
            ["actual"] = "0.0005",
            ["minimum"] = "0.001",
        };

        var enriched = original with { Data = data };

        Assert.IsNull(original.Data);
        Assert.IsNotNull(enriched.Data);
        Assert.AreEqual("0.0005", enriched.Data["actual"]);
        Assert.AreEqual("0.001", enriched.Data["minimum"]);
    }

    [TestMethod]
    public void EqualityIgnoresData()
    {
        // 與 Exception 的處理一致:Data 只供診斷,不參與相等性比較。
        // 這裡涵蓋兩種情境:一邊有 Data 一邊沒有,以及兩邊都有但內容不同。
        var withoutData = new Error("qty.too_small", "數量太小", ErrorCategory.Validation);
        var withData = withoutData with
        {
            Data = new Dictionary<string, string> { ["actual"] = "0.0005" },
        };
        var withDifferentData = withoutData with
        {
            Data = new Dictionary<string, string> { ["actual"] = "0.0009" },
        };

        Assert.AreEqual(withoutData, withData);
        Assert.AreEqual(withData, withDifferentData);
    }

    [TestMethod]
    public void GetHashCodeIgnoresData()
    {
        var withoutData = new Error("qty.too_small", "數量太小", ErrorCategory.Validation);
        var withData = withoutData with
        {
            Data = new Dictionary<string, string> { ["actual"] = "0.0005" },
        };

        Assert.AreEqual(withoutData.GetHashCode(), withData.GetHashCode());
    }

    // ---------- Exhausted 工廠 ----------

    [TestMethod]
    public void ExhaustedFactoryProducesNonTransientExhausted()
    {
        var error = Error.Exhausted("ws.reconnect_exhausted", "重連次數已用盡");

        Assert.AreEqual("ws.reconnect_exhausted", error.Code);
        Assert.AreEqual("重連次數已用盡", error.Message);
        Assert.AreEqual(ErrorCategory.Exhausted, error.Category);
        Assert.IsFalse(error.IsTransient, "重試機會用盡的錯誤不可以被判定為暫時性");
    }

    // ---------- WithData ----------

    [TestMethod]
    public void WithDataAddsAnEntryWithoutTouchingTheOriginal()
    {
        var original = Error.Validation("qty.too_small", "數量太小");

        var enriched = original.WithData("actual", "0.0005");

        Assert.IsNull(original.Data, "原錯誤必須維持不變");
        Assert.IsNotNull(enriched.Data);
        Assert.AreEqual("0.0005", enriched.Data["actual"]);
    }

    [TestMethod]
    public void WithDataKeepsEntriesAddedEarlier()
    {
        var error = Error.RateLimited("exchange.rate_limit", "被限流")
            .WithData("statusCode", 429L)
            .WithData("retryAfterMs", 1500L);

        Assert.IsNotNull(error.Data);
        Assert.AreEqual(2, error.Data.Count);
        Assert.AreEqual("429", error.Data["statusCode"]);
        Assert.AreEqual("1500", error.Data["retryAfterMs"]);
    }

    [TestMethod]
    public void WithDataOverwritesTheSameKey()
    {
        var error = new Error("code", "message").WithData("k", "first").WithData("k", "second");

        Assert.AreEqual("second", error.Data!["k"]);
    }

    [TestMethod]
    public void WithDataDoesNotMutateADictionarySharedWithAnotherError()
    {
        // Data 可能被 ToFailure 之類的方法沿用到別的錯誤上,就地改寫會波及那些實例。
        var shared = new Dictionary<string, string> { ["a"] = "1" };
        var first = new Error("code", "message") with { Data = shared };

        var second = first.WithData("b", "2");

        Assert.AreEqual(1, shared.Count, "來源字典不可被改寫");
        Assert.AreEqual(1, first.Data!.Count);
        Assert.AreEqual(2, second.Data!.Count);
    }

    [TestMethod]
    public void WithDataSerialisesDecimalWithoutExponentNotation()
    {
        var error = new Error("code", "message").WithData("price", 0.00000001m);

        Assert.AreEqual("0.00000001", error.Data!["price"]);
    }

    [TestMethod]
    public void WithDataStripsTrailingZerosOnDecimal()
    {
        var error = new Error("code", "message").WithData("price", 1.2300m);

        Assert.AreEqual("1.23", error.Data!["price"]);
    }

    [TestMethod]
    public void WithDataSerialisesBooleanInLowercase()
    {
        var error = new Error("code", "message").WithData("retryable", true).WithData("fatal", false);

        Assert.AreEqual("true", error.Data!["retryable"]);
        Assert.AreEqual("false", error.Data["fatal"]);
    }

    [TestMethod]
    public void WithDataSerialisesNegativeAndLargeIntegers()
    {
        var error = new Error("code", "message")
            .WithData("min", long.MinValue)
            .WithData("max", long.MaxValue);

        Assert.AreEqual("-9223372036854775808", error.Data!["min"]);
        Assert.AreEqual("9223372036854775807", error.Data["max"]);
    }

    [TestMethod]
    public void WithDataAcceptsSeveralEntriesAtOnce()
    {
        var error = new Error("code", "message").WithData(
        [
            new KeyValuePair<string, string>("a", "1"),
            new KeyValuePair<string, string>("b", "2"),
        ]);

        Assert.AreEqual(2, error.Data!.Count);
        Assert.AreEqual("1", error.Data["a"]);
        Assert.AreEqual("2", error.Data["b"]);
    }

    [TestMethod]
    public void WithDataMergesSeveralEntriesOntoExistingOnes()
    {
        var error = new Error("code", "message")
            .WithData("a", "1")
            .WithData([new KeyValuePair<string, string>("a", "overwritten"), new KeyValuePair<string, string>("b", "2")]);

        Assert.AreEqual("overwritten", error.Data!["a"]);
        Assert.AreEqual("2", error.Data["b"]);
    }

    [TestMethod]
    public void WithDataAcceptsAnEmptySequence()
    {
        var error = new Error("code", "message").WithData([]);

        Assert.IsNotNull(error.Data);
        Assert.AreEqual(0, error.Data.Count);
    }

    [TestMethod]
    public void WithDataRejectsBlankKeysAndNullValues()
    {
        var error = new Error("code", "message");

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = error.WithData(null!, "v"); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = error.WithData(string.Empty, "v"); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = error.WithData("  ", "v"); });
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = error.WithData("k", (string)null!); });
    }

    [TestMethod]
    public void WithDataRejectsNullSequenceAndBadEntries()
    {
        var error = new Error("code", "message");

        Assert.ThrowsExactly<ArgumentNullException>(
            () => { _ = error.WithData((IEnumerable<KeyValuePair<string, string>>)null!); });
        Assert.ThrowsExactly<ArgumentException>(
            () => { _ = error.WithData([new KeyValuePair<string, string>(" ", "v")]); });
        Assert.ThrowsExactly<ArgumentNullException>(
            () => { _ = error.WithData([new KeyValuePair<string, string>(null!, "v")]); });
        Assert.ThrowsExactly<ArgumentNullException>(
            () => { _ = error.WithData([new KeyValuePair<string, string>("k", null!)]); });
    }

    [TestMethod]
    public void WithDataDoesNotAffectEquality()
    {
        // Data 不參與相等性比較,所以附加資料之後仍然與原錯誤相等。
        var original = Error.Validation("qty.too_small", "數量太小");

        var enriched = original.WithData("actual", 0.0005m).WithData("minimum", 0.001m);

        Assert.AreEqual(original, enriched);
        Assert.AreEqual(original.GetHashCode(), enriched.GetHashCode());
    }

    [TestMethod]
    public void WithDataKeepsTheCapturedException()
    {
        var source = new TimeoutException("底層逾時");
        var error = Error.FromException(source, "http.timeout", ErrorCategory.Timeout);

        var enriched = error.WithData("statusCode", 504L);

        Assert.AreSame(source, enriched.Exception);
        Assert.AreEqual(ErrorCategory.Timeout, enriched.Category);
    }

    // ---------- TryGetData 系列 ----------

    [TestMethod]
    public void TryGetDataReadsBackWhatWithDataWrote()
    {
        var error = new Error("code", "message").WithData("body", "too many requests");

        Assert.IsTrue(error.TryGetData("body", out var value));
        Assert.AreEqual("too many requests", value);
    }

    [TestMethod]
    public void TryGetDataReturnsFalseWhenThereIsNoData()
    {
        var error = new Error("code", "message");

        Assert.IsFalse(error.TryGetData("missing", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryGetDataReturnsFalseForUnknownKey()
    {
        var error = new Error("code", "message").WithData("a", "1");

        Assert.IsFalse(error.TryGetData("b", out var value));
        Assert.IsNull(value);
    }

    [TestMethod]
    public void TryGetDataReturnsFalseForBlankKeyInsteadOfThrowing()
    {
        // 與 Precision.TryFloorToStep 的取捨一致:讀取端的「找不到」是正常情況,不用例外表達。
        var error = new Error("code", "message").WithData("a", "1");

        Assert.IsFalse(error.TryGetData(null!, out _));
        Assert.IsFalse(error.TryGetData(string.Empty, out _));
        Assert.IsFalse(error.TryGetData("   ", out _));
    }

    [TestMethod]
    public void TryGetDataIsCaseSensitive()
    {
        var error = new Error("code", "message").WithData("Key", "1");

        Assert.IsTrue(error.TryGetData("Key", out _));
        Assert.IsFalse(error.TryGetData("key", out _));
    }

    [TestMethod]
    public void TryGetDecimalRoundTripsThroughWithData()
    {
        var error = new Error("code", "message")
            .WithData("actual", 0.00050m)
            .WithData("minimum", -1.5m);

        Assert.IsTrue(error.TryGetDecimal("actual", out var actual));
        Assert.AreEqual(0.0005m, actual);

        Assert.IsTrue(error.TryGetDecimal("minimum", out var minimum));
        Assert.AreEqual(-1.5m, minimum);
    }

    [TestMethod]
    public void TryGetDecimalReturnsFalseAndZeroWhenMissingOrUnparsable()
    {
        var error = new Error("code", "message").WithData("text", "not a number");

        Assert.IsFalse(error.TryGetDecimal("text", out var fromText));
        Assert.AreEqual(0m, fromText);

        Assert.IsFalse(error.TryGetDecimal("missing", out var fromMissing));
        Assert.AreEqual(0m, fromMissing);
    }

    [TestMethod]
    public void TryGetInt64RoundTripsThroughWithData()
    {
        var error = new Error("code", "message")
            .WithData("statusCode", 429L)
            .WithData("offset", -42L);

        Assert.IsTrue(error.TryGetInt64("statusCode", out var statusCode));
        Assert.AreEqual(429L, statusCode);

        Assert.IsTrue(error.TryGetInt64("offset", out var offset));
        Assert.AreEqual(-42L, offset);
    }

    [TestMethod]
    public void TryGetInt64RejectsValuesThatAreNotWholeNumbers()
    {
        var error = new Error("code", "message").WithData("price", 1.5m);

        Assert.IsFalse(error.TryGetInt64("price", out var value));
        Assert.AreEqual(0L, value);
    }

    [TestMethod]
    public void TryGetBooleanRoundTripsThroughWithData()
    {
        var error = new Error("code", "message").WithData("retryable", true).WithData("fatal", false);

        Assert.IsTrue(error.TryGetBoolean("retryable", out var retryable));
        Assert.IsTrue(retryable);

        Assert.IsTrue(error.TryGetBoolean("fatal", out var fatal));
        Assert.IsFalse(fatal);
    }

    [TestMethod]
    public void TryGetBooleanAcceptsTheCapitalisedFormTooAndRejectsOthers()
    {
        // bool.ToString() 產生的 True/False 也讀得回來,但數字不行 —— 不猜測呼叫端的意思。
        var error = new Error("code", "message").WithData("a", "True").WithData("b", "1");

        Assert.IsTrue(error.TryGetBoolean("a", out var capitalised));
        Assert.IsTrue(capitalised);

        Assert.IsFalse(error.TryGetBoolean("b", out var numeric));
        Assert.IsFalse(numeric);
    }

    [TestMethod]
    public void TypedReadersReturnFalseWhenThereIsNoDataAtAll()
    {
        var error = new Error("code", "message");

        Assert.IsFalse(error.TryGetDecimal("k", out _));
        Assert.IsFalse(error.TryGetInt64("k", out _));
        Assert.IsFalse(error.TryGetBoolean("k", out _));
    }

    /// <summary>
    /// 測試用的例外型別,避免相依於任何實際的傳輸層例外。
    /// </summary>
    private sealed class HttpRequestExceptionStandIn : Exception
    {
    }
}

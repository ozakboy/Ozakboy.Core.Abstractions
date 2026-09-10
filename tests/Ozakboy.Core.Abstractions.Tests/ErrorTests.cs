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

    /// <summary>
    /// 測試用的例外型別,避免相依於任何實際的傳輸層例外。
    /// </summary>
    private sealed class HttpRequestExceptionStandIn : Exception
    {
    }
}

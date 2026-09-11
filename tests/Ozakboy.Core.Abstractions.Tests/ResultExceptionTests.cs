namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="ResultException"/> 的單元測試。
/// 這個型別的價值在於繼承關係,因此相容性(仍然是 InvalidOperationException)與資訊保留是測試重點。
/// </summary>
[TestClass]
public sealed class ResultExceptionTests
{
    [TestMethod]
    public void DerivesFromInvalidOperationException()
    {
        // 這條保證是整個設計成立的前提:既有的 catch (InvalidOperationException) 不受影響。
        var exception = new ResultException(Error.Validation("qty.too_small", "數量太小"));

        Assert.IsInstanceOfType<InvalidOperationException>(exception);
    }

    [TestMethod]
    public void KeepsTheErrorItWasBuiltFrom()
    {
        var error = Error.Timeout("http.timeout", "請求逾時");

        var exception = new ResultException(error);

        Assert.AreSame(error, exception.Error);
    }

    [TestMethod]
    public void UsesTheErrorTextAsMessage()
    {
        var error = Error.Validation("qty.too_small", "數量太小");

        var exception = new ResultException(error);

        Assert.AreEqual(error.ToString(), exception.Message);
    }

    [TestMethod]
    public void UsesTheErrorInnerExceptionAsInnerException()
    {
        var source = new TimeoutException("底層逾時");
        var error = Error.FromException(source, "http.timeout", ErrorCategory.Timeout);

        var exception = new ResultException(error);

        Assert.AreSame(source, exception.InnerException);
    }

    [TestMethod]
    public void HasNoInnerExceptionWhenTheErrorCarriesNone()
    {
        var exception = new ResultException(Error.NotFound("symbol.missing", "找不到商品"));

        Assert.IsNull(exception.InnerException);
    }

    [TestMethod]
    public void ThrowsWhenErrorIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = new ResultException((Error)null!); });
    }

    [TestMethod]
    public void ParameterlessConstructorStillProducesANonNullError()
    {
        var exception = new ResultException();

        Assert.IsNotNull(exception.Error);
        Assert.AreEqual("core.result_exception", exception.Error.Code);
        Assert.AreEqual(ErrorCategory.Unexpected, exception.Error.Category);
        Assert.AreEqual(exception.Message, exception.Error.Message);
    }

    [TestMethod]
    public void MessageConstructorSynthesisesAnErrorFromTheMessage()
    {
        var exception = new ResultException("出事了");

        Assert.AreEqual("出事了", exception.Message);
        Assert.AreEqual("出事了", exception.Error.Message);
        Assert.AreEqual("core.result_exception", exception.Error.Code);
        Assert.IsNull(exception.Error.Exception);
    }

    [TestMethod]
    public void MessageAndInnerExceptionConstructorKeepsBoth()
    {
        var inner = new TimeoutException("底層逾時");

        var exception = new ResultException("出事了", inner);

        Assert.AreEqual("出事了", exception.Message);
        Assert.AreSame(inner, exception.InnerException);
        Assert.AreSame(inner, exception.Error.Exception);
    }

    [TestMethod]
    public void BlankMessageFallsBackToTheDefaultErrorMessage()
    {
        // Error 不接受空白訊息,但從例外建構式擲出 ArgumentException 只會掩蓋真正要回報的失敗。
        var fromEmpty = new ResultException(string.Empty);
        var fromWhitespace = new ResultException("   ", new TimeoutException("底層逾時"));

        Assert.IsFalse(string.IsNullOrWhiteSpace(fromEmpty.Error.Message));
        Assert.IsFalse(string.IsNullOrWhiteSpace(fromWhitespace.Error.Message));
        Assert.AreEqual(fromEmpty.Error.Message, fromWhitespace.Error.Message);
    }

    [TestMethod]
    public void NullMessageFallsBackToTheDefaultErrorMessage()
    {
        // 必須明確轉型:ResultException 同時有 (Error) 與 (string) 建構式,裸 null 會是模稜兩可的呼叫。
        var exception = new ResultException((string)null!);

        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Error.Message));
    }

    [TestMethod]
    public void ErrorToExceptionProducesTheSameThingAsTheConstructor()
    {
        var error = Error.RateLimited("exchange.rate_limit", "被限流");

        var exception = error.ToException();

        Assert.AreSame(error, exception.Error);
        Assert.AreEqual(error.ToString(), exception.Message);
    }

    [TestMethod]
    public void CanBeCaughtAsInvalidOperationExceptionAcrossABclBoundary()
    {
        // 模擬簽章固定的邊界:只能丟例外,不能回傳 Result。
        var error = Error.Network("net.down", "斷線");

        try
        {
            throw error.ToException();
        }
        catch (InvalidOperationException caught)
        {
            // 舊呼叫端:攔得到,但取不回 Error。
            Assert.AreEqual(error.ToString(), caught.Message);

            // 新呼叫端:改攔 ResultException 就取得回來。
            var recovered = (ResultException)caught;
            Assert.AreSame(error, recovered.Error);
        }
    }
}

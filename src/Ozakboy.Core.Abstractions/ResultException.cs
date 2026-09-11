namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 攜帶 <see cref="Abstractions.Error"/> 跨越例外邊界的例外型別。
/// The exception used to carry an <see cref="Abstractions.Error"/> across an exception boundary.
/// </summary>
/// <remarks>
/// <para>
/// 實作 BCL 介面時簽章是固定的,<see cref="Result{T}"/> 過不去:<c>DelegatingHandler.SendAsync</c> 必須
/// 回傳 <c>Task&lt;HttpResponseMessage&gt;</c>,<c>BackgroundService.ExecuteAsync</c> 必須回傳 <c>Task</c>。
/// 這種地方只剩例外能用。若每個套件各自定義一種例外來攜帶錯誤,消費端就得攔 N 種例外才取得回
/// <see cref="Abstractions.Error"/>,而且每一種的取法都要查文件。這個型別就是那個統一的載具。
/// BCL interfaces fix their signatures, and <see cref="Result{T}"/> cannot cross them:
/// <c>DelegatingHandler.SendAsync</c> must return <c>Task&lt;HttpResponseMessage&gt;</c> and
/// <c>BackgroundService.ExecuteAsync</c> must return <c>Task</c>. Only an exception fits there. If every package
/// defines its own carrier, consumers end up catching N exception types to recover an
/// <see cref="Abstractions.Error"/>, each with its own retrieval convention. This type is the single carrier.
/// </para>
/// <para>
/// <b>繼承 <see cref="InvalidOperationException"/> 是刻意的。</b><see cref="Result.ThrowIfFailure"/> 與
/// <see cref="Result{T}.GetValueOrThrow"/> 原本擲出的就是 <see cref="InvalidOperationException"/>;改成擲出
/// 本型別之後,既有那些 <c>catch (InvalidOperationException)</c> 的呼叫端完全不受影響,而想取回
/// <see cref="Abstractions.Error"/> 的呼叫端改攔本型別即可。這讓它是純新增而不是破壞性變更。
/// <b>Deriving from <see cref="InvalidOperationException"/> is deliberate.</b> <see cref="Result.ThrowIfFailure"/>
/// and <see cref="Result{T}.GetValueOrThrow"/> already threw <see cref="InvalidOperationException"/>; throwing this
/// type instead leaves every existing <c>catch (InvalidOperationException)</c> working, while callers that want the
/// <see cref="Abstractions.Error"/> back simply catch this type. That makes it purely additive rather than breaking.
/// </para>
/// <para>
/// 這仍然是例外,適用的仍然是原本的規則:預期之內的失敗請用 <see cref="Result"/> 表達,只有在型別簽章
/// 擋住 <see cref="Result"/> 的邊界上才改用它。
/// It is still an exception and the usual rule still applies: express expected failures with <see cref="Result"/>,
/// and reach for this only at a boundary whose signature leaves no room for one.
/// </para>
/// </remarks>
public sealed class ResultException : InvalidOperationException
{
    /// <summary>
    /// 未提供 <see cref="Abstractions.Error"/> 時使用的錯誤代碼。
    /// The error code used when no <see cref="Abstractions.Error"/> was supplied.
    /// </summary>
    private const string FallbackCode = "core.result_exception";

    /// <summary>
    /// 未提供訊息時使用的預設訊息。
    /// The default message used when none was supplied.
    /// </summary>
    private const string FallbackMessage = "結果為失敗。The result represents a failure.";

    /// <summary>
    /// 以既有的錯誤建立例外。這是正常路徑,其餘建構式只是為了滿足例外型別的標準形狀。
    /// Creates the exception from an existing error. This is the intended path; the other constructors exist only
    /// to satisfy the standard shape expected of an exception type.
    /// </summary>
    /// <param name="error">
    /// 要攜帶的錯誤。
    /// The error to carry.
    /// </param>
    /// <remarks>
    /// 例外訊息取自 <see cref="Abstractions.Error.ToString"/>,<see cref="Exception.InnerException"/> 取自
    /// <see cref="Abstractions.Error.Exception"/> —— 沒有攔到這個型別的呼叫端(或只看 log 的人)仍然讀得到
    /// 完整資訊,不會因為錯誤被包進物件而消失。
    /// The message comes from <see cref="Abstractions.Error.ToString"/> and <see cref="Exception.InnerException"/>
    /// from <see cref="Abstractions.Error.Exception"/>, so a caller that does not catch this type — or anyone reading
    /// a log — still sees everything; nothing disappears just because the error moved into a property.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    public ResultException(Error error)
        : base(Require(error).ToString(), error.Exception)
    {
        Error = error;
    }

    /// <summary>
    /// 以預設訊息建立例外。
    /// Creates the exception with a default message.
    /// </summary>
    /// <remarks>
    /// 例外型別的標準建構式之一。沒有來源錯誤時會合成一個 <see cref="ErrorCategory.Unexpected"/> 分類的
    /// 錯誤,讓 <see cref="Error"/> 的「保證非 null」契約在任何建構路徑上都成立。
    /// One of the standard exception constructors. With no source error, a synthetic
    /// <see cref="ErrorCategory.Unexpected"/> error is created so that the non-null guarantee on <see cref="Error"/>
    /// holds no matter which constructor was used.
    /// </remarks>
    public ResultException()
        : base(FallbackMessage)
    {
        Error = CreateFallbackError(FallbackMessage, null);
    }

    /// <summary>
    /// 以指定訊息建立例外。
    /// Creates the exception with the supplied message.
    /// </summary>
    /// <param name="message">例外訊息。The exception message.</param>
    public ResultException(string message)
        : base(message)
    {
        Error = CreateFallbackError(message, null);
    }

    /// <summary>
    /// 以指定訊息與內層例外建立例外。
    /// Creates the exception with the supplied message and inner exception.
    /// </summary>
    /// <param name="message">例外訊息。The exception message.</param>
    /// <param name="innerException">內層例外。The inner exception.</param>
    public ResultException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = CreateFallbackError(message, innerException);
    }

    /// <summary>
    /// 這次失敗的內容。保證非 <see langword="null"/>,無論走哪一個建構式。
    /// The failure details. Never <see langword="null"/>, whichever constructor was used.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// 驗證錯誤非 null,供建構式在呼叫基底建構式之前使用。
    /// Validates that the error is non-null, for use before the base constructor call.
    /// </summary>
    /// <param name="error">要驗證的錯誤。The error to validate.</param>
    /// <returns>同一個錯誤。The same error.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    private static Error Require(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error;
    }

    /// <summary>
    /// 在沒有來源錯誤時合成一個,維持 <see cref="Error"/> 的非 null 契約。
    /// Synthesises an error when there is no source one, preserving the non-null contract on <see cref="Error"/>.
    /// </summary>
    /// <param name="message">
    /// 例外訊息。空白時改用預設訊息 —— <see cref="Abstractions.Error"/> 不接受空白訊息,而從例外建構式
    /// 擲出 <see cref="ArgumentException"/> 只會掩蓋真正要回報的失敗。
    /// The exception message. A blank one falls back to the default: <see cref="Abstractions.Error"/> rejects blank
    /// messages, and throwing an <see cref="ArgumentException"/> out of an exception constructor would only bury the
    /// failure that was being reported.
    /// </param>
    /// <param name="exception">要保留的內層例外。The inner exception to keep, if any.</param>
    /// <returns>合成的錯誤。The synthesised error.</returns>
    private static Error CreateFallbackError(string? message, Exception? exception) =>
        new(FallbackCode, string.IsNullOrWhiteSpace(message) ? FallbackMessage : message)
        {
            Exception = exception,
        };
}

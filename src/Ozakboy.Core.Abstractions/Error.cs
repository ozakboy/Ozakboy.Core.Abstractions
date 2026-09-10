namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 描述一次失敗:錯誤代碼、可讀訊息與分類。作為 <see cref="Result"/> 與 <see cref="Result{T}"/> 的失敗內容。
/// Describes a failure with a code, a human-readable message, and a category. Carried by <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// 這個型別的用途是把「預期之內的失敗」從例外機制中拉出來。網路逾時、被交易所限流、訂單數量不符合
/// 最小下單量 —— 這些在交易系統裡是每天都會發生的正常情況,用例外表達會讓呼叫端被迫用 try/catch
/// 處理控制流,也讓「忘記處理」變成執行期才會發現的問題。真正的程式缺陷仍然應該丟例外。
/// This type exists to move expected failures out of the exception mechanism. Network timeouts, exchange
/// rate limiting, and orders below the minimum notional are routine events in a trading system; expressing
/// them as exceptions forces callers to use try/catch for control flow and turns "forgot to handle it" into
/// a runtime surprise. Genuine defects should still throw.
/// </remarks>
// CA1716:型別名稱 Error 與 Visual Basic 的 Error 陳述式同名,分析器建議改名。
// 這裡刻意保留 —— Error 是這個領域的標準命名(Rust、F#、Swift 以及各家 Result 函式庫皆然),
// 改成 ResultError 或 Failure 只會讓 API 讀起來更差,而本套件並不以 Visual Basic 消費端為目標。
#pragma warning disable CA1716
public sealed record Error
#pragma warning restore CA1716
{
    /// <summary>
    /// 建立錯誤。
    /// Creates an error.
    /// </summary>
    /// <param name="code">
    /// 機器可讀的錯誤代碼,用於程式分支與統計。不可為空白。
    /// A machine-readable code used for branching and aggregation. Must not be blank.
    /// </param>
    /// <param name="message">
    /// 人類可讀的錯誤訊息。不可為空白。
    /// A human-readable message. Must not be blank.
    /// </param>
    /// <param name="category">
    /// 錯誤分類,預設為 <see cref="ErrorCategory.Unexpected"/>。
    /// The failure category; defaults to <see cref="ErrorCategory.Unexpected"/>.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="code"/> 或 <paramref name="message"/> 為 <see langword="null"/>、空字串或僅含空白時擲出。
    /// Thrown when <paramref name="code"/> or <paramref name="message"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public Error(string code, string message, ErrorCategory category = ErrorCategory.Unexpected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
        Category = category;
    }

    /// <summary>
    /// 機器可讀的錯誤代碼。
    /// The machine-readable error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// 人類可讀的錯誤訊息。
    /// The human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// 錯誤分類。
    /// The failure category.
    /// </summary>
    public ErrorCategory Category { get; }

    /// <summary>
    /// 造成這次失敗的例外(若有)。僅供診斷用,不參與相等性比較。
    /// The exception behind this failure, if any. Diagnostic only; it does not take part in equality.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// 這次失敗是否為暫時性(值得在退避後重試)。
    /// Whether this failure is transient and worth retrying after a backoff.
    /// </summary>
    public bool IsTransient => Category.IsTransient();

    /// <summary>
    /// 尚未初始化的錯誤。當 <see cref="Result"/> 或 <see cref="Result{T}"/> 是 <see langword="default"/>
    /// 值(例如未指派的欄位、陣列預設元素)時,其錯誤內容即為此值。
    /// The placeholder used when a <see cref="Result"/> or <see cref="Result{T}"/> is a <see langword="default"/>
    /// value, such as an unassigned field or a default array element.
    /// </summary>
    /// <remarks>
    /// 看到這個錯誤代表某處回傳了未經賦值的結果,屬於程式缺陷,而不是執行期的正常失敗。
    /// Encountering this error means some code returned an unassigned result, which is a defect rather than
    /// a normal runtime failure.
    /// </remarks>
    public static Error Uninitialized { get; } =
        new("core.uninitialized_result", "結果值未經初始化。The result value was never initialised.", ErrorCategory.Internal);

    /// <summary>
    /// 建立輸入驗證失敗的錯誤。
    /// Creates a validation failure.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Validation"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Validation"/>.</returns>
    public static Error Validation(string code, string message) => new(code, message, ErrorCategory.Validation);

    /// <summary>
    /// 建立資源不存在的錯誤。
    /// Creates a not-found failure.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.NotFound"/> 的錯誤。An error categorised as <see cref="ErrorCategory.NotFound"/>.</returns>
    public static Error NotFound(string code, string message) => new(code, message, ErrorCategory.NotFound);

    /// <summary>
    /// 建立狀態衝突的錯誤。
    /// Creates a conflict failure.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Conflict"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Conflict"/>.</returns>
    public static Error Conflict(string code, string message) => new(code, message, ErrorCategory.Conflict);

    /// <summary>
    /// 建立逾時錯誤(暫時性)。
    /// Creates a timeout failure, which is transient.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Timeout"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Timeout"/>.</returns>
    public static Error Timeout(string code, string message) => new(code, message, ErrorCategory.Timeout);

    /// <summary>
    /// 建立網路層錯誤(暫時性)。
    /// Creates a network failure, which is transient.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Network"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Network"/>.</returns>
    public static Error Network(string code, string message) => new(code, message, ErrorCategory.Network);

    /// <summary>
    /// 建立限流錯誤(暫時性)。
    /// Creates a rate-limit failure, which is transient.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.RateLimited"/> 的錯誤。An error categorised as <see cref="ErrorCategory.RateLimited"/>.</returns>
    public static Error RateLimited(string code, string message) => new(code, message, ErrorCategory.RateLimited);

    /// <summary>
    /// 建立內部缺陷錯誤。
    /// Creates an internal-defect failure.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Internal"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Internal"/>.</returns>
    public static Error Internal(string code, string message) => new(code, message, ErrorCategory.Internal);

    /// <summary>
    /// 由例外建立錯誤,並保留原始例外供診斷。
    /// Creates an error from an exception, keeping the original for diagnostics.
    /// </summary>
    /// <param name="exception">
    /// 來源例外。
    /// The source exception.
    /// </param>
    /// <param name="code">
    /// 錯誤代碼;若省略則使用例外型別名稱。
    /// The error code; the exception type name is used when omitted.
    /// </param>
    /// <param name="category">
    /// 錯誤分類,預設為 <see cref="ErrorCategory.Unexpected"/>。
    /// The failure category; defaults to <see cref="ErrorCategory.Unexpected"/>.
    /// </param>
    /// <returns>
    /// 保留了 <see cref="Exception"/> 的錯誤。
    /// An error carrying the original <see cref="Exception"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="exception"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="exception"/> is <see langword="null"/>.
    /// </exception>
    public static Error FromException(
        Exception exception,
        string? code = null,
        ErrorCategory category = ErrorCategory.Unexpected)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return new Error(code ?? exception.GetType().Name, exception.Message, category)
        {
            Exception = exception,
        };
    }

    /// <summary>
    /// 比較兩個錯誤是否相同。僅比較 <see cref="Code"/>、<see cref="Message"/> 與 <see cref="Category"/>;
    /// <see cref="Exception"/> 不列入比較,因為例外實例沒有值語義,兩個內容相同的例外並不相等。
    /// Compares two errors by <see cref="Code"/>, <see cref="Message"/>, and <see cref="Category"/> only.
    /// <see cref="Exception"/> is excluded because exception instances have no value semantics.
    /// </summary>
    /// <param name="other">要比較的另一個錯誤。The other error to compare with.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when they are equivalent.</returns>
    public bool Equals(Error? other) =>
        other is not null
        && string.Equals(Code, other.Code, StringComparison.Ordinal)
        && string.Equals(Message, other.Message, StringComparison.Ordinal)
        && Category == other.Category;

    /// <summary>
    /// 取得雜湊碼,與 <see cref="Equals(Error?)"/> 的比較欄位一致。
    /// Returns a hash code consistent with the fields compared by <see cref="Equals(Error?)"/>.
    /// </summary>
    /// <returns>雜湊碼。The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Code, Message, Category);

    /// <summary>
    /// 回傳「代碼: 訊息」形式的字串。
    /// Returns the error in "code: message" form.
    /// </summary>
    /// <returns>可讀的錯誤敘述。A readable description of the error.</returns>
    public override string ToString() => $"{Code}: {Message}";
}

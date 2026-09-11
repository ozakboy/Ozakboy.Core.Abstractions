using System.Globalization;

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
    /// 與這次失敗有關的結構化資料(若有)。不參與相等性比較。
    /// Structured data related to this failure, if any. Does not take part in equality.
    /// </summary>
    /// <remarks>
    /// 用途是讓上層不必從訊息字串裡剖析數值。例如「數量低於最小下單量」除了給人看的訊息之外,
    /// 還可以附上實際數量與最小值,讓上層能直接決定要不要補到最小值,而不是用正規表示式去撈。
    /// 訊息是給人看的,會隨時被改寫;要被程式讀的東西應該放在這裡。
    /// This spares callers from parsing numbers back out of the message. A "quantity below minimum" failure can
    /// carry the actual quantity and the minimum alongside the human-readable text, so the caller can decide
    /// whether to round up without running a regular expression over the message. Messages are for people and get
    /// rewritten; anything a program needs to read belongs here.
    /// </remarks>
    public IReadOnlyDictionary<string, string>? Data { get; init; }

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
    /// 建立「重試機會已用盡」的終局錯誤(非暫時性)。
    /// Creates a terminal "attempts exhausted" failure, which is not transient.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <returns>分類為 <see cref="ErrorCategory.Exhausted"/> 的錯誤。An error categorised as <see cref="ErrorCategory.Exhausted"/>.</returns>
    /// <remarks>
    /// 用於重連或重試次數用盡:操作原本可行,但這個物件的生命週期已經結束,呼叫端要換一個新的而不是重試。
    /// For a spent reconnect or retry budget: the operation used to work, but this object's lifetime is over and the
    /// caller must obtain a fresh one rather than retry.
    /// </remarks>
    public static Error Exhausted(string code, string message) => new(code, message, ErrorCategory.Exhausted);

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
    /// 把這個錯誤包成可以跨越例外邊界的 <see cref="ResultException"/>。
    /// Wraps this error in a <see cref="ResultException"/> so it can cross an exception boundary.
    /// </summary>
    /// <returns>攜帶這個錯誤的例外。An exception carrying this error.</returns>
    /// <remarks>
    /// 給「簽章固定、<see cref="Result"/> 過不去」的地方使用,例如實作 <c>DelegatingHandler.SendAsync</c>。
    /// 寫成 <c>throw error.ToException();</c> 比 <c>throw new ResultException(error);</c> 短,而且讀起來像是
    /// 錯誤本身換了個載具,不是憑空生出一個例外。
    /// For places where the signature is fixed and a <see cref="Result"/> cannot pass, such as an implementation of
    /// <c>DelegatingHandler.SendAsync</c>. Writing <c>throw error.ToException();</c> is shorter than
    /// <c>throw new ResultException(error);</c> and reads as the error changing vehicle rather than a new exception
    /// appearing from nowhere.
    /// </remarks>
    public ResultException ToException() => new(this);

    /// <summary>
    /// 回傳附加了一筆 <see cref="Data"/> 的新錯誤,原錯誤不變。
    /// Returns a new error with one <see cref="Data"/> entry added; this instance is unchanged.
    /// </summary>
    /// <param name="key">資料鍵。不可為空白。The data key; must not be blank.</param>
    /// <param name="value">資料值。The data value.</param>
    /// <returns>附加資料後的新錯誤。A new error carrying the added entry.</returns>
    /// <remarks>
    /// <para>
    /// 沒有這組方法時,要附上「statusCode 加 body」得先建一個 <see cref="Dictionary{TKey, TValue}"/> 再
    /// <c>with</c> 進去,三行才寫得完一件小事;有了之後就是
    /// <c>error.WithData("statusCode", 429).WithData("retryAfterMs", 1500)</c>。
    /// Without these, attaching a status code and a body means building a <see cref="Dictionary{TKey, TValue}"/> and
    /// then <c>with</c>-ing it in — three lines for a small thing. With them it reads as
    /// <c>error.WithData("statusCode", 429).WithData("retryAfterMs", 1500)</c>.
    /// </para>
    /// <para>
    /// 同一個鍵重複附加時以最後一次為準。<see cref="Data"/> 不參與相等性比較,所以附加資料不會改變
    /// 這個錯誤與其他錯誤的相等關係。
    /// Adding the same key twice keeps the last value. <see cref="Data"/> takes no part in equality, so adding data
    /// never changes how this error compares with another.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="key"/> 為空白時擲出。
    /// Thrown when <paramref name="key"/> is blank.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="key"/> 或 <paramref name="value"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="key"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    public Error WithData(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var merged = CopyData();
        merged[key] = value;

        return this with { Data = merged };
    }

    /// <summary>
    /// 回傳附加了一筆十進位數值的新錯誤。以 <see cref="Precision.ToPlainString"/> 序列化。
    /// Returns a new error with a decimal entry added, serialised through <see cref="Precision.ToPlainString"/>.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">資料值。The value.</param>
    /// <returns>附加資料後的新錯誤。A new error carrying the added entry.</returns>
    /// <remarks>
    /// 值仍然存成字串,因為 <see cref="Data"/> 要能整份序列化進日誌與資料庫;但寫入與讀取兩端都提供型別化的
    /// 方法,呼叫端就不必自己記得 <see cref="CultureInfo.InvariantCulture"/>,也不會在某個 locale 下才爆。
    /// 走 <see cref="Precision.ToPlainString"/> 而不是 <c>ToString</c>,是為了避免小數值寫成 <c>1E-05</c>。
    /// The value is still stored as text because <see cref="Data"/> must serialise wholesale into logs and databases;
    /// but typed methods on both sides mean the caller never has to remember
    /// <see cref="CultureInfo.InvariantCulture"/> and never discovers the omission in a different locale. It goes
    /// through <see cref="Precision.ToPlainString"/> rather than <c>ToString</c> so small values do not become
    /// <c>1E-05</c>.
    /// </remarks>
    public Error WithData(string key, decimal value) => WithData(key, Precision.ToPlainString(value));

    /// <summary>
    /// 回傳附加了一筆整數的新錯誤。以 <see cref="CultureInfo.InvariantCulture"/> 序列化。
    /// Returns a new error with an integer entry added, serialised with <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">資料值。The value.</param>
    /// <returns>附加資料後的新錯誤。A new error carrying the added entry.</returns>
    public Error WithData(string key, long value) => WithData(key, value.ToString(CultureInfo.InvariantCulture));

    /// <summary>
    /// 回傳附加了一筆布林值的新錯誤。序列化為小寫的 <c>true</c> 或 <c>false</c>。
    /// Returns a new error with a boolean entry added, serialised as lowercase <c>true</c> or <c>false</c>.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">資料值。The value.</param>
    /// <returns>附加資料後的新錯誤。A new error carrying the added entry.</returns>
    /// <remarks>
    /// 刻意用小寫而不是 <c>bool.ToString()</c> 的 <c>True</c>/<c>False</c>:<see cref="Data"/> 常常直接落進
    /// JSON 日誌,小寫才是那裡的慣例。讀回來的 <see cref="TryGetBoolean"/> 不分大小寫,兩種寫法都吃得下。
    /// Lowercase on purpose rather than the <c>True</c>/<c>False</c> that <c>bool.ToString()</c> produces:
    /// <see cref="Data"/> often lands straight in a JSON log, where lowercase is the convention.
    /// <see cref="TryGetBoolean"/> is case-insensitive and reads either form back.
    /// </remarks>
    public Error WithData(string key, bool value) => WithData(key, value ? "true" : "false");

    /// <summary>
    /// 回傳附加了多筆 <see cref="Data"/> 的新錯誤。
    /// Returns a new error with several <see cref="Data"/> entries added.
    /// </summary>
    /// <param name="entries">要附加的資料。The entries to add.</param>
    /// <returns>附加資料後的新錯誤。A new error carrying the added entries.</returns>
    /// <remarks>
    /// 重複的鍵以列舉順序中最後一筆為準。
    /// A repeated key keeps the last value in enumeration order.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> 為 <see langword="null"/>,或其中任一筆的鍵或值為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="entries"/> is <see langword="null"/>, or any key or value within it is.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// 任一筆的鍵為空白時擲出。
    /// Thrown when any key within it is blank.
    /// </exception>
    public Error WithData(IEnumerable<KeyValuePair<string, string>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var merged = CopyData();
        foreach (var entry in entries)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(entry.Key, nameof(entries));
            ArgumentNullException.ThrowIfNull(entry.Value, nameof(entries));

            merged[entry.Key] = entry.Value;
        }

        return this with { Data = merged };
    }

    /// <summary>
    /// 讀取一筆 <see cref="Data"/>。
    /// Reads one <see cref="Data"/> entry.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">
    /// 找到時輸出資料值,否則為 <see langword="null"/>。
    /// Receives the value when found; otherwise <see langword="null"/>.
    /// </param>
    /// <returns>找到時回傳 <see langword="true"/>。<see langword="true"/> when the key was found.</returns>
    /// <remarks>
    /// 與 <see cref="WithData(string, string)"/> 不同,這裡對不合法的鍵回傳 <see langword="false"/> 而不擲出
    /// 例外 —— 與 <see cref="Precision.TryFloorToStep"/> 的取捨相同:寫入端的錯誤鍵是缺陷,讀取端則常常是
    /// 「這筆資料本來就可能不存在」。
    /// Unlike <see cref="WithData(string, string)"/>, an invalid key returns <see langword="false"/> here instead of
    /// throwing — the same trade-off as <see cref="Precision.TryFloorToStep"/>: a bad key on the writing side is a
    /// defect, while on the reading side "this entry may simply not be there" is the normal case.
    /// </remarks>
    public bool TryGetData(string key, out string? value)
    {
        if (Data is null || string.IsNullOrWhiteSpace(key))
        {
            value = null;
            return false;
        }

        return Data.TryGetValue(key, out value);
    }

    /// <summary>
    /// 讀取一筆 <see cref="Data"/> 並解析為十進位數值,一律使用 <see cref="CultureInfo.InvariantCulture"/>。
    /// Reads one <see cref="Data"/> entry and parses it as a decimal, always with
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">解析成功時輸出數值,否則為零。Receives the value on success; zero otherwise.</param>
    /// <returns>找到且解析成功時回傳 <see langword="true"/>。<see langword="true"/> when found and parsed.</returns>
    public bool TryGetDecimal(string key, out decimal value)
    {
        value = 0m;
        return TryGetData(key, out var text) && Precision.TryParsePlain(text, out value);
    }

    /// <summary>
    /// 讀取一筆 <see cref="Data"/> 並解析為 64 位元整數,一律使用 <see cref="CultureInfo.InvariantCulture"/>。
    /// Reads one <see cref="Data"/> entry and parses it as a 64-bit integer, always with
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">解析成功時輸出數值,否則為零。Receives the value on success; zero otherwise.</param>
    /// <returns>找到且解析成功時回傳 <see langword="true"/>。<see langword="true"/> when found and parsed.</returns>
    public bool TryGetInt64(string key, out long value)
    {
        value = 0L;
        return TryGetData(key, out var text)
            && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// 讀取一筆 <see cref="Data"/> 並解析為布林值,不分大小寫。
    /// Reads one <see cref="Data"/> entry and parses it as a boolean, case-insensitively.
    /// </summary>
    /// <param name="key">資料鍵。The data key.</param>
    /// <param name="value">解析成功時輸出數值,否則為 <see langword="false"/>。Receives the value on success; otherwise <see langword="false"/>.</param>
    /// <returns>找到且解析成功時回傳 <see langword="true"/>。<see langword="true"/> when found and parsed.</returns>
    public bool TryGetBoolean(string key, out bool value)
    {
        value = false;
        return TryGetData(key, out var text) && bool.TryParse(text, out value);
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

    /// <summary>
    /// 複製目前的 <see cref="Data"/> 成一份可寫入的字典,供增補方法使用。
    /// Copies the current <see cref="Data"/> into a writable dictionary for the augmenting methods.
    /// </summary>
    /// <returns>可寫入的副本;原本沒有資料時為空字典。A writable copy; empty when there was no data.</returns>
    /// <remarks>
    /// 一律複製而不是就地改寫。<see cref="Error"/> 是不可變的值,而且同一份 <see cref="Data"/> 可能被
    /// <see cref="Result{T}.ToFailure{TOut}"/> 之類的方法沿用到別的錯誤上,就地改寫會波及那些實例。
    /// Always copies rather than mutating in place. <see cref="Error"/> is an immutable value, and the same
    /// <see cref="Data"/> instance may have been carried onto another error by something like
    /// <see cref="Result{T}.ToFailure{TOut}"/>; mutating it would reach those instances too.
    /// </remarks>
    private Dictionary<string, string> CopyData() =>
        Data is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(Data, StringComparer.Ordinal);
}

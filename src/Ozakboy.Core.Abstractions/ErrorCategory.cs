namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 錯誤分類。用於區分失敗的性質,呼叫端據此決定要重試、要放棄,還是要告警。
/// Classification of a failure, letting callers decide whether to retry, give up, or raise an alert.
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// 未指定分類的錯誤。預設值,語意等同「非預期的失敗」。
    /// An unclassified failure. The default value, semantically equivalent to an unexpected failure.
    /// </summary>
    Unexpected = 0,

    /// <summary>
    /// 輸入不合法(參數超出範圍、格式錯誤、必填欄位缺漏)。重試不會有幫助。
    /// Invalid input such as an out-of-range argument, malformed value, or missing required field. Retrying will not help.
    /// </summary>
    Validation = 1,

    /// <summary>
    /// 找不到目標資源。
    /// The requested resource does not exist.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// 與目前狀態衝突(例如重複建立、狀態機不允許的轉換)。
    /// Conflicts with the current state, such as a duplicate creation or a transition the state machine forbids.
    /// </summary>
    Conflict = 3,

    /// <summary>
    /// 未通過身分驗證(憑證缺漏、過期或錯誤)。
    /// Authentication failed because credentials are missing, expired, or incorrect.
    /// </summary>
    Unauthorized = 4,

    /// <summary>
    /// 身分正確但權限不足。
    /// The caller is authenticated but lacks permission for this operation.
    /// </summary>
    Forbidden = 5,

    /// <summary>
    /// 操作逾時。屬於暫時性錯誤,通常值得重試。
    /// The operation timed out. Transient, and usually worth retrying.
    /// </summary>
    Timeout = 6,

    /// <summary>
    /// 網路層失敗(連線中斷、DNS 解析失敗、TLS 交握失敗)。屬於暫時性錯誤。
    /// A network-level failure such as a dropped connection, DNS failure, or TLS handshake error. Transient.
    /// </summary>
    Network = 7,

    /// <summary>
    /// 被對方限流。屬於暫時性錯誤,但重試前必須先退避,否則會加重限流。
    /// The caller is being rate limited. Transient, but you must back off before retrying or the limit will tighten.
    /// </summary>
    RateLimited = 8,

    /// <summary>
    /// 服務暫時無法使用(維護中、上游故障)。屬於暫時性錯誤。
    /// The service is temporarily unavailable due to maintenance or an upstream outage. Transient.
    /// </summary>
    Unavailable = 9,

    /// <summary>
    /// 操作被取消(呼叫端主動取消或程序關閉)。不應視為故障告警。
    /// The operation was cancelled by the caller or during shutdown. Should not be alerted on as a fault.
    /// </summary>
    Cancelled = 10,

    /// <summary>
    /// 自身內部錯誤(程式缺陷、不變條件被打破)。重試不會有幫助,應該記錄並修正。
    /// An internal defect such as a broken invariant. Retrying will not help; log it and fix the code.
    /// </summary>
    Internal = 11,

    /// <summary>
    /// 這個實作不支援該操作。與 <see cref="Validation"/> 的差別在於:輸入沒有問題,是這個實作做不到。
    /// The operation is not supported by this implementation. Unlike <see cref="Validation"/>, the input is fine —
    /// this particular implementation simply cannot do it.
    /// </summary>
    /// <remarks>
    /// 典型情境是同一個介面有多個實作而能力不對等,例如回測用的模擬交易所無法變更保證金模式。
    /// 這不是缺陷也不是暫時性失敗,呼叫端通常應該改走別條路而不是重試。
    /// The typical case is an interface with implementations of unequal capability — a simulated exchange used for
    /// backtesting cannot change margin mode, for instance. This is neither a defect nor a transient failure, and
    /// callers should normally take a different path rather than retry.
    /// </remarks>
    NotSupported = 12,

    /// <summary>
    /// 重試或重連的機會已經用盡,這個物件的生命週期到此為止。重試沒有意義,必須換一個新的。
    /// The retry or reconnect budget is spent and this object's lifetime is over. Retrying is pointless; the caller
    /// must obtain a fresh one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 與其他非暫時性分類的差別在於「曾經可行」:<see cref="Validation"/> 是輸入本身有問題,
    /// <see cref="NotSupported"/> 是這個實作從一開始就做不到,而 <see cref="Exhausted"/> 是原本做得到、
    /// 只是重試機會已經用完了。典型情境是 WebSocket 重連次數用盡、重試次數用盡。
    /// What separates it from the other non-transient categories is that it once worked:
    /// <see cref="Validation"/> means the input is wrong, <see cref="NotSupported"/> means this implementation never
    /// could, and <see cref="Exhausted"/> means it could, but the attempts are spent. The typical cases are a
    /// WebSocket that ran out of reconnect attempts and a retry loop that ran out of tries.
    /// </para>
    /// <para>
    /// 這個分類存在的理由是 <see cref="ErrorCategoryExtensions.IsTransient"/> 必須維持「是否值得重試」的
    /// 單一真相來源。「重連次數用盡」語意上最接近 <see cref="Unavailable"/>,但那是暫時性分類,套上去會讓
    /// <see cref="ErrorCategoryExtensions.IsTransient"/> 對這個錯誤給出錯的答案,呼叫端只好改用錯誤代碼判斷
    /// —— 一旦有人這樣做,這個屬性就不再可信了。
    /// This category exists so that <see cref="ErrorCategoryExtensions.IsTransient"/> can remain the single source of
    /// truth for "is this worth retrying". "Out of reconnect attempts" is semantically closest to
    /// <see cref="Unavailable"/>, but that is a transient category, so reusing it would make
    /// <see cref="ErrorCategoryExtensions.IsTransient"/> answer wrongly and push callers into branching on error
    /// codes instead — and once anyone does that, the property has stopped being trustworthy.
    /// </para>
    /// </remarks>
    Exhausted = 13,
}

/// <summary>
/// <see cref="ErrorCategory"/> 的輔助方法。
/// Helper methods for <see cref="ErrorCategory"/>.
/// </summary>
public static class ErrorCategoryExtensions
{
    /// <summary>
    /// 判斷此分類是否為暫時性錯誤(值得在退避後重試)。
    /// Determines whether the category represents a transient failure that is worth retrying after a backoff.
    /// </summary>
    /// <param name="category">
    /// 要判斷的錯誤分類。
    /// The category to inspect.
    /// </param>
    /// <returns>
    /// 暫時性錯誤回傳 <see langword="true"/>,否則回傳 <see langword="false"/>。
    /// <see langword="true"/> for transient failures; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// 暫時性分類為 <see cref="ErrorCategory.Timeout"/>、<see cref="ErrorCategory.Network"/>、
    /// <see cref="ErrorCategory.RateLimited"/> 與 <see cref="ErrorCategory.Unavailable"/>。
    /// 其餘分類重試也不會改變結果,不應納入重試迴圈。
    /// The transient categories are <see cref="ErrorCategory.Timeout"/>, <see cref="ErrorCategory.Network"/>,
    /// <see cref="ErrorCategory.RateLimited"/>, and <see cref="ErrorCategory.Unavailable"/>. Retrying any other
    /// category cannot change the outcome and should stay out of retry loops.
    /// 特別注意 <see cref="ErrorCategory.Exhausted"/>:它描述的是「原本可行但機會已用盡」,因此不是暫時性的,
    /// 不可以因為它看起來像 <see cref="ErrorCategory.Unavailable"/> 就歸為可重試。
    /// Note <see cref="ErrorCategory.Exhausted"/> in particular: it describes something that used to work but has run
    /// out of attempts, so it is not transient, however much it may resemble <see cref="ErrorCategory.Unavailable"/>.
    /// </remarks>
    public static bool IsTransient(this ErrorCategory category) => category switch
    {
        ErrorCategory.Timeout or ErrorCategory.Network or ErrorCategory.RateLimited or ErrorCategory.Unavailable => true,
        _ => false,
    };
}

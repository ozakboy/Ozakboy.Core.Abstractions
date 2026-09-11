namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 重試策略的描述:重試幾次、每次等多久、什麼樣的失敗才值得重試。
/// A description of a retry strategy: how many attempts, how long to wait, and which failures are worth retrying.
/// </summary>
/// <remarks>
/// <para>
/// 這個型別只負責「描述」與「計算間隔」,不負責執行重試迴圈,也不碰任何 I/O。實際的重試由使用它的
/// 元件執行(例如 HTTP 管線),這樣同一份策略可以被套用在完全不同的傳輸層上,也讓間隔計算能夠脫離
/// 時間與網路獨立測試。
/// This type only describes a strategy and computes intervals. It never runs a retry loop and never touches I/O.
/// The component that consumes it does the retrying — an HTTP pipeline, for instance — so one policy can be
/// applied across different transports, and the interval arithmetic stays testable without time or network.
/// </para>
/// <para>
/// <b>下單類請求絕對不要重試。</b>網路逾時不代表對方沒收到,盲目重送會產生重複委託。這類請求請用
/// <see cref="NoRetry"/>,並改以冪等識別碼(例如 client order id)搭配查詢對帳來確認結果。
/// <b>Never retry order placement.</b> A timeout does not mean the request failed to arrive, and resending
/// blindly creates duplicate orders. Use <see cref="NoRetry"/> for those and confirm the outcome through an
/// idempotency key such as a client order id plus a follow-up query.
/// </para>
/// </remarks>
public sealed record RetryPolicy
{
    /// <summary>
    /// 最多嘗試幾次(含第一次)。必須至少為 1。
    /// The maximum number of attempts including the first one. Must be at least 1.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 設定為小於 1 的值時擲出。
    /// Thrown when set to a value below 1.
    /// </exception>
    public int MaxAttempts
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            field = value;
        }
    } = 3;

    /// <summary>
    /// 退避間隔的基準值。不可為負。
    /// The base interval for the backoff. Must not be negative.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 設定為負值時擲出。
    /// Thrown when set to a negative value.
    /// </exception>
    public TimeSpan BaseDelay
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// 間隔的上限。無論策略怎麼成長都不會超過這個值。不可為負。
    /// The ceiling for the interval. No strategy will produce a delay beyond this. Must not be negative.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 設定為負值時擲出。
    /// Thrown when set to a negative value.
    /// </exception>
    public TimeSpan MaxDelay
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero);
            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 間隔的成長方式。
    /// How the interval grows.
    /// </summary>
    public BackoffStrategy Strategy { get; init; } = BackoffStrategy.Exponential;

    /// <summary>
    /// 抖動比例,範圍 0 到 1。0 表示不抖動,0.2 表示間隔在計算值的正負 20% 之間隨機浮動。
    /// The jitter ratio between 0 and 1. Zero disables jitter; 0.2 lets the interval vary by ±20%.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 抖動的用途是打散重試時機。多個客戶端同時遇到同一次故障時,若退避間隔完全一致,它們會在同一刻
    /// 一起重試,對剛恢復的服務造成第二波衝擊。
    /// Jitter spreads retries out. When many clients hit the same outage, identical backoff intervals make them
    /// all retry at the same instant and hit the recovering service with a second wave.
    /// </para>
    /// <para>
    /// 這個屬性使用 <see cref="double"/>。全案禁止用浮點數的規則針對的是價格、數量與金額;等待時間的
    /// 隨機抖動本身就沒有精確性需求,不在該規則的範圍內。
    /// This property is a <see cref="double"/>. The project-wide ban on floating point covers prices, quantities,
    /// and monetary amounts; random jitter on a wait interval has no precision requirement and is out of scope.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 設定為不在 0 到 1 之間的值時擲出。
    /// Thrown when set to a value outside the range 0 to 1.
    /// </exception>
    public double JitterRatio
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 0d);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 1d);
            field = value;
        }
    } = 0.2d;

    /// <summary>
    /// 自訂的重試判斷。設定時<b>完全取代</b> <see cref="Error.IsTransient"/> 的判斷。
    /// A custom retry predicate. When set it <b>entirely replaces</b> the <see cref="Error.IsTransient"/> check.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 語意刻意是「取代」而不是 AND 或 OR:呼叫端要嘛用內建的暫時性判斷,要嘛完全自己決定。混合語意
    /// (例如「暫時性且通過自訂判斷」)會讓「為什麼這個錯誤沒被重試」變成得同時翻兩處才答得出來的問題。
    /// The semantics are deliberately replacement rather than AND or OR: either the caller takes the built-in
    /// transience rule, or the caller decides entirely. A mixed rule — "transient *and* accepted by the predicate" —
    /// turns "why was this error not retried" into a question that needs two places checked to answer.
    /// </para>
    /// <para>
    /// 動機來自 HTTP:同樣是 429,帶著 <c>Retry-After</c> 的值得重試,而「IP 被封」的那種不值得。沒有這個
    /// 屬性時,這類判斷只能寫在 handler 裡,於是策略物件就不再是重試決策的唯一真相來源。
    /// The motivating case is HTTP: one 429 carries a <c>Retry-After</c> and is worth retrying, another means the IP
    /// is banned and is not. Without this, that judgement has to live in the handler, and the policy object stops
    /// being the single source of truth for the retry decision.
    /// </para>
    /// <para>
    /// 次數上限 <see cref="MaxAttempts"/> 不受這個委派影響,仍然一律套用 —— 自訂判斷負責「這個錯誤值不值得
    /// 重試」,不負責「還能不能再試」。
    /// <see cref="MaxAttempts"/> is not affected by this delegate and still always applies: the predicate decides
    /// whether an error is worth retrying, not whether any attempts remain.
    /// </para>
    /// <para>
    /// 這個屬性<b>不參與相等性比較</b>。理由與 <see cref="Error.Exception"/> 相同:委派沒有值語義,兩個內容
    /// 完全相同的 lambda 並不相等,列入比較會讓兩份設定相同的策略被判定為不同。
    /// This property <b>takes no part in equality</b>, for the same reason as <see cref="Error.Exception"/>:
    /// delegates have no value semantics, two identical lambdas are not equal, and including it would make two
    /// policies with identical settings compare as different.
    /// </para>
    /// </remarks>
    public Func<Error, bool>? RetryPredicate { get; init; }

    /// <summary>
    /// 不重試。適用於任何非冪等的操作,特別是送出委託。
    /// Never retry. Use for any non-idempotent operation, above all order placement.
    /// </summary>
    public static RetryPolicy NoRetry { get; } = new()
    {
        MaxAttempts = 1,
        BaseDelay = TimeSpan.Zero,
        Strategy = BackoffStrategy.None,
        JitterRatio = 0d,
    };

    /// <summary>
    /// 一般唯讀請求的預設策略:最多 3 次、200 毫秒起跳的指數退避、上限 30 秒、正負 20% 抖動。
    /// The default for ordinary read-only requests: up to 3 attempts, exponential backoff from 200 ms, capped at
    /// 30 seconds, with ±20% jitter.
    /// </summary>
    public static RetryPolicy Default { get; } = new();

    /// <summary>
    /// 計算第幾次重試前應該等待多久,並套用隨機抖動。
    /// Computes how long to wait before the given attempt, applying random jitter.
    /// </summary>
    /// <param name="attempt">
    /// 嘗試次數,從 1 開始。第 1 次是首次呼叫,通常不需要等待。
    /// The attempt number starting at 1. Attempt 1 is the initial call and normally waits not at all.
    /// </param>
    /// <returns>應等待的時間。The interval to wait.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attempt"/> 小於 1 時擲出。
    /// Thrown when <paramref name="attempt"/> is less than 1.
    /// </exception>
    public TimeSpan GetDelay(int attempt)
    {
        // 抖動只用來打散重試時機,不涉及任何安全性判斷,因此使用一般的虛擬亂數即可。
        // Jitter only spreads retries apart and carries no security decision, so an ordinary PRNG is appropriate.
#pragma warning disable CA5394 // Do not use insecure randomness
        var sample = Random.Shared.NextDouble();
#pragma warning restore CA5394

        return GetDelay(attempt, sample);
    }

    /// <summary>
    /// 以指定的抖動取樣值計算等待時間。供測試取得完全確定的結果。
    /// Computes the wait interval using a supplied jitter sample, giving tests a fully deterministic result.
    /// </summary>
    /// <param name="attempt">
    /// 嘗試次數,從 1 開始。
    /// The attempt number starting at 1.
    /// </param>
    /// <param name="jitterSample">
    /// 介於 0(含)與 1(含)之間的取樣值。0.5 代表不偏移,0 為最短、1 為最長。
    /// A sample between 0 and 1 inclusive. A value of 0.5 means no offset; 0 is the shortest and 1 the longest.
    /// </param>
    /// <returns>應等待的時間。The interval to wait.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="attempt"/> 小於 1,或 <paramref name="jitterSample"/> 不在 0 到 1 之間時擲出。
    /// Thrown when <paramref name="attempt"/> is below 1, or <paramref name="jitterSample"/> is outside 0 to 1.
    /// </exception>
    public TimeSpan GetDelay(int attempt, double jitterSample)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(jitterSample, 0d);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(jitterSample, 1d);

        if (Strategy == BackoffStrategy.None || BaseDelay == TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var baseMilliseconds = BaseDelay.TotalMilliseconds;
        var milliseconds = Strategy switch
        {
            BackoffStrategy.Fixed => baseMilliseconds,
            BackoffStrategy.Linear => baseMilliseconds * attempt,
            BackoffStrategy.Exponential => baseMilliseconds * Math.Pow(2d, attempt - 1),
            _ => baseMilliseconds,
        };

        // 指數成長很快就會超出可表示範圍,先收斂到上限再套抖動。
        // Exponential growth overflows quickly, so clamp to the ceiling before applying jitter.
        var maxMilliseconds = MaxDelay.TotalMilliseconds;
        if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds > maxMilliseconds)
        {
            milliseconds = maxMilliseconds;
        }

        if (JitterRatio > 0d)
        {
            var offset = ((jitterSample * 2d) - 1d) * JitterRatio;
            milliseconds *= 1d + offset;
        }

        milliseconds = Math.Clamp(milliseconds, 0d, maxMilliseconds);

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>
    /// 判斷這次失敗是否應該重試。
    /// Determines whether a failure should be retried.
    /// </summary>
    /// <param name="attempt">
    /// 已經嘗試的次數,從 1 開始。
    /// The number of attempts already made, starting at 1.
    /// </param>
    /// <param name="error">
    /// 這次的失敗內容。
    /// The failure that occurred.
    /// </param>
    /// <returns>
    /// 還有剩餘次數,且失敗通過重試判斷時回傳 <see langword="true"/>。
    /// <see langword="true"/> when attempts remain and the failure passes the retry check.
    /// </returns>
    /// <remarks>
    /// 重試判斷預設是 <see cref="Error.IsTransient"/>;設定了 <see cref="RetryPredicate"/> 時改用它,而且是
    /// 完全取代,不是額外加條件。
    /// The retry check defaults to <see cref="Error.IsTransient"/>. When <see cref="RetryPredicate"/> is set it is
    /// used instead — replacing the default outright, not adding a condition to it.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    public bool ShouldRetry(int attempt, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return attempt < MaxAttempts && (RetryPredicate?.Invoke(error) ?? error.IsTransient);
    }

    /// <summary>
    /// 比較兩份策略是否相同。比較所有設定值,但<b>排除</b> <see cref="RetryPredicate"/>。
    /// Compares two policies by every setting <b>except</b> <see cref="RetryPredicate"/>.
    /// </summary>
    /// <param name="other">要比較的另一份策略。The other policy to compare with.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when they are equivalent.</returns>
    /// <remarks>
    /// record 預設會把每個屬性都列入比較,但 <see cref="Func{T, TResult}"/> 沒有值語義:兩個程式碼一模一樣的
    /// lambda 是兩個不同的委派實例,永遠不相等。若不排除,<c>policy with { MaxAttempts = 3 }</c> 這種只想確認
    /// 「設定沒變」的比較就會無故失敗。作法與 <see cref="Error"/> 排除 <see cref="Error.Exception"/> 一致。
    /// A record compares every property by default, but <see cref="Func{T, TResult}"/> has no value semantics: two
    /// lambdas with identical source are two different delegate instances and never compare equal. Without the
    /// exclusion, a check as ordinary as "are these settings still the same" would fail for no visible reason. This
    /// mirrors how <see cref="Error"/> excludes <see cref="Error.Exception"/>.
    /// </remarks>
    public bool Equals(RetryPolicy? other) =>
        other is not null
        && MaxAttempts == other.MaxAttempts
        && BaseDelay == other.BaseDelay
        && MaxDelay == other.MaxDelay
        && Strategy == other.Strategy
        && JitterRatio.Equals(other.JitterRatio);

    /// <summary>
    /// 取得雜湊碼,與 <see cref="Equals(RetryPolicy?)"/> 的比較欄位一致。
    /// Returns a hash code consistent with the fields compared by <see cref="Equals(RetryPolicy?)"/>.
    /// </summary>
    /// <returns>雜湊碼。The hash code.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(MaxAttempts, BaseDelay, MaxDelay, Strategy, JitterRatio);
}

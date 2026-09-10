namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 重試之間的退避間隔如何隨嘗試次數成長。
/// How the delay between retries grows with each attempt.
/// </summary>
public enum BackoffStrategy
{
    /// <summary>
    /// 不等待,立刻重試。
    /// No delay; retry immediately.
    /// </summary>
    /// <remarks>
    /// 只適合本機、無外部相依的操作。對外部服務用這個等於在對方故障時加重它的負擔。
    /// Only suitable for local operations with no external dependency. Against a remote service this piles load
    /// onto something that is already failing.
    /// </remarks>
    None = 0,

    /// <summary>
    /// 每次都等待相同的間隔。
    /// Wait the same interval before every retry.
    /// </summary>
    Fixed = 1,

    /// <summary>
    /// 間隔隨嘗試次數線性成長(基準 × 次數)。
    /// The interval grows linearly with the attempt number: base × attempt.
    /// </summary>
    Linear = 2,

    /// <summary>
    /// 間隔隨嘗試次數指數成長(基準 × 2 的次方)。
    /// The interval grows exponentially: base × 2 raised to the attempt index.
    /// </summary>
    /// <remarks>
    /// 對外部服務的預設選擇。搭配抖動可以避免多個客戶端在同一時刻一起重試。
    /// The default choice against a remote service. Combined with jitter it stops many clients retrying in lockstep.
    /// </remarks>
    Exponential = 3,
}

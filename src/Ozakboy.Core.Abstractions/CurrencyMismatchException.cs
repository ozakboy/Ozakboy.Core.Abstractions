namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 對兩筆不同幣別的 <see cref="Money"/> 進行運算時擲出。
/// Thrown when an operation is attempted between two <see cref="Money"/> values of different currencies.
/// </summary>
/// <remarks>
/// 這是程式缺陷,不是執行期的正常失敗,所以用例外而不是 <see cref="Result"/> 表達。把 USDT 餘額和
/// BTC 數量相加沒有任何合理語意,應該在開發階段就被打斷。
/// This represents a defect rather than an ordinary runtime failure, which is why it is an exception instead of a
/// <see cref="Result"/>. Adding a USDT balance to a BTC quantity has no sensible meaning and should stop the
/// program during development.
/// </remarks>
public sealed class CurrencyMismatchException : InvalidOperationException
{
    /// <summary>
    /// 以預設訊息建立例外。
    /// Creates the exception with a default message.
    /// </summary>
    public CurrencyMismatchException()
        : base("兩筆金額的幣別不同,無法運算。Cannot operate on two amounts of different currencies.")
    {
    }

    /// <summary>
    /// 以指定訊息建立例外。
    /// Creates the exception with the supplied message.
    /// </summary>
    /// <param name="message">錯誤訊息。The error message.</param>
    public CurrencyMismatchException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 以指定訊息與內部例外建立例外。
    /// Creates the exception with the supplied message and inner exception.
    /// </summary>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <param name="innerException">內部例外。The inner exception.</param>
    public CurrencyMismatchException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 以兩個衝突的幣別建立例外。
    /// Creates the exception from the two conflicting currencies.
    /// </summary>
    /// <param name="left">左運算元的幣別。The currency of the left operand.</param>
    /// <param name="right">右運算元的幣別。The currency of the right operand.</param>
    public CurrencyMismatchException(string left, string right)
        : base($"幣別不同,無法運算:{left} 與 {right}。Cannot operate across currencies: {left} and {right}.")
    {
        Left = left;
        Right = right;
    }

    /// <summary>
    /// 左運算元的幣別。
    /// The currency of the left operand.
    /// </summary>
    public string? Left { get; }

    /// <summary>
    /// 右運算元的幣別。
    /// The currency of the right operand.
    /// </summary>
    public string? Right { get; }
}

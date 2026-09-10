using System.Globalization;

namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 十進位數值的精度處理:步進值對齊、小數位數判斷、字串序列化。
/// Decimal precision helpers: step alignment, scale inspection, and string serialisation.
/// </summary>
/// <remarks>
/// <para>
/// 交易所對每個商品都規定了價格與數量的最小變動單位(常見名稱為 tick size 與 step size),送出的
/// 委託只要沒有對齊就會被拒絕。這個類別提供的就是對齊運算本身,不含任何交易所的規則解讀。
/// Exchanges define a minimum increment for price and quantity on every instrument — commonly called tick size
/// and step size — and reject any order that is not aligned to it. This class provides the alignment arithmetic
/// itself and deliberately contains no exchange-specific rule interpretation.
/// </para>
/// <para>
/// 全部以 <see cref="decimal"/> 運算。金額與數量絕不可用 <see cref="double"/> 或 <see cref="float"/>:
/// 二進位浮點數無法精確表示 0.1 這類十進位小數,誤差會累積成真實的下單金額差異。
/// Everything here operates on <see cref="decimal"/>. Never use <see cref="double"/> or <see cref="float"/> for
/// money or quantities: binary floating point cannot represent decimal fractions such as 0.1 exactly, and the
/// error accumulates into real differences in order size.
/// </para>
/// <para>
/// 對齊運算內部會做 <c>value / step</c>。當數值接近 <see cref="decimal.MaxValue"/> 而步進值又極小時,
/// 這個除法會擲出 <see cref="OverflowException"/>。這裡刻意不攔截:能產生這種組合的輸入本身就不合理
/// (真實的交易規則不會出現這種尺度),把它包裝成失敗值只會掩蓋上游的資料問題。
/// Alignment internally computes <c>value / step</c>. With a value near <see cref="decimal.MaxValue"/> and a very
/// small step, that division throws an <see cref="OverflowException"/>. This is deliberately not caught: any input
/// producing such a combination is already nonsensical — real exchange rules never reach that scale — and wrapping
/// it in a failure value would only mask a data problem upstream.
/// </para>
/// </remarks>
public static class Precision
{
    /// <summary>
    /// 用於移除尾隨零的除數。除以 1 不改變數值,但會讓 <see cref="decimal"/> 重新計算有效位數。
    /// The divisor used to strip trailing zeros. Dividing by one leaves the value unchanged but makes
    /// <see cref="decimal"/> recompute its scale.
    /// </summary>
    private const decimal TrailingZeroStripper = 1.000000000000000000000000000000m;

    /// <summary>
    /// 將數值向下對齊到步進值的整數倍(往負無窮方向)。
    /// Aligns a value down to a multiple of the step, rounding towards negative infinity.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值,必須大於零。The step; must be greater than zero.</param>
    /// <returns>
    /// 不大於 <paramref name="value"/> 的最大步進值整數倍。
    /// The largest multiple of the step that does not exceed <paramref name="value"/>.
    /// </returns>
    /// <remarks>
    /// 送單數量一律用這個方向對齊。向上對齊會讓實際部位大於風控算出來的規模,等於偷偷放大風險。
    /// Always align order quantities in this direction. Rounding up makes the real position larger than the size
    /// risk management calculated, which quietly increases exposure.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> 小於或等於零時擲出。
    /// Thrown when <paramref name="step"/> is less than or equal to zero.
    /// </exception>
    public static decimal FloorToStep(decimal value, decimal step)
    {
        ValidateStep(step);

        var result = decimal.Floor(value / step) * step;
        result = ClampToScale(result, GetSignificantScale(step));

        // 除法在極端有效位數下可能產生微小誤差,使結果略微超出原值;逐步修正回來。
        // Division can drift slightly at extreme scales and push the result past the input; walk it back.
        while (result > value)
        {
            result -= step;
        }

        return result;
    }

    /// <summary>
    /// 將數值向上對齊到步進值的整數倍(往正無窮方向)。
    /// Aligns a value up to a multiple of the step, rounding towards positive infinity.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值,必須大於零。The step; must be greater than zero.</param>
    /// <returns>
    /// 不小於 <paramref name="value"/> 的最小步進值整數倍。
    /// The smallest multiple of the step that is not below <paramref name="value"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> 小於或等於零時擲出。
    /// Thrown when <paramref name="step"/> is less than or equal to zero.
    /// </exception>
    public static decimal CeilingToStep(decimal value, decimal step)
    {
        ValidateStep(step);

        var result = decimal.Ceiling(value / step) * step;
        result = ClampToScale(result, GetSignificantScale(step));

        while (result < value)
        {
            result += step;
        }

        return result;
    }

    /// <summary>
    /// 將數值四捨五入對齊到步進值的整數倍。
    /// Aligns a value to the nearest multiple of the step.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值,必須大於零。The step; must be greater than zero.</param>
    /// <param name="mode">
    /// 中點的處理方式,預設為銀行家捨入(<see cref="MidpointRounding.ToEven"/>)。
    /// How midpoints are handled; defaults to banker's rounding (<see cref="MidpointRounding.ToEven"/>).
    /// </param>
    /// <returns>最接近的步進值整數倍。The nearest multiple of the step.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> 小於或等於零時擲出。
    /// Thrown when <paramref name="step"/> is less than or equal to zero.
    /// </exception>
    public static decimal RoundToStep(decimal value, decimal step, MidpointRounding mode = MidpointRounding.ToEven)
    {
        ValidateStep(step);

        var result = decimal.Round(value / step, 0, mode) * step;
        return ClampToScale(result, GetSignificantScale(step));
    }

    /// <summary>
    /// 嘗試將數值向下對齊到步進值的整數倍,步進值不合法時回傳 <see langword="false"/> 而不擲出例外。
    /// Tries to align a value down to a multiple of the step, returning <see langword="false"/> instead of throwing
    /// when the step is invalid.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值。The step.</param>
    /// <param name="result">
    /// 成功時輸出對齊後的數值,失敗時為零。
    /// Receives the aligned value on success; zero on failure.
    /// </param>
    /// <returns>
    /// 步進值合法且對齊成功時回傳 <see langword="true"/>。
    /// <see langword="true"/> when the step is valid and alignment succeeded.
    /// </returns>
    /// <remarks>
    /// 給「步進值來自外部資料因此可能不合法」的呼叫端使用 —— 交易規則是交易所回傳的,
    /// 不該假設它一定正確。呼叫端若本來就要把結果包成失敗值回傳,用這個版本比先自己檢查再呼叫更直接。
    /// For callers whose step comes from external data and may therefore be invalid: exchange rules arrive over the
    /// wire and should not be assumed correct. If the caller is going to wrap the outcome in a failure value anyway,
    /// this is simpler than checking the step separately before calling.
    /// </remarks>
    public static bool TryFloorToStep(decimal value, decimal step, out decimal result)
    {
        if (step <= 0m)
        {
            result = 0m;
            return false;
        }

        result = FloorToStep(value, step);
        return true;
    }

    /// <summary>
    /// 嘗試將數值向上對齊到步進值的整數倍,步進值不合法時回傳 <see langword="false"/> 而不擲出例外。
    /// Tries to align a value up to a multiple of the step, returning <see langword="false"/> instead of throwing
    /// when the step is invalid.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值。The step.</param>
    /// <param name="result">成功時輸出對齊後的數值,失敗時為零。Receives the aligned value on success; zero on failure.</param>
    /// <returns>步進值合法時回傳 <see langword="true"/>。<see langword="true"/> when the step is valid.</returns>
    public static bool TryCeilingToStep(decimal value, decimal step, out decimal result)
    {
        if (step <= 0m)
        {
            result = 0m;
            return false;
        }

        result = CeilingToStep(value, step);
        return true;
    }

    /// <summary>
    /// 嘗試將數值四捨五入對齊到步進值的整數倍,步進值不合法時回傳 <see langword="false"/> 而不擲出例外。
    /// Tries to align a value to the nearest multiple of the step, returning <see langword="false"/> instead of
    /// throwing when the step is invalid.
    /// </summary>
    /// <param name="value">要對齊的數值。The value to align.</param>
    /// <param name="step">步進值。The step.</param>
    /// <param name="result">成功時輸出對齊後的數值,失敗時為零。Receives the aligned value on success; zero on failure.</param>
    /// <param name="mode">中點的處理方式。How midpoints are handled.</param>
    /// <returns>步進值合法時回傳 <see langword="true"/>。<see langword="true"/> when the step is valid.</returns>
    public static bool TryRoundToStep(
        decimal value,
        decimal step,
        out decimal result,
        MidpointRounding mode = MidpointRounding.ToEven)
    {
        if (step <= 0m)
        {
            result = 0m;
            return false;
        }

        result = RoundToStep(value, step, mode);
        return true;
    }

    /// <summary>
    /// 判斷數值是否已經是步進值的整數倍。
    /// Determines whether a value is already an exact multiple of the step.
    /// </summary>
    /// <param name="value">要檢查的數值。The value to check.</param>
    /// <param name="step">步進值,必須大於零。The step; must be greater than zero.</param>
    /// <returns>
    /// 已對齊時回傳 <see langword="true"/>。
    /// <see langword="true"/> when the value is aligned.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> 小於或等於零時擲出。
    /// Thrown when <paramref name="step"/> is less than or equal to zero.
    /// </exception>
    public static bool IsAlignedToStep(decimal value, decimal step)
    {
        ValidateStep(step);
        return value % step == 0m;
    }

    /// <summary>
    /// 將數值往零的方向截斷到指定的小數位數。
    /// Truncates a value towards zero at the given number of decimal places.
    /// </summary>
    /// <param name="value">要截斷的數值。The value to truncate.</param>
    /// <param name="decimals">保留的小數位數,範圍 0 到 28。Decimal places to keep, from 0 to 28.</param>
    /// <returns>截斷後的數值。The truncated value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="decimals"/> 不在 0 到 28 之間時擲出。
    /// Thrown when <paramref name="decimals"/> is outside the range 0 to 28.
    /// </exception>
    public static decimal Truncate(decimal value, int decimals)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimals, 28);

        return decimal.Round(value, decimals, MidpointRounding.ToZero);
    }

    /// <summary>
    /// 取得數值目前的小數位數,含尾隨零。
    /// Returns the current scale of a value, including trailing zeros.
    /// </summary>
    /// <param name="value">要檢查的數值。The value to inspect.</param>
    /// <returns>
    /// 小數位數。例如 <c>1.2300m</c> 回傳 4。
    /// The scale. For example <c>1.2300m</c> returns 4.
    /// </returns>
    public static int GetScale(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);

        return (bits[3] >> 16) & 0xFF;
    }

    /// <summary>
    /// 取得數值去除尾隨零之後的小數位數。
    /// Returns the scale of a value after trailing zeros are removed.
    /// </summary>
    /// <param name="value">要檢查的數值。The value to inspect.</param>
    /// <returns>
    /// 有效小數位數。例如 <c>1.2300m</c> 回傳 2,<c>0.001m</c> 回傳 3。
    /// The significant scale. For example <c>1.2300m</c> returns 2 and <c>0.001m</c> returns 3.
    /// </returns>
    /// <remarks>
    /// 交易所給的步進值常常帶尾隨零(例如 <c>0.00100000</c>),用這個方法可以得到真正需要的小數位數。
    /// Exchange step sizes often arrive with trailing zeros such as <c>0.00100000</c>; this returns the number of
    /// decimal places that actually matter.
    /// </remarks>
    public static int GetSignificantScale(decimal value) => GetScale(Normalize(value));

    /// <summary>
    /// 移除尾隨零,回傳數學上相等但有效位數最小的數值。
    /// Removes trailing zeros, returning a mathematically equal value with the smallest scale.
    /// </summary>
    /// <param name="value">要正規化的數值。The value to normalise.</param>
    /// <returns>
    /// 去除尾隨零後的數值。例如 <c>1.2300m</c> 回傳 <c>1.23m</c>。
    /// The value without trailing zeros. For example <c>1.2300m</c> returns <c>1.23m</c>.
    /// </returns>
    public static decimal Normalize(decimal value) => value / TrailingZeroStripper;

    /// <summary>
    /// 將數值序列化為不含科學記號、不含尾隨零的字串。
    /// Serialises a value to a string with no exponent notation and no trailing zeros.
    /// </summary>
    /// <param name="value">要序列化的數值。The value to serialise.</param>
    /// <returns>可直接送往 API 的字串。A string ready to send to an API.</returns>
    /// <remarks>
    /// 交易所 API 一律以字串接收價格與數量。若讓數值以科學記號(例如 <c>1E-05</c>)或多餘的尾隨零送出,
    /// 對方通常直接回簽章或參數錯誤,而且訊息完全看不出真正原因。序列化前一律經過這個方法。
    /// Exchange APIs take prices and quantities as strings. Sending exponent notation such as <c>1E-05</c>, or
    /// redundant trailing zeros, usually comes back as an opaque signature or parameter error that gives no hint
    /// of the real cause. Always serialise through this method.
    /// </remarks>
    public static string ToPlainString(decimal value) =>
        Normalize(value).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// 把對齊運算的結果收斂到指定小數位數,消除除法帶來的尾端誤差。
    /// Clamps the result of an alignment to the given scale, removing tail error introduced by division.
    /// </summary>
    /// <param name="value">對齊運算的中間結果。The intermediate alignment result.</param>
    /// <param name="scale">目標小數位數。The target scale.</param>
    /// <returns>收斂後的數值。The clamped value.</returns>
    private static decimal ClampToScale(decimal value, int scale) =>
        scale <= 28 ? decimal.Round(value, scale, MidpointRounding.ToEven) : value;

    /// <summary>
    /// 驗證步進值必須為正數。
    /// Validates that a step is positive.
    /// </summary>
    /// <param name="step">要驗證的步進值。The step to validate.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="step"/> 小於或等於零時擲出。
    /// Thrown when <paramref name="step"/> is less than or equal to zero.
    /// </exception>
    private static void ValidateStep(decimal step) =>
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(step, 0m);
}

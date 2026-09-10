using System.Globalization;

namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 帶幣別的金額。以 <see cref="decimal"/> 儲存數量,並在運算時強制幣別一致。
/// A monetary amount tagged with its currency. Stores the quantity as a <see cref="decimal"/> and enforces
/// currency agreement on every operation.
/// </summary>
/// <remarks>
/// <para>
/// 存在的理由是防呆。交易系統裡同時流動著計價幣的餘額(USDT)與基礎幣的數量(BTC),兩者都是
/// <see cref="decimal"/>,型別系統擋不住把它們相加。把幣別綁進型別之後,這種錯誤在編譯期無法察覺、
/// 但會在第一次執行到就立刻擲出 <see cref="CurrencyMismatchException"/>,而不是安靜地算出一個錯誤的部位規模。
/// This type exists to prevent a specific mistake. A trading system carries quote-currency balances (USDT) and
/// base-currency quantities (BTC) side by side; both are <see cref="decimal"/>, so nothing stops you adding them.
/// Tagging the currency turns that into an immediate <see cref="CurrencyMismatchException"/> on first execution
/// rather than a quietly wrong position size.
/// </para>
/// <para>
/// <see langword="default"/> 值是「未指定幣別的零」,可以和任何幣別相加,方便作為累加的起始值。
/// 除此之外,任何跨幣別運算都會擲出例外。
/// The <see langword="default"/> value is a currency-less zero that can be added to any currency, which makes it
/// a convenient seed for accumulation. Every other cross-currency operation throws.
/// </para>
/// </remarks>
public readonly struct Money : IEquatable<Money>, IComparable<Money>, IFormattable
{
    private readonly string? _currency;

    /// <summary>
    /// 建立金額。
    /// Creates a monetary amount.
    /// </summary>
    /// <param name="amount">數量。The quantity.</param>
    /// <param name="currency">
    /// 幣別代碼,會去除前後空白並轉為大寫。不可為空白。
    /// The currency code; trimmed and upper-cased. Must not be blank.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="currency"/> 為 <see langword="null"/>、空字串或僅含空白時擲出。
    /// Thrown when <paramref name="currency"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public Money(decimal amount, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        Amount = amount;
        _currency = currency.Trim().ToUpperInvariant();
    }

    /// <summary>
    /// 數量。
    /// The quantity.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// 幣別代碼(大寫)。未指定幣別時為空字串。
    /// The upper-cased currency code, or an empty string when no currency is set.
    /// </summary>
    public string Currency => _currency ?? string.Empty;

    /// <summary>
    /// 是否為「未指定幣別的零」,也就是 <see langword="default"/> 值。
    /// Whether this is the currency-less zero, that is, the <see langword="default"/> value.
    /// </summary>
    public bool IsNeutralZero => _currency is null && Amount == 0m;

    /// <summary>
    /// 數量是否為零。
    /// Whether the quantity is zero.
    /// </summary>
    public bool IsZero => Amount == 0m;

    /// <summary>
    /// 建立指定幣別的零。
    /// Creates a zero amount in the given currency.
    /// </summary>
    /// <param name="currency">幣別代碼。The currency code.</param>
    /// <returns>該幣別的零。Zero in that currency.</returns>
    public static Money Zero(string currency) => new(0m, currency);

    /// <summary>
    /// 以相同幣別建立新的金額。
    /// Creates a new amount in the same currency.
    /// </summary>
    /// <param name="amount">新的數量。The new quantity.</param>
    /// <returns>相同幣別、指定數量的金額。An amount with the same currency and the given quantity.</returns>
    /// <exception cref="InvalidOperationException">
    /// 在未指定幣別的值上呼叫時擲出。
    /// Thrown when called on a value that has no currency.
    /// </exception>
    public Money WithAmount(decimal amount)
    {
        if (_currency is null)
        {
            throw new InvalidOperationException(
                "未指定幣別的金額無法衍生新值。Cannot derive a new value from an amount with no currency.");
        }

        return new Money(amount, _currency);
    }

    /// <summary>
    /// 將數量四捨五入到指定小數位數,幣別不變。
    /// Rounds the quantity to the given number of decimal places, keeping the currency.
    /// </summary>
    /// <param name="decimals">保留的小數位數。Decimal places to keep.</param>
    /// <param name="mode">中點處理方式。How midpoints are handled.</param>
    /// <returns>四捨五入後的金額。The rounded amount.</returns>
    public Money Round(int decimals, MidpointRounding mode = MidpointRounding.ToEven) =>
        _currency is null ? this : new Money(decimal.Round(Amount, decimals, mode), _currency);

    /// <summary>
    /// 相加。幣別必須相同,或其中一方為未指定幣別的零。
    /// Adds two amounts. The currencies must match, or one side must be the currency-less zero.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>相加後的金額。The sum.</returns>
    /// <exception cref="CurrencyMismatchException">
    /// 兩者幣別不同時擲出。
    /// Thrown when the currencies differ.
    /// </exception>
    public static Money Add(Money left, Money right)
    {
        var currency = ResolveCurrency(left, right);
        return currency is null ? default : new Money(left.Amount + right.Amount, currency);
    }

    /// <summary>
    /// 相減。幣別必須相同,或其中一方為未指定幣別的零。
    /// Subtracts one amount from another. The currencies must match, or one side must be the currency-less zero.
    /// </summary>
    /// <param name="left">被減數。The minuend.</param>
    /// <param name="right">減數。The subtrahend.</param>
    /// <returns>相減後的金額。The difference.</returns>
    /// <exception cref="CurrencyMismatchException">
    /// 兩者幣別不同時擲出。
    /// Thrown when the currencies differ.
    /// </exception>
    public static Money Subtract(Money left, Money right)
    {
        var currency = ResolveCurrency(left, right);
        return currency is null ? default : new Money(left.Amount - right.Amount, currency);
    }

    /// <summary>
    /// 乘以一個係數,幣別不變。
    /// Multiplies by a scalar factor, keeping the currency.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <param name="factor">係數。The factor.</param>
    /// <returns>相乘後的金額。The product.</returns>
    public static Money Multiply(Money money, decimal factor) =>
        money._currency is null ? default : new Money(money.Amount * factor, money._currency);

    /// <summary>
    /// 除以一個除數,幣別不變。
    /// Divides by a scalar divisor, keeping the currency.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <param name="divisor">除數,不可為零。The divisor; must not be zero.</param>
    /// <returns>相除後的金額。The quotient.</returns>
    /// <exception cref="DivideByZeroException">
    /// <paramref name="divisor"/> 為零時擲出,未指定幣別的零也不例外。
    /// Thrown when <paramref name="divisor"/> is zero, including on the currency-less zero.
    /// </exception>
    /// <remarks>
    /// 除數檢查刻意放在幣別短路之前。否則 <see langword="default"/> 值除以零會安靜回傳零,
    /// 而累加迴圈裡拿起始值直接相除正是最容易寫出這個錯誤的地方。
    /// The divisor check deliberately precedes the currency short-circuit. Otherwise dividing a
    /// <see langword="default"/> value by zero would quietly return zero, and dividing an accumulator seed is
    /// exactly where that mistake gets written.
    /// </remarks>
    public static Money Divide(Money money, decimal divisor)
    {
        if (divisor == 0m)
        {
            throw new DivideByZeroException("金額的除數不可為零。The divisor of a monetary amount must not be zero.");
        }

        return money._currency is null ? default : new Money(money.Amount / divisor, money._currency);
    }

    /// <summary>
    /// 取負值,幣別不變。
    /// Negates the amount, keeping the currency.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <returns>正負相反的金額。The negated amount.</returns>
    public static Money Negate(Money money) =>
        money._currency is null ? default : new Money(-money.Amount, money._currency);

    /// <summary>
    /// 相加。<see cref="Add"/> 的運算子形式。
    /// Adds two amounts; the operator form of <see cref="Add"/>.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>相加後的金額。The sum.</returns>
    public static Money operator +(Money left, Money right) => Add(left, right);

    /// <summary>
    /// 相減。<see cref="Subtract"/> 的運算子形式。
    /// Subtracts one amount from another; the operator form of <see cref="Subtract"/>.
    /// </summary>
    /// <param name="left">被減數。The minuend.</param>
    /// <param name="right">減數。The subtrahend.</param>
    /// <returns>相減後的金額。The difference.</returns>
    public static Money operator -(Money left, Money right) => Subtract(left, right);

    /// <summary>
    /// 取負值。<see cref="Negate"/> 的運算子形式。
    /// Negates the amount; the operator form of <see cref="Negate"/>.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <returns>正負相反的金額。The negated amount.</returns>
    public static Money operator -(Money money) => Negate(money);

    /// <summary>
    /// 乘以係數。<see cref="Multiply"/> 的運算子形式。
    /// Multiplies by a scalar; the operator form of <see cref="Multiply"/>.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <param name="factor">係數。The factor.</param>
    /// <returns>相乘後的金額。The product.</returns>
    public static Money operator *(Money money, decimal factor) => Multiply(money, factor);

    /// <summary>
    /// 除以除數。<see cref="Divide"/> 的運算子形式。
    /// Divides by a scalar; the operator form of <see cref="Divide"/>.
    /// </summary>
    /// <param name="money">金額。The amount.</param>
    /// <param name="divisor">除數。The divisor.</param>
    /// <returns>相除後的金額。The quotient.</returns>
    public static Money operator /(Money money, decimal divisor) => Divide(money, divisor);

    /// <summary>
    /// 比較大小。幣別必須相同,或其中一方為未指定幣別的零。
    /// Compares two amounts. The currencies must match, or one side must be the currency-less zero.
    /// </summary>
    /// <param name="other">要比較的金額。The amount to compare with.</param>
    /// <returns>
    /// 小於回傳負數、相等回傳零、大於回傳正數。
    /// A negative number when smaller, zero when equal, a positive number when greater.
    /// </returns>
    /// <exception cref="CurrencyMismatchException">
    /// 兩者幣別不同時擲出。
    /// Thrown when the currencies differ.
    /// </exception>
    public int CompareTo(Money other)
    {
        _ = ResolveCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    /// <summary>
    /// 判斷左運算元是否小於右運算元。
    /// Determines whether the left operand is less than the right.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>小於時回傳 <see langword="true"/>。<see langword="true"/> when smaller.</returns>
    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;

    /// <summary>
    /// 判斷左運算元是否小於或等於右運算元。
    /// Determines whether the left operand is less than or equal to the right.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>小於或等於時回傳 <see langword="true"/>。<see langword="true"/> when smaller or equal.</returns>
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;

    /// <summary>
    /// 判斷左運算元是否大於右運算元。
    /// Determines whether the left operand is greater than the right.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>大於時回傳 <see langword="true"/>。<see langword="true"/> when greater.</returns>
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;

    /// <summary>
    /// 判斷左運算元是否大於或等於右運算元。
    /// Determines whether the left operand is greater than or equal to the right.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>大於或等於時回傳 <see langword="true"/>。<see langword="true"/> when greater or equal.</returns>
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// 判斷兩筆金額是否相等(數量與幣別皆相同)。跨幣別比較回傳 <see langword="false"/>,不擲出例外。
    /// Determines whether two amounts are equal in both quantity and currency. Cross-currency comparison returns
    /// <see langword="false"/> rather than throwing.
    /// </summary>
    /// <param name="other">要比較的金額。The amount to compare with.</param>
    /// <returns>相等時回傳 <see langword="true"/>。<see langword="true"/> when equal.</returns>
    public bool Equals(Money other) =>
        Amount == other.Amount && string.Equals(Currency, other.Currency, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Money other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);

    /// <summary>
    /// 判斷兩筆金額是否相等。
    /// Determines whether two amounts are equal.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>相等時回傳 <see langword="true"/>。<see langword="true"/> when equal.</returns>
    public static bool operator ==(Money left, Money right) => left.Equals(right);

    /// <summary>
    /// 判斷兩筆金額是否不等。
    /// Determines whether two amounts are different.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>不等時回傳 <see langword="true"/>。<see langword="true"/> when different.</returns>
    public static bool operator !=(Money left, Money right) => !left.Equals(right);

    /// <summary>
    /// 回傳「數量 幣別」形式的字串,數量不含科學記號與尾隨零。
    /// Returns the amount as "quantity currency", with no exponent notation or trailing zeros.
    /// </summary>
    /// <returns>可讀敘述。A readable description.</returns>
    public override string ToString() =>
        _currency is null ? Precision.ToPlainString(Amount) : $"{Precision.ToPlainString(Amount)} {_currency}";

    /// <summary>
    /// 以指定格式回傳字串。
    /// Returns the amount formatted with the supplied format.
    /// </summary>
    /// <param name="format">
    /// 數量的格式字串;為 <see langword="null"/> 時使用不含尾隨零的預設形式。
    /// The numeric format string; the trailing-zero-free default is used when <see langword="null"/>.
    /// </param>
    /// <param name="formatProvider">
    /// 格式提供者;為 <see langword="null"/> 時使用不變文化。
    /// The format provider; the invariant culture is used when <see langword="null"/>.
    /// </param>
    /// <returns>格式化後的字串。The formatted string.</returns>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var provider = formatProvider ?? CultureInfo.InvariantCulture;
        var amount = format is null
            ? Precision.ToPlainString(Amount)
            : Amount.ToString(format, provider);

        return _currency is null ? amount : $"{amount} {_currency}";
    }

    /// <summary>
    /// 取得兩個運算元共同的幣別;其中一方為未指定幣別的零時採用另一方的幣別。
    /// Resolves the shared currency of two operands, taking the other side's currency when one is the
    /// currency-less zero.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>
    /// 共同的幣別;兩者皆未指定幣別時回傳 <see langword="null"/>。
    /// The shared currency, or <see langword="null"/> when neither operand has one.
    /// </returns>
    /// <exception cref="CurrencyMismatchException">
    /// 兩者幣別不同時擲出。
    /// Thrown when the currencies differ.
    /// </exception>
    private static string? ResolveCurrency(Money left, Money right)
    {
        if (left._currency is null)
        {
            return right._currency;
        }

        if (right._currency is null)
        {
            return left._currency;
        }

        return string.Equals(left._currency, right._currency, StringComparison.Ordinal)
            ? left._currency
            : throw new CurrencyMismatchException(left._currency, right._currency);
    }
}

namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="Money"/> 的單元測試。
/// </summary>
[TestClass]
public sealed class MoneyTests
{
    // ---------- 建構與正規化 ----------

    [TestMethod]
    public void ConstructorUpperCasesAndTrimsCurrency()
    {
        var money = new Money(1.5m, "  usdt  ");

        Assert.AreEqual(1.5m, money.Amount);
        Assert.AreEqual("USDT", money.Currency);
    }

    [TestMethod]
    public void ConstructorKeepsMixedCaseNormalized()
    {
        Assert.AreEqual("BTC", new Money(1m, "Btc").Currency);
        Assert.AreEqual("BTC", new Money(1m, "bTc").Currency);
    }

    [TestMethod]
    public void ConstructorThrowsWhenCurrencyIsBlank()
    {
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Money(1m, string.Empty); });
        Assert.ThrowsExactly<ArgumentException>(() => { _ = new Money(1m, "   "); });
    }

    [TestMethod]
    public void ConstructorThrowsWhenCurrencyIsNull()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = new Money(1m, null!); });
    }

    [TestMethod]
    public void ZeroFactoryProducesZeroInThatCurrency()
    {
        var zero = Money.Zero("usdt");

        Assert.AreEqual(0m, zero.Amount);
        Assert.AreEqual("USDT", zero.Currency);
        Assert.IsTrue(zero.IsZero);
        Assert.IsFalse(zero.IsNeutralZero, "指定幣別的零不是未指定幣別的零");
    }

    [TestMethod]
    public void DefaultValueIsTheNeutralZero()
    {
        Money neutral = default;

        Assert.AreEqual(0m, neutral.Amount);
        Assert.AreEqual(string.Empty, neutral.Currency);
        Assert.IsTrue(neutral.IsZero);
        Assert.IsTrue(neutral.IsNeutralZero);
    }

    // ---------- 同幣別四則運算 ----------

    [TestMethod]
    public void AddSumsAmountsOfTheSameCurrency()
    {
        var sum = new Money(1.5m, "USDT") + new Money(2.25m, "USDT");

        Assert.AreEqual(3.75m, sum.Amount);
        Assert.AreEqual("USDT", sum.Currency);
    }

    [TestMethod]
    public void SubtractDiffersAmountsOfTheSameCurrency()
    {
        var difference = new Money(1.5m, "USDT") - new Money(2.25m, "USDT");

        Assert.AreEqual(-0.75m, difference.Amount);
        Assert.AreEqual("USDT", difference.Currency);
    }

    [TestMethod]
    public void MultiplyScalesTheAmountAndKeepsCurrency()
    {
        var product = new Money(1.5m, "USDT") * 3m;

        Assert.AreEqual(4.5m, product.Amount);
        Assert.AreEqual("USDT", product.Currency);
        Assert.AreEqual(product, Money.Multiply(new Money(1.5m, "USDT"), 3m));
    }

    [TestMethod]
    public void DivideScalesTheAmountAndKeepsCurrency()
    {
        var quotient = new Money(4.5m, "USDT") / 3m;

        Assert.AreEqual(1.5m, quotient.Amount);
        Assert.AreEqual("USDT", quotient.Currency);
        Assert.AreEqual(quotient, Money.Divide(new Money(4.5m, "USDT"), 3m));
    }

    [TestMethod]
    public void DivideThrowsOnZeroDivisor()
    {
        Assert.ThrowsExactly<DivideByZeroException>(() => { _ = new Money(1m, "USDT") / 0m; });
    }

    [TestMethod]
    public void NegateFlipsTheSignAndKeepsCurrency()
    {
        var negated = -new Money(1.5m, "USDT");

        Assert.AreEqual(-1.5m, negated.Amount);
        Assert.AreEqual("USDT", negated.Currency);
        Assert.AreEqual(negated, Money.Negate(new Money(1.5m, "USDT")));
    }

    [TestMethod]
    public void NegateOnNeutralZeroStaysNeutral()
    {
        Assert.IsTrue((-default(Money)).IsNeutralZero);
    }

    [TestMethod]
    public void AddAndSubtractAreAvailableAsNamedMethods()
    {
        var left = new Money(3m, "USDT");
        var right = new Money(1m, "USDT");

        Assert.AreEqual(new Money(4m, "USDT"), Money.Add(left, right));
        Assert.AreEqual(new Money(2m, "USDT"), Money.Subtract(left, right));
    }

    // ---------- 跨幣別 ----------

    [TestMethod]
    public void AddThrowsAcrossCurrencies()
    {
        var thrown = Assert.ThrowsExactly<CurrencyMismatchException>(
            () => { _ = new Money(1m, "USDT") + new Money(1m, "BTC"); });

        Assert.AreEqual("USDT", thrown.Left);
        Assert.AreEqual("BTC", thrown.Right);
    }

    [TestMethod]
    public void SubtractThrowsAcrossCurrencies()
    {
        Assert.ThrowsExactly<CurrencyMismatchException>(
            () => { _ = new Money(1m, "USDT") - new Money(1m, "BTC"); });
    }

    [TestMethod]
    public void NeutralZeroCanBeAddedToAnyCurrency()
    {
        var money = new Money(5m, "USDT");

        Assert.AreEqual(money, default(Money) + money);
        Assert.AreEqual(money, money + default(Money));
    }

    [TestMethod]
    public void NeutralZeroCanBeSubtractedFromAnyCurrency()
    {
        var money = new Money(5m, "USDT");

        Assert.AreEqual(money, money - default(Money));
        Assert.AreEqual(new Money(-5m, "USDT"), default(Money) - money);
    }

    [TestMethod]
    public void NeutralZeroWorksAsAccumulationSeed()
    {
        // 這是 default(Money) 存在的理由:累加時不必事先知道幣別。
        Money[] fills =
        [
            new(120.5m, "USDT"),
            new(30.25m, "USDT"),
            new(9.25m, "USDT"),
        ];

        var total = default(Money);
        foreach (var fill in fills)
        {
            total += fill;
        }

        Assert.AreEqual(160m, total.Amount);
        Assert.AreEqual("USDT", total.Currency);
        Assert.IsFalse(total.IsNeutralZero);
    }

    [TestMethod]
    public void AddingTwoNeutralZerosStaysNeutral()
    {
        var sum = default(Money) + default(Money);

        Assert.IsTrue(sum.IsNeutralZero);
        Assert.AreEqual(string.Empty, sum.Currency);
    }

    [TestMethod]
    public void SubtractingTwoNeutralZerosStaysNeutral()
    {
        Assert.IsTrue((default(Money) - default(Money)).IsNeutralZero);
    }

    [TestMethod]
    public void ScalingANeutralZeroStaysNeutral()
    {
        // 未指定幣別的零沒有幣別可繼承,乘除之後仍然是未指定幣別的零。
        Assert.IsTrue(Money.Multiply(default, 5m).IsNeutralZero);
        Assert.IsTrue((default(Money) * 5m).IsNeutralZero);
        Assert.IsTrue(Money.Divide(default, 5m).IsNeutralZero);
        Assert.IsTrue((default(Money) / 5m).IsNeutralZero);
    }

    [TestMethod]
    public void BoxedEqualityWorks()
    {
        object boxed = new Money(1.5m, "USDT");

        Assert.IsTrue(new Money(1.5m, "USDT").Equals(boxed));
        Assert.IsFalse(new Money(1.5m, "BTC").Equals(boxed));
    }

    // ---------- 相等性與比較的不對稱 ----------

    [TestMethod]
    public void EqualsReturnsFalseAcrossCurrenciesWithoutThrowing()
    {
        // 刻意的設計:相等性比較必須是全序安全的,不能擲出例外。
        var usdt = new Money(1m, "USDT");
        var btc = new Money(1m, "BTC");

        Assert.IsFalse(usdt.Equals(btc));
        Assert.IsFalse(usdt == btc);
        Assert.IsTrue(usdt != btc);
    }

    [TestMethod]
    public void CompareToThrowsAcrossCurrencies()
    {
        // 與 Equals 相反:排序不允許跨幣別,否則會排出無意義的順序。
        var usdt = new Money(1m, "USDT");
        var btc = new Money(1m, "BTC");

        Assert.ThrowsExactly<CurrencyMismatchException>(() => { _ = usdt.CompareTo(btc); });
        Assert.ThrowsExactly<CurrencyMismatchException>(() => { _ = usdt < btc; });
        Assert.ThrowsExactly<CurrencyMismatchException>(() => { _ = usdt <= btc; });
        Assert.ThrowsExactly<CurrencyMismatchException>(() => { _ = usdt > btc; });
        Assert.ThrowsExactly<CurrencyMismatchException>(() => { _ = usdt >= btc; });
    }

    [TestMethod]
    public void EqualsComparesAmountAndCurrency()
    {
        Assert.AreEqual(new Money(1.50m, "USDT"), new Money(1.5m, "USDT"));
        Assert.AreNotEqual(new Money(1.5m, "USDT"), new Money(1.6m, "USDT"));
        Assert.IsFalse(new Money(1m, "USDT").Equals("not money"));
    }

    [TestMethod]
    public void NeutralZeroDoesNotEqualCurrencyZero()
    {
        // 幣別不同(空字串 vs USDT),因此不相等 —— 但兩者仍可相加。
        Assert.AreNotEqual(Money.Zero("USDT"), default(Money));
    }

    [TestMethod]
    public void GetHashCodeIsStableForEqualValues()
    {
        Assert.AreEqual(new Money(1.5m, "USDT").GetHashCode(), new Money(1.5m, "usdt").GetHashCode());
    }

    // ---------- 比較運算子 ----------

    [TestMethod]
    public void ComparisonOperatorsOrderSameCurrencyAmounts()
    {
        var small = new Money(1m, "USDT");
        var large = new Money(2m, "USDT");

        Assert.IsTrue(small < large);
        Assert.IsTrue(small <= large);
        Assert.IsFalse(small > large);
        Assert.IsFalse(small >= large);
        Assert.IsTrue(large >= new Money(2m, "USDT"));
        Assert.IsTrue(large <= new Money(2m, "USDT"));
    }

    [TestMethod]
    public void CompareToReturnsSignOfAmountDifference()
    {
        Assert.IsTrue(new Money(1m, "USDT").CompareTo(new Money(2m, "USDT")) < 0);
        Assert.AreEqual(0, new Money(2m, "USDT").CompareTo(new Money(2m, "USDT")));
        Assert.IsTrue(new Money(3m, "USDT").CompareTo(new Money(2m, "USDT")) > 0);
    }

    [TestMethod]
    public void CompareToAcceptsNeutralZeroOnEitherSide()
    {
        var money = new Money(5m, "USDT");

        Assert.IsTrue(money > default(Money));
        Assert.IsTrue(default(Money) < money);
        Assert.AreEqual(0, default(Money).CompareTo(Money.Zero("USDT")));
    }

    [TestMethod]
    public void SortingSameCurrencyAmountsWorks()
    {
        var amounts = new[]
        {
            new Money(3m, "USDT"),
            new Money(1m, "USDT"),
            new Money(2m, "USDT"),
        };

        Array.Sort(amounts);

        Assert.AreEqual(new Money(1m, "USDT"), amounts[0]);
        Assert.AreEqual(new Money(2m, "USDT"), amounts[1]);
        Assert.AreEqual(new Money(3m, "USDT"), amounts[2]);
    }

    // ---------- 衍生值 ----------

    [TestMethod]
    public void WithAmountKeepsCurrency()
    {
        var derived = new Money(1m, "USDT").WithAmount(9m);

        Assert.AreEqual(9m, derived.Amount);
        Assert.AreEqual("USDT", derived.Currency);
    }

    [TestMethod]
    public void WithAmountThrowsOnNeutralZero()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => { _ = default(Money).WithAmount(1m); });
    }

    [TestMethod]
    public void RoundReducesScaleAndKeepsCurrency()
    {
        var rounded = new Money(1.23456m, "USDT").Round(2);

        Assert.AreEqual(1.23m, rounded.Amount);
        Assert.AreEqual("USDT", rounded.Currency);
    }

    [TestMethod]
    public void RoundUsesBankersRoundingByDefault()
    {
        Assert.AreEqual(1.00m, new Money(1.005m, "USDT").Round(2).Amount);
        Assert.AreEqual(1.02m, new Money(1.015m, "USDT").Round(2).Amount);
    }

    [TestMethod]
    public void RoundHonoursAwayFromZeroMode()
    {
        Assert.AreEqual(1.01m, new Money(1.005m, "USDT").Round(2, MidpointRounding.AwayFromZero).Amount);
    }

    [TestMethod]
    public void RoundOnNeutralZeroReturnsNeutralZero()
    {
        Assert.IsTrue(default(Money).Round(2).IsNeutralZero);
    }

    // ---------- 字串化 ----------

    [TestMethod]
    public void ToStringHasNoExponentAndNoTrailingZeros()
    {
        Assert.AreEqual("1.23 USDT", new Money(1.2300m, "USDT").ToString());
        Assert.AreEqual("0.00000001 BTC", new Money(0.00000001m, "BTC").ToString());
        Assert.AreEqual("1 USDT", new Money(1.0000m, "USDT").ToString());
        Assert.AreEqual("-1.5 USDT", new Money(-1.5m, "USDT").ToString());
    }

    [TestMethod]
    public void ToStringOnNeutralZeroOmitsCurrency()
    {
        Assert.AreEqual("0", default(Money).ToString());
    }

    [TestMethod]
    public void ToStringWithNullFormatMatchesDefault()
    {
        var money = new Money(1.2300m, "USDT");

        Assert.AreEqual(money.ToString(), money.ToString(null, null));
    }

    [TestMethod]
    public void ToStringHonoursExplicitFormat()
    {
        var money = new Money(1.5m, "USDT");

        Assert.AreEqual("1.500 USDT", money.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ToStringWithFormatOnNeutralZeroOmitsCurrency()
    {
        Assert.AreEqual("0.00", default(Money).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
    }

    // ---------- 例外型別 ----------

    [TestMethod]
    public void CurrencyMismatchExceptionExposesBothCurrencies()
    {
        var exception = new CurrencyMismatchException("USDT", "BTC");

        Assert.AreEqual("USDT", exception.Left);
        Assert.AreEqual("BTC", exception.Right);
        Assert.Contains("USDT", exception.Message);
        Assert.Contains("BTC", exception.Message);
    }

    [TestMethod]
    public void CurrencyMismatchExceptionSupportsTheStandardConstructors()
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(new CurrencyMismatchException().Message));
        Assert.AreEqual("自訂訊息", new CurrencyMismatchException("自訂訊息").Message);

        var inner = new InvalidOperationException("inner");
        var withInner = new CurrencyMismatchException("外層訊息", inner);
        Assert.AreSame(inner, withInner.InnerException);

        // 是 InvalidOperationException 的子型別,呼叫端可以用較寬的 catch 接住。
        Assert.IsInstanceOfType<InvalidOperationException>(new CurrencyMismatchException());
    }

    [TestMethod]
    public void CurrencyMismatchExceptionHasNoCurrenciesWhenBuiltFromMessage()
    {
        var exception = new CurrencyMismatchException("只有訊息");

        Assert.IsNull(exception.Left);
        Assert.IsNull(exception.Right);
    }
}

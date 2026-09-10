namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="Precision"/> 的單元測試。
/// 這一組直接決定送出去的價格與數量,錯了會下錯單,因此不變條件用大量隨機輸入交叉驗證。
/// </summary>
[TestClass]
public sealed class PrecisionTests
{
    /// <summary>
    /// 交易所實務上常見的步進值。
    /// </summary>
    private static readonly decimal[] Steps =
    [
        0.00001m,
        0.001m,
        0.01m,
        0.1m,
        1m,
        0.25m,
        2.5m,
        5m,
    ];

    /// <summary>
    /// 各種形狀的代表性數值:正、負、零、小於步進、帶尾隨零。
    /// </summary>
    private static readonly decimal[] Samples =
    [
        0m,
        0.000001m,
        0.5m,
        1m,
        1.2345m,
        9.99999m,
        123.456789m,
        1000m,
        -0.000001m,
        -0.5m,
        -1.2345m,
        -123.456789m,
        -1000m,
    ];

    // ---------- FloorToStep ----------

    [TestMethod]
    public void FloorToStepAlignsDownwards()
    {
        Assert.AreEqual(1.23m, Precision.FloorToStep(1.2345m, 0.01m));
        Assert.AreEqual(1.234m, Precision.FloorToStep(1.2345m, 0.001m));
        Assert.AreEqual(1.2m, Precision.FloorToStep(1.2345m, 0.1m));
        Assert.AreEqual(1m, Precision.FloorToStep(1.2345m, 1m));
        Assert.AreEqual(1.23456m, Precision.FloorToStep(1.234567m, 0.00001m));
    }

    [TestMethod]
    public void FloorToStepNeverExceedsInputValue()
    {
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.FloorToStep(value, step);

                Assert.IsTrue(
                    result <= value,
                    $"FloorToStep({value}, {step}) = {result} 超過了原值");
            }
        }
    }

    [TestMethod]
    public void FloorToStepResultIsAlwaysAlignedToStep()
    {
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.FloorToStep(value, step);

                Assert.IsTrue(
                    Precision.IsAlignedToStep(result, step),
                    $"FloorToStep({value}, {step}) = {result} 不是步進值的整數倍");
            }
        }
    }

    [TestMethod]
    public void FloorToStepGivesTheLargestAlignedValueNotAboveInput()
    {
        // 對齊後與原值的差距必須小於一個步進,否則就不是「最大的」整數倍。
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.FloorToStep(value, step);

                Assert.IsTrue(
                    value - result < step,
                    $"FloorToStep({value}, {step}) = {result} 少對齊了至少一個步進");
            }
        }
    }

    [TestMethod]
    public void FloorToStepRoundsNegativeValuesTowardsNegativeInfinity()
    {
        // 往負無窮,不是往零。
        Assert.AreEqual(-1.24m, Precision.FloorToStep(-1.2345m, 0.01m));
        Assert.AreEqual(-2m, Precision.FloorToStep(-1.2345m, 1m));
        Assert.AreEqual(-0.1m, Precision.FloorToStep(-0.05m, 0.1m));
    }

    [TestMethod]
    public void FloorToStepReturnsZeroWhenValueIsBelowStep()
    {
        Assert.AreEqual(0m, Precision.FloorToStep(0.004m, 0.01m));
        Assert.AreEqual(0m, Precision.FloorToStep(0m, 0.01m));
    }

    [TestMethod]
    public void FloorToStepLeavesAlreadyAlignedValuesUnchanged()
    {
        Assert.AreEqual(1.23m, Precision.FloorToStep(1.23m, 0.01m));
        Assert.AreEqual(10m, Precision.FloorToStep(10m, 5m));
        Assert.AreEqual(-1.23m, Precision.FloorToStep(-1.23m, 0.01m));
    }

    [TestMethod]
    public void FloorToStepHandlesVeryLargeValues()
    {
        Assert.AreEqual(decimal.MaxValue, Precision.FloorToStep(decimal.MaxValue, 1m));
        Assert.AreEqual(decimal.MinValue, Precision.FloorToStep(decimal.MinValue, 1m));
    }

    [TestMethod]
    public void FloorToStepThrowsWhenStepIsNotPositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.FloorToStep(1m, 0m); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.FloorToStep(1m, -0.01m); });
    }

    // ---------- CeilingToStep ----------

    [TestMethod]
    public void CeilingToStepAlignsUpwards()
    {
        Assert.AreEqual(1.24m, Precision.CeilingToStep(1.2345m, 0.01m));
        Assert.AreEqual(1.235m, Precision.CeilingToStep(1.2345m, 0.001m));
        Assert.AreEqual(1.3m, Precision.CeilingToStep(1.2345m, 0.1m));
        Assert.AreEqual(2m, Precision.CeilingToStep(1.2345m, 1m));
    }

    [TestMethod]
    public void CeilingToStepNeverFallsBelowInputValue()
    {
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.CeilingToStep(value, step);

                Assert.IsTrue(
                    result >= value,
                    $"CeilingToStep({value}, {step}) = {result} 低於原值");
            }
        }
    }

    [TestMethod]
    public void CeilingToStepResultIsAlwaysAlignedToStep()
    {
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.CeilingToStep(value, step);

                Assert.IsTrue(
                    Precision.IsAlignedToStep(result, step),
                    $"CeilingToStep({value}, {step}) = {result} 不是步進值的整數倍");
            }
        }
    }

    [TestMethod]
    public void CeilingToStepGivesTheSmallestAlignedValueNotBelowInput()
    {
        foreach (var step in Steps)
        {
            foreach (var value in Samples)
            {
                var result = Precision.CeilingToStep(value, step);

                Assert.IsTrue(
                    result - value < step,
                    $"CeilingToStep({value}, {step}) = {result} 多對齊了至少一個步進");
            }
        }
    }

    [TestMethod]
    public void CeilingToStepRoundsNegativeValuesTowardsPositiveInfinity()
    {
        Assert.AreEqual(-1.23m, Precision.CeilingToStep(-1.2345m, 0.01m));
        Assert.AreEqual(-1m, Precision.CeilingToStep(-1.2345m, 1m));
        Assert.AreEqual(0m, Precision.CeilingToStep(-0.05m, 0.1m));
    }

    [TestMethod]
    public void CeilingToStepLeavesAlreadyAlignedValuesUnchanged()
    {
        Assert.AreEqual(1.23m, Precision.CeilingToStep(1.23m, 0.01m));
        Assert.AreEqual(10m, Precision.CeilingToStep(10m, 5m));
    }

    [TestMethod]
    public void CeilingToStepHandlesVeryLargeValues()
    {
        Assert.AreEqual(decimal.MaxValue, Precision.CeilingToStep(decimal.MaxValue, 1m));
        Assert.AreEqual(decimal.MinValue, Precision.CeilingToStep(decimal.MinValue, 1m));
    }

    [TestMethod]
    public void CeilingToStepThrowsWhenStepIsNotPositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.CeilingToStep(1m, 0m); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.CeilingToStep(1m, -1m); });
    }

    // ---------- 屬性式測試 ----------

    [TestMethod]
    public void FloorToStepInvariantHoldsForRandomInputs()
    {
        // 固定種子,失敗可重現。
        var random = new Random(20260911);

        for (var i = 0; i < 20_000; i++)
        {
            var step = Steps[random.Next(Steps.Length)];
            var value = NextValue(random);

            var result = Precision.FloorToStep(value, step);

            Assert.IsTrue(
                result <= value,
                $"第 {i} 輪:FloorToStep({value}, {step}) = {result} 超過原值");
            Assert.IsTrue(
                Precision.IsAlignedToStep(result, step),
                $"第 {i} 輪:FloorToStep({value}, {step}) = {result} 未對齊步進值");
            Assert.IsTrue(
                value - result < step,
                $"第 {i} 輪:FloorToStep({value}, {step}) = {result} 少對齊了至少一個步進");
        }
    }

    [TestMethod]
    public void CeilingToStepInvariantHoldsForRandomInputs()
    {
        var random = new Random(20260912);

        for (var i = 0; i < 20_000; i++)
        {
            var step = Steps[random.Next(Steps.Length)];
            var value = NextValue(random);

            var result = Precision.CeilingToStep(value, step);

            Assert.IsTrue(
                result >= value,
                $"第 {i} 輪:CeilingToStep({value}, {step}) = {result} 低於原值");
            Assert.IsTrue(
                Precision.IsAlignedToStep(result, step),
                $"第 {i} 輪:CeilingToStep({value}, {step}) = {result} 未對齊步進值");
            Assert.IsTrue(
                result - value < step,
                $"第 {i} 輪:CeilingToStep({value}, {step}) = {result} 多對齊了至少一個步進");
        }
    }

    [TestMethod]
    public void RoundToStepInvariantHoldsForRandomInputs()
    {
        var random = new Random(20260913);

        for (var i = 0; i < 20_000; i++)
        {
            var step = Steps[random.Next(Steps.Length)];
            var value = NextValue(random);

            var result = Precision.RoundToStep(value, step);

            Assert.IsTrue(
                Precision.IsAlignedToStep(result, step),
                $"第 {i} 輪:RoundToStep({value}, {step}) = {result} 未對齊步進值");
            Assert.IsTrue(
                Math.Abs(result - value) <= step,
                $"第 {i} 輪:RoundToStep({value}, {step}) = {result} 偏離超過一個步進");
        }
    }

    [TestMethod]
    public void ToPlainStringNeverUsesExponentNotationForRandomInputs()
    {
        var random = new Random(20260914);

        for (var i = 0; i < 20_000; i++)
        {
            var value = NextValue(random);

            var text = Precision.ToPlainString(value);

            Assert.IsFalse(
                text.Contains('E', StringComparison.OrdinalIgnoreCase),
                $"第 {i} 輪:ToPlainString({value}) = {text} 出現了科學記號");
        }
    }

    /// <summary>
    /// 產生一個帶最多 8 位小數、範圍約在正負一百萬之間的隨機十進位數。
    /// </summary>
    /// <param name="random">亂數來源。</param>
    /// <returns>隨機數值。</returns>
    private static decimal NextValue(Random random)
    {
        var whole = random.Next(-1_000_000, 1_000_001);
        var fraction = (decimal)random.Next(0, 100_000_000) / 100_000_000m;

        return whole >= 0 ? whole + fraction : whole - fraction;
    }

    // ---------- RoundToStep ----------

    [TestMethod]
    public void RoundToStepRoundsToNearestMultiple()
    {
        Assert.AreEqual(1.23m, Precision.RoundToStep(1.2340m, 0.01m));
        Assert.AreEqual(1.24m, Precision.RoundToStep(1.2360m, 0.01m));
        Assert.AreEqual(-1.24m, Precision.RoundToStep(-1.2360m, 0.01m));
    }

    [TestMethod]
    public void RoundToStepUsesBankersRoundingByDefault()
    {
        // 1.005 / 0.01 = 100.5,銀行家捨入取偶數 100。
        Assert.AreEqual(1.00m, Precision.RoundToStep(1.005m, 0.01m));

        // 1.015 / 0.01 = 101.5,取偶數 102。
        Assert.AreEqual(1.02m, Precision.RoundToStep(1.015m, 0.01m));
    }

    [TestMethod]
    public void RoundToStepHonoursAwayFromZeroMode()
    {
        Assert.AreEqual(1.01m, Precision.RoundToStep(1.005m, 0.01m, MidpointRounding.AwayFromZero));
        Assert.AreEqual(-1.01m, Precision.RoundToStep(-1.005m, 0.01m, MidpointRounding.AwayFromZero));
    }

    [TestMethod]
    public void RoundToStepThrowsWhenStepIsNotPositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.RoundToStep(1m, 0m); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.RoundToStep(1m, -1m); });
    }

    // ---------- IsAlignedToStep ----------

    [TestMethod]
    public void IsAlignedToStepDetectsExactMultiples()
    {
        Assert.IsTrue(Precision.IsAlignedToStep(1.23m, 0.01m));
        Assert.IsTrue(Precision.IsAlignedToStep(0m, 0.01m));
        Assert.IsTrue(Precision.IsAlignedToStep(-1.23m, 0.01m));
        Assert.IsTrue(Precision.IsAlignedToStep(10m, 5m));
    }

    [TestMethod]
    public void IsAlignedToStepDetectsMisalignedValues()
    {
        Assert.IsFalse(Precision.IsAlignedToStep(1.234m, 0.01m));
        Assert.IsFalse(Precision.IsAlignedToStep(-1.234m, 0.01m));
        Assert.IsFalse(Precision.IsAlignedToStep(11m, 5m));
    }

    [TestMethod]
    public void IsAlignedToStepIgnoresTrailingZerosOnTheStep()
    {
        // 交易所常送來帶尾隨零的步進值,不應影響判斷。
        Assert.IsTrue(Precision.IsAlignedToStep(1.23m, 0.01000000m));
    }

    [TestMethod]
    public void IsAlignedToStepThrowsWhenStepIsNotPositive()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.IsAlignedToStep(1m, 0m); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.IsAlignedToStep(1m, -1m); });
    }

    // ---------- Truncate ----------

    [TestMethod]
    public void TruncateCutsTowardsZero()
    {
        Assert.AreEqual(1.29m, Precision.Truncate(1.2999m, 2));
        Assert.AreEqual(-1.29m, Precision.Truncate(-1.2999m, 2));
        Assert.AreEqual(1m, Precision.Truncate(1.9999m, 0));
        Assert.AreEqual(-1m, Precision.Truncate(-1.9999m, 0));
    }

    [TestMethod]
    public void TruncateKeepsValueWhenScaleIsAlreadySmaller()
    {
        Assert.AreEqual(1.2m, Precision.Truncate(1.2m, 8));
    }

    [TestMethod]
    public void TruncateThrowsWhenDecimalsAreOutOfRange()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.Truncate(1m, -1); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = Precision.Truncate(1m, 29); });
    }

    // ---------- GetScale / GetSignificantScale / Normalize ----------

    [TestMethod]
    public void GetScaleCountsTrailingZeros()
    {
        Assert.AreEqual(4, Precision.GetScale(1.2300m));
        Assert.AreEqual(2, Precision.GetScale(1.23m));
        Assert.AreEqual(0, Precision.GetScale(1m));
        Assert.AreEqual(8, Precision.GetScale(0.00000001m));
        Assert.AreEqual(4, Precision.GetScale(-1.2300m));
    }

    [TestMethod]
    public void GetSignificantScaleIgnoresTrailingZeros()
    {
        Assert.AreEqual(2, Precision.GetSignificantScale(1.2300m));
        Assert.AreEqual(3, Precision.GetSignificantScale(0.001m));
        Assert.AreEqual(3, Precision.GetSignificantScale(0.00100000m));
        Assert.AreEqual(0, Precision.GetSignificantScale(1.0000m));
        Assert.AreEqual(0, Precision.GetSignificantScale(0m));
    }

    [TestMethod]
    public void GetScaleAndGetSignificantScaleDifferOnlyOnTrailingZeros()
    {
        // 兩者的差異就是尾隨零的數量。
        Assert.AreEqual(4, Precision.GetScale(1.2300m));
        Assert.AreEqual(2, Precision.GetSignificantScale(1.2300m));

        Assert.AreEqual(2, Precision.GetScale(1.23m));
        Assert.AreEqual(2, Precision.GetSignificantScale(1.23m));
    }

    [TestMethod]
    public void NormalizeStripsTrailingZerosWithoutChangingTheValue()
    {
        var normalized = Precision.Normalize(1.2300m);

        Assert.AreEqual(1.23m, normalized);
        Assert.AreEqual(4, Precision.GetScale(1.2300m));
        Assert.AreEqual(2, Precision.GetScale(normalized));
    }

    [TestMethod]
    public void NormalizeHandlesZeroAndNegatives()
    {
        Assert.AreEqual(0, Precision.GetScale(Precision.Normalize(0.0000m)));
        Assert.AreEqual(1, Precision.GetScale(Precision.Normalize(-0.5000m)));
        Assert.AreEqual(-0.5m, Precision.Normalize(-0.5000m));
    }

    // ---------- ToPlainString ----------

    [TestMethod]
    public void ToPlainStringNeverUsesExponentNotationForVerySmallValues()
    {
        // 送出 1E-08 給交易所 API 只會換回一個看不出原因的參數錯誤。
        Assert.AreEqual("0.00000001", Precision.ToPlainString(0.00000001m));
        Assert.AreEqual("0.00001", Precision.ToPlainString(0.00001m));
        Assert.AreEqual(
            "0.0000000000000000000000000001",
            Precision.ToPlainString(0.0000000000000000000000000001m));
    }

    [TestMethod]
    public void ToPlainStringStripsTrailingZeros()
    {
        Assert.AreEqual("1.23", Precision.ToPlainString(1.2300m));
        Assert.AreEqual("1", Precision.ToPlainString(1.0000m));
        Assert.AreEqual("0", Precision.ToPlainString(0.0000m));
        Assert.AreEqual("-1.23", Precision.ToPlainString(-1.2300m));
    }

    [TestMethod]
    public void ToPlainStringHandlesLargeValues()
    {
        Assert.AreEqual("1000000000000000000000000", Precision.ToPlainString(1000000000000000000000000m));
        Assert.AreEqual(
            "79228162514264337593543950335",
            Precision.ToPlainString(decimal.MaxValue));
    }

    [TestMethod]
    public void ToPlainStringUsesInvariantDecimalSeparator()
    {
        Assert.AreEqual("1234.5", Precision.ToPlainString(1234.5000m));
        Assert.IsFalse(Precision.ToPlainString(1234.5m).Contains(',', StringComparison.Ordinal));
    }
}

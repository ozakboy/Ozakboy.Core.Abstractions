namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="ErrorCategoryExtensions"/> 的單元測試。
/// </summary>
[TestClass]
public sealed class ErrorCategoryExtensionsTests
{
    /// <summary>
    /// 唯四被視為暫時性的分類。清單寫死在測試裡,任何一方改動都會被發現。
    /// </summary>
    private static readonly ErrorCategory[] TransientCategories =
    [
        ErrorCategory.Timeout,
        ErrorCategory.Network,
        ErrorCategory.RateLimited,
        ErrorCategory.Unavailable,
    ];

    [TestMethod]
    public void IsTransientIsTrueOnlyForTheFourTransientCategories()
    {
        foreach (var category in Enum.GetValues<ErrorCategory>())
        {
            var expected = Array.IndexOf(TransientCategories, category) >= 0;

            Assert.AreEqual(expected, category.IsTransient(), $"分類 {category} 的暫時性判斷不符預期");
        }
    }

    [TestMethod]
    public void IsTransientIsFalseForValueOutsideTheEnum()
    {
        // 由整數強制轉型而來的未定義值不應被誤判為可重試。
        Assert.IsFalse(((ErrorCategory)999).IsTransient());
    }

    [TestMethod]
    public void EnumValuesKeepTheirNumericContract()
    {
        // 這些數值可能被持久化或送上線路,不可任意調動。
        // 先各自轉型存進區域變數,避免字面常數對字面常數的編譯期比較(MSTEST0032)。
        var unexpected = ToInt(ErrorCategory.Unexpected);
        var validation = ToInt(ErrorCategory.Validation);
        var notFound = ToInt(ErrorCategory.NotFound);
        var conflict = ToInt(ErrorCategory.Conflict);
        var unauthorized = ToInt(ErrorCategory.Unauthorized);
        var forbidden = ToInt(ErrorCategory.Forbidden);
        var timeout = ToInt(ErrorCategory.Timeout);
        var network = ToInt(ErrorCategory.Network);
        var rateLimited = ToInt(ErrorCategory.RateLimited);
        var unavailable = ToInt(ErrorCategory.Unavailable);
        var cancelled = ToInt(ErrorCategory.Cancelled);
        var internalCategory = ToInt(ErrorCategory.Internal);
        var notSupported = ToInt(ErrorCategory.NotSupported);
        var exhausted = ToInt(ErrorCategory.Exhausted);

        Assert.AreEqual(0, unexpected);
        Assert.AreEqual(1, validation);
        Assert.AreEqual(2, notFound);
        Assert.AreEqual(3, conflict);
        Assert.AreEqual(4, unauthorized);
        Assert.AreEqual(5, forbidden);
        Assert.AreEqual(6, timeout);
        Assert.AreEqual(7, network);
        Assert.AreEqual(8, rateLimited);
        Assert.AreEqual(9, unavailable);
        Assert.AreEqual(10, cancelled);
        Assert.AreEqual(11, internalCategory);
        Assert.AreEqual(12, notSupported);
        Assert.AreEqual(13, exhausted);
    }

    [TestMethod]
    public void IsTransientIsFalseForNotSupported()
    {
        // NotSupported 代表這個實作做不到,不是暫時性狀況,重試沒有意義。
        Assert.IsFalse(ErrorCategory.NotSupported.IsTransient());
    }

    [TestMethod]
    public void IsTransientIsFalseForExhausted()
    {
        // Exhausted 代表重試機會已用盡。它語意上最接近 Unavailable,而 Unavailable 是暫時性的 ——
        // 這條測試就是在鎖死「不可以因為像就歸為可重試」。
        Assert.IsFalse(ErrorCategory.Exhausted.IsTransient());
        Assert.IsTrue(ErrorCategory.Unavailable.IsTransient());
    }

    [TestMethod]
    public void BackoffStrategyValuesKeepTheirNumericContract()
    {
        // 同上,經由方法呼叫取得數值,避免編譯期常數比較(MSTEST0032)。
        var none = ToInt(BackoffStrategy.None);
        var fixedStrategy = ToInt(BackoffStrategy.Fixed);
        var linear = ToInt(BackoffStrategy.Linear);
        var exponential = ToInt(BackoffStrategy.Exponential);

        Assert.AreEqual(0, none);
        Assert.AreEqual(1, fixedStrategy);
        Assert.AreEqual(2, linear);
        Assert.AreEqual(3, exponential);
    }

    /// <summary>
    /// 以方法呼叫取得列舉底層數值,避免呼叫端的比較被視為編譯期常數而觸發 MSTEST0032。
    /// </summary>
    private static int ToInt(ErrorCategory category) => (int)category;

    /// <summary>
    /// 以方法呼叫取得列舉底層數值,避免呼叫端的比較被視為編譯期常數而觸發 MSTEST0032。
    /// </summary>
    private static int ToInt(BackoffStrategy strategy) => (int)strategy;
}

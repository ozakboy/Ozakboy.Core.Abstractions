namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="RetryPolicy"/> 的單元測試。
/// 間隔計算一律走確定性的 <see cref="RetryPolicy.GetDelay(int, double)"/>,不依賴 <see cref="Random"/>。
/// </summary>
[TestClass]
public sealed class RetryPolicyTests
{
    /// <summary>
    /// 抖動取樣的中點。0.5 表示不偏移,計算結果完全確定。
    /// </summary>
    private const double NoJitterOffsetSample = 0.5d;

    // ---------- 預設值與預設實例 ----------

    [TestMethod]
    public void DefaultInstanceUsesDocumentedDefaults()
    {
        var policy = new RetryPolicy();

        Assert.AreEqual(3, policy.MaxAttempts);
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), policy.BaseDelay);
        Assert.AreEqual(TimeSpan.FromSeconds(30), policy.MaxDelay);
        Assert.AreEqual(BackoffStrategy.Exponential, policy.Strategy);
        Assert.AreEqual(0.2d, policy.JitterRatio);
    }

    [TestMethod]
    public void DefaultStaticPolicyMatchesAFreshInstance()
    {
        Assert.AreEqual(new RetryPolicy(), RetryPolicy.Default);
        Assert.AreSame(RetryPolicy.Default, RetryPolicy.Default);
    }

    [TestMethod]
    public void NoRetryNeverRetriesAndNeverWaits()
    {
        var policy = RetryPolicy.NoRetry;

        Assert.AreEqual(1, policy.MaxAttempts);
        Assert.AreEqual(TimeSpan.Zero, policy.BaseDelay);
        Assert.AreEqual(BackoffStrategy.None, policy.Strategy);
        Assert.AreEqual(0d, policy.JitterRatio);

        Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(5, NoJitterOffsetSample));

        // 即使是暫時性錯誤也不重試 —— 送單類請求正是靠這個保護。
        Assert.IsFalse(policy.ShouldRetry(1, Error.Timeout("http.timeout", "逾時")));
        Assert.IsFalse(policy.ShouldRetry(1, Error.Network("net.down", "斷線")));
    }

    // ---------- 屬性驗證邊界 ----------

    [TestMethod]
    public void MaxAttemptsAcceptsOneAndRejectsZero()
    {
        Assert.AreEqual(1, new RetryPolicy { MaxAttempts = 1 }.MaxAttempts);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = new RetryPolicy { MaxAttempts = 0 }; });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = new RetryPolicy { MaxAttempts = -1 }; });
    }

    [TestMethod]
    public void BaseDelayAcceptsZeroAndRejectsNegative()
    {
        Assert.AreEqual(TimeSpan.Zero, new RetryPolicy { BaseDelay = TimeSpan.Zero }.BaseDelay);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => { _ = new RetryPolicy { BaseDelay = TimeSpan.FromTicks(-1) }; });
    }

    [TestMethod]
    public void MaxDelayAcceptsZeroAndRejectsNegative()
    {
        Assert.AreEqual(TimeSpan.Zero, new RetryPolicy { MaxDelay = TimeSpan.Zero }.MaxDelay);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => { _ = new RetryPolicy { MaxDelay = TimeSpan.FromTicks(-1) }; });
    }

    [TestMethod]
    public void JitterRatioAcceptsTheClosedZeroToOneRange()
    {
        Assert.AreEqual(0d, new RetryPolicy { JitterRatio = 0d }.JitterRatio);
        Assert.AreEqual(1d, new RetryPolicy { JitterRatio = 1d }.JitterRatio);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = new RetryPolicy { JitterRatio = -0.0001d }; });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = new RetryPolicy { JitterRatio = 1.0001d }; });
    }

    [TestMethod]
    public void WithExpressionStillValidates()
    {
        // record 的 with 運算式一樣要走 init 存取子的驗證,不能繞過。
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = RetryPolicy.Default with { MaxAttempts = 0 }; });
        Assert.AreEqual(7, (RetryPolicy.Default with { MaxAttempts = 7 }).MaxAttempts);
    }

    [TestMethod]
    public void RecordEqualityComparesAllSettings()
    {
        var left = new RetryPolicy { MaxAttempts = 5, Strategy = BackoffStrategy.Linear };
        var right = new RetryPolicy { MaxAttempts = 5, Strategy = BackoffStrategy.Linear };

        Assert.AreEqual(left, right);
        Assert.AreEqual(left.GetHashCode(), right.GetHashCode());
        Assert.AreNotEqual(left, left with { MaxAttempts = 6 });
    }

    // ---------- 四種退避策略的間隔計算 ----------

    [TestMethod]
    public void NoneStrategyAlwaysWaitsZero()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.None,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(2, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(10, NoJitterOffsetSample));
    }

    [TestMethod]
    public void FixedStrategyWaitsTheBaseDelayEveryTime()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(2, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(7, NoJitterOffsetSample));
    }

    [TestMethod]
    public void LinearStrategyGrowsWithTheAttemptNumber()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Linear,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), policy.GetDelay(2, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(300), policy.GetDelay(3, NoJitterOffsetSample));
    }

    [TestMethod]
    public void ExponentialStrategyDoublesEachAttempt()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(200), policy.GetDelay(2, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(400), policy.GetDelay(3, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(800), policy.GetDelay(4, NoJitterOffsetSample));
    }

    [TestMethod]
    public void ZeroBaseDelayShortCircuitsEveryStrategy()
    {
        foreach (var strategy in Enum.GetValues<BackoffStrategy>())
        {
            var policy = new RetryPolicy { Strategy = strategy, BaseDelay = TimeSpan.Zero };

            Assert.AreEqual(TimeSpan.Zero, policy.GetDelay(3, NoJitterOffsetSample), $"策略 {strategy} 未回傳零");
        }
    }

    [TestMethod]
    public void UndefinedStrategyFallsBackToTheBaseDelay()
    {
        // 由整數強制轉型而來的未定義策略,退回固定間隔而不是擲出例外或算出奇怪的值。
        var policy = new RetryPolicy
        {
            Strategy = (BackoffStrategy)99,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(5, NoJitterOffsetSample));
    }

    // ---------- 上限收斂 ----------

    [TestMethod]
    public void ExponentialGrowthIsCappedByMaxDelay()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(5),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromSeconds(1), policy.GetDelay(1, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(2, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromSeconds(4), policy.GetDelay(3, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromSeconds(5), policy.GetDelay(4, NoJitterOffsetSample));
        Assert.AreEqual(TimeSpan.FromSeconds(5), policy.GetDelay(10, NoJitterOffsetSample));
    }

    [TestMethod]
    public void LinearGrowthIsCappedByMaxDelay()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Linear,
            BaseDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(2),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromSeconds(2), policy.GetDelay(5, NoJitterOffsetSample));
    }

    [TestMethod]
    public void FixedDelayIsAlsoCappedByMaxDelay()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromMilliseconds(50),
            JitterRatio = 0d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(50), policy.GetDelay(1, NoJitterOffsetSample));
    }

    [TestMethod]
    public void HugeAttemptNumbersDoNotOverflowOrGoNegative()
    {
        // 長時間斷線時 attempt 會一路累加,不能因此溢位或算出負值。
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterRatio = 0d,
        };

        foreach (var attempt in new[] { 32, 64, 100, 1_000, 100_000, int.MaxValue })
        {
            var delay = policy.GetDelay(attempt, NoJitterOffsetSample);

            Assert.AreEqual(TimeSpan.FromSeconds(30), delay, $"attempt {attempt} 未收斂到上限");
        }
    }

    [TestMethod]
    public void HugeAttemptNumbersStayBoundedForEveryStrategy()
    {
        foreach (var strategy in Enum.GetValues<BackoffStrategy>())
        {
            var policy = new RetryPolicy
            {
                Strategy = strategy,
                BaseDelay = TimeSpan.FromMilliseconds(200),
                MaxDelay = TimeSpan.FromSeconds(30),
                JitterRatio = 0.2d,
            };

            foreach (var attempt in new[] { 1, 2, 50, 1_000, int.MaxValue })
            {
                foreach (var sample in new[] { 0d, 0.25d, 0.5d, 0.75d, 1d })
                {
                    var delay = policy.GetDelay(attempt, sample);

                    Assert.IsTrue(
                        delay >= TimeSpan.Zero,
                        $"策略 {strategy}、attempt {attempt}、sample {sample} 得到負的間隔 {delay}");
                    Assert.IsTrue(
                        delay <= policy.MaxDelay,
                        $"策略 {strategy}、attempt {attempt}、sample {sample} 超過上限:{delay}");
                }
            }
        }
    }

    // ---------- 抖動 ----------

    [TestMethod]
    public void ZeroJitterRatioMakesTheResultFullyDeterministic()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0d,
        };

        foreach (var sample in new[] { 0d, 0.1d, 0.5d, 0.9d, 1d })
        {
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, sample), $"sample {sample} 造成了偏移");
        }
    }

    [TestMethod]
    public void JitterSampleAtMidpointMeansNoOffset()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            JitterRatio = 0.2d,
        };

        Assert.AreEqual(TimeSpan.FromMilliseconds(100), policy.GetDelay(1, NoJitterOffsetSample));
    }

    [TestMethod]
    public void JitterSampleZeroAndOneGiveTheLowerAndUpperBounds()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterRatio = 0.2d,
        };

        // 正負 20%:下界 80 毫秒、上界 120 毫秒。浮點數運算容許極小誤差。
        Assert.AreEqual(80d, policy.GetDelay(1, 0d).TotalMilliseconds, 0.001d);
        Assert.AreEqual(120d, policy.GetDelay(1, 1d).TotalMilliseconds, 0.001d);
    }

    [TestMethod]
    public void FullJitterRatioSpansFromZeroToDoubleTheInterval()
    {
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterRatio = 1d,
        };

        Assert.AreEqual(0d, policy.GetDelay(1, 0d).TotalMilliseconds, 0.001d);
        Assert.AreEqual(200d, policy.GetDelay(1, 1d).TotalMilliseconds, 0.001d);
    }

    [TestMethod]
    public void JitterNeverPushesTheDelayPastMaxDelay()
    {
        // 間隔已經頂到上限時,向上的抖動會被上限吃掉。
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Fixed,
            BaseDelay = TimeSpan.FromSeconds(30),
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterRatio = 0.2d,
        };

        Assert.AreEqual(TimeSpan.FromSeconds(30), policy.GetDelay(1, 1d));
        Assert.IsTrue(policy.GetDelay(1, 0d) < TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public void RandomGetDelayStaysWithinBounds()
    {
        // 單參數版本走 Random.Shared,只驗證界線。
        var policy = new RetryPolicy
        {
            Strategy = BackoffStrategy.Exponential,
            BaseDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromSeconds(30),
            JitterRatio = 0.2d,
        };

        for (var attempt = 1; attempt <= 20; attempt++)
        {
            var delay = policy.GetDelay(attempt);

            Assert.IsTrue(delay >= TimeSpan.Zero, $"attempt {attempt} 得到負的間隔");
            Assert.IsTrue(delay <= policy.MaxDelay, $"attempt {attempt} 超過上限");
        }
    }

    // ---------- 參數驗證 ----------

    [TestMethod]
    public void GetDelayRejectsAttemptBelowOne()
    {
        var policy = RetryPolicy.Default;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = policy.GetDelay(0, NoJitterOffsetSample); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = policy.GetDelay(-1, NoJitterOffsetSample); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = policy.GetDelay(0); });
    }

    [TestMethod]
    public void GetDelayRejectsJitterSampleOutsideZeroToOne()
    {
        var policy = RetryPolicy.Default;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = policy.GetDelay(1, -0.0001d); });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => { _ = policy.GetDelay(1, 1.0001d); });
    }

    [TestMethod]
    public void GetDelayAcceptsTheClosedJitterSampleRange()
    {
        var policy = RetryPolicy.Default;

        Assert.IsTrue(policy.GetDelay(1, 0d) >= TimeSpan.Zero);
        Assert.IsTrue(policy.GetDelay(1, 1d) >= TimeSpan.Zero);
    }

    // ---------- ShouldRetry ----------

    [TestMethod]
    public void ShouldRetryIsTrueForTransientErrorWithAttemptsRemaining()
    {
        var policy = new RetryPolicy { MaxAttempts = 3 };

        Assert.IsTrue(policy.ShouldRetry(1, Error.Timeout("http.timeout", "逾時")));
        Assert.IsTrue(policy.ShouldRetry(2, Error.Network("net.down", "斷線")));
    }

    [TestMethod]
    public void ShouldRetryIsFalseWhenAttemptsAreExhausted()
    {
        var policy = new RetryPolicy { MaxAttempts = 3 };

        Assert.IsFalse(policy.ShouldRetry(3, Error.Timeout("http.timeout", "逾時")));
        Assert.IsFalse(policy.ShouldRetry(4, Error.RateLimited("exchange.rate_limit", "被限流")));
    }

    [TestMethod]
    public void ShouldRetryIsFalseForNonTransientErrorEvenWithAttemptsRemaining()
    {
        var policy = new RetryPolicy { MaxAttempts = 10 };

        Assert.IsFalse(policy.ShouldRetry(1, Error.Validation("qty.too_small", "數量太小")));
        Assert.IsFalse(policy.ShouldRetry(1, Error.NotFound("symbol.missing", "找不到商品")));
        Assert.IsFalse(policy.ShouldRetry(1, Error.Conflict("state.invalid", "狀態不允許")));
        Assert.IsFalse(policy.ShouldRetry(1, Error.Internal("core.bug", "程式缺陷")));
        Assert.IsFalse(policy.ShouldRetry(1, new Error("cancelled", "已取消", ErrorCategory.Cancelled)));
    }

    [TestMethod]
    public void ShouldRetryCoversEveryCategoryConsistentlyWithIsTransient()
    {
        var policy = new RetryPolicy { MaxAttempts = 10 };

        foreach (var category in Enum.GetValues<ErrorCategory>())
        {
            var error = new Error("code", "message", category);

            Assert.AreEqual(
                category.IsTransient(),
                policy.ShouldRetry(1, error),
                $"分類 {category} 的重試判斷與 IsTransient 不一致");
        }
    }

    [TestMethod]
    public void ShouldRetryThrowsWhenErrorIsNull()
    {
        var policy = RetryPolicy.Default;

        Assert.ThrowsExactly<ArgumentNullException>(() => { _ = policy.ShouldRetry(1, null!); });
    }
}

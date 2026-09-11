namespace Ozakboy.Core.Abstractions.Tests;

/// <summary>
/// <see cref="ResultExtensions"/> 的單元測試。
/// 重點有二:失敗必須短路(用旗標變數確認委派真的沒被呼叫),以及多載能被正確推斷。
/// </summary>
[TestClass]
public sealed class ResultExtensionsTests
{
    /// <summary>
    /// 測試共用的失敗錯誤。
    /// </summary>
    private static readonly Error Failure = Error.Timeout("http.timeout", "請求逾時");

    // ---------- Task<Result<T>>.MapAsync(同步轉換) ----------

    [TestMethod]
    public async Task MapAsyncWithSyncTransformTransformsValueOnSuccess()
    {
        var result = await SuccessAsync(21).MapAsync(v => v * 2);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task MapAsyncWithSyncTransformCanChangeType()
    {
        var result = await SuccessAsync(1.2300m).MapAsync(Precision.ToPlainString);

        Assert.AreEqual("1.23", result.GetValueOrDefault(string.Empty));
    }

    [TestMethod]
    public async Task MapAsyncWithSyncTransformSkipsTransformOnFailure()
    {
        var transformRan = false;

        var result = await FailureAsync<int>().MapAsync(v =>
        {
            transformRan = true;
            return v * 2;
        });

        Assert.IsFalse(transformRan, "失敗時不應執行轉換函式");
        Assert.AreSame(Failure, result.Error);
    }

    // ---------- Task<Result<T>>.MapAsync(非同步轉換) ----------

    [TestMethod]
    public async Task MapAsyncWithAsyncTransformTransformsValueOnSuccess()
    {
        var result = await SuccessAsync(21).MapAsync(async v =>
        {
            await Task.Yield();
            return v * 2;
        });

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task MapAsyncWithAsyncTransformSkipsTransformOnFailure()
    {
        var transformRan = false;

        var result = await FailureAsync<int>().MapAsync(async v =>
        {
            transformRan = true;
            await Task.Yield();
            return v * 2;
        });

        Assert.IsFalse(transformRan, "失敗時不應執行非同步轉換函式");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task MapAsyncWithAsyncTransformSkipsTransformOnDefaultResult()
    {
        var transformRan = false;
        var uninitialized = Task.FromResult(default(Result<int>));

        var result = await uninitialized.MapAsync(async v =>
        {
            transformRan = true;
            await Task.Yield();
            return v * 2;
        });

        Assert.IsFalse(transformRan);
        Assert.AreEqual(Error.Uninitialized, result.Error);
    }

    // ---------- Task<Result<T>>.ThenAsync ----------

    [TestMethod]
    public async Task ThenAsyncWithSyncNextChainsOnSuccess()
    {
        var result = await SuccessAsync(10).ThenAsync(v => Result.Success(v + 1));

        Assert.AreEqual(11, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task ThenAsyncWithSyncNextCanReturnFailure()
    {
        var guard = Error.Conflict("state.invalid", "狀態不允許");

        var result = await SuccessAsync(10).ThenAsync(_ => Result.Failure<int>(guard));

        Assert.AreSame(guard, result.Error);
    }

    [TestMethod]
    public async Task ThenAsyncWithSyncNextSkipsNextOnFailure()
    {
        var nextRan = false;

        var result = await FailureAsync<int>().ThenAsync(v =>
        {
            nextRan = true;
            return Result.Success(v);
        });

        Assert.IsFalse(nextRan, "失敗時不應執行後續操作");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task ThenAsyncWithAsyncNextChainsOnSuccess()
    {
        var result = await SuccessAsync(10).ThenAsync(async v =>
        {
            await Task.Yield();
            return Result.Success(v + 1);
        });

        Assert.AreEqual(11, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task ThenAsyncWithAsyncNextSkipsNextOnFailure()
    {
        var nextRan = false;

        var result = await FailureAsync<int>().ThenAsync(async v =>
        {
            nextRan = true;
            await Task.Yield();
            return Result.Success(v);
        });

        Assert.IsFalse(nextRan, "失敗時不應執行非同步後續操作");
        Assert.AreSame(Failure, result.Error);
    }

    // ---------- Task<Result<T>>.EnsureAsync ----------

    [TestMethod]
    public async Task EnsureAsyncKeepsResultWhenPredicateHolds()
    {
        var result = await SuccessAsync(5m).EnsureAsync(v => v > 0m, Error.Validation("qty.non_positive", "數量必須為正"));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(5m, result.GetValueOrDefault(-1m));
    }

    [TestMethod]
    public async Task EnsureAsyncTurnsIntoSuppliedFailureWhenPredicateDoesNotHold()
    {
        var guard = Error.Validation("qty.non_positive", "數量必須為正");

        var result = await SuccessAsync(0m).EnsureAsync(v => v > 0m, guard);

        Assert.AreSame(guard, result.Error);
    }

    [TestMethod]
    public async Task EnsureAsyncSkipsPredicateOnFailure()
    {
        var predicateRan = false;

        var result = await FailureAsync<int>().EnsureAsync(
            v =>
            {
                predicateRan = true;
                return v > 0;
            },
            Error.Validation("qty.non_positive", "數量必須為正"));

        Assert.IsFalse(predicateRan, "失敗時不應執行條件判斷");
        Assert.AreSame(Failure, result.Error);
    }

    // ---------- Task<Result<T>>.MatchAsync ----------

    [TestMethod]
    public async Task MatchAsyncRunsSuccessBranchOnSuccess()
    {
        var failureBranchRan = false;

        var text = await SuccessAsync(7).MatchAsync(
            onSuccess: v => $"值 {v}",
            onFailure: _ =>
            {
                failureBranchRan = true;
                return "bad";
            });

        Assert.AreEqual("值 7", text);
        Assert.IsFalse(failureBranchRan, "成功時不應執行失敗分支");
    }

    [TestMethod]
    public async Task MatchAsyncRunsFailureBranchOnFailure()
    {
        var successBranchRan = false;

        var text = await FailureAsync<int>().MatchAsync(
            onSuccess: _ =>
            {
                successBranchRan = true;
                return "ok";
            },
            onFailure: e => e.Code);

        Assert.AreEqual("http.timeout", text);
        Assert.IsFalse(successBranchRan, "失敗時不應執行成功分支");
    }

    // ---------- Task<Result<T>>.ToResultAsync ----------

    [TestMethod]
    public async Task ToResultAsyncDiscardsValueOnSuccess()
    {
        var result = await SuccessAsync(42).ToResultAsync();

        Assert.AreEqual(Result.Success(), result);
    }

    [TestMethod]
    public async Task ToResultAsyncKeepsErrorOnFailure()
    {
        var result = await FailureAsync<int>().ToResultAsync();

        Assert.IsTrue(result.IsFailure);
        Assert.AreSame(Failure, result.Error);
    }

    // ---------- 同步 Result<T> 接非同步後續 ----------

    [TestMethod]
    public async Task SyncResultThenAsyncChainsOnSuccess()
    {
        var result = await Result.Success(10).ThenAsync(async v =>
        {
            await Task.Yield();
            return Result.Success(v + 1);
        });

        Assert.AreEqual(11, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task SyncResultThenAsyncSkipsNextOnFailure()
    {
        var nextRan = false;

        var result = await Result.Failure<int>(Failure).ThenAsync(async v =>
        {
            nextRan = true;
            await Task.Yield();
            return Result.Success(v);
        });

        Assert.IsFalse(nextRan, "失敗時不應執行非同步後續操作");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task SyncResultMapAsyncTransformsValueOnSuccess()
    {
        var result = await Result.Success(21).MapAsync(async v =>
        {
            await Task.Yield();
            return v * 2;
        });

        Assert.AreEqual(42, result.GetValueOrDefault(-1));
    }

    [TestMethod]
    public async Task SyncResultMapAsyncSkipsTransformOnFailure()
    {
        var transformRan = false;

        var result = await Result.Failure<int>(Failure).MapAsync(async v =>
        {
            transformRan = true;
            await Task.Yield();
            return v * 2;
        });

        Assert.IsFalse(transformRan, "失敗時不應執行非同步轉換函式");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task SyncResultMapAsyncOnDefaultValueForwardsUninitializedError()
    {
        Result<int> uninitialized = default;

        var result = await uninitialized.MapAsync(v => Task.FromResult(v * 2));

        Assert.AreEqual(Error.Uninitialized, result.Error);
    }

    // ---------- Task<Result> ----------

    [TestMethod]
    public async Task ValuelessThenAsyncWithAsyncNextChainsOnSuccess()
    {
        var ran = false;

        var result = await Task.FromResult(Result.Success()).ThenAsync(async () =>
        {
            ran = true;
            await Task.Yield();
            return Result.Success();
        });

        Assert.IsTrue(ran);
        Assert.IsTrue(result.IsSuccess);
    }

    [TestMethod]
    public async Task ValuelessThenAsyncWithAsyncNextSkipsNextOnFailure()
    {
        var ran = false;

        var result = await Task.FromResult(Result.Failure(Failure)).ThenAsync(async () =>
        {
            ran = true;
            await Task.Yield();
            return Result.Success();
        });

        Assert.IsFalse(ran, "失敗時不應執行非同步後續操作");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task ValuelessThenAsyncWithSyncNextChainsOnSuccess()
    {
        var result = await Task.FromResult(Result.Success()).ThenAsync(() => Result.Failure("next.failed", "後續步驟失敗"));

        Assert.AreEqual("next.failed", result.Error!.Code);
    }

    [TestMethod]
    public async Task ValuelessThenAsyncWithSyncNextSkipsNextOnFailure()
    {
        var ran = false;

        var result = await Task.FromResult(Result.Failure(Failure)).ThenAsync(() =>
        {
            ran = true;
            return Result.Success();
        });

        Assert.IsFalse(ran, "失敗時不應執行後續操作");
        Assert.AreSame(Failure, result.Error);
    }

    [TestMethod]
    public async Task ValuelessThenAsyncSkipsNextOnDefaultResult()
    {
        var ran = false;

        var result = await Task.FromResult(default(Result)).ThenAsync(() =>
        {
            ran = true;
            return Result.Success();
        });

        Assert.IsFalse(ran);
        Assert.AreEqual(Error.Uninitialized, result.Error);
    }

    // ---------- 串接整合 ----------

    [TestMethod]
    public async Task ChainedAsyncPipelineStopsAtTheFirstFailure()
    {
        // 這正是這組 API 存在的理由:三步串接,中途不需要任何手動 await 與轉發。
        var mapRan = false;
        var thenRan = false;
        var guard = Error.Validation("qty.non_positive", "數量必須為正");

        var result = await SuccessAsync(0m)
            .EnsureAsync(v => v > 0m, guard)
            .MapAsync(v =>
            {
                mapRan = true;
                return v * 2m;
            })
            .ThenAsync(async v =>
            {
                thenRan = true;
                await Task.Yield();
                return Result.Success(v);
            });

        Assert.IsFalse(mapRan);
        Assert.IsFalse(thenRan);
        Assert.AreSame(guard, result.Error);
    }

    [TestMethod]
    public async Task ChainedAsyncPipelineCarriesTheValueThroughEveryStep()
    {
        var text = await SuccessAsync(20m)
            .EnsureAsync(v => v > 0m, Error.Validation("qty.non_positive", "數量必須為正"))
            .ThenAsync(async v =>
            {
                await Task.Yield();
                return Result.Success(v + 1.5m);
            })
            .MapAsync(Precision.ToPlainString)
            .MatchAsync(v => v, e => e.Code);

        Assert.AreEqual("21.5", text);
    }

    // ---------- 參數驗證 ----------

    [TestMethod]
    public async Task EveryCombinatorRejectsNullDelegates()
    {
        // 非同步方法的參數檢查由回傳的工作帶出,因此必須用 ThrowsExactlyAsync 而不是同步版本。
        var success = SuccessAsync(1);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.MapAsync((Func<int, string>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.MapAsync((Func<int, Task<string>>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.ThenAsync((Func<int, Result<string>>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => success.ThenAsync((Func<int, Task<Result<string>>>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => success.EnsureAsync(null!, Error.Validation("c", "m")));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.EnsureAsync(v => v > 0, null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.MatchAsync(null!, e => e.Code));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.MatchAsync(v => v.ToString(), null!));
    }

    [TestMethod]
    public async Task EveryCombinatorRejectsNullTask()
    {
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ((Task<Result<int>>)null!).MapAsync(v => v * 2));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).MapAsync(v => Task.FromResult(v * 2)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).ThenAsync(v => Result.Success(v)));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).ThenAsync(v => Task.FromResult(Result.Success(v))));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).EnsureAsync(v => v > 0, Error.Validation("c", "m")));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result<int>>)null!).MatchAsync(v => v, _ => 0));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => ((Task<Result<int>>)null!).ToResultAsync());
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result>)null!).ThenAsync(() => Task.FromResult(Result.Success())));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => ((Task<Result>)null!).ThenAsync(() => Result.Success()));
    }

    [TestMethod]
    public async Task SyncReceiverCombinatorsRejectNullDelegates()
    {
        var success = Result.Success(1);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => success.ThenAsync((Func<int, Task<Result<string>>>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            () => success.MapAsync((Func<int, Task<string>>)null!));
    }

    [TestMethod]
    public async Task ValuelessThenAsyncRejectsNullDelegates()
    {
        var success = Task.FromResult(Result.Success());

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.ThenAsync((Func<Task<Result>>)null!));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => success.ThenAsync((Func<Result>)null!));
    }

    /// <summary>
    /// 產生一個已完成、成功的非同步結果。
    /// </summary>
    /// <typeparam name="T">回傳值型別。</typeparam>
    /// <param name="value">回傳值。</param>
    /// <returns>成功的非同步結果。</returns>
    private static Task<Result<T>> SuccessAsync<T>(T value) => Task.FromResult(Result.Success(value));

    /// <summary>
    /// 產生一個已完成、失敗的非同步結果,錯誤固定為 <see cref="Failure"/>。
    /// </summary>
    /// <typeparam name="T">回傳值型別。</typeparam>
    /// <returns>失敗的非同步結果。</returns>
    private static Task<Result<T>> FailureAsync<T>() => Task.FromResult(Result.Failure<T>(Failure));
}

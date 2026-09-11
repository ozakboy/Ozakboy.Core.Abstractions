namespace Ozakboy.Core.Abstractions;

/// <summary>
/// <see cref="Result"/> 與 <see cref="Result{T}"/> 的非同步組合子。
/// Asynchronous combinators for <see cref="Result"/> and <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Result{T}"/> 本身的 <c>Map</c>、<c>Then</c>、<c>Ensure</c> 只吃同步委派,所以一旦某一步是
/// 非同步的,串接就斷了:得先 <c>await</c> 拿到 <see cref="Result{T}"/>、判斷成敗、再手動把失敗轉發下去。
/// 「串三步、每步都可能失敗」的程式碼會因此比需要的長一倍,而且每一次手動轉發都是一個漏判的機會。
/// The combinators on <see cref="Result{T}"/> take synchronous delegates, so a chain breaks the moment one step is
/// asynchronous: you have to <c>await</c>, inspect the outcome, and forward the failure by hand. Three fallible
/// steps end up twice as long as they need to be, and every hand-written forward is another chance to miss one.
/// </para>
/// <para>
/// 這裡的方法同時接受 <c>Task&lt;Result&lt;T&gt;&gt;</c> 作為接收者與非同步委派作為後續,因此可以直接寫成
/// <c>await LoadAsync(id).ThenAsync(ValidateAsync).MapAsync(ToDto)</c>,中途不需要任何 <c>await</c>。
/// 失敗一律短路:第一個失敗之後的委派不會被呼叫。
/// These accept both a <c>Task&lt;Result&lt;T&gt;&gt;</c> receiver and asynchronous continuations, so a pipeline
/// reads as <c>await LoadAsync(id).ThenAsync(ValidateAsync).MapAsync(ToDto)</c> with no intermediate <c>await</c>.
/// Failures short-circuit: no delegate after the first failure is invoked.
/// </para>
/// <para>
/// 所有 <c>await</c> 都加上 <c>ConfigureAwait(false)</c>。這是函式庫的必要作法:在有
/// <see cref="System.Threading.SynchronizationContext"/> 的宿主(WPF、WinForms)底下,缺了它的續行會被排回
/// UI 執行緒,呼叫端只要在任何一層同步等待就會死鎖。這個套件的上層正好就是一個 WPF 應用程式。
/// Every <c>await</c> uses <c>ConfigureAwait(false)</c>. That is mandatory for a library: under a host with a
/// <see cref="System.Threading.SynchronizationContext"/> such as WPF or WinForms, a continuation without it is
/// posted back to the UI thread, and any synchronous wait anywhere up the stack deadlocks. A WPF application sits
/// above this package.
/// </para>
/// <para>
/// 參數驗證發生在非同步方法內部,因此 <see cref="ArgumentNullException"/> 會由回傳的
/// <see cref="System.Threading.Tasks.Task"/> 帶出,而不是在呼叫的當下擲出。要斷言它請用
/// <c>await</c>,不要用同步的 try/catch 包住呼叫本身。
/// Argument validation happens inside the asynchronous methods, so an <see cref="ArgumentNullException"/> surfaces
/// through the returned <see cref="System.Threading.Tasks.Task"/> rather than at the call site. Assert on it by
/// awaiting, not by wrapping the call itself in a synchronous try/catch.
/// </para>
/// </remarks>
public static class ResultExtensions
{
    /// <summary>
    /// 等待結果,成功時以同步轉換函式改變回傳值的型別;失敗時原樣傳遞錯誤。
    /// Awaits the result and transforms the value with a synchronous function on success; propagates the error
    /// untouched on failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">轉換後的型別。The transformed type.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="transform">轉換函式。失敗時不會被呼叫。The transform; not invoked on failure.</param>
    /// <returns>轉換後的結果,或原本的失敗。The transformed result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> task, Func<T, TOut> transform)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(transform);

        var result = await task.ConfigureAwait(false);

        return result.Map(transform);
    }

    /// <summary>
    /// 等待結果,成功時以非同步轉換函式改變回傳值的型別;失敗時原樣傳遞錯誤。
    /// Awaits the result and transforms the value with an asynchronous function on success; propagates the error
    /// untouched on failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">轉換後的型別。The transformed type.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="transform">非同步轉換函式。失敗時不會被呼叫。The asynchronous transform; not invoked on failure.</param>
    /// <returns>轉換後的結果,或原本的失敗。The transformed result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> task, Func<T, Task<TOut>> transform)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(transform);

        var result = await task.ConfigureAwait(false);
        if (!result.TryGetValue(out var value))
        {
            return result.ToFailure<TOut>();
        }

        return Result.Success(await transform(value).ConfigureAwait(false));
    }

    /// <summary>
    /// 等待結果,成功時接著執行下一個同步操作;失敗時原樣傳遞錯誤。
    /// Awaits the result and chains the next synchronous operation on success; propagates the error untouched on
    /// failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">接續操作的回傳值型別。The value type of the chained operation.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="next">要接續執行的操作。失敗時不會被呼叫。The operation to chain; not invoked on failure.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> ThenAsync<T, TOut>(this Task<Result<T>> task, Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        var result = await task.ConfigureAwait(false);

        return result.Then(next);
    }

    /// <summary>
    /// 等待結果,成功時接著執行下一個非同步操作;失敗時原樣傳遞錯誤。
    /// Awaits the result and chains the next asynchronous operation on success; propagates the error untouched on
    /// failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">接續操作的回傳值型別。The value type of the chained operation.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="next">要接續執行的非同步操作。失敗時不會被呼叫。The asynchronous operation to chain; not invoked on failure.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> ThenAsync<T, TOut>(
        this Task<Result<T>> task,
        Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        var result = await task.ConfigureAwait(false);
        if (!result.TryGetValue(out var value))
        {
            return result.ToFailure<TOut>();
        }

        return await next(value).ConfigureAwait(false);
    }

    /// <summary>
    /// 等待結果,成功時檢查回傳值是否滿足條件,不滿足則轉為指定的失敗。
    /// Awaits the result and checks the value against a predicate on success, turning it into the supplied failure
    /// if it does not hold.
    /// </summary>
    /// <typeparam name="T">回傳值型別。The value type.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="predicate">要滿足的條件。失敗時不會被呼叫。The predicate; not invoked on failure.</param>
    /// <param name="error">條件不滿足時使用的錯誤。The error used when the predicate does not hold.</param>
    /// <returns>原結果,或條件不滿足時的失敗。The original result, or a failure when the predicate does not hold.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when any argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<T>> EnsureAsync<T>(
        this Task<Result<T>> task,
        Func<T, bool> predicate,
        Error error)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        var result = await task.ConfigureAwait(false);

        return result.Ensure(predicate, error);
    }

    /// <summary>
    /// 等待結果,依成功或失敗分別執行對應的函式,回傳同一型別的值。
    /// Awaits the result and runs one of two functions depending on the outcome, returning a value of the same type.
    /// </summary>
    /// <typeparam name="T">回傳值型別。The value type.</typeparam>
    /// <typeparam name="TOut">兩個分支共同的回傳型別。The return type shared by both branches.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="onSuccess">成功時執行,參數為回傳值。Invoked on success with the value.</param>
    /// <param name="onFailure">失敗時執行,參數為錯誤內容。Invoked on failure with the error.</param>
    /// <returns>被執行的函式所回傳的值。The value returned by whichever function ran.</returns>
    /// <remarks>
    /// 這是管線的收尾:把 <see cref="Result{T}"/> 攤平成一個一般的值,之後就不再需要判斷成敗。
    /// This closes a pipeline: it flattens the <see cref="Result{T}"/> into an ordinary value, after which there is
    /// nothing left to check.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when any argument is <see langword="null"/>.
    /// </exception>
    public static async Task<TOut> MatchAsync<T, TOut>(
        this Task<Result<T>> task,
        Func<T, TOut> onSuccess,
        Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        var result = await task.ConfigureAwait(false);

        return result.Match(onSuccess, onFailure);
    }

    /// <summary>
    /// 等待結果並捨棄回傳值,轉成沒有回傳值的結果。
    /// Awaits the result and discards the value, converting to a valueless result.
    /// </summary>
    /// <typeparam name="T">回傳值型別。The value type.</typeparam>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <returns>對應的 <see cref="Result"/>。The corresponding <see cref="Result"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="task"/> 為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when <paramref name="task"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<Result> ToResultAsync<T>(this Task<Result<T>> task)
    {
        ArgumentNullException.ThrowIfNull(task);

        var result = await task.ConfigureAwait(false);

        return result.ToResult();
    }

    /// <summary>
    /// 在同步的結果上接著執行非同步操作;失敗時原樣傳遞錯誤。
    /// Chains an asynchronous operation onto a synchronous result; propagates the error untouched on failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">接續操作的回傳值型別。The value type of the chained operation.</typeparam>
    /// <param name="result">來源結果。The source result.</param>
    /// <param name="next">要接續執行的非同步操作。失敗時不會被呼叫。The asynchronous operation to chain; not invoked on failure.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <remarks>
    /// 管線的第一步是同步的、第二步才開始非同步時用這個 —— 不必為了接上去而先把同步結果包成
    /// <c>Task.FromResult</c>。
    /// For a pipeline whose first step is synchronous and whose second is not: there is no need to wrap the
    /// synchronous result in <c>Task.FromResult</c> just to join the chain.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> 為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> ThenAsync<T, TOut>(this Result<T> result, Func<T, Task<Result<TOut>>> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (!result.TryGetValue(out var value))
        {
            return result.ToFailure<TOut>();
        }

        return await next(value).ConfigureAwait(false);
    }

    /// <summary>
    /// 在同步的結果上以非同步轉換函式改變回傳值的型別;失敗時原樣傳遞錯誤。
    /// Transforms the value of a synchronous result with an asynchronous function; propagates the error untouched on
    /// failure.
    /// </summary>
    /// <typeparam name="T">來源回傳值型別。The source value type.</typeparam>
    /// <typeparam name="TOut">轉換後的型別。The transformed type.</typeparam>
    /// <param name="result">來源結果。The source result.</param>
    /// <param name="transform">非同步轉換函式。失敗時不會被呼叫。The asynchronous transform; not invoked on failure.</param>
    /// <returns>轉換後的結果,或原本的失敗。The transformed result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="transform"/> 為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when <paramref name="transform"/> is <see langword="null"/>.
    /// </exception>
    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Result<T> result, Func<T, Task<TOut>> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        if (!result.TryGetValue(out var value))
        {
            return result.ToFailure<TOut>();
        }

        return Result.Success(await transform(value).ConfigureAwait(false));
    }

    /// <summary>
    /// 等待無回傳值的結果,成功時接著執行下一個非同步操作;失敗時原樣傳遞錯誤。
    /// Awaits a valueless result and chains the next asynchronous operation on success; propagates the error
    /// untouched on failure.
    /// </summary>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="next">要接續執行的非同步操作。失敗時不會被呼叫。The asynchronous operation to chain; not invoked on failure.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result> ThenAsync(this Task<Result> task, Func<Task<Result>> next)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        var result = await task.ConfigureAwait(false);
        if (result.IsFailure)
        {
            return result;
        }

        return await next().ConfigureAwait(false);
    }

    /// <summary>
    /// 等待無回傳值的結果,成功時接著執行下一個同步操作;失敗時原樣傳遞錯誤。
    /// Awaits a valueless result and chains the next synchronous operation on success; propagates the error
    /// untouched on failure.
    /// </summary>
    /// <param name="task">產生結果的工作。The task producing the result.</param>
    /// <param name="next">要接續執行的操作。失敗時不會被呼叫。The operation to chain; not invoked on failure.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一參數為 <see langword="null"/> 時,由回傳的工作帶出。
    /// Surfaced through the returned task when either argument is <see langword="null"/>.
    /// </exception>
    public static async Task<Result> ThenAsync(this Task<Result> task, Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(next);

        var result = await task.ConfigureAwait(false);

        return result.Then(next);
    }
}

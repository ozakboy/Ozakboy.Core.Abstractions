using System.Diagnostics.CodeAnalysis;

namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 帶回傳值的操作結果:成功並附帶一個 <typeparamref name="T"/>,或失敗並附帶一個 <see cref="Abstractions.Error"/>。
/// The outcome of an operation that returns a value: success with a <typeparamref name="T"/>, or failure with an <see cref="Abstractions.Error"/>.
/// </summary>
/// <typeparam name="T">
/// 成功時的回傳值型別。
/// The type of the value produced on success.
/// </typeparam>
/// <remarks>
/// <para>
/// 這是值型別,成功路徑不會產生堆積配置。<see langword="default"/> 值視為失敗,其錯誤為
/// <see cref="Abstractions.Error.Uninitialized"/>。
/// This is a value type, so the success path allocates nothing on the heap. A <see langword="default"/> value is
/// treated as a failure carrying <see cref="Abstractions.Error.Uninitialized"/>.
/// </para>
/// <para>
/// 取值一律經由 <see cref="TryGetValue"/>、<see cref="GetValueOrDefault(T)"/> 或 <see cref="Match{TOut}"/>;
/// 刻意不提供「失敗時會擲出例外」的取值屬性,避免呼叫端在沒有檢查的情況下直接讀取。
/// Values are read through <see cref="TryGetValue"/>, <see cref="GetValueOrDefault(T)"/>, or <see cref="Match{TOut}"/>.
/// There is deliberately no property that throws on failure, so callers cannot read a value without checking first.
/// </para>
/// </remarks>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly bool _isSuccess;
    private readonly T? _value;
    private readonly Error? _error;

    private Result(bool isSuccess, T? value, Error? error)
    {
        _isSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>
    /// 操作是否成功。為 <see langword="false"/> 時 <see cref="Error"/> 保證非 <see langword="null"/>。
    /// Whether the operation succeeded. When <see langword="false"/>, <see cref="Error"/> is guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// 操作是否失敗。為 <see langword="true"/> 時 <see cref="Error"/> 保證非 <see langword="null"/>。
    /// Whether the operation failed. When <see langword="true"/>, <see cref="Error"/> is guaranteed non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// 失敗的內容;成功時為 <see langword="null"/>。
    /// The failure details, or <see langword="null"/> when the operation succeeded.
    /// </summary>
    public Error? Error => _isSuccess ? null : (_error ?? Abstractions.Error.Uninitialized);

    /// <summary>
    /// 建立成功的結果。
    /// Creates a successful result.
    /// </summary>
    /// <param name="value">回傳值。The value.</param>
    /// <returns>成功的結果。A successful result.</returns>
    internal static Result<T> FromValue(T value) => new(true, value, null);

    /// <summary>
    /// 建立失敗的結果。
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">失敗的內容。The failure details.</param>
    /// <returns>失敗的結果。A failed result.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="error"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    internal static Result<T> FromError(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(false, default, error);
    }

    /// <summary>
    /// 取得成功時的回傳值。
    /// Gets the value produced on success.
    /// </summary>
    /// <param name="value">
    /// 成功時輸出回傳值,失敗時為型別預設值。
    /// Receives the value on success; the type default on failure.
    /// </param>
    /// <returns>
    /// 成功時回傳 <see langword="true"/>。
    /// <see langword="true"/> when the result represents success.
    /// </returns>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;
        return _isSuccess;
    }

    /// <summary>
    /// 取得成功時的回傳值,失敗時回傳指定的替代值。
    /// Returns the value on success, or the supplied fallback on failure.
    /// </summary>
    /// <param name="fallback">失敗時使用的替代值。The fallback used on failure.</param>
    /// <returns>回傳值或替代值。The value, or the fallback.</returns>
    public T GetValueOrDefault(T fallback) => _isSuccess ? _value! : fallback;

    /// <summary>
    /// 取得成功時的回傳值,失敗時回傳型別預設值。
    /// Returns the value on success, or the type default on failure.
    /// </summary>
    /// <returns>回傳值或型別預設值。The value, or the type default.</returns>
    public T? GetValueOrDefault() => _value;

    /// <summary>
    /// 依成功或失敗分別執行對應的函式,回傳同一型別的值。
    /// Runs one of two functions depending on the outcome and returns a value of the same type.
    /// </summary>
    /// <typeparam name="TOut">回傳值型別。The return type.</typeparam>
    /// <param name="onSuccess">成功時執行,參數為回傳值。Invoked on success with the value.</param>
    /// <param name="onFailure">失敗時執行,參數為錯誤內容。Invoked on failure with the error.</param>
    /// <returns>被執行的函式所回傳的值。The value returned by whichever function ran.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一函式為 <see langword="null"/> 時擲出。
    /// Thrown when either function is <see langword="null"/>.
    /// </exception>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsFailure ? onFailure(Error) : onSuccess(_value!);
    }

    /// <summary>
    /// 成功時把回傳值轉換成另一種型別;失敗時原樣傳遞錯誤,不執行轉換。
    /// Transforms the value into another type when successful; propagates the error untouched when not.
    /// </summary>
    /// <typeparam name="TOut">轉換後的型別。The transformed type.</typeparam>
    /// <param name="transform">轉換函式。The transform to apply.</param>
    /// <returns>轉換後的結果,或原本的失敗。The transformed result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="transform"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="transform"/> is <see langword="null"/>.
    /// </exception>
    public Result<TOut> Map<TOut>(Func<T, TOut> transform)
    {
        ArgumentNullException.ThrowIfNull(transform);

        return IsFailure ? Result<TOut>.FromError(Error) : Result<TOut>.FromValue(transform(_value!));
    }

    /// <summary>
    /// 成功時接著執行下一個同樣會回傳結果的操作;失敗時原樣傳遞錯誤,不執行後續動作。
    /// Chains the next result-returning operation when successful; propagates the error untouched when not.
    /// </summary>
    /// <typeparam name="TOut">接續操作的回傳值型別。The value type of the chained operation.</typeparam>
    /// <param name="next">要接續執行的操作。The operation to chain.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public Result<TOut> Then<TOut>(Func<T, Result<TOut>> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return IsFailure ? Result<TOut>.FromError(Error) : next(_value!);
    }

    /// <summary>
    /// 成功時檢查回傳值是否滿足條件,不滿足則轉為指定的失敗。
    /// Checks the value against a predicate when successful, turning it into the supplied failure if it does not hold.
    /// </summary>
    /// <param name="predicate">要滿足的條件。The predicate the value must satisfy.</param>
    /// <param name="error">條件不滿足時使用的錯誤。The error used when the predicate does not hold.</param>
    /// <returns>原結果,或條件不滿足時的失敗。The original result, or a failure when the predicate does not hold.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="predicate"/> 或 <paramref name="error"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="predicate"/> or <paramref name="error"/> is <see langword="null"/>.
    /// </exception>
    public Result<T> Ensure(Func<T, bool> predicate, Error error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(error);

        if (IsFailure)
        {
            return this;
        }

        return predicate(_value!) ? this : FromError(error);
    }

    /// <summary>
    /// 捨棄回傳值,轉成沒有回傳值的結果。
    /// Discards the value and converts to a valueless result.
    /// </summary>
    /// <returns>對應的 <see cref="Result"/>。The corresponding <see cref="Result"/>.</returns>
    public Result ToResult() => IsFailure ? Result.Failure(Error) : Result.Success();

    /// <summary>
    /// 把失敗原樣轉發成另一種回傳值型別的失敗結果。
    /// Forwards this failure unchanged as a failed result of a different value type.
    /// </summary>
    /// <typeparam name="TOut">目標回傳值型別。The target value type.</typeparam>
    /// <returns>帶著同一個錯誤的失敗結果。A failed result carrying the same error.</returns>
    /// <remarks>
    /// 用於「內層操作失敗,外層要用不同的型別把同一個錯誤往上傳」的情境:
    /// <code>
    /// var quantity = NormalizeQuantity(raw);
    /// if (quantity.IsFailure)
    /// {
    ///     return quantity.ToFailure&lt;OrderRequest&gt;();
    /// }
    /// </code>
    /// 沒有這個方法就得寫 <c>Result.Failure&lt;OrderRequest&gt;(quantity.Error!)</c> —— 因為
    /// <see cref="Error"/> 是可為 null 的屬性,必須加上空值寬恕運算子,而那個 <c>!</c> 在轉發失敗的
    /// 程式碼裡會出現得非常頻繁,讓「這裡真的檢查過了嗎」變得難以一眼判斷。
    /// This covers the case where an inner operation fails and the outer scope must propagate the same error under
    /// a different type. Without it you would write <c>Result.Failure&lt;OrderRequest&gt;(quantity.Error!)</c>: because
    /// <see cref="Error"/> is nullable, a null-forgiving operator is required, and that <c>!</c> shows up so often in
    /// forwarding code that it stops being a useful signal of "has this really been checked".
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// 在成功的結果上呼叫時擲出 —— 成功的結果沒有可以轉發的錯誤,這代表呼叫端漏了檢查。
    /// Thrown when called on a successful result: there is no error to forward, which means the caller skipped a check.
    /// </exception>
    public Result<TOut> ToFailure<TOut>()
    {
        if (IsSuccess)
        {
            throw new InvalidOperationException(
                "成功的結果沒有可轉發的錯誤。A successful result has no error to forward.");
        }

        return Result<TOut>.FromError(Error);
    }

    /// <summary>
    /// 失敗時擲出 <see cref="InvalidOperationException"/>,成功時回傳值。
    /// Throws an <see cref="InvalidOperationException"/> on failure; returns the value on success.
    /// </summary>
    /// <remarks>
    /// 只在「失敗代表程式缺陷」的地方使用。一般的執行期失敗請用 <see cref="TryGetValue"/> 或
    /// <see cref="Match{TOut}"/> 處理,不要靠例外走控制流。
    /// Use this only where a failure would indicate a defect. Handle ordinary runtime failures with
    /// <see cref="TryGetValue"/> or <see cref="Match{TOut}"/> rather than through exceptions.
    /// </remarks>
    /// <returns>成功時的回傳值。The value produced on success.</returns>
    /// <exception cref="InvalidOperationException">
    /// 結果為失敗時擲出。
    /// Thrown when the result represents a failure.
    /// </exception>
    public T GetValueOrThrow()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error.ToString(), Error.Exception);
        }

        return _value!;
    }

    /// <summary>
    /// 將值隱含轉換為成功的結果,讓 <c>return value;</c> 可以直接寫。
    /// Implicitly converts a value into a successful result so that <c>return value;</c> compiles.
    /// </summary>
    /// <param name="value">回傳值。The value.</param>
    public static implicit operator Result<T>(T value) => FromValue(value);

    /// <summary>
    /// 將錯誤隱含轉換為失敗的結果,讓 <c>return someError;</c> 可以直接寫。
    /// Implicitly converts an error into a failed result so that <c>return someError;</c> compiles.
    /// </summary>
    /// <param name="error">失敗內容。The failure details.</param>
    public static implicit operator Result<T>(Error error) => FromError(error);

    /// <summary>
    /// 比較兩個結果是否相同。
    /// Compares two results for equality.
    /// </summary>
    /// <param name="other">另一個結果。The other result.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when equivalent.</returns>
    /// <remarks>
    /// 比較的是對外可見的 <see cref="Error"/> 而不是內部欄位,理由與 <see cref="Result.Equals(Result)"/> 相同:
    /// <see langword="default"/> 值的內部欄位為 <see langword="null"/>,但對外呈現為
    /// <see cref="Abstractions.Error.Uninitialized"/>。
    /// Comparison uses the externally visible <see cref="Error"/> rather than the backing field, for the same
    /// reason as <see cref="Result.Equals(Result)"/>: a <see langword="default"/> value has a null field but
    /// reports <see cref="Abstractions.Error.Uninitialized"/>.
    /// </remarks>
    public bool Equals(Result<T> other) =>
        _isSuccess == other._isSuccess
        && EqualityComparer<T?>.Default.Equals(_value, other._value)
        && Equals(Error, other.Error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_isSuccess, _value, Error);

    /// <summary>
    /// 判斷兩個結果是否相同。
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when equal.</returns>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個結果是否不同。
    /// Determines whether two results are different.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>不同時回傳 <see langword="true"/>。<see langword="true"/> when different.</returns>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <summary>
    /// 回傳可讀敘述:成功為 <c>Success(值)</c>,失敗為錯誤字串。
    /// Returns a readable description: <c>Success(value)</c>, or the error text on failure.
    /// </summary>
    /// <returns>可讀敘述。A readable description.</returns>
    public override string ToString() => IsFailure ? Error.ToString() : $"Success({_value})";
}

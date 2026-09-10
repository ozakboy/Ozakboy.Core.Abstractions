using System.Diagnostics.CodeAnalysis;

namespace Ozakboy.Core.Abstractions;

/// <summary>
/// 沒有回傳值的操作結果:成功,或帶著一個 <see cref="Abstractions.Error"/> 的失敗。
/// The outcome of an operation that returns no value: either success, or a failure carrying an <see cref="Abstractions.Error"/>.
/// </summary>
/// <remarks>
/// <para>
/// 這是值型別,成功路徑不會產生堆積配置。<see langword="default"/> 值視為失敗,其錯誤為
/// <see cref="Abstractions.Error.Uninitialized"/> —— 這樣未經賦值的結果不會被誤判為成功。
/// This is a value type, so the success path allocates nothing on the heap. A <see langword="default"/> value
/// is treated as a failure carrying <see cref="Abstractions.Error.Uninitialized"/>, so an unassigned result is
/// never mistaken for success.
/// </para>
/// <para>
/// <see cref="IsSuccess"/> 與 <see cref="IsFailure"/> 都標註了可空性契約,因此檢查過之後
/// 編譯器就知道 <see cref="Error"/> 是否為 <see langword="null"/>,不需要額外的 <c>!</c> 運算子。
/// Both <see cref="IsSuccess"/> and <see cref="IsFailure"/> carry nullability contracts, so after a check the
/// compiler knows whether <see cref="Error"/> is <see langword="null"/> without a null-forgiving operator.
/// </para>
/// </remarks>
public readonly struct Result : IEquatable<Result>
{
    private readonly bool _isSuccess;
    private readonly Error? _error;

    private Result(bool isSuccess, Error? error)
    {
        _isSuccess = isSuccess;
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
    /// <returns>成功的結果。A successful result.</returns>
    public static Result Success() => new(true, null);

    /// <summary>
    /// 建立帶回傳值且成功的結果。
    /// Creates a successful result that carries a value.
    /// </summary>
    /// <typeparam name="T">回傳值型別。The value type.</typeparam>
    /// <param name="value">回傳值。The value.</param>
    /// <returns>成功的結果。A successful result.</returns>
    /// <remarks>
    /// 這裡不擋 <see langword="null"/>。對 <c>Result&lt;int?&gt;</c> 這類型別,null 是合法且有意義的成功值;
    /// 泛型在此無法區分兩者,加上約束會連帶犧牲可為 null 的實值型別。
    /// 若 <typeparamref name="T"/> 是參考型別,呼叫端必須自行確保值非 null —— 「成功但值為 null」的結果會讓
    /// 下游在取值後才發生 <see cref="NullReferenceException"/>,而且看起來像是 Result 沒發揮作用。
    /// 要表達「操作成功但沒有資料」,請讓 <typeparamref name="T"/> 本身是可為 null 的型別,讓意圖出現在簽章上。
    /// This does not reject <see langword="null"/>. For a type such as <c>Result&lt;int?&gt;</c>, null is a legitimate
    /// success value, and a generic constraint here would rule out nullable value types along with it.
    /// When <typeparamref name="T"/> is a reference type the caller must ensure the value is non-null: a
    /// "successful null" surfaces as a <see cref="NullReferenceException"/> downstream and looks like the Result
    /// achieved nothing. To express "succeeded with no data", make <typeparamref name="T"/> itself nullable so the
    /// intent shows up in the signature.
    /// </remarks>
    public static Result<T> Success<T>(T value) => Result<T>.FromValue(value);

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
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, error);
    }

    /// <summary>
    /// 建立失敗的結果。
    /// Creates a failed result.
    /// </summary>
    /// <param name="code">錯誤代碼。The error code.</param>
    /// <param name="message">錯誤訊息。The error message.</param>
    /// <param name="category">錯誤分類。The failure category.</param>
    /// <returns>失敗的結果。A failed result.</returns>
    public static Result Failure(string code, string message, ErrorCategory category = ErrorCategory.Unexpected) =>
        new(false, new Error(code, message, category));

    /// <summary>
    /// 建立帶回傳值型別但失敗的結果。
    /// Creates a failed result for an operation that would have returned a value.
    /// </summary>
    /// <typeparam name="T">回傳值型別。The value type.</typeparam>
    /// <param name="error">失敗的內容。The failure details.</param>
    /// <returns>失敗的結果。A failed result.</returns>
    public static Result<T> Failure<T>(Error error) => Result<T>.FromError(error);

    /// <summary>
    /// 依成功或失敗分別執行對應的函式,回傳同一型別的值。
    /// Runs one of two functions depending on the outcome and returns a value of the same type.
    /// </summary>
    /// <typeparam name="TOut">回傳值型別。The return type.</typeparam>
    /// <param name="onSuccess">成功時執行。Invoked on success.</param>
    /// <param name="onFailure">失敗時執行,參數為錯誤內容。Invoked on failure with the error.</param>
    /// <returns>被執行的函式所回傳的值。The value returned by whichever function ran.</returns>
    /// <exception cref="ArgumentNullException">
    /// 任一函式為 <see langword="null"/> 時擲出。
    /// Thrown when either function is <see langword="null"/>.
    /// </exception>
    public TOut Match<TOut>(Func<TOut> onSuccess, Func<Error, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsFailure ? onFailure(Error) : onSuccess();
    }

    /// <summary>
    /// 成功時接著執行下一個操作;失敗時原樣傳遞錯誤,不執行後續動作。
    /// Chains the next operation when successful; propagates the existing error untouched when not.
    /// </summary>
    /// <param name="next">要接續執行的操作。The operation to chain.</param>
    /// <returns>接續操作的結果,或原本的失敗。The chained result, or the original failure.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> 為 <see langword="null"/> 時擲出。
    /// Thrown when <paramref name="next"/> is <see langword="null"/>.
    /// </exception>
    public Result Then(Func<Result> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return _isSuccess ? next() : this;
    }

    /// <summary>
    /// 失敗時擲出 <see cref="InvalidOperationException"/>;成功時不做任何事。
    /// Throws an <see cref="InvalidOperationException"/> when the result is a failure; does nothing on success.
    /// </summary>
    /// <remarks>
    /// 只在「失敗代表程式缺陷」的地方使用,例如啟動階段的不變條件檢查。一般的執行期失敗請用
    /// <see cref="IsFailure"/> 或 <see cref="Match{TOut}"/> 處理,不要靠例外走控制流。
    /// Use this only where a failure would indicate a defect, such as a start-up invariant check. Handle ordinary
    /// runtime failures with <see cref="IsFailure"/> or <see cref="Match{TOut}"/> rather than through exceptions.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// 結果為失敗時擲出。
    /// Thrown when the result represents a failure.
    /// </exception>
    public void ThrowIfFailure()
    {
        if (IsFailure)
        {
            throw new InvalidOperationException(Error.ToString(), Error.Exception);
        }
    }

    /// <summary>
    /// 將錯誤隱含轉換為失敗的結果,讓 <c>return someError;</c> 可以直接寫。
    /// Implicitly converts an error into a failed result so that <c>return someError;</c> compiles.
    /// </summary>
    /// <param name="error">失敗內容。The failure details.</param>
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// 由錯誤建立失敗的結果,是隱含轉換運算子的具名替代方法。
    /// Creates a failed result from an error; the named alternative to the implicit conversion operator.
    /// </summary>
    /// <param name="error">失敗內容。The failure details.</param>
    /// <returns>失敗的結果。A failed result.</returns>
    public static Result FromError(Error error) => Failure(error);

    /// <summary>
    /// 比較兩個結果是否相同。
    /// Compares two results for equality.
    /// </summary>
    /// <param name="other">另一個結果。The other result.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when equivalent.</returns>
    /// <remarks>
    /// 比較的是對外可見的 <see cref="Error"/> 而不是內部欄位。差別出現在 <see langword="default"/> 值上:
    /// 它的內部欄位是 <see langword="null"/>,但對外呈現為 <see cref="Abstractions.Error.Uninitialized"/>,
    /// 若比較內部欄位,兩個外觀完全相同的失敗結果會不相等。
    /// Comparison uses the externally visible <see cref="Error"/> rather than the backing field. The difference
    /// shows up on <see langword="default"/> values: the field is <see langword="null"/> while the property
    /// reports <see cref="Abstractions.Error.Uninitialized"/>, so comparing fields would make two identical-looking
    /// failures unequal.
    /// </remarks>
    public bool Equals(Result other) => _isSuccess == other._isSuccess && Equals(Error, other.Error);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(_isSuccess, Error);

    /// <summary>
    /// 判斷兩個結果是否相同。
    /// Determines whether two results are equal.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>相同時回傳 <see langword="true"/>。<see langword="true"/> when equal.</returns>
    public static bool operator ==(Result left, Result right) => left.Equals(right);

    /// <summary>
    /// 判斷兩個結果是否不同。
    /// Determines whether two results are different.
    /// </summary>
    /// <param name="left">左運算元。The left operand.</param>
    /// <param name="right">右運算元。The right operand.</param>
    /// <returns>不同時回傳 <see langword="true"/>。<see langword="true"/> when different.</returns>
    public static bool operator !=(Result left, Result right) => !left.Equals(right);

    /// <summary>
    /// 回傳可讀敘述:成功為 <c>Success</c>,失敗為錯誤字串。
    /// Returns a readable description: <c>Success</c>, or the error text on failure.
    /// </summary>
    /// <returns>可讀敘述。A readable description.</returns>
    public override string ToString() => IsFailure ? Error.ToString() : "Success";
}

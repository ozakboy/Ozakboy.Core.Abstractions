# Ozakboy.Core.Abstractions

零相依的 .NET 基礎型別,給需要精確金額運算與明確錯誤處理的系統用。

[English](README.md) | 繁體中文

```
dotnet add package Ozakboy.Core.Abstractions
```

需要 .NET 10。除了 BCL 之外沒有任何相依。

---

## 這裡面有什麼

四個型別,各自解決一個實際踩過的問題。

| 型別 | 解決的問題 |
| --- | --- |
| `Result` / `Result<T>` | 預期之內的失敗不該用例外表達 |
| `Money` | 不同幣別的金額被相加,而型別系統擋不住 |
| `Precision` | 十進位步進值對齊,以及送給 API 時的字串序列化 |
| `RetryPolicy` | 重試策略的描述與退避間隔計算 |

原本是為了一套自動交易系統寫的,但沒有任何交易相關的概念混在裡面,任何專案都能用。

---

## Result:把預期的失敗從例外裡拿出來

網路逾時、被對方限流、參數不符合規格 —— 這些是每天都會發生的事,不是異常狀況。用例外表達它們,呼叫端就得用 `try/catch` 寫控制流,而且「忘記處理」會拖到執行期才爆。

```csharp
Result<Order> Submit(OrderRequest request)
{
    if (request.Quantity <= 0)
    {
        return Error.Validation("order.quantity_invalid", "數量必須大於零");
    }

    return new Order(request);   // 隱含轉換,直接 return 值就好
}
```

讀取的一端:

```csharp
var result = Submit(request);

if (result.TryGetValue(out var order))
{
    Console.WriteLine($"已送出 {order.Id}");
}
else
{
    // 編譯器知道這裡的 Error 不會是 null
    Console.WriteLine(result.Error.Message);
}
```

`IsSuccess` 與 `IsFailure` 都標了 `MemberNotNullWhen`,檢查過之後編譯器就知道 `Error` 是不是 null,不需要 `!` 運算子。

串接時失敗會自動短路,中間的委派根本不會被呼叫:

```csharp
var report = LoadAccount(id)
    .Ensure(a => a.IsActive, Error.Conflict("account.inactive", "帳戶已停用"))
    .Map(a => a.Balance)
    .Then(BuildReport);
```

**`default(Result<T>)` 是失敗**,錯誤為 `Error.Uninitialized`。這是刻意的:未經賦值的結果如果被當成成功,問題會潛伏很久。

真正的程式缺陷仍然應該丟例外,`Result` 不是拿來取代例外的。

### 非同步串接

上面那些組合子只吃同步委派,所以一旦有某一步是非同步的,串接就斷了。`*Async` 這組擴充方法同時接受 `Task<Result<T>>` 當接收者與非同步委派當後續,串接就接得下去:

```csharp
var name = await LoadAccountAsync(id)
    .EnsureAsync(a => a.IsActive, Error.Conflict("account.inactive", "帳戶已停用"))
    .ThenAsync(FetchProfileAsync)     // Func<Account, Task<Result<Profile>>>
    .MapAsync(p => p.DisplayName)
    .MatchAsync(n => n, e => e.Code);
```

失敗一樣會短路:第一個失敗之後的委派不會被呼叫。內部每個 `await` 都加了 `ConfigureAwait(false)`。

### 跨越例外邊界

有些簽章塞不進 `Result` —— `DelegatingHandler.SendAsync` 必須回傳 `Task<HttpResponseMessage>`,`BackgroundService.ExecuteAsync` 必須回傳 `Task`。那些地方統一用 `ResultException` 當載具:

```csharp
protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
{
    var result = await _pipeline.SendAsync(request, ct);

    return result.TryGetValue(out var response)
        ? response
        : throw result.Error.ToException();
}
```

```csharp
catch (ResultException ex)
{
    logger.LogWarning("{Code}: {Message}", ex.Error.Code, ex.Error.Message);
    if (ex.Error.IsTransient) { /* ... */ }
}
```

它衍生自 `InvalidOperationException`,而那正是 `ThrowIfFailure()` 與 `GetValueOrThrow()` 擲出的型別 —— 既有的 `catch (InvalidOperationException)` 照常運作,只有想取回 `Error` 的那段程式需要改。

---

## Money:讓幣別變成型別的一部分

交易系統裡同時流著計價幣餘額(USDT)和基礎幣數量(BTC),兩個都是 `decimal`,編譯器不會攔你把它們相加。

```csharp
var balance = new Money(1000m, "USDT");
var position = new Money(0.05m, "BTC");

var wrong = balance + position;   // CurrencyMismatchException
```

同幣別的運算照常:

```csharp
var fee = balance * 0.0004m;
var net = balance - fee;

Console.WriteLine(net);           // 999.6 USDT
```

`default(Money)` 是「未指定幣別的零」,可以跟任何幣別相加,拿來當累加起點很方便:

```csharp
var total = fills.Aggregate(default(Money), (sum, f) => sum + f.Amount);
```

有個刻意設計的不對稱:`Equals` 跨幣別回傳 `false`(不丟例外),但 `CompareTo` 跨幣別**會丟**。相等性比較必須對任何輸入都安全,排序則不允許把不同幣別混在一起比大小。

---

## Precision:步進值對齊與字串序列化

交易所對每個商品都規定價格與數量的最小變動單位,沒對齊的委託直接被拒絕。

```csharp
Precision.FloorToStep(0.123456m, 0.001m);    // 0.123
Precision.CeilingToStep(0.123456m, 0.001m);  // 0.124
Precision.IsAlignedToStep(0.123m, 0.001m);   // true
```

**數量請一律用 `FloorToStep`。** 向上對齊會讓實際部位大於風控算出來的規模,等於在不知不覺中放大曝險。

另一個高頻地雷是序列化。API 用字串收價格,而 `decimal` 直接輸出會帶著尾隨零:

```csharp
(1.2300m).ToString();              // "1.2300"
Precision.ToPlainString(1.2300m);  // "1.23"
Precision.ToPlainString(0.00000001m);  // "0.00000001",不會變成 1E-08
```

送出科學記號或多餘的零,對方通常回一個完全看不出原因的參數錯誤,查起來很花時間。

`TryParsePlain` 與 `ParsePlain` 是它的反向操作,保證往返一致,而且一律走 `InvariantCulture` —— 那正是最容易漏掉、又只會在別的 locale 才爆的那一段:

```csharp
Precision.TryParsePlain("0.00000001", out var qty);   // true
Precision.TryParsePlain("1E-08", out _);              // false,刻意不接受
Precision.ParsePlain("nope");                          // 失敗的 Result<decimal>,分類是 Validation
```

---

## RetryPolicy:只描述策略,不跑迴圈

```csharp
var policy = new RetryPolicy
{
    MaxAttempts = 5,
    BaseDelay = TimeSpan.FromMilliseconds(200),
    MaxDelay = TimeSpan.FromSeconds(30),
    Strategy = BackoffStrategy.Exponential,
    JitterRatio = 0.2,
};

for (var attempt = 1; attempt <= policy.MaxAttempts; attempt++)
{
    var result = await CallAsync();
    if (result.IsSuccess || !policy.ShouldRetry(attempt, result.Error))
    {
        return result;
    }

    await Task.Delay(policy.GetDelay(attempt));
}
```

它不執行重試、不碰 I/O,所以同一份策略可以套在任何傳輸層上,間隔計算也能脫離時間獨立測試 —— `GetDelay(attempt, jitterSample)` 讓你傳入固定的抖動取樣值,結果完全確定。

只有暫時性的錯誤(`Timeout`、`Network`、`RateLimited`、`Unavailable`)才會被 `ShouldRetry` 判定為值得重試。`ErrorCategory.Exhausted` 刻意不在其中:它代表原本做得到、但重試或重連的機會已經用盡,呼叫端該換一個新的物件而不是再試一次。

當「暫時性」不是對的判準時 —— 同樣是 429,帶著 `Retry-After` 的值得重試,IP 被封的那種不值得 —— 設 `RetryPredicate`。它是**完全取代** `IsTransient` 判斷,不是額外加條件,所以「為什麼這個錯誤沒被重試」永遠只需要翻一個地方:

```csharp
var policy = RetryPolicy.Default with
{
    RetryPredicate = e => e.TryGetInt64("retryAfterMs", out var ms) && ms < 5000,
};
```

`MaxAttempts` 仍然一律套用。`RetryPredicate` 不參與相等性比較,因為兩個內容相同的 lambda 永遠不相等,列入比較會讓兩份設定相同的策略被判定為不同。

**非冪等的操作不要重試。** 逾時不代表對方沒收到,盲目重送會產生重複的副作用。那種情況請用 `RetryPolicy.NoRetry`,改以冪等識別碼加事後查詢來確認結果。

---

## 授權

MIT

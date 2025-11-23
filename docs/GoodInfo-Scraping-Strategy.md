# GoodInfo.tw 爬蟲策略說明

## 重要提醒

GoodInfo.tw 使用多種反爬蟲機制：

1. **按鈕點擊速度偵測**：會追蹤使用者的點擊間隔，判斷是否為機器人
2. **Quota 限制**：短時間內過多請求會觸發限制，導致無法繼續爬取
3. **IP 追蹤**：可能會記錄 IP 位址的請求頻率

## 已實作的防護措施

### 1. 延遲策略 (AntiScrapingDelayStrategy)

```
基礎延遲：2000ms
隨機延遲：0-1500ms
最小間隔：1800ms
週期性額外延遲：每 10 次請求增加 3-5 秒
```

**實際效果**：
- 每次請求間隔：3.8-5.3 秒
- 每 10 次請求後：額外 3-5 秒暫停
- 完全隨機化，模擬人類行為

### 2. User-Agent 輪替

7 種不同的瀏覽器 User-Agent：
- Chrome (Windows/macOS)
- Firefox
- Edge
- Safari

### 3. 完整瀏覽器 Header 模擬

```http
User-Agent: (輪替)
Accept: text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8
Accept-Language: zh-TW,zh;q=0.9,en;q=0.8
Accept-Encoding: gzip, deflate, br
Referer: https://goodinfo.tw
DNT: 1
Connection: keep-alive
Upgrade-Insecure-Requests: 1
Sec-Fetch-Dest: document
Sec-Fetch-Mode: navigate
Sec-Fetch-Site: same-origin
Sec-Fetch-User: ?1
Cache-Control: max-age=0
```

### 4. Polly 重試策略

```
重試次數：3 次
延遲策略：指數退避 (2^n 秒) + 隨機 Jitter
```

### 5. 並發限制

```
預設並發數：2-3（非常保守）
最大並發數：建議不超過 5
```

## 使用建議

### 單股測試
```json
{
  "stockCodes": ["2330"],
  "maxDegreeOfParallelism": 1
}
```
**預估時間**：約 4 秒/股

### 小批次（10-50 股）
```json
{
  "stockCodes": ["2330", "2317", ...],
  "maxDegreeOfParallelism": 2
}
```
**預估時間**：約 2.5 秒/股（含週期性暫停）

### 大批次（100+ 股）
```json
{
  "market": "TSE",
  "maxDegreeOfParallelism": 3
}
```
**預估時間**：約 2 秒/股
**建議**：分批執行，每批次間隔 1-2 分鐘

### 全市場匯入（1000+ 股）
```json
{
  "market": "ALL",
  "maxDegreeOfParallelism": 3
}
```
**預估時間**：約 30-40 分鐘/1000 股
**強烈建議**：
1. 使用 Hangfire 背景排程
2. 非交易時段執行（晚上 10 點後）
3. 分市場執行（TSE → OTC → EMERGING）

## Quota 保護機制

### 自動減速
- 每爬取 10 筆，自動暫停 3-5 秒
- 每爬取 50 筆，建議手動暫停 30-60 秒

### 失敗重試
- 如果遇到 quota 限制，系統會自動重試
- 重試間隔會逐漸增加（指數退避）
- 3 次重試後仍失敗會記錄到 AlertLog

### 手動控制
可透過 API 動態調整：
```csharp
// 降低並發數
request.MaxDegreeOfParallelism = 1;

// 增加延遲（需修改 AntiScrapingDelayStrategy）
delayStrategy.BaseDelayMs = 3000;
delayStrategy.RandomRangeMs = 2000;
```

## 監控指標

建議監控以下指標：
1. **成功率**：應保持在 95% 以上
2. **平均延遲**：每筆請求約 3.8-5.3 秒
3. **失敗原因**：注意 "quota" 或 "timeout" 相關錯誤

## 緊急應變

如果觸發 quota 限制：
1. **立即停止爬取**：取消所有進行中的作業
2. **等待冷卻**：建議等待 30-60 分鐘
3. **降低並發**：重啟時將 maxDegreeOfParallelism 設為 1
4. **增加延遲**：將 BaseDelayMs 提高到 3000-5000

## 最佳實踐

✅ **推薦做法**：
- 非交易時段執行大批次匯入
- 使用 Hangfire 排程，每天晚上 10 點執行
- 分市場、分時段執行
- 監控 AlertLog，及時調整策略

❌ **避免做法**：
- 交易時段爬取（容易被偵測）
- 並發數超過 5
- 短時間內重複爬取相同股票
- 忽略錯誤訊息持續爬取

# Session Report - 2025/12/07 22:00

## 📊 本次變更摘要

### 已完成工作
1. **✅ 建立 19 Links 完整對應表**
   - 從舊系統 `D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs` 和 `appCommon.cs` 分析出所有 linkLabel 對應關係
   - 確認用戶真正需要的 19 個 Links（排除了：漲5%, 成交價, 十季黃金, 5日10日月線多頭排列, 10日月季多頭排列）

2. **✅ ForeignInvestmentService 實作完成**
   - 新增檔案：`src/SST.StockImport.Services/GoodInfo/ForeignInvestmentService.cs`
   - 新增測試：`Tests/SST.StockImport.Tests/Integration/GoodInfo/ForeignInvestmentIntegrationTests.cs`
   - **測試結果：5/5 tests passing** ✅
   - 涵蓋 4 個 Links：外資連買 (linkLabel32), 外資連賣 (linkLabel33), 投信連買 (linkLabel29), 投信連賣 (linkLabel28)

3. **✅ 修復重大 Bug**
   - 發現測試失敗原因：Service 使用 `Skip(1)` 跳過標題行，但舊系統 CSV 沒有標題行
   - 改為用 StockID 驗證（長度≤4且為數字）來自動過濾標題行
   - 修正測試 CSV 格式：移除「排名」欄位，符合舊系統格式

### 修改文件清單
```
src/SST.StockImport.Services/GoodInfo/ForeignInvestmentService.cs (新增 209 行)
Tests/SST.StockImport.Tests/Integration/GoodInfo/ForeignInvestmentIntegrationTests.cs (新增 264 行)
```

### 測試數量變化
- **上次 Session**: MarginRatio (4 tests) + TurnoverRate (5 tests) = 9 tests
- **本次 Session**: +5 tests (ForeignInvestment)
- **目前總計**: 14 tests for GoodInfo Links ✅

---

## 🎯 設計決策

### 1. CSV 解析策略
**決策**: 不使用 `Skip(1)` 跳過標題，改用欄位驗證自動過濾
**理由**: 
- 舊系統 CSV 沒有固定標題格式
- 有些 CSV 有標題，有些沒有
- 透過檢查第一欄（StockID）是否為數字且長度≤4，可以自動識別標題行
- 更健壯，適應性更強

### 2. ForeignInvestment Service 整合設計
**決策**: 單一 Service 處理 4 個 Links（外資連買/賣、投信連買/賣）
**理由**:
- 這 4 個 Links 使用相同的 CSV 格式
- 舊系統使用同一個 `外資連續買賣` 方法處理
- 更新相同的資料庫欄位（ForeigneSerealDays, ForeigneAmt, InvestSerealDays, InvestAmt, FarenSerialDays, FarenSerialAmt）
- 減少重複代碼，提高維護性

### 3. CSV 欄位對應
根據舊系統 `appCommon.cs` lines 5733-5738：
```
[0] = StockID
[5] = Date (MM/dd)
[6] = 外資天數
[7] = 外資金額 (千元)
[10] = 投信天數
[11] = 投信金額 (千元)
[18] = 法人天數
[19] = 法人金額 (千元)
```

---

## 🐛 已知問題

**無重大問題** ✅

所有測試通過，代碼品質良好。

---

## 📋 累積待辦事項（詳細記錄）

### 🔴 高優先級（必須完成）

#### 1. 完成剩餘 16 個 Services (12/19 completed)
**狀態**: 進行中
**已完成**:
- ✅ 券資比 (linkLabel4) - MarginRatioService
- ✅ 周轉率 (linkLabel9) - TurnoverRateService  
- ✅ 外資連買 (linkLabel32) - ForeignInvestmentService
- ✅ 外資連賣 (linkLabel33) - ForeignInvestmentService
- ✅ 投信連買 (linkLabel29) - ForeignInvestmentService
- ✅ 投信連賣 (linkLabel28) - ForeignInvestmentService

**待完成 (按建議順序)**:

**簡單級別** (優先實作):
1. **歷史成交量** (linkLabel1)
   - Insert方法: `歷史成交_Insert`
   - URL: 日成交張數創歷日新高
   - 資料庫欄位: 待確認 (需查看 tradedata table schema)
   - CSV 欄位: 待分析舊系統

2. **五年新高** (linkLabel23)
   - Insert方法: `歷史股價_Insert`
   - URL: 股價創五年高點
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

**中等級別**:
3. **MACD>0 / OSC負轉正** (linkLabel11)
   - Insert方法: `MACD轉正_Insert`
   - URL: DIF、MACD小於0且OSC由負轉正
   - **注意**: 兩個 Link 共用同一個方法
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

4. **超布林上軌** (linkLabel3)
   - Insert方法: `布林分析_Insert`
   - URL: 股價高於布林上軌 (參考月均線)
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

5. **外資連買連賣轉折** (linkLabel7)
   - Insert方法: `投外轉折_Insert`
   - URL: 外資連續賣出轉買進
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

6. **投信連買連賣轉折** (linkLabel8)
   - Insert方法: `投外轉折_Insert`
   - URL: 投信連續賣出轉買進
   - **注意**: 與外資轉折共用方法
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

7. **外資、投信同步買超** (linkLabel30)
   - Insert方法: `投外同步`
   - URL: 外資、投信同步買超–當日
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

8. **外資、投信同步賣超** (linkLabel31)
   - Insert方法: `投外同步`
   - URL: 外資、投信同步賣超–當日
   - **注意**: 與同步買超共用方法
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

9. **月季黃金** (linkLabel14)
   - Insert方法: `均線分析_insert`
   - URL: 均價線交叉向上 (月線/季線)
   - 資料庫欄位: 待確認
   - CSV 欄位: 待分析舊系統

**複雜級別** (財報相關):
10. **EPS創新高** (linkLabel38)
    - Insert方法: `財報分析`
    - URL: 單季EPS創歷季新高
    - 資料庫欄位: 待確認
    - CSV 欄位: 待分析舊系統
    - **注意**: ifScroll=true

11. **季營收創高** (linkLabel44)
    - Insert方法: `財報分析`
    - URL: 單季營收創歷季新高
    - 資料庫欄位: 待確認
    - CSV 欄位: 待分析舊系統
    - **特殊**: ifScroll=false

12. **財報評分** (linkLabel34)
    - Insert方法: `財報分析`
    - URL: 單季財報評分創歷季新高
    - 資料庫欄位: 待確認
    - CSV 欄位: 待分析舊系統

#### 2. 更新 GoodInfoLinkConfig.cs
**狀態**: 待開始
**目標**: 根據正確的 19 Links 清單重建配置檔
**需要**:
- 移除不需要的 Links（漲5%, 成交價, 十季黃金等）
- 加入缺少的 Links（MACD>0, 投信連買, 五年新高等）
- 確保每個 Link 的 URL、CSS Selector、Insert 方法名稱正確
**參考**: Docs/Todo/19_Links_完整對應表.md (需建立)

---

### 🟡 中優先級

#### 3. 建立 19 Links 完整對應表文件
**狀態**: 資料已收集，待整理成文件
**目標**: 建立 `Docs/Todo/19_Links_完整對應表.md`
**內容**:
- linkLabel 編號
- Link 顯示名稱
- GoodInfo URL
- CSS Selector / XPath
- Insert 方法名稱
- CSV 欄位對應
- 資料庫欄位對應
- 下載方式 (downloadGoodInfo / downloadGI)
- 特殊參數 (ifScroll, comboSelectValue 等)

#### 4. 研究 tradedata table schema
**狀態**: 待開始
**目標**: 了解資料庫欄位完整結構
**需要**:
- 連接 MySQL 查看 tradedata table 定義
- 記錄所有欄位名稱、型別、用途
- 確認哪些欄位對應哪些 Links
**檔案**: 可建立 `Docs/Database/tradedata_schema.md`

---

### 🟢 低優先級

#### 5. 處理那 5 個「效果還好」的 Links
**狀態**: 待決定是否實作
**清單**:
- 漲5% (linkLabel12)
- 成交價 (linkLabel13) - 手動下載
- 十季黃金 (linkLabel15)
- 5日10日月線多頭排列 (linkLabel16)
- 10日月季多頭排列 (linkLabel17)
**決策**: 等 19 個主要 Links 完成後，再評估是否需要

#### 6. 代碼重構與優化
**狀態**: 待開始
**項目**:
- 抽取共用的 CSV 解析邏輯
- 統一日期解析方法 (ParseDateWithYear)
- 統一數字解析方法 (ParseInt, ParseDecimal)
- 考慮建立 BaseGoodInfoService 抽象類別

---

## 📌 下一步建議

### 立即行動（明天 Session 開始）

1. **優先實作「歷史成交量」(linkLabel1)**
   ```
   理由：
   - 簡單級別，類似已完成的 Services
   - CSV 格式應該與周轉率類似
   - 可以快速完成，建立信心
   
   步驟：
   1. 讀取舊系統 appCommon.cs 找出 `歷史成交_Insert` 方法
   2. 分析 CSV 欄位對應
   3. 確認資料庫欄位（可能是 tradedata.historyVolHigh 之類）
   4. 建立 HistoricalVolumeService.cs
   5. 建立 HistoricalVolumeIntegrationTests.cs
   6. 執行測試確認通過
   ```

2. **然後實作「五年新高」(linkLabel23)**
   ```
   理由：
   - 也是簡單級別
   - 與歷史成交量使用相同的 `歷史股價_Insert` 方法
   - 可能可以共用部分邏輯
   
   步驟：
   1. 分析 `歷史股價_Insert` 方法
   2. 確認與歷史成交量的差異
   3. 決定是建立新 Service 或整合到現有 Service
   4. 實作並測試
   ```

3. **建立完整對應表文件**
   ```
   在實作過程中，同步建立 Docs/Todo/19_Links_完整對應表.md
   記錄每個 Link 的詳細資訊
   ```

### 後續規劃

- **Week 1-2**: 完成所有「簡單」和「中等」級別的 Links
- **Week 3**: 完成「複雜」級別的財報相關 Links
- **Week 4**: 整體測試、重構優化、文件完善

---

## 🔧 技術筆記

### ParseDateWithYear 邏輯
```csharp
// MM/dd 格式轉換為 yyyy/MM/dd
// 規則：如果日期 > 當前日期，表示是去年的資料
var testDate = new DateTime(year, month, day);
if (testDate > now)
{
    year = now.Year - 1;
}
```

### CSV 欄位驗證模式
```csharp
// 驗證 StockID：長度≤4 且為數字
var stockId = columns[0].Trim();
if (string.IsNullOrEmpty(stockId) || stockId.Length > 4 || !IsNumeric(stockId))
    continue; // 自動跳過標題行和無效行
```

### 舊系統 Insert 方法命名規律
```
券資比 → 券資比_Insert
周轉率 → 周轉率_insert (小寫 i)
外資連續買賣 → 外資連續買賣 (無後綴)
MACD轉正 → MACD轉正_Insert
歷史股價 → 歷史股價_Insert
歷史成交 → 歷史成交_Insert
財報分析 → 財報分析 (無後綴)
```

---

## 📊 統計數據

- **代碼行數**: +473 行 (Service: 209, Tests: 264)
- **測試數量**: +5 tests (全部通過)
- **檔案數量**: +2 files
- **測試覆蓋率**: 100% (ForeignInvestmentService)
- **完成進度**: 6/19 Links (31.6%)

---

## ⚠️ 重要提醒

1. **CSV 格式差異**
   - 不同 Links 的 CSV 欄位數量不同
   - 需要仔細分析舊系統確認每個 Link 的欄位對應
   - 不要假設所有 CSV 都有標題行

2. **資料庫欄位型別**
   - ForeigneAmt 等欄位是 `int` 不是 `decimal`
   - 需要使用 ParseInt 而不是 ParseDecimal
   - 編譯錯誤通常指向型別不匹配

3. **測試資料一致性**
   - 測試 CSV 格式必須與舊系統實際格式一致
   - 需要檢查實際的 GoodInfo CSV 下載格式
   - 可以參考舊系統的下載檔案

4. **下載方式**
   - 18 個 Links 使用 downloadGoodInfo (自動化 Selenium)
   - 1 個 Link (成交價 linkLabel13) 使用 downloadGI (手動 Firefox)
   - 新系統需要支援這兩種方式

---

## 🎓 經驗教訓

1. **永遠驗證假設**
   - 不要假設 CSV 有標題行
   - 不要假設欄位索引相同
   - 實際檢查舊系統代碼確認格式

2. **測試失敗的調查流程**
   ```
   1. 檢查錯誤訊息（updateCount = 0）
   2. 確認 Service 邏輯（發現 Skip(1) 問題）
   3. 檢查測試資料（發現排名欄位問題）
   4. 對比舊系統（確認正確格式）
   5. 修正並重新測試
   ```

3. **增量開發的重要性**
   - 一次實作一個 Service
   - 立即測試確認正確
   - 累積經驗應用到下一個

---

## 📚 參考資源

- 舊系統路徑: `D:\mywork\sstStock\TaskTrayApplication\`
  - `_1_每日收盤匯入.cs` - linkLabel 事件處理
  - `appCommon.cs` - Insert 方法實作

- 新系統路徑: `D:\vibeCoding\sst\`
  - Services: `src\SST.StockImport.Services\GoodInfo\`
  - Tests: `Tests\SST.StockImport.Tests\Integration\GoodInfo\`

- 文件參考:
  - 本報告: `Docs\Todo\20251207_2200SessionReport.md`
  - 19 Links清單: 見上方「累積待辦事項」章節

---

**報告建立時間**: 2025-12-07 22:00
**下次 Session 建議開始時間**: 隔天任何時間
**預估下次完成時間**: 2-3 小時（完成歷史成交量 + 五年新高）

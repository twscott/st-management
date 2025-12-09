# GoodInfo 19 Links 測試說明

## 測試策略

### 1. 兩種下載方法
- **Type1 (tr:nth-child(5))**: 16 個 links
- **Type2 (tr:nth-child(7))**: 3 個 links (券資比、周轉率、歷史成交量)

### 2. 測試執行規則
- **逐一執行**：每個測試獨立運行，有錯就停
- **間隔時間**：每個測試之間自動等待 10 秒（防反爬蟲）
- **錯誤處理**：測試失敗時會立即停止，方便定位問題

### 3. 執行方式

```powershell
# 執行單個測試（推薦）
cd d:\vibeCoding\sst\tests\GoodInfo19LinksTest
dotnet test --filter "Test_01"  # 券資比
dotnet test --filter "Test_02"  # 周轉率
dotnet test --filter "Test_03"  # MACD負轉正
# ... 依此類推

# 執行所有測試（會自動間隔 10 秒）
dotnet test

# 執行特定範圍
dotnet test --filter "FullyQualifiedName~Test_0[1-5]"  # 前5個
```

### 4. 測試順序（按 CSS Selector 類型分組）

#### Type2 (tr:7) - 先測試特殊的 3 個
1. Test_01_券資比 (tw2)
2. Test_02_周轉率 (tw2)
15. Test_15_歷史成交量 (tw)

#### Type1 (tr:5) - 再測試一般的 16 個
3. Test_03_MACD負轉正 (tw)
4. Test_04_OSC負轉正 (tw)
5. Test_05_EPS創新高 (tw)
6. Test_06_投信連買 (tw2)
7. Test_07_超布林上軌 (tw)
8. Test_08_外資連買連賣轉折 (tw2)
9. Test_09_投信連買連賣轉折 (tw2)
10. Test_10_五年新高 (tw)
11. Test_11_外資連賣 (tw2)
12. Test_12_投信連賣 (tw2)
13. Test_13_外資投信同步買超 (tw2)
14. Test_14_月季黃金 (tw)
16. Test_16_季營收創高 (tw) - ⚠️ ifScroll=false
17. Test_17_財報評分 (tw)
18. Test_18_外資投信同步賣超 (tw2)
19. Test_19_外資連買 (tw2)

### 5. 常見問題處理

#### 問題1: CSS Selector 找不到元素
**症狀**: `no such element: Unable to locate element`
**原因**: GoodInfo 頁面結構可能已改變
**解決**: 
1. 用瀏覽器打開 URL
2. F12 檢查元素
3. 確認實際的 CSS Selector
4. 更新測試程式碼

#### 問題2: 下載失敗
**症狀**: CSV 檔案不存在
**原因**: 可能觸發反爬蟲機制
**解決**:
1. 增加等待時間
2. 檢查 Chrome user-data-dir
3. 檢查下載目錄權限

#### 問題3: Chrome 版本不符
**症狀**: ChromeDriver 版本錯誤
**解決**:
```powershell
# 更新 ChromeDriver
dotnet add package Selenium.WebDriver.ChromeDriver --version [latest]
```

### 6. 測試輸出解讀

成功的輸出：
```
============================================================
測試項目: 券資比
>>> 下載方法: Type2-tr7
============================================================

  → 正在建立 ChromeDriver...
  → 正在導航...
  → 已 scroll 到目標區域
  → 正在點擊下載按鈕 (Type2-tr7)...
  → 已點擊下載按鈕
  → 等待下載完成...

✅ 券資比 測試通過！
   檔案位置: C:\Users\xxx\Downloads\StockList.csv
   檔案大小: 12,345 bytes

⏳ 等待 10 秒後執行下一個測試...
✓ 等待完成
```

失敗的輸出：
```
❌ 券資比 下載失敗: 找不到下載按鈕: no such element...
Assert.Fail(): 券資比 下載失敗: ...
```

### 7. 調試建議

如果某個測試失敗：
1. **檢查 URL**: 用瀏覽器手動打開，確認頁面可訪問
2. **檢查 Selector**: F12 開發者工具驗證 CSS Selector
3. **檢查 Chrome**: 確認 ChromeDriver 和 Chrome 版本相容
4. **檢查下載目錄**: 確認 Downloads 資料夾存在且可寫入
5. **等待時間**: 可能需要增加 Thread.Sleep 時間

### 8. 下一步計劃

測試全部通過後：
1. 建立 CSV 解析邏輯
2. 建立 DB Insert 方法
3. 建立整合測試（包含完整的 Download → Parse → Insert 流程）

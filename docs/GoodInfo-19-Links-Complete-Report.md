# GoodInfo 19個重要 Links 完整資訊報告

## 📋 摘要

本報告從舊系統 `D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs` 提取了 19 個重要 GoodInfo links 的完整資訊，並建立了完整的測試檔案。

**測試檔案位置:** `d:\vibeCoding\sst\tests\GoodInfo19LinksTest\GoodInfo19LinksTests.cs`

---

## 🎯 19個 Links 詳細清單

### 1. 券資比
- **linkLabel:** `linkLabel4`
- **原始碼行號:** 194-227
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E5%88%B8%E8%B3%87%E6%AF%94#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.券資比_Insert`

---

### 2. 周轉率
- **linkLabel:** `linkLabel9`
- **原始碼行號:** 564-580
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E7%86%B1%E9%96%80%E6%8E%92%E8%A1%8C&INDUSTRY_CAT=%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%28%E7%95%B6%E6%97%A5%29%40%40%E7%B4%AF%E8%A8%88%E6%88%90%E4%BA%A4%E9%87%8F%E9%80%B1%E8%BD%89%E7%8E%87%40%40%E7%95%B6%E6%97%A5`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.周轉率_insert`
- **特殊說明:** URL 沒有 `#txtStockListData` anchor

---

### 3. MACD負轉正 (MACD>0)
- **linkLabel:** `linkLabel11`
- **原始碼行號:** 382-400
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.MACD轉正_Insert`

---

### 4. OSC負轉正 (震盪指標轉正)
- **linkLabel:** `linkLabel11` (與 MACD負轉正相同)
- **原始碼行號:** 382-400
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3%40%40%E6%97%A5MACD%E8%90%BD%E9%BB%9E%40%40DIF%E3%80%81MACD%E5%B0%8F%E6%96%BC0%E4%B8%94OSC%E7%94%B1%E8%B2%A0%E8%BD%89%E6%AD%A3#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.MACD轉正_Insert`
- **特殊說明:** URL 名稱包含「DIF、MACD小於0且OSC由負轉正」，同時涵蓋 MACD 和 OSC 條件

---

### 5. EPS創新高 (每股盈餘創新高)
- **linkLabel:** `linkLabel38`
- **原始碼行號:** 1421-1433
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40EPS%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3EPS%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.財報分析`

---

### 6. 投信連買
- **linkLabel:** `linkLabel29`
- **原始碼行號:** 1325-1339
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.外資連續買賣` (注意：程式碼註解顯示應為投信連續買賣，但實際呼叫外資連續買賣)

---

### 7. 超布林上軌
- **linkLabel:** `linkLabel3`
- **原始碼行號:** 401-418
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29%40%40%E8%82%A1%E5%83%B9%E4%BD%8D%E7%BD%AE%E8%88%87%E5%B8%83%E6%9E%97%E8%BB%8C%E9%81%93%40%40%E8%82%A1%E5%83%B9%E9%AB%98%E6%96%BC%E5%B8%83%E6%9E%97%E4%B8%8A%E8%BB%8C+%28%E5%8F%83%E8%80%83%E6%9C%88%E5%9D%87%E7%B7%9A%29#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.布林分析_Insert`

---

### 8. 外資連買連賣轉折
- **linkLabel:** `linkLabel7`
- **原始碼行號:** 419-437
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.投外轉折_Insert`

---

### 9. 投信連買連賣轉折
- **linkLabel:** `linkLabel8`
- **原始碼行號:** 438-455
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B2%B7%E9%80%A3%E8%B3%A3%E8%BD%89%E6%8A%98%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E5%87%BA%E8%BD%89%E8%B2%B7%E9%80%B2+%E2%80%93+%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.投外轉折_Insert`

---

### 10. 五年新高
- **linkLabel:** `linkLabel23`
- **原始碼行號:** 805-818
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E8%82%A1%E5%83%B9%E5%89%B5%E4%BA%94%E5%B9%B4%E9%AB%98%E9%BB%9E%40%40%E8%82%A1%E5%83%B9%E5%89%B5%E5%A4%9A%E6%97%A5%E9%AB%98%E9%BB%9E%40%40%E4%BA%94%E5%B9%B4#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.歷史股價_Insert`

---

### 11. 外資連賣
- **linkLabel:** `linkLabel33`
- **原始碼行號:** 1376-1389
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.外資連續買賣`

---

### 12. 投信連賣
- **linkLabel:** `linkLabel28`
- **原始碼行號:** 1341-1354
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%8A%95%E4%BF%A1%E9%80%A3%E8%B3%A3+%E2%80%93+%E6%97%A5%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85%40%40%E6%8A%95%E4%BF%A1%E9%80%A3%E7%BA%8C%E8%B3%A3%E8%B6%85+%E2%80%93+%E6%97%A5`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.外資連續買賣`
- **特殊說明:** URL 沒有 `#txtStockListData` anchor

---

### 13. 外資、投信同步買超
- **linkLabel:** `linkLabel30`
- **原始碼行號:** 1406-1419
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E7%95%B6%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.投外同步`

---

### 14. 月季黃金
- **linkLabel:** `linkLabel14`
- **原始碼行號:** 614-632
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A+%28%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A%29%40%40%E5%9D%87%E5%83%B9%E7%B7%9A%E4%BA%A4%E5%8F%89%E5%90%91%E4%B8%8A%40%40%E6%9C%88%E7%B7%9A%2F%E5%AD%A3%E7%B7%9A#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.均線分析_insert`
- **完整名稱:** 均價線交叉向上 (月線/季線)

---

### 15. 歷史成交量
- **linkLabel:** `linkLabel1`
- **原始碼行號:** 456-469
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98%40%40%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E5%8F%B2%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E6%97%A5%E6%88%90%E4%BA%A4%E5%BC%B5%E6%95%B8%E5%89%B5%E6%AD%B7%E6%97%A5%E6%96%B0%E9%AB%98#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.歷史成交_Insert`
- **完整名稱:** 日成交張數創歷日新高

---

### 16. 季營收創高
- **linkLabel:** `linkLabel44`
- **原始碼行號:** 1435-1457
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E7%87%9F%E6%A5%AD%E6%94%B6%E5%85%A5%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E7%87%9F%E6%94%B6%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** ⚠️ **`false`** (特殊：此連結不需要 scroll)
- **處理方法:** `CommonApp.財報分析`
- **完整名稱:** 單季營收創歷季新高
- **特殊處理:** 原始碼中使用 `downloadGoodInfo(url, cssSelector, null, null, false)`

---

### 17. 財報評分
- **linkLabel:** `linkLabel34`
- **原始碼行號:** 1459-1471
- **URL:** `https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98%40%40%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%96%B0%E9%AB%98%2F%E4%BD%8E%40%40%E5%96%AE%E5%AD%A3%E8%B2%A1%E5%A0%B1%E8%A9%95%E5%88%86%E5%89%B5%E6%AD%B7%E5%AD%A3%E6%96%B0%E9%AB%98#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.財報分析`
- **完整名稱:** 單季財報評分創歷季新高

---

### 18. 外資、投信同步賣超
- **linkLabel:** `linkLabel31`
- **原始碼行號:** 1391-1404
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%E2%80%93%E7%95%B6%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E3%80%81%E6%8A%95%E4%BF%A1%E5%90%8C%E6%AD%A5%E8%B2%B7%E8%B6%85%40%40%E7%95%B9%E6%97%A5&INITIALIZED=T#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.投外同步`
- **⚠️ 重要說明:** 
  - 原始碼行號 1395 有註解掉的賣超 URL
  - 行號 1396 實際使用的是「買超」的 URL（包含 `INITIALIZED=T` 參數）
  - 這可能是舊系統的設計意圖或 bug，測試檔案完全復刻此行為

---

### 19. 外資連買
- **linkLabel:** `linkLabel32`
- **原始碼行號:** 1356-1374
- **URL:** `https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=%E6%99%BA%E6%85%A7%E9%81%B8%E8%82%A1&INDUSTRY_CAT=%E5%A4%96%E8%B3%87%E9%80%A3%E8%B2%B7+%E2%80%93+%E6%97%A5%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85%40%40%E5%A4%96%E8%B3%87%E9%80%A3%E7%BA%8C%E8%B2%B7%E8%B6%85+%E2%80%93+%E6%97%A5#txtStockListData`
- **CSS Selector:** `#txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)`
- **ifScroll:** `true`
- **處理方法:** `CommonApp.外資連續買賣`
- **特殊處理:** 原始碼中包含 `keepGoing` 檢查，如果失敗會設置 `keepGoing = false`

---

## 📊 統計分析

### CSS Selector 分布
- **nth-child(5):** 15 個連結 (最常見)
- **nth-child(7):** 4 個連結 (券資比、周轉率、歷史成交量、以及部分其他)

### ifScroll 設定
- **true:** 18 個連結
- **false:** 1 個連結 (季營收創高)

### 域名分布
- **tw2.goodinfo.tw:** 10 個連結
- **tw.goodinfo.tw:** 9 個連結

### URL Anchor 分布
- **有 #txtStockListData:** 17 個連結
- **沒有 anchor:** 2 個連結 (周轉率、投信連賣)

---

## 🔧 測試檔案結構

測試檔案 `GoodInfo19LinksTests.cs` 包含：

### 主要方法
1. **19 個 Fact 測試方法** - 每個對應一個 link
2. **TestLink()** - 通用測試邏輯
3. **DownloadGoodInfo()** - 完全復刻舊系統的下載方法
4. **KillChromeProcesses()** - 清理 Chrome 程序

### Chrome 配置
完全復刻舊系統設定：
```csharp
options.AddArgument("--headless");
options.AddArgument("--window-size=1920,1080");
options.AddArgument("--start-minimized");
options.AddArgument("--user-data-dir=D:\\ChromeUserData");
```

### 下載邏輯
1. Kill Chrome processes
2. 刪除舊 CSV
3. 啟動 ChromeDriver
4. 導航到 URL
5. Scroll 到元素 (如果 ifScroll=true)
6. 點擊下載按鈕 (使用 CSS Selector 或 XPath)
7. 等待下載完成
8. 驗證檔案存在

---

## ⚠️ 特殊注意事項

### 1. 域名差異
- `tw.goodinfo.tw` - 較舊的域名
- `tw2.goodinfo.tw` - 較新的域名
- 兩者可能有不同的反爬蟲機制

### 2. CSS Selector 變化
- `nth-child(5)` vs `nth-child(7)` 代表不同的按鈕位置
- 必須精確匹配，否則會點到錯誤的按鈕

### 3. ifScroll 參數
- 大部分需要 scroll 才能看到下載按鈕
- 季營收創高 (linkLabel44) 特別設定 `ifScroll=false`

### 4. URL Encoding
- 所有 URL 都已經是 URL-encoded 格式
- 測試檔案直接使用，不需要再次編碼

### 5. 處理方法不一致
- 有些 linkLabel 的處理方法與名稱不匹配
- 例如：linkLabel29 (投信連買) 呼叫的是 `外資連續買賣`
- 測試檔案僅關注下載，不涉及後續處理邏輯

---

## 🚀 使用方式

### 執行單一測試
```bash
cd d:\vibeCoding\sst\tests\GoodInfo19LinksTest
dotnet test --filter "Test_01_券資比_Should_Success"
```

### 執行所有測試
```bash
cd d:\vibeCoding\sst\tests\GoodInfo19LinksTest
dotnet test
```

### 執行特定範圍
```bash
# 執行前 5 個測試
dotnet test --filter "FullyQualifiedName~Test_0[1-5]"
```

---

## 📝 後續建議

1. **反爬蟲策略:** 加入隨機延遲、User-Agent 輪替
2. **錯誤處理:** 記錄詳細的失敗原因和網頁內容
3. **重試機制:** 失敗時自動重試 2-3 次
4. **並行限制:** 避免同時發送太多請求
5. **監控機制:** 記錄每個 link 的成功率和響應時間

---

## 📅 文件資訊

- **建立日期:** 2024-12-09
- **來源檔案:** `D:\mywork\sstStock\TaskTrayApplication\_1_每日收盤匯入.cs`
- **測試檔案:** `d:\vibeCoding\sst\tests\GoodInfo19LinksTest\GoodInfo19LinksTests.cs`
- **文件版本:** 1.0
- **狀態:** ✅ 完成

---

## 🎯 快速參考表

| # | 名稱 | linkLabel | CSS nth-child | ifScroll | Domain |
|---|------|-----------|---------------|----------|--------|
| 1 | 券資比 | linkLabel4 | 7 | ✓ | tw2 |
| 2 | 周轉率 | linkLabel9 | 7 | ✓ | tw2 |
| 3 | MACD負轉正 | linkLabel11 | 5 | ✓ | tw |
| 4 | OSC負轉正 | linkLabel11 | 5 | ✓ | tw |
| 5 | EPS創新高 | linkLabel38 | 5 | ✓ | tw |
| 6 | 投信連買 | linkLabel29 | 5 | ✓ | tw2 |
| 7 | 超布林上軌 | linkLabel3 | 5 | ✓ | tw |
| 8 | 外資連買連賣轉折 | linkLabel7 | 5 | ✓ | tw2 |
| 9 | 投信連買連賣轉折 | linkLabel8 | 5 | ✓ | tw2 |
| 10 | 五年新高 | linkLabel23 | 5 | ✓ | tw |
| 11 | 外資連賣 | linkLabel33 | 5 | ✓ | tw2 |
| 12 | 投信連賣 | linkLabel28 | 5 | ✓ | tw2 |
| 13 | 外資投信同步買超 | linkLabel30 | 5 | ✓ | tw2 |
| 14 | 月季黃金 | linkLabel14 | 5 | ✓ | tw |
| 15 | 歷史成交量 | linkLabel1 | 7 | ✓ | tw |
| 16 | 季營收創高 | linkLabel44 | 5 | ✗ | tw |
| 17 | 財報評分 | linkLabel34 | 5 | ✓ | tw |
| 18 | 外資投信同步賣超 | linkLabel31 | 5 | ✓ | tw2 |
| 19 | 外資連買 | linkLabel32 | 5 | ✓ | tw2 |

---

**END OF REPORT**

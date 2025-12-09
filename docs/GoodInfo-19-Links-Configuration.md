# GoodInfo 19 Links 完整配置

基於舊系統 `_1_每日收盤匯入.cs` 的完整分析

## Links 列表

| # | 名稱 | LinkLabel | 域名 | CSS Selector | Insert 方法 |
|---|------|-----------|------|--------------|------------|
| 1 | 券資比 | linkLabel4 | tw2 | tr:nth-child(7) | 券資比_Insert |
| 2 | 周轉率 | linkLabel9 | tw2 | tr:nth-child(7) | 周轉率_insert |
| 3 | MACD負轉正 | linkLabel11 | tw | tr:nth-child(5) | MACD轉正_Insert |
| 4 | 超布林上軌 | linkLabel3 | tw | tr:nth-child(5) | 布林分析_Insert |
| 5 | 外資連買連賣轉折 | linkLabel7 | tw2 | tr:nth-child(5) | 投外轉折_Insert |
| 6 | 投信連買連賣轉折 | linkLabel8 | tw2 | tr:nth-child(5) | 投外轉折_Insert |
| 7 | 歷史成交量 | linkLabel1 | tw | tr:nth-child(7) | 歷史成交_Insert |
| 8 | 五年新高 | linkLabel23 | tw | tr:nth-child(5) | 歷史股價_Insert |
| 9 | 投信連買 | linkLabel29 | tw2 | tr:nth-child(5) | 外資連續買賣 |
| 10 | 投信連賣 | linkLabel28 | tw2 | tr:nth-child(5) | 外資連續買賣 |
| 11 | 外資連買 | linkLabel32 | tw2 | tr:nth-child(5) | 外資連續買賣 |
| 12 | 外資連賣 | linkLabel33 | tw2 | tr:nth-child(5) | 外資連續買賣 |
| 13 | 外資投信同步買超 | linkLabel30 | tw2 | tr:nth-child(5) | 投外同步 |
| 14 | 外資投信同步賣超 | linkLabel31 | tw2 | tr:nth-child(5) | 投外同步 |
| 15 | EPS創新高 | linkLabel38 | tw | tr:nth-child(5) | 財報分析 |
| 16 | 季營收創高 | linkLabel44 | tw | tr:nth-child(5) | 財報分析 |
| 17 | 財報評分 | linkLabel34 | tw | tr:nth-child(5) | 財報分析 |
| 18 | 月季黃金 | linkLabel17 | tw | tr:nth-child(5) | 均線分析_insert |
| 19 | OSC負轉正 | (同 MACD) | tw | tr:nth-child(5) | MACD轉正_Insert |

## 詳細配置

### 1. 券資比 (linkLabel4)
```csharp
// Line: 200
URL: https://goodinfo.tw/tw2/StockList.asp?MARKET_CAT=熱門排行&INDUSTRY_CAT=券資比#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.券資比_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 2. 周轉率 (linkLabel9)
```csharp
// Line: 564
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=熱門排行&INDUSTRY_CAT=累計成交量週轉率(當日)@@累計成交量週轉率@@當日
Selector: #txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.周轉率_insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 3. MACD負轉正 (linkLabel11)
```csharp
// Line: 379
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=DIF、MACD小於0且OSC由負轉正@@日MACD落點@@DIF、MACD小於0且OSC由負轉正#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.MACD轉正_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw
```

### 4. 超布林上軌 (linkLabel3)
```csharp
// Line: 400
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=股價高於布林上軌 (參考月均線)@@股價位置與布林軌道@@股價高於布林上軌 (參考月均線)#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.布林分析_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw
```

### 5. 外資連買連賣轉折 (linkLabel7)
```csharp
// Line: 418
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=外資連續賣出轉買進 – 日@@外資連買連賣轉折@@外資連續賣出轉買進 – 日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.投外轉折_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 6. 投信連買連賣轉折 (linkLabel8)
```csharp
// Line: 436
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=投信連續賣出轉買進 – 日@@投信連買連賣轉折@@投信連續賣出轉買進 – 日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.投外轉折_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 7. 歷史成交量 (linkLabel1)
```csharp
// Line: 470
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=日成交張數創歷日新高@@成交張數創歷史新高/低@@日成交張數創歷日新高#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(7) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.歷史成交_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw
```

### 8. 五年新高 (linkLabel23)
```csharp
// Line: 805
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=股價創五年高點@@股價創多日高點@@五年#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.歷史股價_Insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw
```

### 9. 投信連買 (linkLabel29)
```csharp
// Line: 1325
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=投信連買 – 日@@投信連續買超@@投信連續買超 – 日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.外資連續買賣(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 10. 投信連賣 (linkLabel28)
```csharp
// Line: 1341
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=投信連賣 – 日@@投信連續賣超@@投信連續賣超 – 日
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.外資連續買賣(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 11. 外資連買 (linkLabel32)
```csharp
// Line: 1356
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=外資連買 – 日@@外資連續買超@@外資連續買超 – 日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.外資連續買賣(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 12. 外資連賣 (linkLabel33)
```csharp
// Line: 1376
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=外資連賣 – 日@@外資連續賣超@@外資連續賣超 – 日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.外資連續買賣(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 13. 外資投信同步買超 (linkLabel30)
```csharp
// Line: 1406
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=外資、投信同步買超–當日@@外資、投信同步買超@@當日#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.投外同步(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
```

### 14. 外資投信同步賣超 (linkLabel31)
```csharp
// Line: 1391
URL: https://goodinfo.tw/tw2/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=外資、投信同步買超–當日@@外資、投信同步買超@@當日&INITIALIZED=T#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.投外同步(textProcessing.Text, ref dataDate, menualLink)
Domain: tw2
Note: 有 INITIALIZED=T 參數
```

### 15. EPS創新高 (linkLabel38)
```csharp
// Line: 1421
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=單季EPS創歷季新高@@EPS創新高/低@@單季EPS創歷季新高#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.財報分析(textProcessing.Text, dtp1.Text, menualLink)
Domain: tw
```

### 16. 季營收創高 (linkLabel44)
```csharp
// Line: 1435
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=單季營收創歷季新高@@營業收入創新高/低@@單季營收創歷季新高#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.財報分析(textProcessing.Text, dtp1.Text, menualLink)
Domain: tw
Special: ifScroll=false
```

### 17. 財報評分 (linkLabel34)
```csharp
// Line: 1459
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=單季財報評分創歷季新高@@財報評分創新高/低@@單季財報評分創歷季新高#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.財報分析(textProcessing.Text, dtp1.Text, menualLink)
Domain: tw
```

### 18. 月季黃金 (linkLabel17)
```csharp
// Line: 716
URL: https://goodinfo.tw/tw/StockList.asp?RPT_TIME=&MARKET_CAT=智慧選股&INDUSTRY_CAT=10日/月/季線多頭排列且均線走揚@@均價線多頭排列且走揚@@10日/月/季#txtStockListData
Selector: #txtStockListData > table > tbody > tr:nth-child(5) > td:nth-child(2) > input[type=button]:nth-child(2)
Insert: CommonApp.均線分析_insert(textProcessing.Text, ref dataDate, menualLink)
Domain: tw
```

### 19. OSC負轉正
**注意**：OSC負轉正和MACD負轉正是同一個 link (linkLabel11)，因為 GoodInfo 的條件是 "DIF、MACD小於0且OSC由負轉正"

## 域名分類

### tw 域名 (9個)
1. MACD負轉正 (linkLabel11) - tr:nth-child(5)
2. 超布林上軌 (linkLabel3) - tr:nth-child(5)
3. 歷史成交量 (linkLabel1) - tr:nth-child(7) ⚠️
4. 五年新高 (linkLabel23) - tr:nth-child(5)
5. EPS創新高 (linkLabel38) - tr:nth-child(5)
6. 季營收創高 (linkLabel44) - tr:nth-child(5)
7. 財報評分 (linkLabel34) - tr:nth-child(5)
8. 月季黃金 (linkLabel17) - tr:nth-child(5)
9. OSC負轉正 (同linkLabel11) - tr:nth-child(5)

### tw2 域名 (10個)
1. 券資比 (linkLabel4) - tr:nth-child(7) ⚠️
2. 周轉率 (linkLabel9) - tr:nth-child(7) ⚠️
3. 外資連買連賣轉折 (linkLabel7) - tr:nth-child(5)
4. 投信連買連賣轉折 (linkLabel8) - tr:nth-child(5)
5. 投信連買 (linkLabel29) - tr:nth-child(5)
6. 投信連賣 (linkLabel28) - tr:nth-child(5)
7. 外資連買 (linkLabel32) - tr:nth-child(5)
8. 外資連賣 (linkLabel33) - tr:nth-child(5)
9. 外資投信同步買超 (linkLabel30) - tr:nth-child(5)
10. 外資投信同步賣超 (linkLabel31) - tr:nth-child(5)

## CSS Selector 規則

### tr:nth-child(5) - 最常見 (16個)
大部分 GoodInfo 頁面使用此 selector

### tr:nth-child(7) - 特殊情況 (3個)
- 券資比 (tw2)
- 周轉率 (tw2)
- 歷史成交量 (tw)

## Insert 方法分類

1. **券資比_Insert** - 券資比
2. **周轉率_insert** - 周轉率
3. **MACD轉正_Insert** - MACD負轉正, OSC負轉正
4. **布林分析_Insert** - 超布林上軌
5. **投外轉折_Insert** - 外資連買連賣轉折, 投信連買連賣轉折
6. **歷史成交_Insert** - 歷史成交量
7. **歷史股價_Insert** - 五年新高
8. **外資連續買賣** - 投信連買, 投信連賣, 外資連買, 外資連賣
9. **投外同步** - 外資投信同步買超, 外資投信同步賣超
10. **財報分析** - EPS創新高, 季營收創高, 財報評分
11. **均線分析_insert** - 月季黃金

## 特殊注意事項

1. **季營收創高** (linkLabel44) 使用 `ifScroll=false` 參數
2. **外資投信同步賣超** (linkLabel31) URL 有 `&INITIALIZED=T` 參數
3. **財報相關** (linkLabel38, 44, 34) 的 Insert 方法使用 `dtp1.Text` 而不是 `ref dataDate`
4. **tw2 域名**的下載按鈕位置可能不同，需要特別測試

## 測試優先順序

### 高優先級（已測試成功）
- ✅ 券資比 (tw2, tr:7) - 已成功

### 中優先級（tw 域名，tr:5）
應該都能用相同邏輯成功：
- MACD負轉正, 超布林上軌, 五年新高, EPS創新高, 季營收創高, 財報評分, 月季黃金

### 需要特殊處理
- 周轉率 (tw2, tr:7) - 需測試
- 歷史成交量 (tw, tr:7) - 需測試
- 外資投信相關 (tw2, tr:5) - 10個，需批次測試

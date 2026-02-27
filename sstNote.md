#启动脚本
cd D:\OpenCode\sst
opencode .

dotnet build D:\OpenCode\sst\src\SST.StockImport.Services\SST.StockImport.Services.csproj
.\start-api.ps1 -NoBuild

cd D:\vibeCoding\sst
dotnet build D:\vibeCoding\sst\src\SST.StockImport.Services\SST.StockImport.Services.csproj
.\start-api.ps1 -NoBuild

cd D:\vibeCoding\sst
.\StartAll.ps1 
////////////////////////

cd D:\OpenCode\sst
.\start-web.ps1

cd D:\vibeCoding\sst
.\start-web.ps1



http://localhost:5089/

#測試金字塔與驗證策略
### 測試層級（由下而上，層層相依）
```
Sandbox 內測試：
  單元測試 → 無伺服器整合測試 → 有伺服器的整合測試(webapi) 
  ↓ (通過後才能出 Sandbox)
出 Sandbox 測試：
  無伺服器整合測試 → 有伺服器的整合測試 → ui 整合 -> e2e  
說明：前一層是後一層的基礎，層層相依，不可跳過
嚴禁 Mock/ hardcording 離開 Sandbox
1. 在 Sandbox 内开发代码
2. UI 所有會調用API的event, 一定在L3 有相對應的測試點
3. **完整测试 L1→L2→L3**
4. 所有测试通过后才移出 Sandbox
5.  测试脚本 移到 D:\vibeCoding\PrdReport\Scripts 
   依照 UC / phase/ project 正确位置存放  ， 确保 金字塔测试脚本 可重用
```
除非 UI call 的不是 有伺服器的整合測試(webapi)
用金字塔的好處就是 L2 如果測試通過
L3 基本上都不會有任何問題
因為他是基於L2, 差别只在 register API 
 
 请继续

#CORS 設定參考 :
以下是我另外一个server的设定
请参考:

# CORS 中間件
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],        # ✅ 允許所有來源
    allow_credentials=True,     # ✅ 允許攜帶憑證（cookies）
    allow_methods=["*"],        # ✅ 允許所有 HTTP 方法（GET/POST/PUT/DELETE）
    allow_headers=["*"],        # ✅ 允許所有 Header
)

allow_origins=[
    "http://localhost:3000",      # 开发前端
    "http://192.168.1.78:8150",   # 本地 server
    "https://yourdomain.com"      # 生产域名
],  # 避免 ["*"] 在生产环境使用


2.開收工規範 
## 開工 Checklist
□ 讀取 copilot-instructions.md
□ 讀取最新的 Session Report 和 累積待辦事項.md (Docs/Todo/*)
□ 確認累積待辦事項
□ 檢查專案注意事項: Docs/projectNote.md
□ **確認測試環境使用 SQLite or 测试 DB，絕不連接生產 DB**
□ 严格遵守：
	a. 務必先經過讨论 不可以用猜的， 还没仔细分析，不清楚状况之下 不可以直接动手改code
	b. 本系统 在 Docs 目录之下有详细的 UC的文件 请先看完 UC 文件再开始分析更改重试 
	c. 如果在开发 或修改 UC a 发现 UC b 的程式有错或是不足 要修改前一定要先经过讨论 不可以直接修改 
	d. 所有的開發/修改 都應該通過 通過金字塔驗證 再整合到 UI
       新 UC 開發或是較大幅度bug 修改, 一定要進入 Sandbox, 完成金字塔驗證
	   小bug 修改，只做最小修改， 可以不進入 Sandbox, 但仍需完成
	   單元測試 → 無伺服器整合測試 → 有伺服器的整合測試(webapi) 
	   並 確定相關 UC 的 單元/整合測試 也都安全通過
	e. 127.0.0.1 DB 的 acc/pwd： root / 
	   (no password)

## 開發期間注意事項
□ 所有測試必須在隔離環境執行
□ 不得在生產 DB 執行寫入操作
□ 遵循 Design-First 原則
□ 遵循 測試金字塔與驗證策略
□ 單元測試覆蓋率 ≥ 95%
## 🚨 最重要的提醒 🚨
**測試絕對不可以修改生產資料庫！**
**有疑慮時，寧可多問一次，也不要冒險！**
**要開始改程式以前先確認問題與做法 ** 
**對話用用中文，產生 ps1 脚本用英文， 避免中文編碼干擾

## 收工 Checklist
□ **驗證所有測試通過且未影響生產 DB**
□ 確認明天無縫交接待辦事項 产生 => Docs\todo\YYMMdd_HHmm_SessionReport.md(用日期时间作为档名)
□ 資料庫欄位問題記錄到 Docs\DATABASE_SCHEMA_ISSUES.md，让管理者可以 准确的手動部署 DB 的
□ 脚本请依照 projectNote 规范整理，仅保留可重用脚本，例如:金字塔测试脚本
□ 依照 Docs/SESSION_CHECKLIST.md 執行
 Phase 1: 文件先行（完整版）
 如果是修改現行功能(包含 debug), 必须回头去 查修正 原先的 设计 UC文件 
 Phase 2: 代碼品質
 Phase 3: 依賴與整潔
 	  錯的/舊的過期程式就直接刪除
	  不要留在專案， 造成未來的混淆  
	  Mock/ hardcording 必须得到确认， 才可以进 git
 Phase 4: commit and push to git server 
□ 更新 Session Report
□ 更新累積待辦事項


/////
这个系统是我自己找一些特殊的指标，去监控股票市场的行为
跟一般的股票市场的那些标准指标差很多
我的做法是说，把现在的交易看成一张大地图，
我记录了每一只股票每天的交易量，每 5 分钟就会下载一次最新的交易资料
然后去比较说，这 5 分钟里面的交易量，如果超出这只股票5 分钟平均交易量的 10 倍以上
代表这边有热度，那就开始去关切
靠着关注地图上的点位，来决定该买进什么股票

table: alertlist 这是用来看瞬间热度的
paRatePosCnt : 这是当天 5 分钟内跳升 2%以上 的次数
paRateNegCnt 这是当天 5 分钟内跳跌 1%以上 的次数
pLVRatePosCnt 價漲且出大量(5 分钟的量大于昨天全天的 1/2) 的次数 ， 代表有人在进货
pLVRateNegCnt 價跌且出大量(5 分钟的量大于昨天全天的 1/2) 的次数 ， 代表有人在出货
p5VRatePosCnt 價漲且出大量(5 分钟的量大于 5日均量的 1/2) 的次数 ， 代表有人在进货
p5VRateNegCnt 價跌且出大量(5 分钟的量大于 5日均量的 1/2) 的次数 ， 代表有人在出货
pApRatePosCnt 價漲且出大量(5 分钟的量大于 昨日5分钟盘量的10倍) 的次数 ， 代表有人在进货
pApRateNegCnt 價跌且出大量(5 分钟的量大于 昨日5分钟盘量的10倍) 的次数 ， 代表有人在出货
maxPLVR 当天最大的 5 分钟盘量


2025/11/03 的這些股票，能找到共同的特徵碼 ？ 
1 	3163 	90 	17.0x 	28 天 	240.50 	+74.01% 	26 天 	⭐ 78
2 	3105 	90 	15.0x 	28 天 	118.00 	+61.86% 	28 天 	⭐ 72
3 	4542 	90 	13.0x 	28 天 	59.80 	+58.03% 	36 天 	⭐ 67
4 	3455 	90 	18.0x 	28 天 	87.80 	+36.10% 	41 天 	⭐ 57


 	37 	KD_RSV 				decimal(10,2) 			否 	0.00 	
	38 	KD_K 				decimal(10,2) 			否 	0.00 		
	39 	KD_D 				decimal(10,2) 			否 	0.00 	
	40 	boolUp 				decimal(10,2) 			否 	0.00 	布林上軌 
	41 	boolMid 			decimal(10,2) 			否 	0.00 	布林中線  	
	42 	boolDown 			decimal(10,2) 			否 	0.00 	布林下軌
	43 	boolkaikouDiffRate 	decimal(10,2) 			否 	0.00 	布林寬價幅 
	
继续之前的 SST 项目工作，我们做到了：
1. 修复了 TWSEScraper 数据完整性验证
2. 修复了 ImportController 移除假数据
3. 完成了「资料源统计与验证」功能
请继续第二阶段：GoodInfo 导入功能	


这个系统是用来管理股市交易资料
资料的来源有三个：
每天从交易所下载的收盘资料，交易所包括：
a. 1. 上市 2. 上柜 3. 新柜
b. 每 5 分钟的交易资料统计，这主要是去看分盘的交易量和突然的涨跌幅
c. 每天从 GoodInfo 下载的股票统计资料

目前就是稳定地保存这些资料就好了，还没有开始做比较有意义的分析。

交易所資料： table:weekall, tradedata, 固定每天下載， 儲存 weekall 僅存 5 天資料， tradedata 存半年
 
最穩定的是 alertlist, alertLog, dapan, detector, investbase, 這是由另一個系統固定時間到券商拉資料
tradedata 除了交易所的資料, 主要的資料來源是 goodinfo 來的統計·資料
stock60days 則是綜合的統計資料， 由本系統自己計算

现在遇到的问题是：我的资料来源是不是都稳定而正确地被存入他们各自的 table
最简单的从交易所下载资料并存到这两个 table:weekall, tradedata，到目前为止都一直失败。


1. 首頁的 下載交易資料：是用 investbase 的 recDate, 作爲下載日期的預設日， 而非 下載當時的日期
2. 交易所來的資料， 必須統一轉成以 張(千股) 爲單位
3. 使用者可以多次下载资料，也就是每次下载的时候都要用 INSERT ... ON DUPLICATE KEY UPDATE
4. 下载的档案应该要放到 D:\vibeCoding\sst\srcBackup, 以日期为单位的子目录, 例如 D:\vibeCoding\sst\srcBackup\20260226
这些跟交易有关的 table 一定会有两个日期：一个是交易日，一个是前一天的交易日。

因为交易日期不是连续的（也就是它跟日历日不一样），所以一定要靠 Last Date 去指到前一个交易日，这样才能够连贯下来。


我需要在 Menu 增加一个查看指定日期的资料功能。
包括: 
1. 筆數 - weekall, tradedata, alertlist, alertLog, dapan, detector, investbase, stock60days
2. dapan - 今昨上市/上櫃 點數
3. weekall, tradedata  
   今昨	上市/上櫃/興櫃 筆數, %
   今昨 上市/上櫃/興櫃縂成交金額, %
   今昨 上市/上櫃/興櫃縂成交張數, %
 
 
 交易所标准的下载流程
 到 InvestBase 找 RECDATE , 这个就是要下載的交易日期
 到网站下载这个日期的资料 csv
 Parse CSV 档，Insert update 到那两个 Table 去
 
 
 明白了！stock60days 是一个重要的 table，存储 60 日统计数据，包含：
- KD 指标 (KD_RSV, KD_K, KD_D)
- 布林带 (boolUp, boolMid, boolDown)
- 移动平均线等
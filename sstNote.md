#启动脚本
cd D:\OpenCode\sst
dotnet build D:\OpenCode\sst\src\SST.StockImport.Services\SST.StockImport.Services.csproj
.\start-api.ps1 -NoBuild

cd D:\OpenCode\sst
.\start-api.ps1 -NoBuild
////////////////////////

cd D:\OpenCode\sst
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
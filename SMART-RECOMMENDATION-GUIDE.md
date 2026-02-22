# 智能推荐系统 - 快速启动和测试指南

## ✅ 系统状态
- API: http://localhost:5008 ✓ 正在运行
- Web UI: http://localhost:5089 ✓ 正在运行

## 🎯 测试步骤

### 方法1：浏览器手动测试（推荐）

1. 打开浏览器访问：
   ```
   http://localhost:5089/smart-recommendation
   ```

2. 在界面上操作：
   - **推荐日期**：选择 `2025-12-01`（历史日期，有数据）
   - **推荐数量**：保持 `3`
   - **最小成熟度**：保持 `60`
   - **冷却期（天）**：保持 `8 - 30`

3. 点击 **"获取推荐"** 按钮

4. 应该看到：
   - ✅ 绿色回测统计面板（显示成功率等）
   - ✅ 3 张推荐卡片（股票 2719 等）
   - ✅ 每张卡片显示实际表现（涨跌情况）

### 方法2：PowerShell 命令行测试

```powershell
# 测试 API 直接调用
Invoke-RestMethod -Uri "http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=3&minMaturityScore=60&minCoolingDays=8&maxCoolingDays=30" | ConvertTo-Json
```

## 📊 已验证的测试结果

### 测试1：今日推荐（2026-02-21）
```
结果：0 条推荐
原因：过年期间无交易数据（正常）
```

### 测试2：历史回测（2025-12-01）
```
✓ 推荐数量：3 只股票
✓ 候选池：50 个
✓ Top Pick：2719（评分 100 分）
✓ 入场价：¥32.00
✓ 目标价（+20%）：¥38.40
✓ 回测统计：平均涨幅 6.84%
```

### 测试3：历史回测（2025-11-15）
```
✓ 推荐数量：5 只股票
✓ 候选池：50 个
✓ Top 3：2719（85分）、5902（85分）、7516（80分）
```

## 🐛 调试技巧

### 如果浏览器没反应：
1. **按 F12 打开开发者工具**
2. 切换到 **Console** 标签
3. 点击"获取推荐"按钮
4. 查看 Console 中的日志（应该看到 🔍 📡 ✅ 等 emoji）
5. 切换到 **Network** 标签，查看 API 请求（应该看到 `/api/SmartRecommendation/...`）

### 如果 API 错误：
```powershell
# 查看 API 终端输出
# 检查是否有异常堆栈

# 重启 API
Get-Process dotnet | Where-Object { $_.CommandLine -like "*API*" } | Stop-Process -Force
dotnet run --project src/SST.StockImport.API --urls "http://localhost:5008"
```

### 如果端口占用：
```powershell
# 释放 5008 端口（API）
$listener = Get-NetTCPConnection -LocalPort 5008 -ErrorAction SilentlyContinue
if ($listener) { Stop-Process -Id $listener.OwningProcess -Force }

# 释放 5089 端口（Web）
$listener = Get-NetTCPConnection -LocalPort 5089 -ErrorAction SilentlyContinue
if ($listener) { Stop-Process -Id $listener.OwningProcess -Force }
```

## 📝 日期选择建议

| 日期 | 预期结果 | 用途 |
|------|---------|------|
| 2026-02-21（今日） | 0 条推荐 | 验证过年期间逻辑 |
| 2025-12-01 | 3 条推荐 | 验证回测功能 |
| 2025-11-15 | 5 条推荐 | 验证更多样本 |
| 2025-10-01 | 1-5 条推荐 | 验证较早日期 |

## 🎉 预期效果

成功时应该看到：
- 页面顶部显示 4 个统计卡片（推荐时间、推荐数量、候选池、生成时间）
- 绿色回测统计面板（仅历史日期）
- 每个股票一张卡片，包含：
  - 推荐排名（🥇 首选、🥈 次选、🥉 第三）
  - 成熟度评分（进度条）
  - 关键指标（冷却期、量能倍数）
  - 推荐理由（4-5 条）
  - 实际表现（仅历史日期，绿色/红色标注）

## 🔧 相关脚本

- `test-smart-recommendation-complete.ps1` - 完整测试 API
- `start-api.ps1` - 启动 API
- `start-web.ps1` - 启动 Web UI
- `start-smart-recommendation.ps1` - 同时启动前后台

开始测试吧！🚀

# 🎯 Golden Master 测试框架 - 快速参考指南

## 1️⃣ 一行命令启动

```powershell
# 第 1 步: 导出测试数据（从原系统）
.\Tests\GoldenMaster\Export-GoldenMasterData.ps1 -Days 3

# 第 2 步: 运行 Golden Master 测试
.\Tests\GoldenMaster\Run-GoldenMasterTests.ps1

# 第 3 步: 查看报告
.\Tests\GoldenMaster\Reports\GoldenMaster-Report.html
```

---

## 2️⃣ 完整工作流

### Step A: 数据准备（5 分钟）

**导出原系统数据:**
```powershell
cd d:\vibeCoding\sst
.\Tests\GoldenMaster\Export-GoldenMasterData.ps1 `
    -Days 3 `
    -ConnString "Server=localhost;User=root;Password=xxx;Database=sst_db" `
    -OutputPath ".\Tests\GoldenMaster\TestData"
```

**预期输出:**
```
✅ 连接数据库成功
✅ 导出警示日志: 87 条记录
✅ 导出交易数据: 245 条记录
✅ 序列化为 JSON: GoldenMasterSnapshots.json
✅ 统计完成: ExportStatistics.json
```

### Step B: 运行测试（2 分钟）

**基础测试:**
```powershell
.\Tests\GoldenMaster\Run-GoldenMasterTests.ps1
```

**性能基准测试:**
```powershell
.\Tests\GoldenMaster\Run-GoldenMasterTests.ps1 -Mode "Benchmark"
```

**生成代码覆盖率报告:**
```powershell
.\Tests\GoldenMaster\Run-GoldenMasterTests.ps1 -Mode "Coverage" -OutputFormat "Html"
```

### Step C: 查看结果（1 分钟）

**生成的报告:**
- `Reports/test-results.xml` - xUnit 标准格式
- `Reports/GoldenMaster-Report.html` - 可视化报告
- `Reports/GoldenMaster-Report.json` - 机器可读格式

---

## 3️⃣ 核心测试用例说明

### Test 1: 单个股票处理准确性
```csharp
[Fact(DisplayName = "单个股票处理 - 警示准确性")]
public async Task SingleStockAlert_ShouldMatchExpectedOutput()
```
**验证内容:**
- ✓ PanVolume (盘中成交量) 精确匹配
- ✓ PanAvgVolRate (盘中均量比) 精度 ±1%
- ✓ PanLastVolRate (盘中昨量比) 精度 ±1%
- ✓ AlertLevel (警示等级) 精确匹配

**通过标准:** 所有数值在容差内

---

### Test 2: 批量处理独立性
```csharp
[Fact(DisplayName = "批量处理10个股票")]
public async Task BatchProcessing_10Stocks_ShouldProcessIndependently()
```
**验证内容:**
- ✓ 10 个不同股票独立处理
- ✓ 股票间不互相影响
- ✓ 处理顺序无关

**通过标准:** 所有结果有效且数值正确

---

### Test 3: 高量警示（20倍检测）
```csharp
[Fact(DisplayName = "高量警示 - 20倍检测")]
public async Task HighVolumeAlert_20xDetection_ShouldTrigger()
```
**验证内容:**
- ✓ 当成交量达到昨日平均 20 倍时
- ✓ 正确生成 VolumeSpikeCritical 警示
- ✓ AlertTitle 包含 "20倍" 文字

**通过标准:** 警示等级 = VolumeSpikeCritical, 比率 ≥ 20.0

---

### Test 4: 价格跳跃警示
```csharp
[Fact(DisplayName = "价格跳跃警示 - 检测")]
public async Task PriceJumpAlert_ShouldDetectJumps()
```
**验证内容:**
- ✓ 检测到价格瞬间跳升
- ✓ 生成 PriceJump 级别警示

**通过标准:** 警示等级 = PriceJump

---

### Test 5: Golden Master 全套测试（最重要！）
```csharp
[Fact(DisplayName = "Golden Master - 所有预期输出")]
public async Task GoldenMaster_AllTestCases_ShouldMatchExpectedOutput()
```
**验证内容:**
- ✓ 运行所有导出的历史测试用例
- ✓ 将新系统输出与原系统输出对比
- ✓ 生成逐个测试的对比报告

**通过标准:** 所有测试用例在容差内通过

**此测试失败处理:**
```
❌ 如果失败，查看报告中的 Differences 字段
   例: PanAvgVolRate: 15.234 vs 15.201

→ 差值 = (15.234 - 15.201) / 15.201 = 0.22% 
→ 在 ±1% 容差内，应通过

→ 如果超过容差，检查：
   1. 时间戳转换是否正确
   2. 数据库初始化是否完整
   3. 算法实现是否有差异
```

---

## 4️⃣ 性能基准测试说明

### 基准 A: 单股票处理延迟

```
原系统: 15 ms/股票
目标:   ≤12 ms/股票 (20% 改进)

通过标准: 平均处理时间 ≤ 14.4 ms (允许20%误差)
```

**执行:**
```powershell
dotnet test Tests/GoldenMaster/AlertProcessingGoldenMasterTests.cs `
  -f net8.0 `
  --filter "Name=PerformanceBenchmark_SingleStock_ShouldMeetTarget" `
  -c Release
```

---

### 基准 B: 批量吞吐量

```
原系统: 50 stocks/sec
目标:   ≥60 stocks/sec (20% 改进)

通过标准: 吞吐量 ≥ 48 stocks/sec (允许20%误差)
```

**执行:**
```powershell
dotnet test Tests/GoldenMaster/AlertProcessingGoldenMasterTests.cs `
  -f net8.0 `
  --filter "Name=PerformanceBenchmark_BatchThroughput_ShouldMeetTarget" `
  -c Release
```

---

### 性能报告示例

```
╔════════════════════════════════════════════════════════╗
║        性能基准 - 批量吞吐量                           ║
╚════════════════════════════════════════════════════════╝

处理股票数:   500
总耗时:       8,425 ms
吞吐量:       59.3 stocks/sec
目标吞吐量:   60 stocks/sec
性能改进:     18.6% (vs 原系统)

⚠️ 吞吐量未达目标 (59.3 < 60)
→ 但在允许范围内（允许 -20%）
```

---

## 5️⃣ 常见问题排查

### Q1: 测试找不到 GoldenMasterSnapshots.json

**错误信息:**
```
FileNotFoundException: 测试数据文件不存在
```

**解决方案:**
```powershell
# 1. 检查文件是否存在
ls Tests/GoldenMaster/TestData/

# 2. 如果不存在，运行导出脚本
.\Tests\GoldenMaster\Export-GoldenMasterData.ps1

# 3. 检查数据库连接字符串是否正确
```

---

### Q2: 数据库连接失败

**错误信息:**
```
MySqlConnector.MySqlException: Access denied for user 'root'
```

**解决方案:**
```powershell
# 修改连接字符串（在导出脚本中）
$ConnString = "Server=localhost;User=root;Password=YOUR_PASSWORD;Database=sst_db"

# 验证数据库连接
mysql -h localhost -u root -p -e "SELECT COUNT(*) FROM sst_db.alertlog;"
```

---

### Q3: 测试超时（Timeout）

**错误信息:**
```
TimeoutException: The operation did not complete within the time limit
```

**原因:** 数据库查询过慢或数据量过大

**解决方案:**
```powershell
# 1. 减少导出数据量
.\Export-GoldenMasterData.ps1 -Days 1

# 2. 增加超时时间
# 在测试文件中修改:
[Fact(Timeout = 60000)]  # 增加到 60 秒

# 3. 优化数据库查询
# 为 alertlog 表添加索引：
CREATE INDEX idx_alertlog_created ON alertlog(created);
```

---

### Q4: 测试用例失败（Golden Master 不匹配）

**错误信息:**
```
PanAvgVolRate: 15.234 vs 15.201
Differences found in test case TEST_001
```

**分析步骤:**

1. **计算差值百分比:**
   ```
   差值% = |15.234 - 15.201| / 15.201 × 100 = 0.22%
   容差  = 1%
   → 在容差内，应该通过
   ```

2. **如果超过容差，检查原因:**
   ```
   可能原因:
   ✓ 浮点精度损失（使用 decimal 代替 double）
   ✓ 时间戳转换错误（验证时区）
   ✓ 数据库值不同（检查初始化）
   ✓ 算法实现差异（对比原系统代码）
   ```

3. **调试方法:**
   ```csharp
   // 在测试中添加日志
   Console.WriteLine($@"
       预期: {expected.PanAvgVolRate:F10}
       实际: {actual.PanAvgVolRate:F10}
       差值: {Math.Abs(expected.PanAvgVolRate - actual.PanAvgVolRate):F10}
   ");
   ```

---

### Q5: 性能测试未达目标

**错误信息:**
```
Assertion failed: 59.3 stocks/sec should be >= 60 stocks/sec
```

**分析步骤:**

1. **在 Release 模式下测试:**
   ```powershell
   dotnet test -c Release  # 不要用 Debug
   ```

2. **关闭其他应用:**
   ```powershell
   # 高效能测试需要系统资源
   taskkill /F /IM chrome.exe  # 关闭浏览器等
   ```

3. **多次运行取平均值:**
   ```powershell
   # 运行 3 次取平均
   for ($i=1; $i -le 3; $i++) {
       dotnet test -c Release
   }
   ```

4. **如果仍未达目标:**
   ```
   • 检查是否有其他后台进程
   • 验证数据库连接质量
   • 考虑优化 AlertProcessingService 的算法
   • 或调整目标数值为可达成的值
   ```

---

## 6️⃣ 高级用法

### 自定义容差值

**修改文件:** `AlertProcessingGoldenMasterTests.cs`

```csharp
// 在 GoldenMaster_AllTestCases 测试中修改
const double toleranceRatio = 0.02;  // 改为 2% 容差

// 或者在比较时指定
var comparison = CompareResults(
    actualResult,
    expectedOutput,
    testId,
    tolerance: 0.05  // 5% 容差
);
```

---

### 添加自定义测试用例

**方法 1: 手动创建 JSON**

```json
{
  "id": "CUSTOM_001",
  "description": "我的自定义测试",
  "snapshots": [
    {
      "code": "2330",
      "exchange": "TSE",
      "close": 650.0,
      "volume": 15000,
      "totalVolume": 18000000,
      "yesterdayVolume": 900000
    }
  ],
  "expectedOutput": {
    "panVolume": 45000,
    "panAvgVolRate": 25.0,
    "priority": 20,
    "alertTitle": "20倍量能"
  }
}
```

**方法 2: 编程方式生成**

```csharp
var testCase = new GoldenTestCase
{
    Id = "CUSTOM_002",
    Snapshots = new List<SnapshotData>
    {
        new SnapshotData
        {
            Code = "2330",
            Close = 650.0,
            TotalVolume = 18000000,
            YesterdayVolume = 900000
        }
    },
    ExpectedOutput = new ExpectedAlertOutput
    {
        PanVolume = 45000,
        PanAvgVolRate = 25.0
    }
};
```

---

### 持续集成集成

**GitHub Actions 示例:**

```yaml
name: Golden Master Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    
    steps:
      - uses: actions/checkout@v2
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Run Golden Master Tests
        run: dotnet test Tests/GoldenMaster/AlertProcessingGoldenMasterTests.cs -c Release
      
      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v2
        with:
          name: test-results
          path: Tests/GoldenMaster/Reports/
```

---

## 7️⃣ 性能改进指南

如果性能未达目标，尝试以下优化：

### 优化 1: 使用 Parallel 处理

```csharp
// 在 AlertProcessingService 中
public async Task ProcessSnapshotsAsync(List<SnapshotData> snapshots)
{
    var tasks = snapshots
        .Select(s => ProcessSnapshotAsync(s.Code, s.Close, ...))
        .ToList();
    
    await Task.WhenAll(tasks);  // 并行处理
}
```

### 优化 2: 缓存数据库查询

```csharp
private Dictionary<string, InvestBase> _cache = new();

private InvestBase GetCachedInvestBase(string code)
{
    if (!_cache.ContainsKey(code))
    {
        _cache[code] = _dbContext.InvestBase.Find(code);
    }
    return _cache[code];
}
```

### 优化 3: 批量数据库操作

```csharp
// 改为批量插入，而不是逐条插入
_dbContext.AlertLogs.AddRange(alerts);
await _dbContext.SaveChangesAsync();
```

---

## 8️⃣ 下一步

✅ **测试全部通过:**
```
1. 运行生产环境部署检查清单
2. 部署到测试环境
3. 进行端到端测试
```

❌ **有测试失败:**
```
1. 分析失败原因（见第 5 节常见问题）
2. 修改代码
3. 重新运行测试
4. 直到所有测试通过
```

⚠️ **性能未达目标:**
```
1. 实施第 7 节的优化
2. 重新测试
3. 如果仍无法达到，考虑调整目标值
```

---

## 📞 联系与支持

- **问题追踪:** 在 GitHub Issues 中创建 issue
- **文档:** 见 TESTING_GUIDE.md（详细版本）
- **性能基准:** 见 PerformanceComparison.cs

---

**最后更新:** 2024-12-20  
**版本:** 1.0  
**维护者:** SST Development Team

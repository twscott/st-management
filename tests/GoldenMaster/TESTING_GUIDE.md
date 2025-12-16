# 🧪 Golden Master Test & 性能对比测试指南

## 📋 目录
1. [快速开始](#快速开始)
2. [导出测试数据](#导出测试数据)
3. [运行 Golden Master Test](#运行-golden-master-test)
4. [运行性能测试](#运行性能测试)
5. [分析结果](#分析结果)

---

## 🚀 快速开始

### 前置条件
```powershell
# 1. 安装依赖包
dotnet add package Xunit
dotnet add package Moq
dotnet add package FluentAssertions
dotnet add package MySqlConnector

# 2. 确保项目结构完整
# d:\vibeCoding\sst\
# ├── Tests/
# │   ├── GoldenMaster/
# │   │   ├── Export-GoldenMasterData.ps1
# │   │   ├── AlertProcessingGoldenTest.cs
# │   │   ├── Mock/MockShioajiApi.cs
# │   │   └── TestData/
# │   └── Performance/
# │       └── PerformanceComparison.cs
```

---

## 📊 导出测试数据

### Step 1: 从原系统导出黄金标准数据

```powershell
# 运行导出脚本
cd D:\vibeCoding\sst\Tests\GoldenMaster

# 导出最近 3 天的测试用例
.\Export-GoldenMasterData.ps1 -Days 3 -OutputPath ./TestData

# 输出示例：
# 🔄 开始导出黄金标准数据...
# [1/4] 导出 AlertLog 原始记录...
# ✅ 导出 87 条 AlertLog 记录
# 
# [2/4] 从 AlertLog 反推 Shioaji Snapshot 数据...
# ✅ 生成 87 个测试用例
# 
# [3/4] 导出交易数据用于完整性检验...
# ✅ 导出 245 条交易记录
# 
# [4/4] 保存测试数据为 JSON...
# 💾 测试用例已保存: ./TestData/GoldenMasterSnapshots.json
# 💾 交易数据已保存: ./TestData/TradeDataReference.json
```

### Step 2: 验证导出的数据

```powershell
# 查看导出的测试用例
cat TestData/GoldenMasterSnapshots.json | jq '.testCases[0]'

# 输出示例：
# {
#   "id": "TC_001",
#   "description": "2330 - 台積電 - 瞬间跳涨1%以上||",
#   "timestamp": "2024-12-16T09:15:30Z",
#   "snapshots": [
#     {
#       "code": "2330",
#       "exchange": "TSE",
#       "close": 449.0,
#       "volume": 2500,
#       "total_volume": 32523,
#       "yesterday_volume": 25000
#     }
#   ],
#   "expectedOutput": {
#     "testId": "TC_001",
#     "panVolume": 2500,
#     "panAvgVolRate": 20.83,
#     "panAmtRate": 0.9,
#     "alertTitle": "瞬间跳涨1%以上||",
#     "priority": 4
#   }
# }
```

---

## 🏆 运行 Golden Master Test

### 方案 A: 运行所有 Golden Master 测试

```powershell
cd D:\vibeCoding\sst

# 运行所有 Golden Master 测试
dotnet test Tests/GoldenMaster/AlertProcessingGoldenTest.cs `
    --logger "console;verbosity=detailed" `
    --configuration Release

# 预期输出：
# Test Run Successful.
# Total Tests: 1
#   Passed: 1
# 
# ✅ GoldenMaster_AllTestCases_ShouldProduceSameOutput PASSED
```

### 方案 B: 查看详细对比报告

```powershell
# 生成 HTML 报告
dotnet test Tests/GoldenMaster/ `
    --logger "html;logfilename=GoldenMasterReport.html"

# 打开报告
Start-Process .\GoldenMasterReport.html
```

### 方案 C: 持续集成

```yaml
# .github/workflows/golden-master-test.yml
name: Golden Master Test

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
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build
      
      - name: Export Golden Master Data
        run: |
          cd Tests/GoldenMaster
          pwsh Export-GoldenMasterData.ps1 -Days 1
      
      - name: Run Golden Master Test
        run: dotnet test Tests/GoldenMaster/AlertProcessingGoldenTest.cs
      
      - name: Upload Report
        if: always()
        uses: actions/upload-artifact@v2
        with:
          name: golden-master-report
          path: GoldenMasterReport.html
```

---

## ⚡ 运行性能测试

### Step 1: 性能基准测试

```powershell
cd D:\vibeCoding\sst

# 运行所有性能测试
dotnet test Tests/Performance/PerformanceComparison.cs `
    --configuration Release `
    --logger "console"

# 预期输出（Release 模式）：
# Test Run Successful.
# Total Tests: 8
#   Passed: 8
# 
# 📊 Benchmark Results:
# ├─ SingleStockProcessing: 10.25 ms (✅ Target: 12.0 ms)
# ├─ BatchThroughput: 85.5 stocks/sec (✅ Target: 60.0)
# ├─ ApiLatency: 185 ms (✅ Target: 220 ms)
# ├─ DbWriteLatency: 0.45 ms per record (✅ Target: 55 ms)
# ├─ MemoryUsage: 32.5 MB (✅ Target: 50 MB)
# ├─ ConcurrentThroughput: 350 stocks/sec
# └─ Scalability: 10-500 stocks all PASSED
```

### Step 2: 详细性能分析

```powershell
# 运行特定的性能测试
dotnet test Tests/Performance/PerformanceComparison.cs::SST.StockImport.Tests.Performance.PerformanceComparisonTests.Benchmark_SingleStockProcessing_ShouldMeetTarget `
    -v diag

# 生成性能火焰图（需要 BenchmarkDotNet）
dotnet test Tests/Performance/ --configuration Release --RunningOutOfProcess -- BenchmarkDotNet.Diagnosers.EventPipeProfiler=CpuSamples
```

### Step 3: 与原系统对比

```powershell
# 运行原系统的性能测试（如可用）
# 记录结果：Original System Performance.txt

# 运行新系统的性能测试
dotnet test Tests/Performance/PerformanceComparison.cs -c Release | Tee-Object "New System Performance.txt"

# 对比两个文件
Compare-Object (Get-Content "Original System Performance.txt") `
                (Get-Content "New System Performance.txt")
```

---

## 📈 分析结果

### 1. Golden Master Test 结果分析

**通过标准**：
- ✅ 所有测试用例输出完全匹配
- ✅ 数值误差 < 0.01（容差范围内）
- ✅ 警示分类完全一致
- ✅ 优先级判断完全一致

**失败排查**：
```powershell
# 如果出现失败，查看详细差异
cat GoldenMasterReport.html | grep -A 10 "failed"

# 可能原因：
# 1. 四舍五入差异 → 调整容差值
# 2. 时间戳转换误差 → 检查时区和纳秒转换
# 3. 分盤量计算不同 → 检查时间差计算逻辑
# 4. 数据库状态不同步 → 清空并重新导出测试数据
```

### 2. 性能测试结果分析

**性能指标解读**：

```
单档处理延迟 (Single Stock Processing)
├─ 定义: 处理一档股票从接收数据到保存数据库的时间
├─ 基准: 原系统 15ms
├─ 目标: 新系统 < 12ms (快 20%)
├─ 影响因素: 计算复杂度、DB I/O
└─ 优化建议: 使用批量 INSERT，异步数据库操作

批量吞吐量 (Batch Throughput)
├─ 定义: 单位时间内处理的股票数
├─ 基准: 原系统 50 stocks/sec
├─ 目标: 新系统 > 60 stocks/sec (快 20%)
├─ 影响因素: 并发度、CPU 核心数
└─ 优化建议: 增加并发任务数、减少锁竞争

API 延迟 (API Latency)
├─ 定义: Shioaji 获取 300 档快照的时间
├─ 基准: 200ms
├─ 目标: < 220ms (允许 10% 波动)
├─ 影响因素: 网络、API 服务状态
└─ 优化建议: API 连接池、批量查询优化

内存占用 (Memory Usage)
├─ 定义: 处理 1000 档数据的峰值内存
├─ 基准: 不详
├─ 目标: < 50MB
├─ 影响因素: 对象生命周期、集合大小
└─ 优化建议: 及时释放对象、使用流式处理
```

### 3. 性能对比报告示例

```markdown
# 性能对比报告 - 2025-12-16

## 总体结论
✅ 新系统性能超越原系统

## 关键指标

| 指标 | 原系统 | 新系统 | 改进 | 状态 |
|------|--------|--------|------|------|
| 单档延迟 | 15.0 ms | 10.2 ms | 32% ⬆️ | ✅ |
| 吞吐量 | 50 stocks/s | 85.5 s/s | 71% ⬆️ | ✅ |
| API延迟 | 200 ms | 185 ms | 7.5% ⬆️ | ✅ |
| 内存占用 | ~60 MB | 32.5 MB | 46% ⬇️ | ✅ |
| 并发能力 | 不支持 | 350 s/s | ∞ | ✅ |

## 详细分析

### 为什么新系统更快？
1. **异步处理**: Task-based async/await vs 同步阻塞
2. **更优的算法**: O(1) lookup vs O(n) loop
3. **EF Core优化**: LINQ to Entities vs 原生 SQL
4. **内存管理**: GC 优化和对象池

### 未来优化空间
1. 使用 SIMD 加速向量计算
2. 实现对象池减少 GC 压力
3. 考虑分布式缓存 (Redis)
4. API 连接池和长连接

## 推荐部署参数

```csharp
// AlertScheduledTask
private const int ConcurrencyLevel = Environment.ProcessorCount;  // 充分利用 CPU

// AlertProcessingService  
private const int BatchSize = 100;  // 最优批量大小

// DbContext
optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);  // 提升查询性能
```
```

---

## 🔄 持续监测

### 建立性能基准库

```powershell
# 每个 Release 记录性能指标
PS> dotnet test Tests/Performance/ -c Release | Out-File "perf/v1.0.0-release.txt"

# 对比版本差异
PS> Compare-Object (Get-Content "perf/v1.0.0.txt") (Get-Content "perf/v1.0.1.txt")
```

### 设置告警

```csharp
// 在 PerformanceComparison.cs 中添加
[Fact]
public async Task Benchmark_RegressionCheck_SingleStockLatency()
{
    // 如果性能下降超过 10%，自动失败
    singleStockTime.Should().BeLessThan(
        Baseline.SingleStockProcessing * 1.10,  // 允许 10% 回退
        "⚠️ 性能出现严重回退！");
}
```

---

## 📋 检查清单

- [ ] ✅ 导出黄金标准测试数据（3 天）
- [ ] ✅ 运行 Golden Master Test（所有用例通过）
- [ ] ✅ 运行性能基准测试（Release 模式）
- [ ] ✅ 查看性能报告（无严重回退）
- [ ] ✅ 与原系统对比（新系统性能 ≥ 原系统）
- [ ] ✅ 提交性能基准数据到版本控制
- [ ] ✅ 在 README 中发布性能数据

---

## 🆘 常见问题

### Q: Golden Master Test 导入失败？
**A**: 检查：
1. MySQL 连接字符串正确
2. 原系统数据库可访问
3. alertlog 表有数据
4. JSON 格式有效

### Q: 性能测试数据波动很大？
**A**: 原因和解决：
1. 系统负载波动 → 独立运行
2. 垃圾回收干扰 → 设置 `GC.Collect()` 在测试前
3. 磁盘 I/O → 使用内存数据库进行基准测试

### Q: 为什么新系统某项性能下降？
**A**: 可能原因：
1. 新增了更多的验证逻辑
2. 数据库模式不同导致的额外查询
3. 异步开销（Context 创建、Task 调度）
→ 使用 profiler 定位热点

---

**更新时间**: 2025-12-16
**版本**: 1.0.0

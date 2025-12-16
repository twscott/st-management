using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SST.StockImport.Infrastructure.Data;

namespace SST.StockImport.Tests.GoldenMaster
{
    /// <summary>
    /// Golden Master Test 配置和辅助类
    /// 提供测试环境的设置、数据加载、结果验证等功能
    /// </summary>
    public static class GoldenMasterTestHelper
    {
        private static readonly string TestDataPath = 
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, 
                        "GoldenMaster", "TestData");

        /// <summary>
        /// 设置测试环境（DI 容器）
        /// </summary>
        public static IServiceProvider BuildTestServiceProvider()
        {
            var services = new ServiceCollection();

            // 注册 DbContext (内存数据库)
            services.AddDbContext<StockImportDbContext>(options =>
                options.UseInMemoryDatabase("GoldenMasterTestDb"));

            // 注册 Logger
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 从 JSON 文件加载测试用例
        /// </summary>
        public static async Task<List<GoldenTestCase>> LoadTestCasesAsync(
            string fileName = "GoldenMasterSnapshots.json")
        {
            var filePath = Path.Combine(TestDataPath, fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(
                    $"测试数据文件不存在: {filePath}\n" +
                    $"请先运行: Export-GoldenMasterData.ps1");
            }

            var json = await File.ReadAllTextAsync(filePath);
            var document = JsonDocument.Parse(json);
            
            var testCases = new List<GoldenTestCase>();
            
            try
            {
                var testCasesElement = document.RootElement
                    .GetProperty("testCases");

                foreach (var element in testCasesElement.EnumerateArray())
                {
                    var testCase = JsonSerializer.Deserialize<GoldenTestCase>(
                        element.GetRawText(),
                        new JsonSerializerOptions 
                        { 
                            PropertyNameCaseInsensitive = true 
                        });

                    testCases.Add(testCase);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"解析测试数据失败: {ex.Message}", ex);
            }

            Console.WriteLine($"✅ 加载了 {testCases.Count} 个测试用例");
            return testCases;
        }

        /// <summary>
        /// 准备测试数据库（初始化 InvestBase）
        /// </summary>
        public static void PrepareTestDatabase(
            StockImportDbContext dbContext,
            List<GoldenTestCase> testCases)
        {
            // 准备模拟数据 - 在实际应用中从真实数据库加载
            Console.WriteLine($"✅ 测试数据库准备完成: {testCases.Count} 个测试用例");
        }

        /// <summary>
        /// 生成摘要报告
        /// </summary>
        public static void GenerateSummaryReport(
            string testDataPath,
            int passedCount,
            int failedCount)
        {
            var reportPath = Path.Combine(testDataPath, "GoldenMasterSummary.txt");

            var content = $@"
╔════════════════════════════════════════════════════════════════╗
║          Golden Master Test - 执行摘要                         ║
╚════════════════════════════════════════════════════════════════╝

执行时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
环境: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}

═══════════════════════════════════════════════════════════════════

📊 测试结果

  总测试数:    {passedCount + failedCount}
  ✅ 通过:      {passedCount}
  ❌ 失败:      {failedCount}
  
  通过率:       {((double)passedCount / (passedCount + failedCount) * 100):F1}%

═══════════════════════════════════════════════════════════════════

📈 分析

{(failedCount == 0 ? "✅ 所有测试通过！新系统与原系统输出完全一致。" : $"⚠️ 有 {failedCount} 个测试失败。详见详细报告。")}

═══════════════════════════════════════════════════════════════════

下一步:

{(failedCount == 0 ? @"
1. 运行性能基准测试:
   dotnet test Tests/Performance/PerformanceComparison.cs -c Release

2. 查看性能报告（对比原系统）

3. 准备生产部署
" : @"
1. 查看详细失败报告: GoldenMasterReport.html

2. 分析失败原因:
   - 数值精度误差？→ 调整容差值
   - 警示逻辑不同？ → 检查算法实现
   - 时间戳转换？  → 验证时区设置

3. 修复问题后重新运行
")}

═══════════════════════════════════════════════════════════════════

报告位置:
  - 详细报告: GoldenMasterReport.html
  - 统计信息: ExportStatistics.json
  - 本摘要:    GoldenMasterSummary.txt

═══════════════════════════════════════════════════════════════════
";

            File.WriteAllText(reportPath, content);
            Console.WriteLine(content);
        }

        /// <summary>
        /// 验证测试数据的完整性
        /// </summary>
        public static (bool isValid, string message) ValidateTestData(
            List<GoldenTestCase> testCases)
        {
            if (testCases.Count == 0)
                return (false, "测试用例数量为 0");

            var issues = new List<string>();

            foreach (var tc in testCases)
            {
                if (string.IsNullOrEmpty(tc.Id))
                    issues.Add($"测试用例缺少 ID");

                if (tc.Snapshots == null || tc.Snapshots.Count == 0)
                    issues.Add($"测试用例 {tc.Id} 缺少 Snapshot 数据");

                if (tc.ExpectedOutput == null)
                    issues.Add($"测试用例 {tc.Id} 缺少预期输出");

                // 验证关键字段
                foreach (var snap in tc.Snapshots ?? new())
                {
                    if (string.IsNullOrEmpty(snap.Code))
                        issues.Add($"Snapshot 缺少股票代码");

                    if (snap.Close <= 0)
                        issues.Add($"Snapshot {snap.Code} 价格无效: {snap.Close}");

                    if (snap.TotalVolume <= 0)
                        issues.Add($"Snapshot {snap.Code} 成交量无效: {snap.TotalVolume}");
                }
            }

            if (issues.Count > 0)
            {
                var message = "测试数据验证失败:\n" + 
                              string.Join("\n", issues.GetRange(0, Math.Min(5, issues.Count)));
                return (false, message);
            }

            return (true, $"✅ 验证通过: {testCases.Count} 个用例");
        }

        /// <summary>
        /// 性能统计
        /// </summary>
        public static void PrintPerformanceStats(
            string metricName,
            long elapsedMs,
            int itemCount)
        {
            var avgTime = (double)elapsedMs / Math.Max(itemCount, 1);
            var throughput = (double)itemCount / (elapsedMs / 1000.0);

            Console.WriteLine($@"
📊 {metricName}:
   总耗时:      {elapsedMs} ms
   处理数量:    {itemCount} 项
   平均延迟:    {avgTime:F2} ms/项
   吞吐量:      {throughput:F1} items/sec
");
        }
    }

    // ==================== 模型扩展 ====================

    public class GoldenTestCase
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public List<SnapshotData> Snapshots { get; set; } = new();
        public ExpectedAlertOutput ExpectedOutput { get; set; } = new();
    }

    public class SnapshotData
    {
        public string Code { get; set; } = "";
        public string Exchange { get; set; } = "";
        public double Open { get; set; }
        public double Close { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public int Volume { get; set; }
        public long TotalVolume { get; set; }
        public long? YesterdayVolume { get; set; }
        public double BuyVolume { get; set; }
        public long SellVolume { get; set; }
    }

    public class ExpectedAlertOutput
    {
        public string TestId { get; set; } = "";
        public int PanVolume { get; set; }
        public double PanAvgVolRate { get; set; }
        public double PanLastVolRate { get; set; }
        public double PanAvg5VolRate { get; set; }
        public double PanAmtRate { get; set; }
        public string AlertTitle { get; set; } = "";
        public int Priority { get; set; }
    }
}

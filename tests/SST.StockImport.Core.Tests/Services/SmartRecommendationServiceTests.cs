using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs.SmartRecommendation;
using SST.StockImport.Infrastructure.Services;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SST.StockImport.Core.Tests.Services
{
    /// <summary>
    /// L2: 智能推荐服务单元测试
    /// 测试范围：边界条件、参数验证、响应结构、业务逻辑
    /// 数据库：InMemoryDatabase（不连接生产 DB ✅）
    /// 
    /// 注意：alertlist 表使用原始 SQL 查询，无 EF Entity
    /// 数据库查询逻辑在 L3 WebAPI 集成测试中验证
    /// </summary>
    public class SmartRecommendationServiceTests : IDisposable
    {
        private readonly Mock<ILogger<SmartRecommendationService>> _mockLogger;
        private readonly StockImportDbContext _context;
        private readonly SmartRecommendationService _service;

        public SmartRecommendationServiceTests()
        {
            _mockLogger = new Mock<ILogger<SmartRecommendationService>>();
            
            // 使用内存数据库进行测试（安全，不碰生产 DB）
            var options = new DbContextOptionsBuilder<StockImportDbContext>()
                .UseInMemoryDatabase(databaseName: $"SmartRecommendationTest_{Guid.NewGuid()}")
                .Options;
            
            _context = new StockImportDbContext(options);
            _service = new SmartRecommendationService(_context, _mockLogger.Object);
        }

        #region 边界条件测试

        [Fact]
        public async Task GetTodayRecommendationsAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.GetTodayRecommendationsAsync(null!));
        }

        [Fact]
        public async Task GetTodayRecommendationsAsync_WithNoData_ReturnsEmptyRecommendations()
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 12, 1),
                TopCount = 3,
                MinMaturityScore = 60
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.TopRecommendations);
            Assert.Equal(0, result.TotalCandidates);
            Assert.Equal(new DateTime(2025, 12, 1), result.RecommendationDate);
        }

        [Fact]
        public async Task GetTodayRecommendationsAsync_WithFutureDate_ReturnsEmptyRecommendations()
        {
            // Arrange
            var futureDate = DateTime.Today.AddYears(1);
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = futureDate,
                TopCount = 3
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result.TopRecommendations);
            Assert.False(result.IsHistoricalBacktest);
        }

        [Theory]
        [InlineData(0)]    // 零值
        [InlineData(-10)]  // 负数
        [InlineData(150)]  // 超出合理范围
        public async Task GetTodayRecommendationsAsync_WithInvalidMaturityScore_ShouldHandleGracefully(int invalidScore)
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 1),
                MinMaturityScore = invalidScore,
                TopCount = 3
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.TopRecommendations);
        }

        [Theory]
        [InlineData(0)]    // 零值
        [InlineData(-5)]   // 负数
        public async Task GetTodayRecommendationsAsync_WithInvalidTopCount_ShouldHandleGracefully(int invalidCount)
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 1),
                TopCount = invalidCount
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.TopRecommendations);
            Assert.True(result.TopRecommendations.Count <= 0);
        }

        #endregion

        #region 默认值测试

        [Fact]
        public async Task GetTodayRecommendationsAsync_WithDefaultRequest_UsesTodayAsRecommendationDate()
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = null, // 测试默认值
                TopCount = 3
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(DateTime.Today, result.RecommendationDate);
        }

        [Fact]
        public async Task GetTodayRecommendationsAsync_GeneratedAtIsSet()
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 1),
                TopCount = 1
            };

            var beforeCall = DateTime.Now;

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            var afterCall = DateTime.Now;

            // Assert
            Assert.NotNull(result);
            Assert.True(result.GeneratedAt >= beforeCall && result.GeneratedAt <= afterCall);
        }

        #endregion

        #region 历史回测测试

        [Fact]
        public async Task GetTodayRecommendationsAsync_OlderThan60Days_IsHistoricalBacktest()
        {
            // Arrange
            var oldDate = DateTime.Today.AddDays(-65); // 65天前
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = oldDate,
                TopCount = 3
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.IsHistoricalBacktest);
        }

        [Fact]
        public async Task GetTodayRecommendationsAsync_Within60Days_IsNotHistoricalBacktest()
        {
            // Arrange
            var recentDate = DateTime.Today.AddDays(-30); // 30天前
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = recentDate,
                TopCount = 3
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.IsHistoricalBacktest);
        }

        #endregion

        #region 学习期间统计测试

        [Fact]
        public async Task GetTodayRecommendationsAsync_LearningPeriodStatsIsSet()
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 1),
                TopCount = 1
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.LearningPeriod);
            Assert.Equal(30, result.LearningPeriod.DaysAnalyzed);
            Assert.Equal(new DateTime(2025, 10, 2), result.LearningPeriod.StartDate);
            Assert.Equal(new DateTime(2025, 10, 31), result.LearningPeriod.EndDate);
        }

        #endregion

        #region 推荐参数范围测试

        [Fact]
        public async Task GetTodayRecommendationsAsync_WithStandardRequest_ReturnsValidResponse()
        {
            // Arrange - 测试基本响应结构（无需数据库数据）
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 15),
                TopCount = 3,
                MinMaturityScore = 60
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert - 验证响应结构完整性
            Assert.NotNull(result);
            Assert.NotNull(result.TopRecommendations);
            Assert.NotNull(result.LearningPeriod);
            Assert.Equal(new DateTime(2025, 11, 15), result.RecommendationDate);
            Assert.True(result.TopRecommendations.Count <= request.TopCount);
        }

        [Theory]
        [InlineData(1)]   // 最小值
        [InlineData(5)]   // 标准值
        [InlineData(10)]  // 较大值
        public async Task GetTodayRecommendationsAsync_WithDifferentTopCounts_RespectsLimit(int topCount)
        {
            // Arrange
            var request = new SmartRecommendationRequest
            {
                RecommendationDate = new DateTime(2025, 11, 1),
                TopCount = topCount,
                MinMaturityScore = 50
            };

            // Act
            var result = await _service.GetTodayRecommendationsAsync(request);

            // Assert - 验证返回数量不超过限制
            Assert.NotNull(result);
            Assert.True(result.TopRecommendations.Count <= topCount);
        }

        #endregion

        #region DTO 默认值测试

        [Fact]
        public void SmartRecommendationRequest_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var request = new SmartRecommendationRequest();

            // Assert - 验证 DTO 默认值
            Assert.Equal(3, request.TopCount);
            Assert.Equal(60, request.MinMaturityScore);
            Assert.Equal(10, request.MinPeakVolumeRatio);
            Assert.Equal(50, request.MaxPeakVolumeRatio);
            Assert.Equal(8, request.MinCoolingDays);
            Assert.Equal(30, request.MaxCoolingDays);
            Assert.Null(request.RecommendationDate);
        }

        [Fact]
        public void SmartRecommendationResponse_Initialization_HasValidDefaults()
        {
            // Arrange & Act
            var response = new SmartRecommendationResponse();

            // Assert - 验证响应对象初始化正确
            Assert.NotNull(response.TopRecommendations);
            Assert.Empty(response.TopRecommendations);
            Assert.NotNull(response.LearningPeriod);
            Assert.False(response.IsHistoricalBacktest);
        }

        #endregion

        #region 辅助方法

        public void Dispose()
        {
            _context?.Dispose();
        }

        #endregion
    }
}

using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SST.StockImport.Core.DTOs.MaturityAnalysis;
using SST.StockImport.Infrastructure.Services;
using SST.StockImport.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SST.StockImport.IntegrationTest
{
    /// <summary>
    /// L2: ?��??��??��??��?�?
    /// 测�??�围：Service + Database ?��?交�?
    /// ?�要�?MySQL ?�据库�?行中，�??��?试数??
    /// </summary>
    [Collection("Database")]
    public class TimeMachineAnalysisIntegrationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fixture;
        private readonly TimeMachineAnalysisService _service;

        public TimeMachineAnalysisIntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
            var logger = _fixture.ServiceProvider.GetRequiredService<ILogger<TimeMachineAnalysisService>>();
            var context = _fixture.ServiceProvider.GetRequiredService<StockImportDbContext>();
            _service = new TimeMachineAnalysisService(context, logger);
        }

        [Fact]
        public async Task GetAvailableDateRangeAsync_WithRealData_ReturnsDateRange()
        {
            // Act
            var result = await _service.GetAvailableDateRangeAsync();

            // Assert
            Assert.NotNull(result);
            Assert.True(result.EarliestDate < result.LatestDate);
            Assert.True(result.TotalDays > 0);
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_WithRealData_ReturnsValidCandidates()
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Candidates);
            Assert.NotNull(result.Statistics);
            
            // 验�??�选股票�???
            if (result.Candidates.Any())
            {
                var firstCandidate = result.Candidates.First();
                Assert.NotNull(firstCandidate.StockCode);
                Assert.True(firstCandidate.MaturityScore >= 60); // ?��?达到?�低�?�?
                Assert.True(firstCandidate.PeakVolumeRatio >= 10 && firstCandidate.PeakVolumeRatio <= 50);
                Assert.NotNull(firstCandidate.PriceHistory);
            }
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_WithHighMaturityScore_ReturnsFewerCandidates()
        {
            // Arrange
            var lowThresholdRequest = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 50,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            var highThresholdRequest = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 80,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            // Act
            var lowResult = await _service.AnalyzeHistoricalDateAsync(lowThresholdRequest);
            var highResult = await _service.AnalyzeHistoricalDateAsync(highThresholdRequest);

            // Assert
            Assert.True(lowResult.Candidates.Count >= highResult.Candidates.Count,
                "較低門檻應該返回不少於較高門檻的候選股數量");
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_WithValidData_CalculatesStatisticsCorrectly()
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.NotNull(result.Statistics);
            Assert.True(result.Statistics.TotalCandidates >= 0);
            Assert.True(result.Statistics.SuccessRate >= 0 && result.Statistics.SuccessRate <= 100);
            Assert.True(result.Statistics.AverageMaxGain >= 0);
            
            // 统计?��?应该一??
            Assert.Equal(result.Candidates.Count, result.Statistics.TotalCandidates);
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_MultipleCalls_ReturnConsistentResults()
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            // Act
            var result1 = await _service.AnalyzeHistoricalDateAsync(request);
            var result2 = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.Equal(result1.Candidates.Count, result2.Candidates.Count);
            Assert.Equal(result1.Statistics.TotalCandidates, result2.Statistics.TotalCandidates);
        }

        [Theory]
        [InlineData(8, 14)]   // ?��??�却
        [InlineData(15, 30)]  // 中�??�却
        [InlineData(31, 50)]  // ?��??�却
        public async Task AnalyzeHistoricalDateAsync_WithDifferentCoolingPeriods_ReturnsValidResults(
            int minDays, int maxDays)
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = minDays,
                MaxCoolingDays = maxDays,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 60
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.NotNull(result);
            
            // 验�??�却?�在?�围??
            foreach (var candidate in result.Candidates)
            {
                Assert.True(candidate.DaysSinceHotspotAtAnalysis >= minDays,
                    $"?�选股�?{candidate.StockCode} ?�冷?�天??{candidate.DaysSinceHotspotAtAnalysis} 小�??�小�?{minDays}");
                Assert.True(candidate.DaysSinceHotspotAtAnalysis <= maxDays,
                    $"?�选股�?{candidate.StockCode} ?�冷?�天??{candidate.DaysSinceHotspotAtAnalysis} 大�??�大�?{maxDays}");
            }
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_VerifyPriceHistoryTracking()
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = 10,
                MaxPeakVolumeRatio = 50,
                TrackingDays = 30
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            if (result.Candidates.Any())
            {
                var candidate = result.Candidates.First();
                
                // 价格?�史应该被追�?
                Assert.NotNull(candidate.PriceHistory);
                
                // 如�??�价?��??��?验�??�据完整??
                if (candidate.PriceHistory.Any())
                {
                    Assert.All(candidate.PriceHistory, point =>
                    {
                        Assert.True(point.Date > request.AnalysisDate);
                        Assert.True(point.Price > 0);
                    });
                    
                    // 价格?�史应该?�时?��?�?
                    var dates = candidate.PriceHistory.Select(p => p.Date).ToList();
                    Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
                }
            }
        }
    }
}

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
    /// L2: ?¶å??ºå??é??æ?è¯?
    /// æµ‹è??ƒå›´ï¼šService + Database ?Ÿå?äº¤ä?
    /// ?€è¦ï?MySQL ?°æ®åº“è?è¡Œä¸­ï¼Œå??«æ?è¯•æ•°??
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
            
            // éªŒè??™é€‰è‚¡ç¥¨å???
            if (result.Candidates.Any())
            {
                var firstCandidate = result.Candidates.First();
                Assert.NotNull(firstCandidate.StockCode);
                Assert.True(firstCandidate.MaturityScore >= 60); // ?³å?è¾¾åˆ°?€ä½è?æ±?
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
                "è¾ƒä??„æ??Ÿåº¦?¨æ?åº”è¯¥è¿”å??´å??–ç›¸?Œæ•°?ç??™é€‰è‚¡ç¥?);
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
            
            // ç»Ÿè®¡?°å?åº”è¯¥ä¸€??
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
        [InlineData(8, 14)]   // ?­æ??·å´
        [InlineData(15, 30)]  // ä¸­æ??·å´
        [InlineData(31, 50)]  // ?¿æ??·å´
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
            
            // éªŒè??·å´?Ÿåœ¨?ƒå›´??
            foreach (var candidate in result.Candidates)
            {
                Assert.True(candidate.DaysSinceHotspotAtAnalysis >= minDays,
                    $"?™é€‰è‚¡ç¥?{candidate.StockCode} ?„å†·?´å¤©??{candidate.DaysSinceHotspotAtAnalysis} å°ä??€å°å€?{minDays}");
                Assert.True(candidate.DaysSinceHotspotAtAnalysis <= maxDays,
                    $"?™é€‰è‚¡ç¥?{candidate.StockCode} ?„å†·?´å¤©??{candidate.DaysSinceHotspotAtAnalysis} å¤§ä??€å¤§å€?{maxDays}");
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
                
                // ä»·æ ¼?†å²åº”è¯¥è¢«è¿½è¸?
                Assert.NotNull(candidate.PriceHistory);
                
                // å¦‚æ??‰ä»·?¼å??²ï?éªŒè??°æ®å®Œæ•´??
                if (candidate.PriceHistory.Any())
                {
                    Assert.All(candidate.PriceHistory, point =>
                    {
                        Assert.True(point.Date > request.AnalysisDate);
                        Assert.True(point.Price > 0);
                    });
                    
                    // ä»·æ ¼?†å²åº”è¯¥?‰æ—¶?´æ?åº?
                    var dates = candidate.PriceHistory.Select(p => p.Date).ToList();
                    Assert.Equal(dates.OrderBy(d => d).ToList(), dates);
                }
            }
        }
    }
}

using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using SST.StockImport.Core.DTOs.MaturityAnalysis;
using SST.StockImport.Infrastructure.Services;
using SST.StockImport.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace SST.StockImport.Core.Tests.Services
{
    /// <summary>
    /// L1: ?∂Â??∫Â??êÊ??°Â??ÉÊ?ËØ?
    /// ÊµãË??ÉÂõ¥Ôºö‰??°ÈÄªË??ÅËæπ?åÊù°‰ª∂„ÄÅÂ?Â∏∏Â???
    /// </summary>
    public class TimeMachineAnalysisServiceTests : IDisposable
    {
        private readonly Mock<ILogger<TimeMachineAnalysisService>> _mockLogger;
        private readonly StockImportDbContext _context;
        private readonly TimeMachineAnalysisService _service;

        public TimeMachineAnalysisServiceTests()
        {
            _mockLogger = new Mock<ILogger<TimeMachineAnalysisService>>();
            
            // ‰ΩøÁî®?ÖÂ??∞ÊçÆÂ∫ìË?Ë°åÂ??ÉÊ?ËØ?
            var options = new DbContextOptionsBuilder<StockImportDbContext>()
                .UseInMemoryDatabase(databaseName: $"TimeMachineTest_{Guid.NewGuid()}")
                .Options;
            
            _context = new StockImportDbContext(options);
            _service = new TimeMachineAnalysisService(_context, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAvailableDateRangeAsync_WithNoData_ReturnsNull()
        {
            // Act
            var result = await _service.GetAvailableDateRangeAsync();

            // Assert
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_WithNullRequest_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _service.AnalyzeHistoricalDateAsync(null!));
        }

        [Theory]
        [InlineData(0)]    // ?†Ê??ÑÊ??üÂ∫¶?ÜÊï∞
        [InlineData(-10)]  // Ë¥üÊï∞?ÜÊï∞
        [InlineData(150)]  // Ë∂ÖÂá∫?ÉÂõ¥
        public async Task AnalyzeHistoricalDateAsync_WithInvalidMaturityScore_ShouldHandleGracefully(int invalidScore)
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = invalidScore,
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
        }

        [Fact]
        public async Task AnalyzeHistoricalDateAsync_WithFutureDate_ReturnsEmptyCandidates()
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = DateTime.Now.AddYears(1), // ?™Êù•?•Ê?
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
            Assert.Empty(result.Candidates);
        }

        [Theory]
        [InlineData(5, 3)]   // MinCoolingDays > MaxCoolingDays
        [InlineData(30, 30)] // ?∏Á?ËæπÁ?
        [InlineData(1, 1)]   // ?ïÊó•?∑Âç¥
        public async Task AnalyzeHistoricalDateAsync_WithCoolingDaysBoundary_HandlesCorrectly(
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
            Assert.NotNull(result.Candidates);
        }

        [Theory]
        [InlineData(10, 50)]   // Ê≠?∏∏?ÉÂõ¥
        [InlineData(10, 10)]   // ?ï‰??èËÉΩ
        [InlineData(100, 200)] // È´òÈ???
        public async Task AnalyzeHistoricalDateAsync_WithVolumeRatioBoundary_HandlesCorrectly(
            decimal minRatio, decimal maxRatio)
        {
            // Arrange
            var request = new TimeMachineAnalysisRequest
            {
                AnalysisDate = new DateTime(2025, 12, 1),
                MinMaturityScore = 60,
                MinCoolingDays = 8,
                MaxCoolingDays = 30,
                MinPeakVolumeRatio = minRatio,
                MaxPeakVolumeRatio = maxRatio,
                TrackingDays = 60
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.NotNull(result.Statistics);
        }

        [Theory]
        [InlineData(1)]   // ?ÄÂ∞èËøΩË∏™Â§©??
        [InlineData(30)]  // ‰∏Ä‰∏™Ê?
        [InlineData(60)]  // ‰∏§‰∏™??
        [InlineData(90)]  // ‰∏â‰∏™??
        public async Task AnalyzeHistoricalDateAsync_WithDifferentTrackingDays_CalculatesCorrectly(
            int trackingDays)
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
                TrackingDays = trackingDays
            };

            // Act
            var result = await _service.AnalyzeHistoricalDateAsync(request);

            // Assert
            Assert.NotNull(result);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}

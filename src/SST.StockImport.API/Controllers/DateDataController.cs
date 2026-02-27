using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.DTOs;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DateDataController : ControllerBase
{
    private readonly IDateDataService _dateDataService;
    private readonly ILogger<DateDataController> _logger;

    public DateDataController(IDateDataService dateDataService, ILogger<DateDataController> logger)
    {
        _dateDataService = dateDataService;
        _logger = logger;
    }

    [HttpGet("available-dates")]
    public async Task<ActionResult<DateRangeDto>> GetAvailableDates()
    {
        var result = await _dateDataService.GetAvailableDateRangeAsync();
        return Ok(result);
    }

    [HttpGet("{date:datetime}")]
    public async Task<ActionResult<DateDataSummaryDto>> GetDateData(DateTime date)
    {
        try
        {
            var result = await _dateDataService.GetDateDataSummaryAsync(date);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching date data for {Date}", date);
            return StatusCode(500, new { message = ex.Message });
        }
    }
}

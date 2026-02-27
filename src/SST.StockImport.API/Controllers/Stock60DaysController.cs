using Microsoft.AspNetCore.Mvc;
using SST.StockImport.Core.Interfaces;

namespace SST.StockImport.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class Stock60DaysController : ControllerBase
{
    private readonly IStock60DaysRecalcService _recalcService;
    private readonly ILogger<Stock60DaysController> _logger;

    public Stock60DaysController(
        IStock60DaysRecalcService recalcService,
        ILogger<Stock60DaysController> logger)
    {
        _recalcService = recalcService;
        _logger = logger;
    }

    [HttpPost("recalc")]
    public async Task<ActionResult<Stock60DaysRecalcResult>> Recalculate([FromBody] Stock60DaysRecalcRequest request)
    {
        try
        {
            _logger.LogInformation("收到 Stock60Days 重算請求: StartLastDate={StartLastDate}, Days={Days}", 
                request.StartLastDate, request.Days);

            var result = await _recalcService.RecalculateAsync(request.StartLastDate, request.Days);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stock60Days 重算發生錯誤");
            return StatusCode(500, new { Error = ex.Message });
        }
    }

    [HttpPost("cancel")]
    public ActionResult Cancel()
    {
        _recalcService.Cancel();
        return Ok(new { Message = "取消請求已發送" });
    }

    [HttpGet("progress")]
    public ActionResult<Stock60DaysRecalcProgress> GetProgress()
    {
        var progress = _recalcService.GetProgress();
        return Ok(progress);
    }
}

public class Stock60DaysRecalcRequest
{
    public DateTime StartLastDate { get; set; }
    public int Days { get; set; }
}

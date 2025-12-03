namespace SST.StockImport.Web.Services;

/// <summary>
/// 統一的錯誤處理服務 - 提供一致的錯誤處理和用戶友好的錯誤訊息
/// </summary>
public class ErrorHandlingService
{
    private readonly ExecutionLogService _logService;
    private readonly ILogger<ErrorHandlingService> _logger;

    public ErrorHandlingService(ExecutionLogService logService, ILogger<ErrorHandlingService> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    /// <summary>
    /// 處理並格式化例外錯誤
    /// </summary>
    public string HandleException(Exception exception, string operationName)
    {
        _logger.LogError(exception, "{OperationName} 發生例外", operationName);
        
        var userFriendlyMessage = GetUserFriendlyMessage(exception, operationName);
        _logService.AddLog($"❌ {operationName}失敗：{userFriendlyMessage}", LogLevel.Error);
        
        return userFriendlyMessage;
    }

    /// <summary>
    /// 處理 API 呼叫錯誤
    /// </summary>
    public string HandleApiError(HttpRequestException httpException, string apiEndpoint)
    {
        _logger.LogError(httpException, "API 呼叫失敗：{Endpoint}", apiEndpoint);
        
        var message = httpException.Message switch
        {
            var msg when msg.Contains("timeout") => "API 呼叫超時，請稍後再試",
            var msg when msg.Contains("404") => "API 端點不存在，請檢查設定",
            var msg when msg.Contains("500") => "伺服器內部錯誤，請聯繫系統管理員",
            var msg when msg.Contains("401") || msg.Contains("403") => "API 授權失敗，請檢查權限設定",
            _ => $"API 呼叫失敗：{httpException.Message}"
        };

        _logService.AddLog($"❌ API 錯誤 ({apiEndpoint})：{message}", LogLevel.Error);
        return message;
    }

    /// <summary>
    /// 處理操作取消錯誤
    /// </summary>
    public string HandleCancellation(OperationCanceledException cancellationException, string operationName)
    {
        _logger.LogWarning("操作被取消：{OperationName}", operationName);
        
        var message = $"{operationName}被使用者取消";
        _logService.AddLog($"⚠️ {message}", LogLevel.Warning);
        
        return message;
    }

    /// <summary>
    /// 處理資料驗證錯誤
    /// </summary>
    public string HandleValidationError(string validationMessage, string operationName)
    {
        _logger.LogWarning("{OperationName} 資料驗證失敗：{ValidationMessage}", operationName, validationMessage);
        
        var message = $"{operationName}資料驗證失敗：{validationMessage}";
        _logService.AddLog($"⚠️ {message}", LogLevel.Warning);
        
        return message;
    }

    /// <summary>
    /// 記錄成功操作
    /// </summary>
    public void LogSuccess(string operationName, string details = "")
    {
        var message = string.IsNullOrEmpty(details) 
            ? $"{operationName}執行成功" 
            : $"{operationName}執行成功：{details}";
            
        _logger.LogInformation("{OperationName} 執行成功：{Details}", operationName, details);
        _logService.AddLog($"✅ {message}", LogLevel.Information);
    }

    /// <summary>
    /// 記錄警告訊息
    /// </summary>
    public void LogWarning(string operationName, string warningMessage)
    {
        _logger.LogWarning("{OperationName} 警告：{WarningMessage}", operationName, warningMessage);
        _logService.AddLog($"⚠️ {operationName}警告：{warningMessage}", LogLevel.Warning);
    }

    /// <summary>
    /// 取得用戶友好的錯誤訊息
    /// </summary>
    private string GetUserFriendlyMessage(Exception exception, string operationName)
    {
        return exception switch
        {
            HttpRequestException httpEx => HandleApiError(httpEx, "API"),
            OperationCanceledException cancelEx => HandleCancellation(cancelEx, operationName),
            TimeoutException => $"{operationName}執行超時，請稍後再試",
            UnauthorizedAccessException => $"{operationName}權限不足，請聯繫管理員",
            ArgumentException argEx => $"{operationName}參數錯誤：{argEx.Message}",
            InvalidOperationException invEx => $"{operationName}操作無效：{invEx.Message}",
            NotSupportedException => $"{operationName}目前不支援此操作",
            _ => $"{operationName}發生未預期的錯誤：{exception.Message}"
        };
    }

    /// <summary>
    /// 執行帶有錯誤處理的操作
    /// </summary>
    public async Task<T?> ExecuteWithErrorHandling<T>(
        Func<Task<T>> operation,
        string operationName,
        T? fallbackValue = default)
    {
        try
        {
            var result = await operation();
            LogSuccess(operationName);
            return result;
        }
        catch (Exception ex)
        {
            HandleException(ex, operationName);
            return fallbackValue;
        }
    }

    /// <summary>
    /// 執行帶有錯誤處理的無回傳值操作
    /// </summary>
    public async Task<bool> ExecuteWithErrorHandling(
        Func<Task> operation,
        string operationName)
    {
        try
        {
            await operation();
            LogSuccess(operationName);
            return true;
        }
        catch (Exception ex)
        {
            HandleException(ex, operationName);
            return false;
        }
    }
}
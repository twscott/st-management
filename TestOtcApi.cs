using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace TestOtcApi
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var today = DateTime.Now;
            var rocYear = today.Year - 1911;
            var dateStr = $"{rocYear}/{today.Month:D2}/{today.Day:D2}";

            Console.WriteLine($"測試日期：{dateStr}");

            var url = $"https://www.tpex.org.tw/web/stock/aftertrading/daily_trading_info/st43_result.php?l=zh-tw&d={dateStr}&_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            Console.WriteLine($"URL: {url}");
            Console.WriteLine();

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            try
            {
                var response = await httpClient.GetStringAsync(url);
                Console.WriteLine("API 回應（前500字）：");
                Console.WriteLine(response.Substring(0, Math.Min(500, response.Length)));
                Console.WriteLine();
                
                var jsonDoc = JsonDocument.Parse(response);
                
                // 檢查有哪些屬性
                Console.WriteLine("JSON 屬性：");
                foreach (var prop in jsonDoc.RootElement.EnumerateObject())
                {
                    Console.WriteLine($"  - {prop.Name}: {prop.Value.ValueKind}");
                }
                
                // 嘗試取得 aaData
                if (jsonDoc.RootElement.TryGetProperty("aaData", out var aaData))
                {
                    Console.WriteLine($"\naaData 筆數: {aaData.GetArrayLength()}");
                    if (aaData.GetArrayLength() > 0)
                    {
                        Console.WriteLine($"第一筆：{aaData[0]}");
                    }
                }
                else
                {
                    Console.WriteLine("\n❌ 沒有 aaData 屬性");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤：{ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
            }
        }
    }
}

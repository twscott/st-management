using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SST.LoadTest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SST Stock Import API 真實環境負載測試 ===");
            Console.WriteLine($"開始時間: {DateTime.Now}");
            Console.WriteLine();

            var httpClient = new HttpClient
            {
                BaseAddress = new Uri("http://localhost:5008"),
                Timeout = TimeSpan.FromHours(2) // 2 小時超時
            };

            var request = new
            {
                TargetDate = DateTime.Today.ToString("yyyy-MM-dd")
            };

            var jsonContent = JsonSerializer.Serialize(request);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Console.WriteLine($"請求內容: {jsonContent}");
            Console.WriteLine("正在發送 API 請求...");
            Console.WriteLine();

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var response = await httpClient.PostAsync("/api/supplement/process-all", httpContent);
                stopwatch.Stop();

                Console.WriteLine($"✅ API 回應狀態: {response.StatusCode}");
                Console.WriteLine($"⏱️ 執行時間: {stopwatch.Elapsed}");
                Console.WriteLine($"📅 完成時間: {DateTime.Now}");

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"📊 回應內容長度: {responseContent.Length} 字元");
                    
                    // 嘗試解析回應以獲取處理器資訊
                    try
                    {
                        using var doc = JsonDocument.Parse(responseContent);
                        if (doc.RootElement.TryGetProperty("processorResults", out var processorResults))
                        {
                            Console.WriteLine($"🔧 處理器數量: {processorResults.GetArrayLength()}");
                        }
                        
                        if (doc.RootElement.TryGetProperty("success", out var success))
                        {
                            Console.WriteLine($"✅ 處理成功: {success.GetBoolean()}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"⚠️ JSON 解析錯誤: {ex.Message}");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"❌ 錯誤內容: {errorContent}");
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                stopwatch.Stop();
                Console.WriteLine($"⏰ 請求超時!");
                Console.WriteLine($"⏱️ 執行時間: {stopwatch.Elapsed}");
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"🌐 HTTP 請求錯誤: {ex.Message}");
                Console.WriteLine($"⏱️ 執行時間: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Console.WriteLine($"💥 未預期錯誤: {ex.Message}");
                Console.WriteLine($"⏱️ 執行時間: {stopwatch.Elapsed}");
            }

            Console.WriteLine();
            Console.WriteLine("測試完成，按任意鍵退出...");
            Console.ReadKey();
        }
    }
}
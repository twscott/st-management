using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System;
using System.IO;
using System.Threading;

namespace GoodInfoDiagnostic
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("GoodInfo 頁面診斷工具");
            
            var options = new ChromeOptions();
            // 不使用 headless 模式，讓我們能看到實際頁面
            // options.AddArgument("--headless");
            options.AddArgument("--disable-web-security");
            options.AddArgument("--disable-features=VizDisplayCompositor");
            
            using var driver = new ChromeDriver(options);
            
            try
            {
                string url = "https://goodinfo.tw/tw/StockList.asp?MARKET_CAT=熱門排行&INDUSTRY_CAT=券資比#txtStockListData";
                Console.WriteLine($"正在載入頁面: {url}");
                
                driver.Navigate().GoToUrl(url);
                
                // 等待頁面完全載入
                Thread.Sleep(10000);
                
                Console.WriteLine("頁面標題: " + driver.Title);
                Console.WriteLine("當前 URL: " + driver.Url);
                
                // 搜尋所有input元素
                var inputs = driver.FindElements(By.TagName("input"));
                Console.WriteLine($"\n找到 {inputs.Count} 個 input 元素:");
                
                for (int i = 0; i < inputs.Count; i++)
                {
                    try
                    {
                        var input = inputs[i];
                        Console.WriteLine($"Input {i}: type={input.GetAttribute("type")}, value={input.GetAttribute("value")}, onclick={input.GetAttribute("onclick")}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Input {i}: Error reading - {ex.Message}");
                    }
                }
                
                // 搜尋包含"下載"文字的元素
                var elementsWithDownload = driver.FindElements(By.XPath("//*[contains(text(), '下載') or contains(@value, '下載') or contains(@onclick, '下載') or contains(@onclick, 'download')]"));
                Console.WriteLine($"\n找到 {elementsWithDownload.Count} 個包含'下載'的元素:");
                
                foreach (var element in elementsWithDownload)
                {
                    try
                    {
                        Console.WriteLine($"元素: {element.TagName}, text={element.Text}, value={element.GetAttribute("value")}, onclick={element.GetAttribute("onclick")}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"元素讀取錯誤: {ex.Message}");
                    }
                }
                
                // 檢查是否有 iframe
                var iframes = driver.FindElements(By.TagName("iframe"));
                Console.WriteLine($"\n找到 {iframes.Count} 個 iframe");
                
                Console.WriteLine("\n按任意鍵繼續...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"錯誤: {ex.Message}");
            }
            finally
            {
                driver.Quit();
            }
        }
    }
}
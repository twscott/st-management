using Xunit;

namespace GoodInfo19LinksTest;

/// <summary>
/// GoodInfo 測試輔助類別
/// 提供 public API 供外部（如 Web API）調用，內部使用已測試通過的私有方法
/// ⚠️ 重要：本類別不修改任何測試邏輯，只是提供 public 接口
/// </summary>
public class GoodInfoTestHelper
{
    private readonly GoodInfo19LinksTests _tests;

    public GoodInfoTestHelper()
    {
        _tests = new GoodInfo19LinksTests();
    }

    /// <summary>
    /// 執行單一連結測試
    /// </summary>
    /// <param name="linkId">連結 ID (1-19)</param>
    /// <returns>(success, errorMessage)</returns>
    public (bool success, string errorMessage) TestLink(int linkId)
    {
        try
        {
            // 根據 linkId 執行對應的測試方法
            switch (linkId)
            {
                case 1: _tests.Test_01_券資比_Should_Success(); break;
                case 2: _tests.Test_02_周轉率_Should_Success(); break;
                case 3: _tests.Test_03_MACD負轉正_Should_Success(); break;
                case 4: _tests.Test_04_OSC負轉正_Should_Success(); break;
                case 5: _tests.Test_05_EPS創新高_Should_Success(); break;
                case 6: _tests.Test_06_投信連買_Should_Success(); break;
                case 7: _tests.Test_07_超布林上軌_Should_Success(); break;
                case 8: _tests.Test_08_外資連買連賣轉折_Should_Success(); break;
                case 9: _tests.Test_09_投信連買連賣轉折_Should_Success(); break;
                case 10: _tests.Test_10_五年新高_Should_Success(); break;
                case 11: _tests.Test_11_外資連賣_Should_Success(); break;
                case 12: _tests.Test_12_投信連賣_Should_Success(); break;
                case 13: _tests.Test_13_外資投信同步買超_Should_Success(); break;
                case 14: _tests.Test_14_月季黃金_Should_Success(); break;
                case 15: _tests.Test_15_歷史成交量_Should_Success(); break;
                case 16: _tests.Test_16_季營收創高_Should_Success(); break;
                case 17: _tests.Test_17_財報評分_Should_Success(); break;
                case 18: _tests.Test_18_外資投信同步賣超_Should_Success(); break;
                case 19: _tests.Test_19_外資連買_Should_Success(); break;
                default:
                    return (false, $"無效的 linkId: {linkId}，必須在 1-19 之間");
            }
            
            return (true, string.Empty);
        }
        catch (Xunit.Sdk.XunitException ex)
        {
            // xUnit 測試失敗會拋出 XunitException
            return (false, ex.Message);
        }
        catch (Exception ex)
        {
            return (false, $"測試執行錯誤: {ex.Message}");
        }
    }

    /// <summary>
    /// 取得連結名稱
    /// </summary>
    public static string GetLinkName(int linkId)
    {
        return linkId switch
        {
            1 => "券資比",
            2 => "周轉率",
            3 => "MACD負轉正",
            4 => "OSC負轉正",
            5 => "EPS創新高",
            6 => "投信連買",
            7 => "超布林上軌",
            8 => "外資連買連賣轉折",
            9 => "投信連買連賣轉折",
            10 => "五年新高",
            11 => "外資連賣",
            12 => "投信連賣",
            13 => "外資投信同步買超",
            14 => "月季黃金",
            15 => "歷史成交量",
            16 => "季營收創高",
            17 => "財報評分",
            18 => "外資投信同步賣超",
            19 => "外資連買",
            _ => $"未知連結 {linkId}"
        };
    }
}

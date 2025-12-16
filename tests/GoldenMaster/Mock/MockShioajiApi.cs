using System;
using System.Collections.Generic;
using System.Linq;

namespace SST.StockImport.Tests.GoldenMaster.Mock
{
    /// <summary>
    /// Mock Shioaji API - 用于单元测试和集成测试
    /// 返回符合真实 API 格式的动态对象
    /// </summary>
    public class MockShioajiApiBuilder
    {
        private Dictionary<string, MockSnapshot> _snapshots = new();

        public MockShioajiApiBuilder AddSnapshot(string code, double close, int volume, 
            long totalVolume, long yesterdayVolume, double open = 0, double high = 0, double low = 0)
        {
            if (high == 0) high = close * 1.02;
            if (low == 0) low = close * 0.98;

            var snapshot = new MockSnapshot
            {
                Code = code,
                Exchange = "TSE",
                Open = open > 0 ? open : close,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                TotalVolume = totalVolume,
                YesterdayVolume = yesterdayVolume,
                Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds()
            };

            _snapshots[code] = snapshot;
            return this;
        }

        /// <summary>
        /// 添加特殊场景：20倍量能
        /// </summary>
        public MockShioajiApiBuilder AddVolumeSpikeX20(string code,
            double basePrice = 100.0,
            long yesterdayVol = 900000)
        {
            var panVol = yesterdayVol * 20 / 3;  // 分盤量 = 20倍 / 3分钟
            return AddSnapshot(
                code,
                close: basePrice * 1.01,  // 小幅上升
                volume: (int)panVol,
                totalVolume: yesterdayVol * 25,
                yesterdayVolume: yesterdayVol,
                open: basePrice
            );
        }

        /// <summary>
        /// 添加特殊场景：10倍量能
        /// </summary>
        public MockShioajiApiBuilder AddVolumeSpikeX10(string code,
            double basePrice = 100.0,
            long yesterdayVol = 900000)
        {
            var panVol = yesterdayVol * 10 / 3;
            return AddSnapshot(
                code,
                close: basePrice * 1.005,
                volume: (int)panVol,
                totalVolume: yesterdayVol * 12,
                yesterdayVolume: yesterdayVol,
                open: basePrice
            );
        }

        /// <summary>
        /// 添加特殊场景：5倍量能
        /// </summary>
        public MockShioajiApiBuilder AddVolumeSpikeX5(string code,
            double basePrice = 100.0,
            long yesterdayVol = 900000)
        {
            var panVol = yesterdayVol * 5 / 3;
            return AddSnapshot(
                code,
                close: basePrice,
                volume: (int)panVol,
                totalVolume: yesterdayVol * 6,
                yesterdayVolume: yesterdayVol,
                open: basePrice
            );
        }

        /// <summary>
        /// 添加正常交易场景
        /// </summary>
        public MockShioajiApiBuilder AddNormalTrading(string code,
            double basePrice = 100.0,
            long yesterdayVol = 900000)
        {
            return AddSnapshot(
                code,
                close: basePrice,
                volume: (int)(yesterdayVol / 2),
                totalVolume: yesterdayVol,
                yesterdayVolume: yesterdayVol,
                open: basePrice
            );
        }

        /// <summary>
        /// 构建快照列表
        /// </summary>
        public List<dynamic> Build()
        {
            return _snapshots.Values.Select(s => s.ToDynamic()).ToList();
        }

        /// <summary>
        /// 获取所有快照的动态对象列表
        /// </summary>
        public List<dynamic> GetSnapshots()
        {
            return Build();
        }
    }

    /// <summary>
    /// Mock Snapshot 数据模型
    /// </summary>
    public class MockSnapshot
    {
        public string Code { get; set; } = "";
        public string Exchange { get; set; } = "TSE";
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public int Volume { get; set; }
        public long TotalVolume { get; set; }
        public long YesterdayVolume { get; set; }
        public long Timestamp { get; set; }

        /// <summary>
        /// 转换为动态对象（模拟 Shioaji 返回格式）
        /// </summary>
        public dynamic ToDynamic()
        {
            dynamic snapshot = new System.Dynamic.ExpandoObject();
            var dict = snapshot as IDictionary<string, object>;
            
            dict["Code"] = Code;
            dict["code"] = Code;
            dict["Exchange"] = Exchange;
            dict["exchange"] = Exchange;
            dict["Open"] = Open;
            dict["open"] = Open;
            dict["High"] = High;
            dict["high"] = High;
            dict["Low"] = Low;
            dict["low"] = Low;
            dict["Close"] = Close;
            dict["close"] = Close;
            dict["Volume"] = Volume;
            dict["volume"] = Volume;
            dict["TotalVolume"] = TotalVolume;
            dict["total_volume"] = TotalVolume;
            dict["YesterdayVolume"] = YesterdayVolume;
            dict["yesterday_volume"] = YesterdayVolume;
            dict["Timestamp"] = Timestamp;
            dict["ts"] = Timestamp * 1000000000;  // 转换为纳秒
            
            return snapshot;
        }
    }

    /// <summary>
    /// 预定义的测试场景
    /// </summary>
    public static class MockScenarios
    {
        public static List<MockSnapshot> NormalTradingDay()
        {
            return new()
            {
                new MockSnapshot
                {
                    Code = "2330",
                    Close = 650.0,
                    High = 652.0,
                    Low = 648.0,
                    Open = 650.5,
                    Volume = 450000,
                    TotalVolume = 18000000,
                    YesterdayVolume = 900000
                },
                new MockSnapshot
                {
                    Code = "2317",
                    Close = 45.5,
                    High = 46.0,
                    Low = 45.0,
                    Open = 45.3,
                    Volume = 2200000,
                    TotalVolume = 88000000,
                    YesterdayVolume = 44000000
                }
            };
        }

        public static List<MockSnapshot> VolumeSpike20x()
        {
            return new()
            {
                new MockSnapshot
                {
                    Code = "2330",
                    Close = 652.0,
                    High = 654.0,
                    Low = 650.0,
                    Open = 651.0,
                    Volume = 6000000,
                    TotalVolume = 720000000,
                    YesterdayVolume = 900000
                }
            };
        }

        public static List<MockSnapshot> VolumeSpikeX10()
        {
            return new()
            {
                new MockSnapshot
                {
                    Code = "2454",
                    Close = 35.0,
                    High = 35.5,
                    Low = 34.5,
                    Open = 34.8,
                    Volume = 3000000,
                    TotalVolume = 360000000,
                    YesterdayVolume = 900000
                }
            };
        }

        public static List<MockSnapshot> PriceJumpUp()
        {
            return new()
            {
                new MockSnapshot
                {
                    Code = "3008",
                    Close = 52.5,
                    High = 52.8,
                    Low = 50.0,
                    Open = 50.2,
                    Volume = 5000000,
                    TotalVolume = 200000000,
                    YesterdayVolume = 100000000
                }
            };
        }

        public static List<MockSnapshot> AllScenarios()
        {
            var all = new List<MockSnapshot>();
            all.AddRange(NormalTradingDay());
            all.AddRange(VolumeSpike20x());
            all.AddRange(VolumeSpikeX10());
            all.AddRange(PriceJumpUp());
            return all;
        }
    }

    /// <summary>
    /// 批量测试数据生成器
    /// </summary>
    public static class TestDataSetGenerator
    {
        public static List<MockSnapshot> GenerateStressTestData(int stockCount = 500)
        {
            var random = new Random(42);
            var snapshots = new List<MockSnapshot>();

            for (int i = 0; i < stockCount; i++)
            {
                var basePrice = 50 + random.Next(500);
                var volume = 1000000 + random.Next(5000000);

                snapshots.Add(new MockSnapshot
                {
                    Code = $"TST{i:D4}",
                    Exchange = random.Next(2) == 0 ? "TSE" : "OTC",
                    Open = basePrice,
                    High = basePrice * 1.02,
                    Low = basePrice * 0.98,
                    Close = basePrice * (0.99 + random.NextDouble() * 0.02),
                    Volume = (int)volume,
                    TotalVolume = volume * 20,
                    YesterdayVolume = volume,
                    Timestamp = DateTimeOffset.Now.AddSeconds(-random.Next(60)).ToUnixTimeSeconds()
                });
            }

            return snapshots;
        }

        public static List<MockSnapshot> GenerateWithVolumeDistribution(
            int normalCount = 100,
            int spikeX5Count = 10,
            int spikeX10Count = 5,
            int spikeX20Count = 2)
        {
            var snapshots = new List<MockSnapshot>();

            // 正常交易
            for (int i = 0; i < normalCount; i++)
            {
                snapshots.Add(new MockSnapshot
                {
                    Code = $"NRM{i:D3}",
                    Close = 100.0,
                    Volume = 500000,
                    TotalVolume = 10000000,
                    YesterdayVolume = 1000000
                });
            }

            // 5 倍量能
            for (int i = 0; i < spikeX5Count; i++)
            {
                snapshots.Add(new MockSnapshot
                {
                    Code = $"SP5{i:D3}",
                    Close = 101.0,
                    Volume = 1666666,
                    TotalVolume = 100000000,
                    YesterdayVolume = 1000000
                });
            }

            // 10 倍量能
            for (int i = 0; i < spikeX10Count; i++)
            {
                snapshots.Add(new MockSnapshot
                {
                    Code = $"SP10{i:D2}",
                    Close = 102.0,
                    Volume = 3333333,
                    TotalVolume = 200000000,
                    YesterdayVolume = 1000000
                });
            }

            // 20 倍量能
            for (int i = 0; i < spikeX20Count; i++)
            {
                snapshots.Add(new MockSnapshot
                {
                    Code = $"SP20{i:D2}",
                    Close = 103.0,
                    Volume = 6666666,
                    TotalVolume = 400000000,
                    YesterdayVolume = 1000000
                });
            }

            return snapshots;
        }
    }
}

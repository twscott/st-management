import pymysql
import pandas as pd
import numpy as np

# Database connection
conn = pymysql.connect(
    host='localhost',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

print("\n" + "="*100)
print("布林带验证 - 检查高绩效股票在推荐日的布林带状态")
print("="*100)

# 使用之前验证的高绩效股票
high_performers = [
    ('3163', '2025-10-06', '2025-11-03', 39.50),  # 涨幅39.50%
    ('3105', '2025-10-06', '2025-11-03', 30.93),  # 涨幅30.93%
    ('4939', '2025-10-06', '2025-11-03', 62.70),  # 涨幅62.70%
    ('6465', '2025-10-06', '2025-11-03', 83.11),  # 涨幅83.11%
    ('6584', '2025-10-06', '2025-11-03', 44.93),  # 涨幅44.93%
    ('4931', '2025-10-06', '2025-11-03', 24.28),  # 涨幅24.28%
]

def calculate_bollinger_bands(prices, period=20, std_dev=2):
    """计算布林带"""
    if len(prices) < period:
        return None, None, None, None
    
    # 中轨（MA20）
    middle_band = prices[-period:].mean()
    
    # 标准差
    std = prices[-period:].std()
    
    # 上轨和下轨
    upper_band = middle_band + (std_dev * std)
    lower_band = middle_band - (std_dev * std)
    
    # 带宽（Bandwidth）= (上轨 - 下轨) / 中轨 * 100
    bandwidth = (upper_band - lower_band) / middle_band * 100 if middle_band > 0 else 0
    
    return middle_band, upper_band, lower_band, bandwidth

results = []

for stock_code, hotspot_date, recommendation_date, gain in high_performers:
    print(f"\n{'='*100}")
    print(f"股票: {stock_code} | 热点日: {hotspot_date} | 推荐日: {recommendation_date} | 30天涨幅: +{gain:.2f}%")
    print(f"{'='*100}")
    
    # 查询热点日前30天到推荐日的价格数据
    query = """
    SELECT StockDate, EndPrice
    FROM stock60days
    WHERE StockID = %s
      AND StockDate BETWEEN DATE_SUB(%s, INTERVAL 40 DAY) AND %s
    ORDER BY StockDate ASC
    """
    
    df = pd.read_sql(query, conn, params=(stock_code, hotspot_date, recommendation_date))
    
    if df.empty or len(df) < 20:
        print("  ❌ 数据不足")
        continue
    
    # 转换日期列为日期类型（如果还不是）
    df['StockDate'] = pd.to_datetime(df['StockDate'])
    
    # 找出热点日和推荐日的索引
    hotspot_idx = df[df['StockDate'] == hotspot_date].index
    recommendation_idx = df[df['StockDate'] == recommendation_date].index
    
    # 如果精确匹配失败，尝试找最接近的日期
    if len(hotspot_idx) == 0:
        closest = df.iloc[(df['StockDate'] - pd.to_datetime(hotspot_date)).abs().argsort()[:1]]
        if len(closest) > 0:
            hotspot_idx = closest.index
            print(f"  ⚠️ 热点日使用最接近日期: {closest.iloc[0]['StockDate'].strftime('%Y-%m-%d')}")
    
    if len(recommendation_idx) == 0:
        closest = df.iloc[(df['StockDate'] - pd.to_datetime(recommendation_date)).abs().argsort()[:1]]
        if len(closest) > 0:
            recommendation_idx = closest.index
            print(f"  ⚠️ 推荐日使用最接近日期: {closest.iloc[0]['StockDate'].strftime('%Y-%m-%d')}")
    
    if len(hotspot_idx) == 0 or len(recommendation_idx) == 0:
        print(f"  ❌ 找不到对应日期数据")
        print(f"  可用日期范围: {df['StockDate'].min()} ~ {df['StockDate'].max()}")
        continue
    
    hotspot_idx = hotspot_idx[0]
    recommendation_idx = recommendation_idx[0]
    
    # 计算热点日的布林带（使用热点日前20天数据）
    if hotspot_idx >= 20:
        hotspot_prices = df.iloc[:hotspot_idx + 1]['EndPrice'].values
        hotspot_middle, hotspot_upper, hotspot_lower, hotspot_bandwidth = calculate_bollinger_bands(hotspot_prices)
        hotspot_price = df.iloc[hotspot_idx]['EndPrice']
        
        print(f"\n📊 热点日 ({hotspot_date}) 布林带状态:")
        print(f"  当前价格: ${hotspot_price:.2f}")
        print(f"  中轨 (MA20): ${hotspot_middle:.2f}")
        print(f"  上轨: ${hotspot_upper:.2f}")
        print(f"  下轨: ${hotspot_lower:.2f}")
        print(f"  带宽 (Bandwidth): {hotspot_bandwidth:.2f}%")
        
        # 判断价格位置
        if hotspot_price > hotspot_upper:
            position = "突破上轨 🔥"
        elif hotspot_price > hotspot_middle:
            position = f"上半部 ({((hotspot_price - hotspot_middle) / (hotspot_upper - hotspot_middle) * 100):.1f}%位置)"
        elif hotspot_price > hotspot_lower:
            position = f"下半部 ({((hotspot_price - hotspot_lower) / (hotspot_middle - hotspot_lower) * 100):.1f}%位置)"
        else:
            position = "突破下轨"
        
        print(f"  价格位置: {position}")
    else:
        hotspot_bandwidth = None
        print(f"\n📊 热点日数据不足")
    
    # 计算推荐日的布林带（使用推荐日前20天数据）
    if recommendation_idx >= 20:
        rec_prices = df.iloc[:recommendation_idx + 1]['EndPrice'].values
        rec_middle, rec_upper, rec_lower, rec_bandwidth = calculate_bollinger_bands(rec_prices)
        rec_price = df.iloc[recommendation_idx]['EndPrice']
        
        print(f"\n📊 推荐日 ({recommendation_date}) 布林带状态:")
        print(f"  当前价格: ${rec_price:.2f}")
        print(f"  中轨 (MA20): ${rec_middle:.2f}")
        print(f"  上轨: ${rec_upper:.2f}")
        print(f"  下轨: ${rec_lower:.2f}")
        print(f"  带宽 (Bandwidth): {rec_bandwidth:.2f}%")
        
        # 判断价格位置
        if rec_price > rec_upper:
            position = "突破上轨 🔥"
        elif rec_price > rec_middle:
            position = f"上半部 ({((rec_price - rec_middle) / (rec_upper - rec_middle) * 100):.1f}%位置)"
        elif rec_price > rec_lower:
            position = f"下半部 ({((rec_price - rec_lower) / (rec_middle - rec_lower) * 100):.1f}%位置)"
        else:
            position = "突破下轨"
        
        print(f"  价格位置: {position}")
        
        # 带宽变化
        if hotspot_bandwidth:
            bandwidth_change = rec_bandwidth - hotspot_bandwidth
            bandwidth_change_pct = (bandwidth_change / hotspot_bandwidth * 100) if hotspot_bandwidth > 0 else 0
            
            print(f"\n💡 布林带变化 (热点日 → 推荐日):")
            print(f"  带宽变化: {hotspot_bandwidth:.2f}% → {rec_bandwidth:.2f}% ({bandwidth_change:+.2f}%)")
            
            if bandwidth_change > 0:
                print(f"  状态: 📈 扩张中 (+{bandwidth_change_pct:.1f}%)")
            elif bandwidth_change < -1:
                print(f"  状态: 📉 收缩中 ({bandwidth_change_pct:.1f}%)")
            else:
                print(f"  状态: ➡️ 横盘整理")
            
            results.append({
                'stock_code': stock_code,
                'hotspot_bandwidth': hotspot_bandwidth,
                'recommendation_bandwidth': rec_bandwidth,
                'bandwidth_change': bandwidth_change,
                'bandwidth_change_pct': bandwidth_change_pct,
                'gain_30d': gain,
                'status': 'expanding' if bandwidth_change > 0 else 'contracting'
            })
    else:
        print(f"\n📊 推荐日数据不足")

# 统计分析
if results:
    df_results = pd.DataFrame(results)
    
    print("\n" + "="*100)
    print("统计分析：布林带 vs 涨幅")
    print("="*100)
    
    expanding_stocks = df_results[df_results['status'] == 'expanding']
    contracting_stocks = df_results[df_results['status'] == 'contracting']
    
    print(f"\n📈 布林带扩张股票:")
    print(f"  数量: {len(expanding_stocks)}/{len(df_results)}")
    if len(expanding_stocks) > 0:
        print(f"  平均涨幅: {expanding_stocks['gain_30d'].mean():.2f}%")
        print(f"  平均带宽变化: {expanding_stocks['bandwidth_change'].mean():.2f}%")
    
    print(f"\n📉 布林带收缩股票:")
    print(f"  数量: {len(contracting_stocks)}/{len(df_results)}")
    if len(contracting_stocks) > 0:
        print(f"  平均涨幅: {contracting_stocks['gain_30d'].mean():.2f}%")
        print(f"  平均带宽变化: {contracting_stocks['bandwidth_change'].mean():.2f}%")
    
    print(f"\n🔍 相关性分析:")
    correlation = df_results['bandwidth_change_pct'].corr(df_results['gain_30d'])
    print(f"  带宽变化 vs 涨幅 相关系数: {correlation:.4f}")
    
    if abs(correlation) > 0.5:
        print(f"  ✅ 强相关（|r| > 0.5）")
    elif abs(correlation) > 0.3:
        print(f"  ⚠️ 中等相关（|r| > 0.3）")
    else:
        print(f"  ❌ 弱相关（|r| < 0.3）")

conn.close()

print("\n" + "="*100)
print("✅ 分析完成")
print("="*100)

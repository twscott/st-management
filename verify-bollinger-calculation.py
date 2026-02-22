import mysql.connector
from datetime import datetime
import pandas as pd
import numpy as np

# 数据库连接配置
db_config = {
    'host': 'localhost',
    'user': 'root',
    'password': 'stock168',
    'database': 'sstv2'
}

def calculate_bollinger_bands(stock_id, target_date, period=20, multiplier=2.0):
    """
    计算布林带
    - 中轨 = 20日移动平均
    - 上轨 = 中轨 + (2 × 标准差)
    - 下轨 = 中轨 - (2 × 标准差)
    """
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    # 取得最近 20 天的收盘价
    query = """
        SELECT StockDate, EndPrice 
        FROM stock60days 
        WHERE StockID = %s AND StockDate <= %s AND EndPrice > 0
        ORDER BY StockDate DESC 
        LIMIT %s
    """
    cursor.execute(query, (stock_id, target_date, period))
    records = cursor.fetchall()
    conn.close()
    
    if len(records) < period:
        return None, f"数据不足，只有 {len(records)} 天（需要 {period} 天）"
    
    # 反转为正序（从旧到新）
    records.reverse()
    prices = [float(r['EndPrice']) for r in records]
    
    # 计算 20 日移动平均（中轨）
    middle_band = np.mean(prices)
    
    # 计算标准差
    std_dev = np.std(prices, ddof=0)  # 使用总体标准差（与原系统一致）
    
    # 计算上下轨
    upper_band = middle_band + multiplier * std_dev
    lower_band = middle_band - multiplier * std_dev
    
    # 确保下轨不为负
    if lower_band < 0:
        lower_band = 0
    
    # 计算带宽率
    bandwidth_rate = 0
    if middle_band > 0:
        bandwidth_rate = ((upper_band - lower_band) / middle_band) * 100
    
    return {
        'upper': round(upper_band, 4),
        'middle': round(middle_band, 4),
        'lower': round(lower_band, 4),
        'bandwidth_rate': round(bandwidth_rate, 4),
        'prices_used': prices
    }, None

def verify_bollinger_bands(stock_id, target_date):
    """
    验证布林带计算的正确性
    """
    print(f"\n=== 验证布林带计算: {stock_id} @ {target_date} ===\n")
    
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    # 1. 取得数据库中的布林带值
    query = """
        SELECT StockID, StockDate, EndPrice, 
               boolUp, boolMid, boolDown, boolkaikouDiffRate
        FROM stock60days
        WHERE StockID = %s AND StockDate = %s
    """
    cursor.execute(query, (stock_id, target_date))
    db_record = cursor.fetchone()
    conn.close()
    
    if not db_record:
        print(f"❌ 找不到记录: {stock_id} @ {target_date}")
        return
    
    print("数据库中的值:")
    print(f"  收盘价: {db_record['EndPrice']}")
    print(f"  上轨 (boolUp):   {db_record['boolUp']}")
    print(f"  中轨 (boolMid):  {db_record['boolMid']}")
    print(f"  下轨 (boolDown): {db_record['boolDown']}")
    print(f"  带宽率: {db_record['boolkaikouDiffRate']}%")
    
    # 2. 重新计算布林带
    calculated, error = calculate_bollinger_bands(stock_id, target_date)
    
    if error:
        print(f"\n❌ 计算失败: {error}")
        return
    
    print("\n重新计算的值:")
    print(f"  上轨 (boolUp):   {calculated['upper']}")
    print(f"  中轨 (boolMid):  {calculated['middle']}")
    print(f"  下轨 (boolDown): {calculated['lower']}")
    print(f"  带宽率: {calculated['bandwidth_rate']}%")
    
    # 3. 对比差异
    print("\n差异对比:")
    upper_diff = abs(float(db_record['boolUp']) - calculated['upper'])
    middle_diff = abs(float(db_record['boolMid']) - calculated['middle'])
    lower_diff = abs(float(db_record['boolDown']) - calculated['lower'])
    bandwidth_diff = abs(float(db_record['boolkaikouDiffRate']) - calculated['bandwidth_rate'])
    
    print(f"  上轨差异:   {upper_diff:.4f}")
    print(f"  中轨差异:   {middle_diff:.4f}")
    print(f"  下轨差异:   {lower_diff:.4f}")
    print(f"  带宽率差异: {bandwidth_diff:.4f}%")
    
    # 4. 判断是否正确
    tolerance = 0.01  # 容错范围
    
    if upper_diff < tolerance and middle_diff < tolerance and lower_diff < tolerance:
        print("\n✅ 布林带计算正确！")
    else:
        print("\n⚠️  布林带计算有差异，可能需要检查")
        print("\n使用的价格数据（最近20天，从旧到新）:")
        for i, price in enumerate(calculated['prices_used'], 1):
            print(f"    {i:2d}. {price:.2f}")
    
    # 5. 价格位置分析
    current_price = float(db_record['EndPrice'])
    upper = calculated['upper']
    middle = calculated['middle']
    lower = calculated['lower']
    
    print("\n价格位置分析:")
    print(f"  收盘价: {current_price}")
    
    if current_price > upper:
        position = "突破上轨"
        signal = "超买，可能回调"
        color = "🔴"
    elif current_price > middle:
        position = "上轨与中轨之间"
        signal = "多头趋势"
        color = "🟢"
    elif current_price > lower:
        position = "中轨与下轨之间"
        signal = "空头趋势"
        color = "🟡"
    else:
        position = "跌破下轨"
        signal = "超卖，可能反弹"
        color = "🔵"
    
    print(f"  {color} 位置: {position}")
    print(f"  信号: {signal}")
    
    # 6. 带宽分析
    bandwidth = calculated['bandwidth_rate']
    print(f"\n带宽分析:")
    print(f"  带宽率: {bandwidth:.2f}%")
    
    if bandwidth < 5:
        print("  📉 布林带收缩（可能即将突破）")
    elif bandwidth > 15:
        print("  📈 布林带扩张（波动加大）")
    else:
        print("  ➡️  布林带正常")

def verify_multiple_stocks(date, count=5):
    """
    验证多个股票的布林带计算
    """
    print(f"\n{'='*60}")
    print(f"批量验证布林带计算 - 目标日期: {date}")
    print(f"{'='*60}")
    
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    # 随机取几支股票验证
    query = """
        SELECT DISTINCT StockID
        FROM stock60days
        WHERE StockDate = %s
        AND boolMid > 0
        ORDER BY RAND()
        LIMIT %s
    """
    cursor.execute(query, (date, count))
    stocks = [row['StockID'] for row in cursor.fetchall()]
    
    conn.close()
    
    for stock_id in stocks:
        verify_bollinger_bands(stock_id, date)

def compare_with_verify_bollinger_bands_py():
    """
    对比与之前的 verify-bollinger-bands.py 的计算结果
    """
    print(f"\n{'='*60}")
    print("对比布林带扩张分析")
    print(f"{'='*60}\n")
    
    # 之前验证过的高收益股票
    test_stocks = [
        ("6465", "2025-10-06", "2025-11-03"),  # +83% 大赢家
        ("3105", "2025-10-06", "2025-11-03"),  # +31%
        ("4939", "2025-10-06", "2025-11-03"),  # +62.7%
    ]
    
    for stock_id, hotspot_date, recommend_date in test_stocks:
        print(f"\n股票 {stock_id}:")
        
        # 热点日期布林带
        hotspot_bb, _ = calculate_bollinger_bands(stock_id, hotspot_date)
        if hotspot_bb:
            hotspot_width = hotspot_bb['bandwidth_rate']
            print(f"  热点日 ({hotspot_date}): 带宽 {hotspot_width:.2f}%")
        
        # 推荐日期布林带
        recommend_bb, _ = calculate_bollinger_bands(stock_id, recommend_date)
        if recommend_bb:
            recommend_width = recommend_bb['bandwidth_rate']
            print(f"  推荐日 ({recommend_date}): 带宽 {recommend_width:.2f}%")
            
            if hotspot_bb:
                expansion = ((recommend_width - hotspot_width) / hotspot_width) * 100
                print(f"  扩张幅度: {expansion:+.1f}%")
                
                if expansion > 50:
                    print(f"  ✅ 布林带明显扩张（{expansion:.1f}%）")
                else:
                    print(f"  ➡️  布林带变化不大（{expansion:.1f}%）")

if __name__ == "__main__":
    # 验证单支股票
    verify_bollinger_bands("8240", "2025-11-03")
    
    # 批量验证
    verify_multiple_stocks("2025-11-03", count=3)
    
    # 对比布林带扩张
    compare_with_verify_bollinger_bands_py()
    
    print("\n" + "="*60)
    print("验证完成！")
    print("="*60)

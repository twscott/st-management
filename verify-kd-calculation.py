import mysql.connector
from datetime import datetime, timedelta
import pandas as pd

# 数据库连接配置
db_config = {
    'host': 'localhost',
    'user': 'root',
    'password': 'stock168',
    'database': 'sstv2'
}

def calculate_rsv(stock_id, target_date, current_price, conn):
    """
    计算 RSV (Raw Stochastic Value)
    RSV = (当前收盘价 - 9日最低价) / (9日最高价 - 9日最低价) × 100
    """
    cursor = conn.cursor(dictionary=True)
    
    # 取得最近 9 天的收盘价
    query = """
        SELECT EndPrice 
        FROM stock60days 
        WHERE StockID = %s AND StockDate <= %s 
        ORDER BY StockDate DESC 
        LIMIT 9
    """
    cursor.execute(query, (stock_id, target_date))
    prices = [row['EndPrice'] for row in cursor.fetchall() if row['EndPrice']]
    
    if not prices:
        return 0
    
    highest = max(prices)
    lowest = min(prices)
    
    if highest == lowest:
        return 50  # 避免除以零
    
    rsv = (current_price - lowest) / (highest - lowest) * 100
    return round(rsv, 4)

def calculate_kd(rsv, prev_k, prev_d):
    """
    计算 K 和 D 值
    K = (2/3) × 前日K + (1/3) × 当日RSV
    D = (2/3) × 前日D + (1/3) × 当日K
    """
    if prev_k == 0 and prev_d == 0:
        # 初始化：K = D = RSV
        return rsv, rsv
    
    current_k = (2.0 / 3.0) * prev_k + (1.0 / 3.0) * rsv
    current_d = (2.0 / 3.0) * prev_d + (1.0 / 3.0) * current_k
    
    return round(current_k, 4), round(current_d, 4)

def verify_kd_calculation(stock_id, target_date):
    """
    验证 KD 计算的正确性
    """
    print(f"\n=== 验证 KD 计算: {stock_id} @ {target_date} ===\n")
    
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    # 1. 取得数据库中的 KD 值
    query = """
        SELECT StockID, StockDate, EndPrice, KD_RSV, KD_K, KD_D
        FROM stock60days
        WHERE StockID = %s AND StockDate = %s
    """
    cursor.execute(query, (stock_id, target_date))
    db_record = cursor.fetchone()
    
    if not db_record:
        print(f"❌ 找不到记录: {stock_id} @ {target_date}")
        conn.close()
        return
    
    print("数据库中的值:")
    print(f"  收盘价: {db_record['EndPrice']}")
    print(f"  KD_RSV: {db_record['KD_RSV']}")
    print(f"  KD_K:   {db_record['KD_K']}")
    print(f"  KD_D:   {db_record['KD_D']}")
    
    # 2. 重新计算 RSV
    calculated_rsv = calculate_rsv(stock_id, target_date, db_record['EndPrice'], conn)
    
    # 3. 取得前一天的 K 和 D
    prev_query = """
        SELECT KD_K, KD_D
        FROM stock60days
        WHERE StockID = %s AND StockDate < %s
        ORDER BY StockDate DESC
        LIMIT 1
    """
    cursor.execute(prev_query, (stock_id, target_date))
    prev_record = cursor.fetchone()
    
    prev_k = prev_record['KD_K'] if prev_record else 0
    prev_d = prev_record['KD_D'] if prev_record else 0
    
    # 4. 重新计算 K 和 D
    calculated_k, calculated_d = calculate_kd(calculated_rsv, prev_k, prev_d)
    
    print("\n重新计算的值:")
    print(f"  KD_RSV: {calculated_rsv}")
    print(f"  KD_K:   {calculated_k}")
    print(f"  KD_D:   {calculated_d}")
    
    # 5. 对比差异
    print("\n差异对比:")
    rsv_diff = abs(db_record['KD_RSV'] - calculated_rsv)
    k_diff = abs(db_record['KD_K'] - calculated_k)
    d_diff = abs(db_record['KD_D'] - calculated_d)
    
    print(f"  RSV 差异: {rsv_diff:.4f}")
    print(f"  K 差异:   {k_diff:.4f}")
    print(f"  D 差异:   {d_diff:.4f}")
    
    # 6. 判断是否正确
    tolerance = 0.01  # 容错范围
    
    if rsv_diff < tolerance and k_diff < tolerance and d_diff < tolerance:
        print("\n✅ KD 计算正确！")
    else:
        print("\n⚠️  KD 计算有差异，可能需要检查")
        
    # 7. 显示前一天的值（用于验证连续性）
    if prev_record:
        print("\n前一交易日的 KD 值:")
        print(f"  KD_K: {prev_k}")
        print(f"  KD_D: {prev_d}")
    
    conn.close()

def verify_multiple_stocks(date):
    """
    验证多个股票的 KD 计算
    """
    print(f"\n{'='*60}")
    print(f"批量验证 KD 计算 - 目标日期: {date}")
    print(f"{'='*60}")
    
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    # 随机取 5 支股票验证
    query = """
        SELECT DISTINCT StockID
        FROM stock60days
        WHERE StockDate = %s
        AND KD_K > 0
        ORDER BY RAND()
        LIMIT 5
    """
    cursor.execute(query, (date,))
    stocks = [row['StockID'] for row in cursor.fetchall()]
    
    conn.close()
    
    for stock_id in stocks:
        verify_kd_calculation(stock_id, date)

def show_kd_trend(stock_id, days=10):
    """
    显示股票的 KD 趋势
    """
    print(f"\n=== {stock_id} 最近 {days} 天 KD 趋势 ===\n")
    
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor(dictionary=True)
    
    query = f"""
        SELECT StockDate, EndPrice, KD_RSV, KD_K, KD_D
        FROM stock60days
        WHERE StockID = %s
        ORDER BY StockDate DESC
        LIMIT {days}
    """
    cursor.execute(query, (stock_id,))
    records = cursor.fetchall()
    
    # 转为 DataFrame 显示
    df = pd.DataFrame(records)
    df = df[::-1]  # 反转，从旧到新
    
    print(df.to_string(index=False))
    
    # 标注买卖信号
    print("\n信号分析:")
    for i in range(len(df) - 1):
        current = df.iloc[i]
        next_day = df.iloc[i + 1]
        
        # 黄金交叉
        if current['KD_K'] <= current['KD_D'] and next_day['KD_K'] > next_day['KD_D']:
            print(f"  📈 {next_day['StockDate']}: 黄金交叉 (买入信号)")
        
        # 死亡交叉
        if current['KD_K'] >= current['KD_D'] and next_day['KD_K'] < next_day['KD_D']:
            print(f"  📉 {next_day['StockDate']}: 死亡交叉 (卖出信号)")
        
        # 超买
        if next_day['KD_K'] > 80:
            print(f"  🔴 {next_day['StockDate']}: 超买区域 (K={next_day['KD_K']:.2f})")
        
        # 超卖
        if next_day['KD_K'] < 20:
            print(f"  🟢 {next_day['StockDate']}: 超卖区域 (K={next_day['KD_K']:.2f})")
    
    conn.close()

if __name__ == "__main__":
    # 验证单支股票
    verify_kd_calculation("8240", "2025-11-03")
    
    # 显示 KD 趋势
    show_kd_trend("8240", days=10)
    
    # 批量验证
    verify_multiple_stocks("2025-11-03")
    
    print("\n" + "="*60)
    print("验证完成！")
    print("="*60)

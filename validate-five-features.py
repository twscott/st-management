import pymysql
import pandas as pd
from datetime import datetime, timedelta

# Database connection
conn = pymysql.connect(
    host='localhost',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

print("\n" + "="*100)
print("五大特征条件验证 - 2025/11/03 ~ 2025/11/07")
print("="*100)

# 定义5个特征条件
print("\n【特征条件定义】")
print("1. 冷却天数：26-30天")
print("2. 量能倍数：13-18倍（10-20倍黄金区间）")
print("3. 资金流向：正流入 > 负流出")
print("4. 推荐频率：≥8次（过去20天）")
print("5. 成熟度评分：≥80分")

# 测试日期范围
test_dates = [
    '2025-11-03',
    '2025-11-04',
    '2025-11-05',
    '2025-11-06',
    '2025-11-07'
]

all_results = []

for test_date in test_dates:
    print(f"\n" + "="*100)
    print(f"测试日期: {test_date}")
    print("="*100)
    
    # Step 1: 找出符合5个条件的候选股票
    query = """
    SELECT 
        a.StockID as stock_code,
        a.alertDate as hotspot_date,
        DATEDIFF(%s, a.alertDate) as cooling_days,
        a.maxPLVR as volume_ratio,
        a.panvolScore as volume_score,
        a.panVol5CntPos as positive_money_days,
        a.panVol5CntNeg as negative_money_days,
        -- 成熟度评分
        (
            CASE 
                WHEN DATEDIFF(%s, a.alertDate) BETWEEN 15 AND 30 THEN 40
                WHEN DATEDIFF(%s, a.alertDate) BETWEEN 31 AND 50 THEN 35
                WHEN DATEDIFF(%s, a.alertDate) BETWEEN 8 AND 14 THEN 25
                ELSE 15
            END +
            CASE 
                WHEN a.maxPLVR BETWEEN 10 AND 20 THEN 30
                WHEN a.maxPLVR BETWEEN 20 AND 30 THEN 25
                WHEN a.maxPLVR BETWEEN 30 AND 50 THEN 15
                ELSE 10
            END +
            CASE 
                WHEN a.panvolScore BETWEEN 20 AND 50 THEN 20
                WHEN a.panvolScore < 20 THEN 10
                WHEN a.panvolScore BETWEEN 50 AND 100 THEN 5
                ELSE 0
            END +
            CASE 
                WHEN a.panVol5CntPos > a.panVol5CntNeg THEN 10
                ELSE 0
            END
        ) as maturity_score
    FROM alertlist a
    WHERE a.alertDate < %s
      AND DATEDIFF(%s, a.alertDate) BETWEEN 26 AND 30
      AND a.maxPLVR BETWEEN 13 AND 18
      AND a.panVol5CntPos > a.panVol5CntNeg
    HAVING maturity_score >= 80
    ORDER BY maturity_score DESC
    """
    
    df_candidates = pd.read_sql(query, conn, params=(
        test_date, test_date, test_date, test_date, test_date, test_date
    ))
    
    if df_candidates.empty:
        print("❌ 无符合条件的候选股票")
        continue
    
    print(f"\n✅ 找到 {len(df_candidates)} 支符合前4个条件的股票")
    
    # Step 2: 检查推荐频率（第5个条件）
    qualified_stocks = []
    for _, row in df_candidates.iterrows():
        stock_code = row['stock_code']
        
        # 查询过去20天的推荐频率
        freq_query = """
        SELECT COUNT(*) as frequency
        FROM alertlist
        WHERE StockID = %s
          AND alertDate BETWEEN DATE_SUB(%s, INTERVAL 20 DAY) AND %s
        """
        freq_result = pd.read_sql(freq_query, conn, params=(stock_code, test_date, test_date))
        frequency = freq_result['frequency'].values[0]
        
        if frequency >= 8:
            row_dict = row.to_dict()
            row_dict['recommendation_frequency'] = frequency
            qualified_stocks.append(row_dict)
    
    if not qualified_stocks:
        print("❌ 无股票满足推荐频率≥8次条件")
        continue
    
    df_qualified = pd.DataFrame(qualified_stocks)
    print(f"\n🎯 满足全部5个条件的股票: {len(df_qualified)} 支")
    print("-" * 100)
    
    # Step 3: 查询后续涨幅（观察期：7天、14天、30天）
    for _, stock in df_qualified.iterrows():
        stock_code = stock['stock_code']
        
        # 获取买入价（推荐日价格）
        entry_query = """
        SELECT EndPrice as entry_price
        FROM stock60days
        WHERE StockID = %s
          AND StockDate <= %s
        ORDER BY ABS(DATEDIFF(StockDate, %s))
        LIMIT 1
        """
        entry_result = pd.read_sql(entry_query, conn, params=(stock_code, test_date, test_date))
        
        if entry_result.empty:
            continue
        
        entry_price = entry_result['entry_price'].values[0]
        
        # 查询未来7天、14天、30天的最高价
        future_query = """
        SELECT 
            MAX(CASE WHEN DATEDIFF(StockDate, %s) <= 7 THEN HPrice END) as high_7d,
            MAX(CASE WHEN DATEDIFF(StockDate, %s) <= 14 THEN HPrice END) as high_14d,
            MAX(CASE WHEN DATEDIFF(StockDate, %s) <= 30 THEN HPrice END) as high_30d
        FROM stock60days
        WHERE StockID = %s
          AND StockDate > %s
          AND StockDate <= DATE_ADD(%s, INTERVAL 30 DAY)
        """
        future_result = pd.read_sql(future_query, conn, params=(
            test_date, test_date, test_date, stock_code, test_date, test_date
        ))
        
        if future_result.empty:
            continue
        
        high_7d = future_result['high_7d'].values[0]
        high_14d = future_result['high_14d'].values[0]
        high_30d = future_result['high_30d'].values[0]
        
        gain_7d = ((high_7d - entry_price) / entry_price * 100) if high_7d else 0
        gain_14d = ((high_14d - entry_price) / entry_price * 100) if high_14d else 0
        gain_30d = ((high_30d - entry_price) / entry_price * 100) if high_30d else 0
        
        print(f"\n{stock_code}: 热点={stock['hotspot_date']}, 冷却={stock['cooling_days']}天, "
              f"量能={stock['volume_ratio']:.1f}x, 频率={stock['recommendation_frequency']}次, "
              f"评分={stock['maturity_score']:.0f}")
        print(f"  买入价: ${entry_price:.2f}")
        print(f"  7天涨幅:  {gain_7d:+6.2f}% (最高 ${high_7d:.2f})")
        print(f"  14天涨幅: {gain_14d:+6.2f}% (最高 ${high_14d:.2f})")
        print(f"  30天涨幅: {gain_30d:+6.2f}% (最高 ${high_30d:.2f})")
        
        all_results.append({
            'test_date': test_date,
            'stock_code': stock_code,
            'hotspot_date': stock['hotspot_date'],
            'cooling_days': stock['cooling_days'],
            'volume_ratio': stock['volume_ratio'],
            'frequency': stock['recommendation_frequency'],
            'maturity_score': stock['maturity_score'],
            'entry_price': entry_price,
            'gain_7d': gain_7d,
            'gain_14d': gain_14d,
            'gain_30d': gain_30d
        })

# 统计分析
print("\n" + "="*100)
print("整体统计分析（2025/11/03 ~ 2025/11/07）")
print("="*100)

if all_results:
    df_results = pd.DataFrame(all_results)
    
    total_stocks = len(df_results)
    
    # 成功率统计（>10%算成功）
    success_7d = len(df_results[df_results['gain_7d'] > 10])
    success_14d = len(df_results[df_results['gain_14d'] > 10])
    success_30d = len(df_results[df_results['gain_30d'] > 10])
    
    # 大成功率统计（>30%算大成功）
    big_success_7d = len(df_results[df_results['gain_7d'] > 30])
    big_success_14d = len(df_results[df_results['gain_14d'] > 30])
    big_success_30d = len(df_results[df_results['gain_30d'] > 30])
    
    print(f"\n📊 样本数量: {total_stocks} 支股票")
    print(f"\n🎯 成功率统计（涨幅>10%）:")
    print(f"  7天成功率:  {success_7d}/{total_stocks} = {success_7d/total_stocks*100:.1f}%")
    print(f"  14天成功率: {success_14d}/{total_stocks} = {success_14d/total_stocks*100:.1f}%")
    print(f"  30天成功率: {success_30d}/{total_stocks} = {success_30d/total_stocks*100:.1f}%")
    
    print(f"\n🔥 大成功率统计（涨幅>30%）:")
    print(f"  7天大成功:  {big_success_7d}/{total_stocks} = {big_success_7d/total_stocks*100:.1f}%")
    print(f"  14天大成功: {big_success_14d}/{total_stocks} = {big_success_14d/total_stocks*100:.1f}%")
    print(f"  30天大成功: {big_success_30d}/{total_stocks} = {big_success_30d/total_stocks*100:.1f}%")
    
    print(f"\n💰 平均涨幅:")
    print(f"  7天平均:  {df_results['gain_7d'].mean():+.2f}%")
    print(f"  14天平均: {df_results['gain_14d'].mean():+.2f}%")
    print(f"  30天平均: {df_results['gain_30d'].mean():+.2f}%")
    
    print(f"\n📈 最佳表现:")
    best_7d = df_results.loc[df_results['gain_7d'].idxmax()]
    best_14d = df_results.loc[df_results['gain_14d'].idxmax()]
    best_30d = df_results.loc[df_results['gain_30d'].idxmax()]
    
    print(f"  7天最佳:  {best_7d['stock_code']} (+{best_7d['gain_7d']:.2f}%)")
    print(f"  14天最佳: {best_14d['stock_code']} (+{best_14d['gain_14d']:.2f}%)")
    print(f"  30天最佳: {best_30d['stock_code']} (+{best_30d['gain_30d']:.2f}%)")
    
    print(f"\n❌ 失败案例（30天涨幅<5%）:")
    failures = df_results[df_results['gain_30d'] < 5]
    if len(failures) > 0:
        for _, fail in failures.iterrows():
            print(f"  {fail['stock_code']}: 30天涨幅 {fail['gain_30d']:+.2f}%")
    else:
        print("  无失败案例！")
    
    # 保存详细结果到CSV
    df_results.to_csv('feature-validation-results.csv', index=False, encoding='utf-8-sig')
    print(f"\n💾 详细结果已保存到: feature-validation-results.csv")
    
else:
    print("\n❌ 测试期间无符合条件的股票")

conn.close()

print("\n" + "="*100)
print("✅ 分析完成")
print("="*100)

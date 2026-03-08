# -*- coding: utf-8 -*-
import pymysql
import pandas as pd

conn = pymysql.connect(host='127.0.0.1',user='root',password='',database='sst',charset='utf8mb4')

TEST_CONFIG = {'signal_type':'tradedata','signal_name':'Big Candle','kd_min':30,'kd_max':70,'cooling_min':10,'cooling_max':25}
TARGET_GAIN,TRACKING_DAYS,LOOKBACK_DAYS = 20,20,10  # Changed to 10 days for testing

print("\n"+"="*80)
print("VALIDATION SCRIPT - Single Config Test")
print("="*80)
print(f"\nSignal: {TEST_CONFIG['signal_name']}")
print(f"KD: {TEST_CONFIG['kd_min']}-{TEST_CONFIG['kd_max']}")
print(f"Cooling: {TEST_CONFIG['cooling_min']}-{TEST_CONFIG['cooling_max']}")
print(f"Goal: {TARGET_GAIN}% in {TRACKING_DAYS} days")
print("="*80)

query = f"""
WITH candidates AS (
    SELECT t.StockID,t.TransDate AS event_date,s60.StockDate AS recommend_date,
           s60.EndPrice AS entry_price,t.StockDiffRate AS candle_gain,s60.KD_K,s60.KD_D
    FROM tradedata t
    INNER JOIN stock60days s60 ON s60.StockID=t.StockID 
        AND s60.StockDate >= DATE_ADD(t.TransDate, INTERVAL {TEST_CONFIG['cooling_min']} DAY)
        AND s60.StockDate <= DATE_ADD(t.TransDate, INTERVAL {TEST_CONFIG['cooling_max']} DAY)
    WHERE t.StockDiffRate>=6.0 AND t.Vol>=1000
      AND t.TransDate>=DATE_SUB(CURDATE(),INTERVAL {LOOKBACK_DAYS+TEST_CONFIG['cooling_max']} DAY)
      AND s60.KD_K BETWEEN {TEST_CONFIG['kd_min']} AND {TEST_CONFIG['kd_max']}
      AND s60.MA20>0
      AND s60.StockDate>=DATE_SUB(CURDATE(),INTERVAL {LOOKBACK_DAYS} DAY)
      AND s60.StockDate<=DATE_SUB(CURDATE(),INTERVAL {TRACKING_DAYS+2} DAY)
)
SELECT c.StockID,c.event_date,c.recommend_date,c.entry_price,c.candle_gain,c.KD_K,c.KD_D,MAX(f.HPrice) AS max_price_20d
FROM candidates c
INNER JOIN stock60days f ON f.StockID=c.StockID
WHERE f.StockDate>c.recommend_date AND f.StockDate<=DATE_ADD(c.recommend_date,INTERVAL {TRACKING_DAYS} DAY)
GROUP BY c.StockID,c.event_date,c.recommend_date,c.entry_price,c.candle_gain,c.KD_K,c.KD_D
ORDER BY c.recommend_date,c.StockID
"""

print("\nExecuting query...")
cursor = conn.cursor()
cursor.execute(query)
results = cursor.fetchall()
cursor.close()

if not results:
    print("\n No recommendations found!")
    conn.close()
    exit()

df = pd.DataFrame(results,columns=['StockID','event_date','recommend_date','entry_price','candle_gain','KD_K','KD_D','max_price_20d'])
df['gain_pct'] = (df['max_price_20d']-df['entry_price'])/df['entry_price']*100
df['success'] = df['gain_pct']>=TARGET_GAIN

total = len(df)
success = df['success'].sum()
accuracy = success/total*100 if total>0 else 0
by_date = df.groupby('recommend_date').size()
days_with = len(by_date)

cursor = conn.cursor()
cursor.execute(f"SELECT COUNT(DISTINCT StockDate) FROM stock60days WHERE StockDate>=DATE_SUB(CURDATE(),INTERVAL {LOOKBACK_DAYS} DAY) AND StockDate<=DATE_SUB(CURDATE(),INTERVAL {TRACKING_DAYS+2} DAY)")
total_days = cursor.fetchone()[0]
cursor.close()

empty_days = total_days - days_with
empty_pct = empty_days/total_days*100 if total_days>0 else 0

print(f"\n{'='*80}")
print("RESULTS")
print(f"{'='*80}")
print(f"\nTrading Days: {total_days}")
print(f"Days with Recs: {days_with}")
print(f"Empty Days: {empty_days} ({empty_pct:.1f}%)")
print(f"Total Recs: {total}")
print(f"Success: {success}")
print(f"Accuracy: {accuracy:.1f}%")
print(f"\nAvg Gain: {df['gain_pct'].mean():.1f}%")
print(f"Median Gain: {df['gain_pct'].median():.1f}%")
print(f"Max Gain: {df['gain_pct'].max():.1f}%")

print(f"\n{'='*80}")
if accuracy>=70:
    print(" EXCELLENT (>=70%)")
elif accuracy>=50:
    print("  GOOD (50-70%)")
else:
    print("  NEEDS IMPROVEMENT")

if empty_pct<=40:
    print(f" Empty days OK ({empty_pct:.1f}%)")
else:
    print(f" Too many empty days ({empty_pct:.1f}%)")

print(f"{'='*80}\n")
conn.close()

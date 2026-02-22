import pymysql
import pandas as pd
from datetime import datetime

# Database connection
conn = pymysql.connect(
    host='localhost',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

target_stocks = ['3163', '3105', '4542', '3455']
target_date = '2025-11-03'

# Based on the user's data showing cooling_days=28, we need to find the hotspot date
# which would be approximately 28 days before the recommendation date
from_date = '2025-10-01'  # About 5-15 days before hotspot
to_date = '2025-10-10'  # About 24-33 days before recommendation

# Query detailed data for these stocks around their hotspot dates
query = """
SELECT 
    StockID as stockid,
    alertDate as alert_date,
    maxPLVR as zhuanzhuanlv,
    panvolScore as volume_score,
    panVol5CntPos as positive_money_days,
    panVol5CntNeg as negative_money_days,
    DATEDIFF(%s, alertDate) as cooling_days
FROM alertlist
WHERE StockID IN %s 
  AND alertDate BETWEEN %s AND %s
  AND maxPLVR BETWEEN 10 AND 50
ORDER BY StockID, alertDate
"""

df = pd.read_sql(query, conn, params=(target_date, target_stocks, from_date, to_date))

print("\n" + "="*80)
print(f"Common Features Analysis for {target_date}")
print("="*80)

if df.empty:
    print("No data found for these stocks in the date range")
    print(f"Searched: {from_date} to {to_date}")
else:
    print(f"\nTotal alerts found: {len(df)}")
    print(f"Unique stocks: {df['stockid'].nunique()}")
    print("\n--- Individual Alert Data ---")
    for _, row in df.iterrows():
        print(f"\nStock: {row['stockid']}")
        print(f"  Alert/Hotspot Date: {row['alert_date']}")
        print(f"  Cooling Days (to {target_date}): {row['cooling_days']} days")
        print(f"  Volume Ratio (maxPLVR): {row['zhuanzhuanlv']:.1f}x")
        print(f"  Volume Score (panvolScore): {row['volume_score']}")
        print(f"  Positive Money Flow Days: {row['positive_money_days']}")
        print(f"  Negative Money Flow Days: {row['negative_money_days']}")
    
    # Filter to get only the alerts closest to 28 days cooling
    df_28d = df[(df['cooling_days'] >= 26) & (df['cooling_days'] <= 30)].copy()
    
    print("\n" + "="*80)
    print("ALERTS WITH ~28 DAYS COOLING (26-30 days)")
    print("="*80)
    print(f"Found {len(df_28d)} alerts matching 28-day cooling criterion")
    
    if not df_28d.empty:
        print("\n--- 28-Day Cooling Alerts ---")
        for _, row in df_28d.iterrows():
            print(f"Stock: {row['stockid']}, Hotspot: {row['alert_date']}, Cooling: {row['cooling_days']}d, Volume: {row['zhuanzhuanlv']:.1f}x")
    
        print("\n" + "="*80)
        print("COMMON FEATURES SUMMARY (28-day cooling alerts)")
        print("="*80)
        
        print(f"\n1. Cooling Days:")
        print(f"   Range: {df_28d['cooling_days'].min()} - {df_28d['cooling_days'].max()} days")
        print(f"   Average: {df_28d['cooling_days'].mean():.1f} days")
        print(f"   All exactly 28 days: {(df_28d['cooling_days'] == 28).all()}")
        
        print(f"\n2. Volume Ratio (maxPLVR/轉轉率):")
        print(f"   Range: {df_28d['zhuanzhuanlv'].min():.1f}x - {df_28d['zhuanzhuanlv'].max():.1f}x")
        print(f"   Average: {df_28d['zhuanzhuanlv'].mean():.1f}x")
        print(f"   All in 10-20x range: {((df_28d['zhuanzhuanlv'] >= 10) & (df_28d['zhuanzhuanlv'] <= 20)).all()}")
        
        print(f"\n3. Volume Score (panvolScore):")
        print(f"   Range: {df_28d['volume_score'].min()} - {df_28d['volume_score'].max()}")
        print(f"   Average: {df_28d['volume_score'].mean():.1f}")
        print(f"   All in 20-50 range: {((df_28d['volume_score'] >= 20) & (df_28d['volume_score'] <= 50)).all()}")
        
        print(f"\n4. Money Flow (資金流向):")
        print(f"   Positive Days Range: {df_28d['positive_money_days'].min()} - {df_28d['positive_money_days'].max()}")
        print(f"   Negative Days Range: {df_28d['negative_money_days'].min()} - {df_28d['negative_money_days'].max()}")
        all_positive_greater = (df_28d['positive_money_days'] > df_28d['negative_money_days']).all()
        print(f"   All Positive > Negative: {all_positive_greater}")

# Now check historical recommendations for these stocks
print("\n" + "="*80)
print("RECOMMENDATION FREQUENCY ANALYSIS")
print("="*80)

freq_query = """
SELECT 
    StockID as stockid,
    COUNT(*) as total_alerts,
    MIN(alertDate) as first_alert,
    MAX(alertDate) as latest_alert,
    GROUP_CONCAT(DATE_FORMAT(alertDate, '%%Y-%%m-%%d') ORDER BY alertDate SEPARATOR ', ') as all_dates
FROM alertlist
WHERE StockID IN %s 
    AND alertDate BETWEEN DATE_SUB(%s, INTERVAL 20 DAY) AND %s
GROUP BY StockID
ORDER BY total_alerts DESC
"""

freq_df = pd.read_sql(freq_query, conn, params=(target_stocks, target_date, target_date))

for _, row in freq_df.iterrows():
    print(f"\nStock {row['stockid']}:")
    print(f"  Total Alerts (20 days): {row['total_alerts']}")
    print(f"  First: {row['first_alert']}")
    print(f"  Latest: {row['latest_alert']}")
    dates_str = str(row['all_dates'])
    if len(dates_str) > 100:
        print(f"  Dates: {dates_str[:100]}...")
    else:
        print(f"  Dates: {dates_str}")

conn.close()

print("\n" + "="*80)
print("✅ Analysis Complete")
print("="*80)

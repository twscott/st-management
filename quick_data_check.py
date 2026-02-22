#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Quick Data Check - Simplified Version
"""

import pymysql

DB_CONFIG = {
    'host': '127.0.0.1',
    'user': 'root',
    'password': '',
    'database': 'sst',
    'charset': 'utf8mb4'
}

print("="*60)
print("Quick Data Check")
print("="*60)

try:
    conn = pymysql.connect(**DB_CONFIG)
    cursor = conn.cursor()
    
    # Check alertlist data
    print("\n1. Checking alertlist data...")
    cursor.execute("""
        SELECT COUNT(*) as total, 
               MIN(alertDate) as min_date, 
               MAX(alertDate) as max_date
        FROM alertlist 
        WHERE alertDate BETWEEN '2025-10-01' AND '2026-01-31'
    """)
    result = cursor.fetchone()
    print(f"   Total records: {result[0]}")
    print(f"   Date range: {result[1]} to {result[2]}")
    
    # Check stock60days data
    print("\n2. Checking stock60days data...")
    cursor.execute("""
        SELECT COUNT(*) as total,
               COUNT(DISTINCT StockID) as unique_stocks
        FROM stock60days
        WHERE StockDate BETWEEN '2025-10-01' AND '2026-03-01'
    """)
    result = cursor.fetchone()
    print(f"   Total records: {result[0]}")
    print(f"   Unique stocks: {result[1]}")
    
    # Quick success case check
    print("\n3. Quick check for potential success cases...")
    cursor.execute("""
        SELECT a.StockID, a.alertDate, a.maxPLVR
        FROM alertlist a
        WHERE a.alertDate BETWEEN '2025-10-01' AND '2025-10-10'
        LIMIT 5
    """)
    results = cursor.fetchall()
    print(f"   Sample alertlist records (first 5 in Oct 2025):")
    for r in results:
        print(f"     {r[0]} | {r[1]} | maxPLVR: {r[2]}")
    
    cursor.close()
    conn.close()
    
    print("\n" + "="*60)
    print("✓ Data check completed")
    print("="*60)
    
except Exception as e:
    print(f"\n❌ Error: {e}")
    import traceback
    traceback.print_exc()

#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fishing Theory Validation - Python Script
Purpose: Execute SQL analysis and display results
Date: 2026-02-21
"""

import pymysql
import sys
from datetime import datetime

# Database configuration
DB_CONFIG = {
    'host': '127.0.0.1',
    'user': 'root',
    'password': '',
    'database': 'sst',
    'charset': 'utf8mb4',
    'cursorclass': pymysql.cursors.DictCursor
}

def execute_query(connection, query, description=""):
    """Execute a single query and return results"""
    try:
        with connection.cursor() as cursor:
            cursor.execute(query)
            results = cursor.fetchall()
            return results
    except Exception as e:
        print(f"❌ Error executing {description}: {e}")
        return None

def print_table(results, title=""):
    """Print results as a formatted table"""
    if not results:
        print(f"\n{title}: No data")
        return
    
    print(f"\n{'='*80}")
    print(f"{title}")
    print(f"{'='*80}")
    
    # Get column names
    if len(results) > 0:
        headers = list(results[0].keys())
        
        # Print header
        header_row = " | ".join([f"{h:20}" for h in headers])
        print(header_row)
        print("-" * len(header_row))
        
        # Print rows
        for row in results:
            values = [str(row[h]) if row[h] is not None else 'NULL' for h in headers]
            print(" | ".join([f"{v:20}" for v in values]))
    
    print(f"\nTotal rows: {len(results)}")

def main():
    print("="*80)
    print("🎣 Fishing Theory Validation Analysis")
    print("="*80)
    print(f"Start time: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print()
    
    try:
        # Connect to database
        print("Connecting to MySQL...")
        connection = pymysql.connect(**DB_CONFIG)
        print("✓ Connected successfully\n")
        
        # Step 1: Create success cases temporary table
        print("Step 1: Identifying success cases (gain >= 30%)...")
        success_query = """
        DROP TEMPORARY TABLE IF EXISTS success_cases;
        CREATE TEMPORARY TABLE success_cases AS
        SELECT DISTINCT
            a.StockID,
            a.alertDate as hotspot_date,
            a.maxPLVR,
            a.panvolScore,
            a.paRatePosCnt,
            a.paRateNegCnt,
            a.pLVRatePosCnt,
            a.pLVRateNegCnt,
            a.p5VRatePosCnt,
            a.p5VRateNegCnt,
            a.pApRatePosCnt,
            a.pApRateNegCnt,
            a.panVol5CntPos,
            a.panVol5CntNeg,
            MAX(((s.HPrice - entry.EndPrice) / entry.EndPrice) * 100) as max_gain_pct
        FROM alertlist a
        LEFT JOIN stock60days entry ON (
            entry.StockID = a.StockID 
            AND entry.StockDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                                    AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
            AND ABS(DATEDIFF(entry.StockDate, a.alertDate)) <= 5
        )
        LEFT JOIN stock60days s ON (
            s.StockID = a.StockID 
            AND s.StockDate BETWEEN a.alertDate AND DATE_ADD(a.alertDate, INTERVAL 60 DAY)
        )
        WHERE a.alertDate BETWEEN '2025-10-01' AND '2026-01-31'
          AND entry.EndPrice IS NOT NULL
          AND entry.EndPrice > 0
        GROUP BY 
            a.StockID, a.alertDate, a.maxPLVR, a.panvolScore,
            a.paRatePosCnt, a.paRateNegCnt, 
            a.pLVRatePosCnt, a.pLVRateNegCnt,
            a.p5VRatePosCnt, a.p5VRateNegCnt,
            a.pApRatePosCnt, a.pApRateNegCnt,
            a.panVol5CntPos, a.panVol5CntNeg
        HAVING max_gain_pct >= 30
        """
        with connection.cursor() as cursor:
            for statement in success_query.split(';'):
                if statement.strip():
                    cursor.execute(statement)
        
        # Count success cases
        count_result = execute_query(connection, "SELECT COUNT(*) as count FROM success_cases")
        success_count = count_result[0]['count'] if count_result else 0
        print(f"✓ Success cases found: {success_count}")
        
        # Step 2: Create failure cases
        print("\nStep 2: Identifying failure cases (gain < 10%)...")
        failure_query = """
        DROP TEMPORARY TABLE IF EXISTS failure_cases;
        CREATE TEMPORARY TABLE failure_cases AS
        SELECT DISTINCT
            a.StockID,
            a.alertDate as hotspot_date,
            a.maxPLVR,
            a.panvolScore,
            a.paRatePosCnt,
            a.paRateNegCnt,
            a.pLVRatePosCnt,
            a.pLVRateNegCnt,
            a.p5VRatePosCnt,
            a.p5VRateNegCnt,
            a.pApRatePosCnt,
            a.pApRateNegCnt,
            a.panVol5CntPos,
            a.panVol5CntNeg,
            MAX(((s.HPrice - entry.EndPrice) / entry.EndPrice) * 100) as max_gain_pct
        FROM alertlist a
        LEFT JOIN stock60days entry ON (
            entry.StockID = a.StockID 
            AND entry.StockDate BETWEEN DATE_SUB(a.alertDate, INTERVAL 5 DAY) 
                                    AND DATE_ADD(a.alertDate, INTERVAL 5 DAY)
            AND ABS(DATEDIFF(entry.StockDate, a.alertDate)) <= 5
        )
        LEFT JOIN stock60days s ON (
            s.StockID = a.StockID 
            AND s.StockDate BETWEEN a.alertDate AND DATE_ADD(a.alertDate, INTERVAL 60 DAY)
        )
        WHERE a.alertDate BETWEEN '2025-10-01' AND '2026-01-31'
          AND entry.EndPrice IS NOT NULL
          AND entry.EndPrice > 0
        GROUP BY 
            a.StockID, a.alertDate, a.maxPLVR, a.panvolScore,
            a.paRatePosCnt, a.paRateNegCnt, 
            a.pLVRatePosCnt, a.pLVRateNegCnt,
            a.p5VRatePosCnt, a.p5VRateNegCnt,
            a.pApRatePosCnt, a.pApRateNegCnt,
            a.panVol5CntPos, a.panVol5CntNeg
        HAVING max_gain_pct < 10
        LIMIT 200
        """
        with connection.cursor() as cursor:
            for statement in failure_query.split(';'):
                if statement.strip():
                    cursor.execute(statement)
        
        count_result = execute_query(connection, "SELECT COUNT(*) as count FROM failure_cases")
        failure_count = count_result[0]['count'] if count_result else 0
        print(f"✓ Failure cases found: {failure_count}")
        
        # Step 3: Compare success vs failure
        print("\nStep 3: Comparing success vs failure characteristics...")
        comparison_query = """
        SELECT 
            '成功组（涨超30%）' as group_name,
            COUNT(*) as sample_count,
            ROUND(AVG(maxPLVR), 2) as avg_max_plvr,
            ROUND(AVG(pLVRatePosCnt), 2) as avg_buying_vs_yesterday,
            ROUND(STDDEV(pLVRatePosCnt), 2) as stddev_buying,
            ROUND(AVG(p5VRatePosCnt), 2) as avg_buying_vs_5day,
            ROUND(AVG(panVol5CntPos), 2) as avg_positive_money_days,
            ROUND(AVG(panVol5CntPos) / (AVG(panVol5CntPos) + AVG(panVol5CntNeg)), 4) as money_flow_ratio
        FROM success_cases
        UNION ALL
        SELECT 
            '失败组（涨幅<10%）' as group_name,
            COUNT(*) as sample_count,
            ROUND(AVG(maxPLVR), 2) as avg_max_plvr,
            ROUND(AVG(pLVRatePosCnt), 2) as avg_buying_vs_yesterday,
            ROUND(STDDEV(pLVRatePosCnt), 2) as stddev_buying,
            ROUND(AVG(p5VRatePosCnt), 2) as avg_buying_vs_5day,
            ROUND(AVG(panVol5CntPos), 2) as avg_positive_money_days,
            ROUND(AVG(panVol5CntPos) / (AVG(panVol5CntPos) + AVG(panVol5CntNeg)), 4) as money_flow_ratio
        FROM failure_cases
        """
        comparison_results = execute_query(connection, comparison_query)
        print_table(comparison_results, "📊 Success vs Failure Comparison")
        
        # Step 4: Top 10 success examples
        print("\nStep 4: Top 10 success examples...")
        top_examples_query = """
        SELECT 
            StockID,
            DATE_FORMAT(hotspot_date, '%Y-%m-%d') as date,
            ROUND(max_gain_pct, 1) as gain_pct,
            maxPLVR,
            pLVRatePosCnt as buying_cnt,
            p5VRatePosCnt as buying_vs_5d,
            panVol5CntPos as pos_days,
            panVol5CntNeg as neg_days
        FROM success_cases
        ORDER BY max_gain_pct DESC
        LIMIT 10
        """
        top_examples = execute_query(connection, top_examples_query)
        print_table(top_examples, "🌟 Top 10 Success Cases")
        
        # Summary
        print(f"\n{'='*80}")
        print("📝 Summary")
        print(f"{'='*80}")
        print(f"Analysis period: 2025-10-01 to 2026-01-31 (4 months)")
        print(f"Success cases (gain >= 30%): {success_count}")
        print(f"Failure cases (gain < 10%): {failure_count}")
        print(f"\nNext steps:")
        print("1. Check if 'avg_buying_vs_yesterday' differs between groups")
        print("2. Check if 'money_flow_ratio' differs significantly")
        print("3. If differences exist, these can be used in the recommendation algorithm")
        
        connection.close()
        print(f"\n{'='*80}")
        print(f"✓ Analysis completed at {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
        print(f"{'='*80}")
        
    except pymysql.Error as e:
        print(f"\n❌ Database error: {e}")
        sys.exit(1)
    except Exception as e:
        print(f"\n❌ Unexpected error: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)

if __name__ == "__main__":
    main()

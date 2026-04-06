#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""檢查最新數據狀態"""

import pymysql
import pandas as pd
from datetime import datetime

def check_table_structure():
    """檢查 weekall 表結構"""
    try:
        conn = pymysql.connect(
            host='127.0.0.1',
            user='root',
            password='',
            database='sstv2',
            charset='utf8'
        )
        
        print("=" * 80)
        print("📋 weekall 表結構")
        print("=" * 80)
        
        cursor = conn.cursor()
        cursor.execute('SHOW COLUMNS FROM weekall')
        cols = cursor.fetchall()
        
        for i, row in enumerate(cols, 1):
            print(f"{i:2d}. {row[0]:25s} {row[1]:30s}")
        
        cursor.close()
        conn.close()
        
    except Exception as e:
        print(f"❌ 錯誤: {e}")

def check_latest_data():
    """檢查最新數據"""
    try:
        conn = pymysql.connect(
            host='127.0.0.1',
            user='root',
            password='',
            database='sstv2',
            charset='utf8'
        )
        
        print("\n" + "=" * 80)
        print("📊 最新數據狀態")
        print("=" * 80)
        
        # 查詢最近 10 個交易日
        cursor = conn.cursor()
        cursor.execute("""
            SELECT DISTINCT StockDate
            FROM weekall
            ORDER BY StockDate DESC
            LIMIT 10
        """)
        dates = cursor.fetchall()
        
        print("\n📅 最近 10 個交易日：")
        for i, (date,) in enumerate(dates, 1):
            print(f"  {i:2d}. {date}")
        
        # 檢查最新一天的數據量
        if dates:
            latest_date = dates[0][0]
            cursor.execute(f"""
                SELECT COUNT(*) as count
                FROM weekall
                WHERE StockDate = '{latest_date}'
            """)
            count = cursor.fetchone()[0]
            print(f"\n✅ 最新日期 {latest_date}: {count} 筆股票數據")
        
        # 檢查 investbase.LastDate
        cursor.execute("SELECT LastDate FROM investbase LIMIT 1")
        result = cursor.fetchone()
        if result:
            print(f"📅 investbase.LastDate: {result[0]}")
        
        cursor.close()
        conn.close()
        
    except Exception as e:
        print(f"❌ 錯誤: {e}")

if __name__ == "__main__":
    check_table_structure()
    check_latest_data()

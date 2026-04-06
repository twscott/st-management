#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""快速測試 MySQL 性能和連接狀態"""

import pymysql
import time

def test_database():
    print("=" * 80)
    print("🔍 MySQL 性能測試")
    print("=" * 80)
    
    try:
        # 1. 測試連接
        print("\n1️⃣ 測試連接...")
        conn = pymysql.connect(
            host='127.0.0.1',
            user='root',
            password='',
            database='sst',
            charset='utf8'
        )
        print("✅ 連接成功")
        
        cursor = conn.cursor()
        
        # 2. 檢查表大小
        print("\n2️⃣ 檢查表大小...")
        cursor.execute('SELECT COUNT(*) FROM tradedata')
        tradedata_count = cursor.fetchone()[0]
        print(f"   tradedata 總記錄數: {tradedata_count:,}")
        
        cursor.execute('SELECT COUNT(*) FROM stock60days')
        stock60days_count = cursor.fetchone()[0]
        print(f"   stock60days 總記錄數: {stock60days_count:,}")
        
        # 3. 測試簡單查詢性能
        print("\n3️⃣ 測試簡單查詢性能...")
        start = time.time()
        cursor.execute('''
            SELECT COUNT(*) 
            FROM tradedata 
            WHERE StockDiffRate >= 6.0 
              AND TransDate >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
        ''')
        result = cursor.fetchone()[0]
        elapsed = time.time() - start
        
        print(f"   查詢結果: {result} 條大陽線")
        print(f"   執行時間: {elapsed:.2f} 秒")
        
        if elapsed > 5:
            print("   ⚠️ 查詢較慢，可能需要進一步檢查")
        else:
            print("   ✅ 性能正常")
        
        # 4. 檢查活躍連接數
        print("\n4️⃣ 檢查活躍連接...")
        cursor.execute('SHOW PROCESSLIST')
        processes = cursor.fetchall()
        print(f"   活躍連接數: {len(processes)}")
        
        # 顯示長時間運行的查詢
        long_running = [p for p in processes if p[5] and p[5] > 60]  # Time > 60秒
        if long_running:
            print(f"   ⚠️ 發現 {len(long_running)} 個長時間運行的查詢:")
            for p in long_running:
                print(f"      ID={p[0]}, Time={p[5]}s, State={p[4]}, Info={p[7][:50] if p[7] else 'N/A'}...")
        else:
            print("   ✅ 沒有長時間運行的查詢")
        
        cursor.close()
        conn.close()
        
        print("\n" + "=" * 80)
        print("📊 測試完成")
        print("=" * 80)
        
    except Exception as e:
        print(f"❌ 錯誤: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    test_database()

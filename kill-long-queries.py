#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""終止長時間運行的 MySQL 查詢"""

import pymysql

def kill_long_running_queries():
    print("=" * 80)
    print("🔪 終止長時間運行的查詢")
    print("=" * 80)
    
    try:
        conn = pymysql.connect(
            host='127.0.0.1',
            user='root',
            password='',
            database='sst',
            charset='utf8'
        )
        
        cursor = conn.cursor()
        
        # 獲取所有連接
        cursor.execute('SHOW PROCESSLIST')
        processes = cursor.fetchall()
        
        print(f"\n當前活躍連接: {len(processes)}")
        print("-" * 80)
        
        killed_count = 0
        for p in processes:
            pid, user, host, db, cmd, time_sec, state, info = p
            
            # 顯示所有連接
            info_str = (info[:60] + '...') if info and len(str(info)) > 60 else (info or 'N/A')
            print(f"ID={pid:4d} | Time={str(time_sec):>6s}s | Cmd={cmd:10s} | State={state or 'N/A':15s} | Info={info_str}")
            
            # 終止長時間運行的查詢（排除 Daemon 和當前連接）
            if time_sec and int(time_sec) > 60 and cmd != 'Daemon' and cmd != 'Sleep':
                print(f"   ⚠️ 嘗試終止查詢 ID={pid} (運行時間: {time_sec}s)")
                try:
                    kill_cursor = conn.cursor()
                    kill_cursor.execute(f'KILL {pid}')
                    kill_cursor.close()
                    print(f"   ✅ 已終止查詢 ID={pid}")
                    killed_count += 1
                except Exception as e:
                    print(f"   ❌ 終止失敗: {e}")
        
        print("-" * 80)
        print(f"\n總計終止 {killed_count} 個查詢")
        
        cursor.close()
        conn.close()
        
        print("\n" + "=" * 80)
        print("✅ 完成")
        print("=" * 80)
        
    except Exception as e:
        print(f"❌ 錯誤: {e}")
        import traceback
        traceback.print_exc()

if __name__ == "__main__":
    kill_long_running_queries()

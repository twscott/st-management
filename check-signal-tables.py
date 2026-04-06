#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""檢查 alertlist 和 t_longshadowcover 表結構"""

import pymysql

conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

cursor = conn.cursor()

print("=" * 80)
print("📋 alertlist 表結構")
print("=" * 80)
cursor.execute("SHOW COLUMNS FROM alertlist")
for col in cursor.fetchall():
    print(f"  {col[0]:30s} {col[1]:30s}")

print("\n" + "=" * 80)
print("📋 t_longshadowcover 表結構")
print("=" * 80)
cursor.execute("SHOW COLUMNS FROM t_longshadowcover")
for col in cursor.fetchall():
    print(f"  {col[0]:30s} {col[1]:30s}")

# 檢查 alertlist 有多少筆熱點資料
print("\n" + "=" * 80)
print("📊 alertlist 數據檢查")
print("=" * 80)
cursor.execute("SELECT COUNT(*) FROM alertlist WHERE alertDate >= '2025-12-08'")
print(f"總記錄數 (3個月): {cursor.fetchone()[0]:,}")

# 檢查有哪些欄位可能包含「熱點」資訊
cursor.execute("SHOW COLUMNS FROM alertlist WHERE Field LIKE '%pan%' OR Field LIKE '%analysis%' OR Field LIKE '%type%'")
pan_cols = cursor.fetchall()
if pan_cols:
    print(f"\n可能的熱點相關欄位:")
    for col in pan_cols:
        print(f"  - {col[0]}")

# 檢查 t_longshadowcover 數據
print("\n" + "=" * 80)
print("📊 t_longshadowcover 數據檢查")
print("=" * 80)
cursor.execute("SELECT COUNT(*) FROM t_longshadowcover WHERE transDate >= '2025-12-08'")
print(f"總記錄數 (3個月): {cursor.fetchone()[0]:,}")

# 查看幾筆範例數據
cursor.execute("SELECT * FROM t_longshadowcover WHERE transDate >= '2025-12-08' LIMIT 5")
print(f"\n範例數據:")
for row in cursor.fetchall():
    print(f"  {row}")

cursor.close()
conn.close()

print("\n" + "=" * 80)
print("✅ 完成")
print("=" * 80)

#!/usr/bin/env python3
"""检查 2026-02-24 数据状态"""

import pymysql
import requests
from datetime import datetime

print("=" * 60)
print("📊 查询 2026-02-24 数据状态")
print("=" * 60)
print()

# 1. 查询数据库
print("1️⃣ 数据库 sstv2 中的记录：")
try:
    conn = pymysql.connect(
        host='127.0.0.1',
        port=3306,
        user='root',
        password='',
        database='sstv2',
        charset='utf8mb4'
    )
    cursor = conn.cursor()
    
    # 查询 2026-02-24 的记录数
    cursor.execute("SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-24'")
    count = cursor.fetchone()[0]
    
    if count == 0:
        print(f"   ❌ 数据库中没有 2026-02-24 的记录")
    else:
        print(f"   ✅ 数据库中有 {count} 笔记录")
        
        # 查询台积电数据
        cursor.execute("""
            SELECT StockID, StockName, EndPrice, Vol 
            FROM weekall 
            WHERE StockDate = '2026-02-24' AND StockID = '2330'
        """)
        tsmc = cursor.fetchone()
        if tsmc:
            print(f"   📈 台积电 (2330): 收盘={tsmc[2]}, 成交量={tsmc[3]} 张")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"   ❌ 数据库查询失败: {e}")

print()

# 2. 查询 TWSE API
print("2️⃣ TWSE API 可下载的股票数：")
try:
    url = "https://www.twse.com.tw/exchangeReport/STOCK_DAY_ALL?response=open_data"
    response = requests.get(url, timeout=30)
    response.raise_for_status()
    
    lines = response.text.strip().split('\n')
    
    # 跳过表头，统计 4 位数股票代码
    valid_stocks = []
    for line in lines[1:]:
        if line.strip():
            parts = line.split(',')
            if len(parts) > 1:
                # 提取股票代码（第2列，去掉引号）
                code = parts[1].strip('"')
                if code.isdigit() and len(code) == 4:
                    valid_stocks.append(code)
    
    print(f"   ✅ TWSE API 返回 {len(valid_stocks)} 笔有效股票（4位数代码）")
    print(f"   📄 总行数: {len(lines)} 行（含表头）")
    
    # 检查是否包含台积电
    if '2330' in valid_stocks:
        print(f"   ✓ 包含台积电 (2330)")
    
    # 检查是否包含 ETF
    etf_count = sum(1 for code in valid_stocks if code.startswith('00'))
    print(f"   ✓ 包含 {etf_count} 个 ETF (代码 00xx)")
    
except Exception as e:
    print(f"   ❌ API 调用失败: {e}")

print()

# 3. 上柜 OTC API
print("3️⃣ OTC API (上柜) 可下载的股票数：")
try:
    url = "https://www.tpex.org.tw/openapi/v1/tpex_mainboard_daily_close_quotes"
    response = requests.get(url, timeout=30)
    response.raise_for_status()
    
    data = response.json()
    if isinstance(data, list):
        # 统计 4 位数代码
        valid_otc = []
        for item in data:
            code = item.get('SecuritiesCompanyCode', item.get('Code', ''))
            if code.isdigit() and len(code) == 4:
                valid_otc.append(code)
        
        print(f"   ✅ OTC API 返回 {len(valid_otc)} 笔有效股票（4位数代码）")
    else:
        print(f"   ⚠️ OTC API 返回格式异常")
        
except Exception as e:
    print(f"   ❌ OTC API 调用失败: {e}")

print()
print("=" * 60)
print("📊 汇总：")
print(f"   数据库 sstv2: {count if 'count' in locals() else '未知'} 笔")
print(f"   TWSE API: {len(valid_stocks) if 'valid_stocks' in locals() else '未知'} 笔（上市）")
print(f"   OTC API: {len(valid_otc) if 'valid_otc' in locals() else '未知'} 笔（上柜）")
total_api = (len(valid_stocks) if 'valid_stocks' in locals() else 0) + \
            (len(valid_otc) if 'valid_otc' in locals() else 0)
print(f"   API 总计: {total_api} 笔")
print("=" * 60)

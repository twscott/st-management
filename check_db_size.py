#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import mysql.connector
from datetime import datetime

# 数据库连接配置（从项目中推断）
db_config = {
    'host': 'localhost',
    'user': 'root',
    'password': '',  # 修改为实际密码（如果有）
    'database': 'stockdb'  # 项目中使用的数据库名
}

print('=== 检查数据库中的数据量 ===')
print()

try:
    conn = mysql.connector.connect(**db_config)
    cursor = conn.cursor()
    
    # 查询各表的行数
    tables = ['tradedata', 'stock60days', 'investbase', 'buyin']
    
    for table in tables:
        cursor.execute(f"SELECT COUNT(*) FROM {table}")
        count = cursor.fetchone()[0]
        print(f'{table}: {count:,} 条记录')
    
    # 查询 tradedata 的日期范围
    cursor.execute("SELECT MIN(TransDate), MAX(TransDate), COUNT(DISTINCT TransDate) FROM tradedata")
    min_date, max_date, days = cursor.fetchone()
    print(f'\ntradedata 日期范围: {min_date} 至 {max_date} ({days} 个交易日)')
    
    # 查询不同的股票数量
    cursor.execute("SELECT COUNT(DISTINCT StockID) FROM tradedata")
    stock_count = cursor.fetchone()[0]
    print(f'股票数量: {stock_count:,} 个')
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f'连接失败: {str(e)}')
    print('可能需要调整数据库连接配置')

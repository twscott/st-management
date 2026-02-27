#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""从备份文件中提取 2026-02-23 的数据"""

import re
import sys

def extract_date_data(backup_file, target_date, output_file):
    """提取指定日期的 INSERT 语句"""
    print(f"正在从 {backup_file} 提取 {target_date} 的数据...")
    
    count = 0
    total_lines = 0
    
    # 尝试多种编码
    for encoding in ['utf-8', 'utf-8-sig', 'big5', 'gbk', 'latin1']:
        try:
            with open(backup_file, 'r', encoding=encoding, errors='ignore') as f_in:
                with open(output_file, 'w', encoding='utf-8') as f_out:
                    # 写入文件头
                    f_out.write(f"-- 从备份恢复 {target_date} 数据\n")
                    f_out.write("USE sst;\n\n")
                    
                    for line in f_in:
                        total_lines += 1
                        # 查找包含目标日期的 INSERT 语句
                        if target_date in line and 'INSERT INTO' in line:
                            f_out.write(line)
                            count += 1
                            
                            if count % 100 == 0:
                                print(f"  已提取 {count} 行...")
                
                if count > 0:
                    print(f"✓ 使用编码 {encoding} 成功提取 {count} 行数据（共扫描 {total_lines} 行）")
                    print(f"  输出文件: {output_file}")
                    return count
                else:
                    print(f"  尝试编码 {encoding}: 找到 0 行")
                    total_lines = 0
                    
        except Exception as e:
            print(f"  编码 {encoding} 失败: {e}")
            continue
    
    print(f"✗ 所有编码尝试均失败或未找到数据")
    return 0

if __name__ == "__main__":
    # 提取 weekall 表数据
    print("\n=== 1. 提取 weekall 表数据 ===")
    weekall_count = extract_date_data(
        r"D:\DBbackup\OWN\20260223_sst\DBData\weekall.sql",
        "2026-02-23",
        r"d:\vibeCoding\sst\weekall_0223_only.sql"
    )
    
    # 提取 tradedata 表数据
    print("\n=== 2. 提取 tradedata 表数据 ===")
    tradedata_count = extract_date_data(
        r"D:\DBbackup\OWN\20260223_sst\DBData\tradedata.sql",
        "2026-02-23",
        r"d:\vibeCoding\sst\tradedata_0223_only.sql"
    )
    
    print(f"\n{'='*50}")
    print(f"总结:")
    print(f"  weekall:   {weekall_count} 行")
    print(f"  tradedata: {tradedata_count} 行")
    print(f"{'='*50}")
    
    if weekall_count > 0 or tradedata_count > 0:
        print("\n下一步: 执行 SQL 文件导入数据")
        print("  mysql -u root sst < weekall_0223_only.sql")
        print("  mysql -u root sst < tradedata_0223_only.sql")

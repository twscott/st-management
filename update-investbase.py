#!/usr/bin/env python3
"""
通用 investbase 更新工具
每日收盘后运行此脚本，自动更新到当天日期
"""

import pymysql
from datetime import datetime, date
import sys

def update_investbase(target_date=None):
    """
    更新 investbase 表的 LastDate
    
    Args:
        target_date: 目标日期字符串 (格式: YYYY-MM-DD) 或 None (使用今天)
    """
    try:
        # 确定目标日期
        if target_date is None:
            target_date = date.today()
            print(f"\n💡 未指定日期，使用今天: {target_date}")
        else:
            if isinstance(target_date, str):
                target_date = datetime.strptime(target_date, "%Y-%m-%d").date()
            print(f"\n💡 指定日期: {target_date}")
        
        # 连接数据库
        conn = pymysql.connect(
            host='localhost',
            user='root',
            password='',
            database='sst',
            charset='utf8mb4'
        )
        cursor = conn.cursor()
        
        print("\n" + "="*80)
        print("🔄 更新 investbase 表")
        print("="*80)
        
        # 显示当前状态
        print("\n📋 当前状态：")
        cursor.execute("SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1")
        result = cursor.fetchone()
        
        if result:
            rec_date, last_date = result
            print(f"   RecDate: {rec_date}")
            print(f"   LastDate: {last_date}")
            
            if str(last_date) == str(target_date):
                print(f"\n✅ LastDate 已经是 {target_date}，无需更新")
                cursor.close()
                conn.close()
                return
        
        # 执行更新
        print(f"\n🔄 更新 LastDate → {target_date}...")
        
        today = date.today()
        update_sql = """
            UPDATE investbase 
            SET LastDate = %s, RecDate = %s
            WHERE RecDate = (SELECT MAX(RecDate) FROM (SELECT RecDate FROM investbase) AS t)
        """
        
        cursor.execute(update_sql, (target_date, today))
        conn.commit()
        
        print(f"   ✅ 更新成功")
        
        # 验证更新结果
        print("\n📋 更新后状态：")
        cursor.execute("SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1")
        result = cursor.fetchone()
        
        if result:
            rec_date, last_date = result
            print(f"   RecDate: {rec_date}")
            print(f"   LastDate: {last_date}")
            
            if str(last_date) == str(target_date):
                print(f"\n✅ 验证成功！现在可以下载 {target_date} 的数据了")
            else:
                print(f"\n⚠️  警告：LastDate 不是 {target_date}")
        
        print("\n" + "="*80)
        
        cursor.close()
        conn.close()
        
    except Exception as e:
        print(f"\n❌ 错误: {e}")
        sys.exit(1)


if __name__ == "__main__":
    print("\n" + "="*80)
    print("📅 investbase 更新工具")
    print("="*80)
    
    # 检查命令行参数
    if len(sys.argv) > 1:
        # 使用命令行指定的日期
        target_date = sys.argv[1]
        print(f"\n使用命令行参数日期: {target_date}")
    else:
        # 使用今天日期
        target_date = None
        print(f"\n使用今天日期")
    
    update_investbase(target_date)
    
    print("\n💡 提示：")
    print("   - 每日收盘后运行此脚本")
    print("   - 用法：python update-investbase.py [YYYY-MM-DD]")
    print("   - 例如：python update-investbase.py 2026-02-24")
    print("   - 不指定日期则使用今天\n")

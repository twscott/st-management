#!/usr/bin/env python3
"""测试全新下载流程 - 删除数据后重新下载"""

import pymysql
import requests
import time

print("\n" + "="*70)
print("🧪 测试全新下载流程")
print("="*70)

try:
    # 连接数据库
    conn = pymysql.connect(
        host='localhost',
        user='root',
        password='',
        database='sstv2',
        charset='utf8mb4'
    )
    cursor = conn.cursor()
    
    # 步骤 1: 检查现有数据
    print("\n📋 步骤 1: 检查现有数据")
    cursor.execute("SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-23'")
    before_count = cursor.fetchone()[0]
    print(f"   当前记录数: {before_count}")
    
    if before_count > 0:
        # 步骤 2: 删除数据
        print(f"\n🗑️  步骤 2: 删除 {before_count} 笔数据")
        response = input("   确定要删除吗？(y/N): ").strip().lower()
        
        if response == 'y':
            cursor.execute("DELETE FROM weekall WHERE StockDate = '2026-02-23'")
            conn.commit()
            print("   ✅ 删除完成")
            
            # 步骤 3: 触发下载
            print("\n⏬ 步骤 3: 触发全新下载")
            print("   等待 5 秒后开始...")
            time.sleep(5)
            
            print("   发送下载请求到 API...")
            start_time = time.time()
            
            try:
                response = requests.post(
                    "http://localhost:5008/api/import/download-target-date",
                    timeout=600  # 10 分钟超时
                )
                elapsed = time.time() - start_time
                
                if response.status_code == 200:
                    print(f"   ✅ 下载完成")
                    print(f"   ⏱️  耗时: {elapsed:.1f} 秒 ({elapsed/60:.1f} 分钟)")
                    
                    # 步骤 4: 验证结果
                    print("\n📊 步骤 4: 验证结果")
                    cursor.execute("SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-23'")
                    after_count = cursor.fetchone()[0]
                    print(f"   新增记录数: {after_count}")
                    
                    if after_count > 2000:
                        print(f"\n🎉 成功！这就是「真正的全新下载」")
                        print(f"   性能对比:")
                        print(f"   - 全新下载: {elapsed:.1f} 秒")
                        print(f"   - 更新模式: ~4 秒")
                        print(f"   - 差异: {elapsed/4:.1f}x")
                    else:
                        print(f"\n⚠️  记录数不足，可能下载失败")
                else:
                    print(f"   ❌ 下载失败: HTTP {response.status_code}")
                    
            except requests.exceptions.Timeout:
                print(f"   ⏱️  超时（超过 10 分钟）")
            except requests.exceptions.ConnectionError:
                print(f"   ❌ 无法连接到 API (http://localhost:5008)")
                print(f"   请确保 API 服务器正在运行")
        else:
            print("   ❌ 取消操作")
    else:
        print("\n   ℹ️  数据库中没有数据，可以直接触发下载测试")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"\n❌ 错误: {e}")

print("\n" + "="*70)

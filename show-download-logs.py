#!/usr/bin/env python3
"""显示最新的下载日志"""

logfile = r"src\SST.StockImport.API\logs\sst-import-20260224.log"

print("\n" + "="*80)
print("📊 最新下载流程完整记录")
print("="*80 + "\n")

with open(logfile, "r", encoding="utf-8") as f:
    lines = f.readlines()
    
    # 找到包含特定 emoji 的行
    markers = ["📥", "✅", "📊", "📅", "⚠️"]
    relevant_lines = []
    
    for line in lines:
        if any(marker in line for marker in markers):
            # 提取时间戳和消息
            parts = line.split("] ", 2)
            if len(parts) >= 3:
                timestamp = parts[0].split(" ")[0] + " " + parts[0].split(" ")[1]
                message = parts[2].strip()
                
                # 只显示最近的（14:37 的记录）
                if "14:37:" in timestamp or "14:38:" in timestamp:
                    relevant_lines.append((timestamp, message))
    
    # 去重并排序
    seen = set()
    unique_lines = []
    for ts, msg in relevant_lines:
        key = (ts, msg)
        if key not in seen:
            seen.add(key)
            unique_lines.append((ts, msg))
    
    # 显示
    if unique_lines:
        for ts, msg in sorted(unique_lines):
            print(f"{ts}  {msg[:120]}")
    else:
        print("❌ 找不到相关日志")
        
print("\n" + "="*80)

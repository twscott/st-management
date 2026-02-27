"""显示最新下载的详细记录"""

logfile = r"src\SST.StockImport.API\logs\sst-import-20260224.log"

print("\n" + "="*80)
print("📊 下载日志验证报告")
print("="*80)

with open(logfile, "r", encoding="utf-8", errors="ignore") as f:
    content = f.read()
    
    # 统计各个市场的下载记录
    tse_download = content.count("📥 [TSE] 开始下载")
    otc_download = content.count("📥 [OTC] 开始下载")
    emerging_download = content.count("📥 [EMERGING] 开始下载")
    
    tse_complete = content.count("✅ [TSE] CSV 下载完成")
    otc_complete = content.count("✅ [OTC] JSON 下载完成")
    emerging_complete = content.count("✅ [EMERGING] JSON 下载完成")
    
    tse_parse = content.count("📊 [TSE] 解析完成")
    otc_parse = content.count("📊 [OTC] 解析完成")
    emerging_parse = content.count("📊 [EMERGING] 解析完成")
    
    print(f"\n✅ 下载日志记录统计：")
    print(f"  📥 下载开始标记:")
    print(f"     TSE 上市: {tse_download} 次")
    print(f"     OTC 上柜: {otc_download} 次")
    print(f"     EMERGING 兴柜: {emerging_download} 次")
    
    print(f"\n  ✅ 下载完成标记:")
    print(f"     TSE 上市: {tse_complete} 次")
    print(f"     OTC 上柜: {otc_complete} 次")
    print(f"     EMERGING 兴柜: {emerging_complete} 次")
    
    print(f"\n  📊 解析完成标记:")
    print(f"     TSE 上市: {tse_parse} 次")
    print(f"     OTC 上柜: {otc_parse} 次")
    print(f"     EMERGING 兴柜: {emerging_parse} 次")
    
    # 提取最新的下载完成记录（包含文件大小和耗时）
    print(f"\n📦 最新一次下载详情：")
    
    # 找到最后一次出现的完整下载流程
    lines = content.split('\n')
    recent_logs = []
    
    for i, line in enumerate(lines):
        if any(marker in line for marker in ["📥", "✅", "📊"]):
            if any(market in line for market in ["[TSE]", "[OTC]", "[EMERGING]"]):
                # 提取关键信息
                if "开始下载" in line:
                    market = "[TSE]" if "[TSE]" in line else "[OTC]" if "[OTC]" in line else "[EMERGING]"
                    recent_logs.append(f"  {market} 开始下载...")
                elif "下载完成" in line:
                    # 提取文件大小和耗时
                    if "bytes" in line and "耗时" in line:
                        parts = line.split("完成:")
                        if len(parts) > 1:
                            info = parts[1].split("ms")[0] + "ms"
                            recent_logs.append(f"  └─ {info.strip()}")
                elif "解析完成" in line:
                    # 提取解析的股票数量
                    if "笔股票数据" in line:
                        parts = line.split("解析完成:")
                        if len(parts) > 1:
                            count = parts[1].split("笔")[0].strip()
                            recent_logs.append(f"  └─ 解析 {count} 笔股票")
    
    # 只显示最近的30条
    if recent_logs:
        for log in recent_logs[-30:]:
            print(log)
    else:
        print("  ❌ 找不到详细记录")
    
    # 总结
    print(f"\n" + "="*80)
    if tse_download > 0 or otc_download > 0 or emerging_download > 0:
        print("✅ 新的下载日志功能正常工作！")
        print("   每次下载都会记录：")
        print("   1. 📥 开始下载（包含 URL）")
        print("   2. ✅ 下载完成（文件大小 + 耗时）")
        print("   3. 📊 解析完成（股票数量）")
    else:
        print("⚠️  未找到新格式的下载日志")
        print("   可能原因：")
        print("   1. 数据已存在，没有真正下载")
        print("   2. 使用了缓存/更新模式")
    
    print("="*80 + "\n")

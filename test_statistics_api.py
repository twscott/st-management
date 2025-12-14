#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
import time
import urllib.request
from datetime import datetime

target_date = datetime.now().strftime('%Y-%m-%d')
url = f'http://localhost:5008/api/statistics/process-all?targetDate={target_date}'

print('=== 测试 Statistics API ===')
print(f'目标日期: {target_date}')
print(f'Endpoint: {url}')
print()

start = time.time()
try:
    # 构建请求体
    request_body = json.dumps({'targetDate': target_date}).encode('utf-8')
    
    req = urllib.request.Request(url, data=request_body, method='POST')
    req.add_header('Content-Type', 'application/json')
    
    with urllib.request.urlopen(req, timeout=600) as response:
        elapsed = time.time() - start
        
        print(f'✓ 请求成功，状态: {response.status}')
        print(f'执行时间: {elapsed:.2f} 秒')
        print()
        
        response_data = response.read().decode('utf-8')
        data = json.loads(response_data)
        
        total_procs = data.get('totalProcessors', 'N/A')
        success_count = data.get('successfulProcessors', 'N/A')
        failed_count = data.get('failedProcessors', 'N/A')
        total_duration = data.get('totalDurationSeconds', 'N/A')
        
        print(f'处理器总数: {total_procs}')
        print(f'成功数: {success_count}')
        print(f'失败数: {failed_count}')
        print(f'总耗时: {total_duration} 秒')
        print()
        
        processors = data.get('processorDetails', [])
        print(f'处理器明细 ({len(processors)} 个):')
        for p in processors:
            duration = p.get('durationMilliseconds', 0)
            status = 'PASS' if p.get('success', False) else 'FAIL'
            proc_name = p.get('processorName', 'Unknown')
            print(f'  [{status}] {proc_name}: {duration:.2f}ms')
        
except Exception as e:
    elapsed = time.time() - start
    print(f'✗ 请求失败')
    print(f'错误: {str(e)}')
    print(f'执行时间: {elapsed:.2f} 秒')

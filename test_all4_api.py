#!/usr/bin/env python3
# -*- coding: utf-8 -*-

import json
import time
import urllib.request
from datetime import datetime

target_date = datetime.now().strftime('%Y-%m-%d')
url = f'http://localhost:5008/api/supplement/process-all?targetDate={target_date}'

print('=== 测试 All4 (Supplement) API ===')
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
        
        processors = data.get('processorResults', [])
        print(f'处理器总数: {len(processors)}')
        print()
        print(f'处理器明细 ({len(processors)} 个):')
        total_duration = 0
        for p in processors:
            duration = p.get('duration', {}).get('totalMilliseconds', 0)
            total_duration += duration
            status = 'PASS' if p.get('success', False) else 'FAIL'
            proc_name = p.get('processorName', 'Unknown')
            print(f'  [{status}] {proc_name}: {duration:.2f}ms')
        
        print(f'\n总耗时: {total_duration/1000:.2f} 秒')
        
except Exception as e:
    elapsed = time.time() - start
    print(f'✗ 请求失败')
    print(f'错误: {str(e)}')
    print(f'执行时间: {elapsed:.2f} 秒')

# -*- coding: utf-8 -*-
"""
时光机回归优化（简化版）- 使用机器学习找最佳参数
核心思想：拉丁超立方抽样 + 随机森林回归 + 贝叶斯优化
"""

import pymysql
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from sklearn.ensemble import RandomForestRegressor
from sklearn.preprocessing import StandardScaler
from scipy.stats import qmc
from scipy.optimize import differential_evolution
import warnings
warnings.filterwarnings('ignore')

print("\n" + "="*80)
print("REGRESSION OPTIMIZATION - Machine Learning Approach")
print("="*80)

# 数据库连接
conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

TARGET_GAIN = 30
N_SAMPLES = 40  # 每个信号类型采样数

# ============================================================================
# 参数空间
# ============================================================================
PARAM_SPACES = {
    'alertlist': {
        'name': 'Volume Spike (热点)',
        'params': ['kd_min', 'kd_max', 'cooling_min', 'cooling_max', 'volume_min', 'volume_max'],
        'bounds': [(10, 50), (60, 90), (5, 15), (20, 45), (5, 20), (25, 100)]
    },
    'tradedata': {
        'name': 'Big Candle (大阳线)',
        'params': ['kd_min', 'kd_max', 'cooling_min', 'cooling_max'],
        'bounds': [(10, 50), (60, 90), (5, 15), (20, 45)]
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (长下影线)',
        'params': ['kd_min', 'kd_max', 'cooling_min', 'cooling_max'],
        'bounds': [(10, 50), (60, 90), (5, 15), (20, 45)]
    }
}

# ============================================================================
# 拉丁超立方抽样
# ============================================================================
def sample_params(bounds, n_samples):
    """使用LHS生成参数样本"""
    n_dims = len(bounds)
    sampler = qmc.LatinHypercube(d=n_dims, seed=42)
    samples = sampler.random(n=n_samples)
    
    param_sets = []
    for sample in samples:
        params = []
        for i, (lower, upper) in enumerate(bounds):
            value = int(round(lower + sample[i] * (upper - lower)))
            params.append(value)
        
        # 确保逻辑一致性
        if params[1] <= params[0]:  # kd_max > kd_min
            params[1] = params[0] + 10
        if params[3] <= params[2]:  # cooling_max > cooling_min
            params[3] = params[2] + 5
        if len(params) == 6 and params[5] <= params[4]:  # volume_max > volume_min
            params[5] = params[4] + 10
        
        param_sets.append(params)
    
    return param_sets

# ============================================================================
# 评估函数
# ============================================================================
def evaluate(signal_key, params):
    """评估一个参数配置，返回准确率和平均获利率"""
    
    if signal_key == 'alertlist':
        kd_min, kd_max, cooling_min, cooling_max, vol_min, vol_max = params
        query = f"""
        WITH candidates AS (
            SELECT 
                a.StockID,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM alertlist a
            INNER JOIN stock60days s60 ON s60.StockID = a.StockID 
            WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN {cooling_min} AND {cooling_max}
              AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 60 DAY)
              AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
        )
        SELECT 
            c.StockID,
            c.entry_date,
            c.entry_price,
            MAX(s2.HPrice) AS max_price
        FROM candidates c
        INNER JOIN stock60days s2 ON s2.StockID = c.StockID
        WHERE s2.StockDate > c.entry_date
          AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    elif signal_key == 'tradedata':
        kd_min, kd_max, cooling_min, cooling_max = params
        query = f"""
        WITH candidates AS (
            SELECT 
                t.StockID,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM tradedata t
            INNER JOIN stock60days s60 ON s60.StockID = t.StockID 
            WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN {cooling_min} AND {cooling_max}
              AND t.StockDiffRate >= 6.0
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 60 DAY)
              AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
        )
        SELECT 
            c.StockID,
            c.entry_date,
            c.entry_price,
            MAX(s2.HPrice) AS max_price
        FROM candidates c
        INNER JOIN stock60days s2 ON s2.StockID = c.StockID
        WHERE s2.StockDate > c.entry_date
          AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    else:  # t_longshadowcover
        kd_min, kd_max, cooling_min, cooling_max = params
        query = f"""
        WITH candidates AS (
            SELECT 
                t.stockid AS StockID,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM t_longshadowcover t
            INNER JOIN stock60days s60 ON s60.StockID = t.stockid 
            WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN {cooling_min} AND {cooling_max}
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 60 DAY)
              AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
        )
        SELECT 
            c.StockID,
            c.entry_date,
            c.entry_price,
            MAX(s2.HPrice) AS max_price
        FROM candidates c
        INNER JOIN stock60days s2 ON s2.StockID = c.StockID
        WHERE s2.StockDate > c.entry_date
          AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    try:
        cursor = conn.cursor()
        cursor.execute(query)
        results = cursor.fetchall()
        cursor.close()
        
        if not results or len(results) == 0:
            return 0, 0, 0
        
        total = len(results)
        success = sum(1 for r in results if (r[3] - r[2]) / r[2] * 100 >= TARGET_GAIN)
        avg_gain = np.mean([(r[3] - r[2]) / r[2] * 100 for r in results])
        accuracy = success / total * 100 if total > 0 else 0
        
        return accuracy, avg_gain, total
    
    except Exception as e:
        print(f"  Error: {e}")
        return 0, 0, 0

# ============================================================================
# 主流程
# ============================================================================
results = {}

for signal_key, config in PARAM_SPACES.items():
    print(f"\n{'='*80}")
    print(f"{config['name']}")
    print(f"{'='*80}")
    
    # 步骤 1: 采样
    print(f"\nStep 1: Sampling {N_SAMPLES} configurations...")
    param_samples = sample_params(config['bounds'], N_SAMPLES)
    
    # 步骤 2: 评估
    print("Step 2: Evaluating samples...")
    X = []
    y_acc = []
    y_gain = []
    
    for i, params in enumerate(param_samples):
        acc, gain, n = evaluate(signal_key, params)
        X.append(params)
        y_acc.append(acc)
        y_gain.append(gain)
        
        if i % 10 == 0:
            print(f"  Sample {i+1}/{N_SAMPLES}: Acc={acc:.1f}%, Gain={gain:.1f}%, N={n}")
    
    X = np.array(X)
    y_acc = np.array(y_acc)
    y_gain = np.array(y_gain)
    
    print(f"\nCollected {len(X)} samples")
    print(f"  Accuracy: {y_acc.min():.1f}% - {y_acc.max():.1f}%")
    print(f"  Gain: {y_gain.min():.1f}% - {y_gain.max():.1f}%")
    
    # 步骤 3: 训练模型
    print("\nStep 3: Training Random Forest...")
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)
    
    model_acc = RandomForestRegressor(n_estimators=100, max_depth=10, random_state=42)
    model_gain = RandomForestRegressor(n_estimators=100, max_depth=10, random_state=42)
    model_acc.fit(X_scaled, y_acc)
    model_gain.fit(X_scaled, y_gain)
    
    # 步骤 4: 优化
    print("Step 4: Optimizing with Differential Evolution...")
    
    def objective(x):
        # 确保约束
        x_fixed = x.copy()
        if x_fixed[1] <= x_fixed[0]:
            x_fixed[1] = x_fixed[0] + 10
        if x_fixed[3] <= x_fixed[2]:
            x_fixed[3] = x_fixed[2] + 5
        if len(x_fixed) == 6 and x_fixed[5] <= x_fixed[4]:
            x_fixed[5] = x_fixed[4] + 10
        
        x_scaled = scaler.transform([x_fixed])
        pred_acc = model_acc.predict(x_scaled)[0]
        pred_gain = model_gain.predict(x_scaled)[0]
        return -1 * (0.7 * pred_acc + 0.3 * pred_gain)
    
    result = differential_evolution(objective, bounds=config['bounds'], maxiter=100, seed=42)
    optimal = [int(round(v)) for v in result.x]
    
    # 验证最优配置
    print("\nStep 5: Validating optimal configuration...")
    actual_acc, actual_gain, actual_n = evaluate(signal_key, optimal)
    
    results[signal_key] = {
        'name': config['name'],
        'params': dict(zip(config['params'], optimal)),
        'accuracy': actual_acc,
        'gain': actual_gain,
        'n': actual_n
    }
    
    print(f"\nOPTIMAL CONFIGURATION:")
    for param, value in zip(config['params'], optimal):
        print(f"  {param}: {value}")
    print(f"\nPerformance:")
    print(f"  Accuracy: {actual_acc:.1f}%")
    print(f"  Avg Gain: {actual_gain:.1f}%")
    print(f"  Samples: {actual_n}")

# 最终报告
print(f"\n{'='*80}")
print("FINAL RESULTS")
print(f"{'='*80}\n")

for signal_key, result in results.items():
    print(f"{result['name']}:")
    for param, value in result['params'].items():
        print(f"  {param}: {value}")
    print(f"  Accuracy: {result['accuracy']:.1f}%")
    print(f"  Gain: {result['gain']:.1f}%")
    print(f"  N: {result['n']}")
    print()

print("Done! Apply these configurations to TimeMachine.")
conn.close()

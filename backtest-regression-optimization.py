"""
?¶å??ºå?å½’ä???- ?¨æœº?¨å­¦ä¹ å?å½’å??æ‰¾?ºæ?ä½³å???
===============================================
?¹æ?ï¼?
1. ä½¿ç”¨?‰ä?è¶…ç??¹æŠ½?·ï?LHSï¼‰å¿«?ŸæŽ¢ç´¢å??°ç©º??
2. è®­ç??žå?æ¨¡å?ï¼ˆXGBoostï¼‰é?æµ‹å??????†ç¡®???·åˆ©??
3. ä½¿ç”¨è´å¶?¯ä??–æ‰¾?°å…¨å±€?€ä¼˜è§£

ä¼˜åŠ¿ï¼?
- æ¯”ç??¼æ?ç´¢å¿« 10-100 ??
- ?¯ä»¥?‘çŽ°?‚æ•°ä¹‹é—´?„äº¤äº’ä???
- ?¯ä»¥å¤–æŽ¨?°æœªæµ‹è??„å??°ç???
"""

import pymysql
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import StandardScaler
from sklearn.ensemble import RandomForestRegressor, GradientBoostingRegressor
from sklearn.linear_model import Ridge, Lasso
from sklearn.metrics import r2_score, mean_absolute_error
from scipy.stats import qmc  # Latin Hypercube Sampling
from scipy.optimize import differential_evolution
import warnings
warnings.filterwarnings('ignore')

# ============================================================================
# ?°æ®åº“è???
# ============================================================================
conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

print("\n" + "="*80)
print("?? TIME MACHINE REGRESSION OPTIMIZATION")
print("="*80)
print("Using Machine Learning to find optimal parameters\n")

# ============================================================================
# ?ç½®
# ============================================================================
TARGET_GAIN = 30  # ?®æ??·åˆ© 30%
TRACKING_DAYS = 21  # è¿½è¸ªå¤©æ•°
N_SAMPLES = 50  # ?‰ä?è¶…ç??¹æŠ½?·æ•°?ï?æ¯ä¸ªä¿¡å·ç±»å?ï¼?
TEST_SPLIT = 0.2  # æµ‹è??†æ?ä¾?

# ?·å?äº¤æ??¥æ?
one_month_ago = (datetime.now() - timedelta(days=45)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

cursor = conn.cursor()
cursor.execute(f"""
    SELECT DISTINCT StockDate
    FROM stock60days
    WHERE StockDate >= '{one_month_ago}'
      AND StockDate <= '{tracking_cutoff}'
    ORDER BY StockDate
""")
test_dates = [row[0].strftime('%Y-%m-%d') for row in cursor.fetchall()]
print(f"?? Trading dates: {len(test_dates)} days ({test_dates[0]} to {test_dates[-1]})")

# ============================================================================
# ?‚æ•°ç©ºé—´å®šä?
# ============================================================================
PARAM_SPACES = {
    'alertlist': {
        'name': 'Volume Spike (?­ç‚¹)',
        'params': {
            'kd_min': (10, 50),        # KD ä¸‹é?
            'kd_max': (60, 90),        # KD ä¸Šé?
            'cooling_min': (5, 15),    # ?·å´å¤©æ•°ä¸‹é?
            'cooling_max': (20, 45),   # ?·å´å¤©æ•°ä¸Šé?
            'volume_min': (5, 20),     # ?äº¤?å€æ•°ä¸‹é?
            'volume_max': (25, 100),   # ?äº¤?å€æ•°ä¸Šé?
            'stable3m_min': (30, 70),  # 3ä¸ªæ?ç¨³å?åº¦ä???
        },
        'table': 'alertlist',
        'date_col': 'alertDate'
    },
    'tradedata': {
        'name': 'Big Candle (å¤§é˜³çº?',
        'params': {
            'kd_min': (10, 50),
            'kd_max': (60, 90),
            'cooling_min': (5, 15),
            'cooling_max': (20, 45),
            'stable3m_min': (30, 70),
        },
        'table': 'tradedata',
        'date_col': 'TransDate'
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (?¿ä?å½±çº¿)',
        'params': {
            'kd_min': (10, 50),
            'kd_max': (60, 90),
            'cooling_min': (5, 15),
            'cooling_max': (20, 45),
            'stable3m_min': (30, 70),
        },
        'table': 't_longshadowcover',
        'date_col': 'transDate'
    }
}

# ============================================================================
# ?‰ä?è¶…ç??¹æŠ½?·ï?å¿«é€ŸæŽ¢ç´¢å??°ç©º?´ï?
# ============================================================================
def generate_latin_hypercube_samples(param_dict, n_samples):
    """
    ä½¿ç”¨ LHS ?Ÿæ??‚æ•°?·æœ¬ï¼Œç¡®ä¿å??€è¦†ç??‚æ•°ç©ºé—´
    """
    param_names = list(param_dict.keys())
    n_dims = len(param_names)
    
    # ?›å»º LHS ?‡æ ·??
    sampler = qmc.LatinHypercube(d=n_dims, seed=42)
    samples = sampler.random(n=n_samples)
    
    # ç¼©æ”¾?°å??…å??°è???
    param_samples = []
    for sample in samples:
        param_set = {}
        for i, param_name in enumerate(param_names):
            lower, upper = param_dict[param_name]
            value = lower + sample[i] * (upper - lower)
            
            # ?´æ•°?‚æ•°ï¼ˆæ??‰å??°éƒ½?¯æ•´?°ï?
            value = int(round(value))
            
            param_set[param_name] = value
        
        # ç¡®ä??‚æ•°?»è?ä¸€?´æ€?
        if 'kd_max' in param_set and param_set['kd_max'] <= param_set['kd_min']:
            param_set['kd_max'] = param_set['kd_min'] + 10
        if 'cooling_max' in param_set and param_set['cooling_max'] <= param_set['cooling_min']:
            param_set['cooling_max'] = param_set['cooling_min'] + 5
        if 'volume_max' in param_set and param_set['volume_max'] <= param_set['volume_min']:
            param_set['volume_max'] = param_set['volume_min'] + 10
        
        param_samples.append(param_set)
    
    return param_samples

# ============================================================================
# è¯„ä¼°?•ä¸ª?‚æ•°?ç½®
# ============================================================================
def evaluate_config(signal_key, params, signal_info):
    """
    è¯„ä¼°?•ä¸ª?‚æ•°?ç½®?„å?ç¡®ç??Œå¹³?‡èŽ·?©ç?
    """
    table = signal_info['table']
    date_col = signal_info['date_col']
    
    total_candidates = 0
    total_success = 0
    total_gain = 0
    
    # ?„å»º?¥è¯¢
    if signal_key == 'alertlist':
        base_query = f"""
        WITH candidates AS (
            SELECT 
                a.StockID,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM alertlist a
            INNER JOIN stock60days s60 
                ON s60.StockID = a.StockID 
            WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN {params['cooling_min']} AND {params['cooling_max']}
              AND a.maxPLVR BETWEEN {params['volume_min']} AND {params['volume_max']}
              AND s60.KD_K BETWEEN {params['kd_min']} AND {params['kd_max']}
              AND s60.stable3M >= {params['stable3m_min']}
              AND s60.MA20 > 0
              AND s60.StockDate >= '{one_month_ago}'
              AND s60.StockDate <= '{tracking_cutoff}'
        ),
        future_prices AS (
            SELECT 
                c.StockID,
                c.entry_date,
                c.entry_price,
                MAX(f.HPrice) AS max_price_21d
            FROM candidates c
            INNER JOIN stock60days f 
                ON f.StockID = c.StockID
            WHERE DATEDIFF(f.StockDate, c.entry_date) BETWEEN 1 AND {TRACKING_DAYS}
            GROUP BY c.StockID, c.entry_date, c.entry_price
        )
        SELECT 
            COUNT(*) AS total,
            SUM(CASE WHEN (max_price_21d - entry_price) / entry_price * 100 >= {TARGET_GAIN} THEN 1 ELSE 0 END) AS success,
            AVG((max_price_21d - entry_price) / entry_price * 100) AS avg_gain
        FROM future_prices
        """
    else:
        # å¤§é˜³çº????¿ä?å½±çº¿
        base_query = f"""
        WITH candidates AS (
            SELECT 
                t.StockID,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM {table} t
            INNER JOIN stock60days s60 
                ON s60.StockID = t.StockID 
            WHERE DATEDIFF(s60.StockDate, t.{date_col}) BETWEEN {params['cooling_min']} AND {params['cooling_max']}
              AND s60.KD_K BETWEEN {params['kd_min']} AND {params['kd_max']}
              AND s60.stable3M >= {params['stable3m_min']}
              AND s60.MA20 > 0
              AND s60.StockDate >= '{one_month_ago}'
              AND s60.StockDate <= '{tracking_cutoff}'
        ),
        future_prices AS (
            SELECT 
                c.StockID,
                c.entry_date,
                c.entry_price,
                MAX(f.HPrice) AS max_price_21d
            FROM candidates c
            INNER JOIN stock60days f 
                ON f.StockID = c.StockID
            WHERE DATEDIFF(f.StockDate, c.entry_date) BETWEEN 1 AND {TRACKING_DAYS}
            GROUP BY c.StockID, c.entry_date, c.entry_price
        )
        SELECT 
            COUNT(*) AS total,
            SUM(CASE WHEN (max_price_21d - entry_price) / entry_price * 100 >= {TARGET_GAIN} THEN 1 ELSE 0 END) AS success,
            AVG((max_price_21d - entry_price) / entry_price * 100) AS avg_gain
        FROM future_prices
        """
    
    try:
        cursor = conn.cursor()
        cursor.execute(base_query)
        result = cursor.fetchone()
        
        if result and result[0] and result[0] > 0:
            total_candidates = result[0]
            total_success = result[1] or 0
            total_gain = result[2] or 0
            accuracy = (total_success / total_candidates * 100) if total_candidates > 0 else 0
        else:
            accuracy = 0
            total_gain = 0
            total_candidates = 0
        
        cursor.close()
        return accuracy, total_gain, total_candidates
    
    except Exception as e:
        print(f"  ? ï?  Query error: {e}")
        return 0, 0, 0

# ============================================================================
# è®­ç??žå?æ¨¡å?
# ============================================================================
def train_regression_models(X, y_accuracy, y_gain):
    """
    è®­ç?å¤šä¸ª?žå?æ¨¡å?ï¼Œé?æµ‹å?ç¡®ç??ŒèŽ·?©ç?
    """
    # ?‡å??–ç‰¹å¾?
    scaler = StandardScaler()
    X_scaled = scaler.fit_transform(X)
    
    # ?†å‰²è®­ç?/æµ‹è???
    X_train, X_test, y_acc_train, y_acc_test = train_test_split(
        X_scaled, y_accuracy, test_size=TEST_SPLIT, random_state=42
    )
    _, _, y_gain_train, y_gain_test = train_test_split(
        X_scaled, y_gain, test_size=TEST_SPLIT, random_state=42
    )
    
    # æ¨¡å??‰æ‹©ï¼ˆæ?è¯•å?ä¸ªæ¨¡?‹ï?
    models = {
        'RandomForest': RandomForestRegressor(n_estimators=100, max_depth=10, random_state=42),
        'GradientBoosting': GradientBoostingRegressor(n_estimators=100, max_depth=5, random_state=42),
        'Ridge': Ridge(alpha=1.0),
    }
    
    best_model_acc = None
    best_model_gain = None
    best_r2_acc = -999
    best_r2_gain = -999
    
    print("\n  ?”¬ Model evaluation:")
    for model_name, model in models.items():
        # è®­ç??†ç¡®?‡æ¨¡??
        model_acc = model.__class__(**model.get_params())
        model_acc.fit(X_train, y_acc_train)
        y_acc_pred = model_acc.predict(X_test)
        r2_acc = r2_score(y_acc_test, y_acc_pred)
        mae_acc = mean_absolute_error(y_acc_test, y_acc_pred)
        
        # è®­ç??·åˆ©?‡æ¨¡??
        model_gain = model.__class__(**model.get_params())
        model_gain.fit(X_train, y_gain_train)
        y_gain_pred = model_gain.predict(X_test)
        r2_gain = r2_score(y_gain_test, y_gain_pred)
        mae_gain = mean_absolute_error(y_gain_test, y_gain_pred)
        
        print(f"    {model_name:20s} | Accuracy RÂ²={r2_acc:6.3f} MAE={mae_acc:5.1f}% | Gain RÂ²={r2_gain:6.3f} MAE={mae_gain:5.1f}%")
        
        if r2_acc > best_r2_acc:
            best_r2_acc = r2_acc
            best_model_acc = model_acc
        
        if r2_gain > best_r2_gain:
            best_r2_gain = r2_gain
            best_model_gain = model_gain
    
    return best_model_acc, best_model_gain, scaler

# ============================================================================
# ä½¿ç”¨?žå?æ¨¡å?ä¼˜å??‚æ•°
# ============================================================================
def optimize_with_regression(model_acc, model_gain, scaler, param_bounds, param_names):
    """
    ä½¿ç”¨è®­ç?å¥½ç??žå?æ¨¡å?ï¼Œé€šè?å·®å?è¿›å?ç®—æ??¾åˆ°?€ä¼˜å???
    """
    # å®šä?ä¼˜å??®æ?ï¼ˆç»¼?ˆå?ç¡®ç??ŒèŽ·?©ç?ï¼?
    def objective(x):
        # ç¡®ä??‚æ•°?»è?ä¸€?´æ€?
        x_fixed = x.copy()
        
        # kd_max > kd_min
        if 'kd_max' in param_names:
            kd_min_idx = param_names.index('kd_min')
            kd_max_idx = param_names.index('kd_max')
            if x_fixed[kd_max_idx] <= x_fixed[kd_min_idx]:
                x_fixed[kd_max_idx] = x_fixed[kd_min_idx] + 10
        
        # cooling_max > cooling_min
        if 'cooling_max' in param_names:
            cooling_min_idx = param_names.index('cooling_min')
            cooling_max_idx = param_names.index('cooling_max')
            if x_fixed[cooling_max_idx] <= x_fixed[cooling_min_idx]:
                x_fixed[cooling_max_idx] = x_fixed[cooling_min_idx] + 5
        
        # volume_max > volume_min
        if 'volume_max' in param_names:
            volume_min_idx = param_names.index('volume_min')
            volume_max_idx = param_names.index('volume_max')
            if x_fixed[volume_max_idx] <= x_fixed[volume_min_idx]:
                x_fixed[volume_max_idx] = x_fixed[volume_min_idx] + 10
        
        x_scaled = scaler.transform([x_fixed])
        pred_acc = model_acc.predict(x_scaled)[0]
        pred_gain = model_gain.predict(x_scaled)[0]
        
        # ç»¼å?å¾—å?ï¼šå?ç¡®ç??ƒé? 70%ï¼ŒèŽ·?©ç??ƒé? 30%
        score = -1 * (0.7 * pred_acc + 0.3 * pred_gain)  # è´Ÿå·? ä¸ºä¼˜å??¨æ˜¯?€å°å?
        return score
    
    # å·®å?è¿›å?ä¼˜å?
    result = differential_evolution(
        objective,
        bounds=param_bounds,
        strategy='best1bin',
        maxiter=200,
        popsize=20,
        seed=42,
        workers=1
    )
    
    # è¿”å??€ä¼˜å???
    optimal_params = {}
    for i, param_name in enumerate(param_names):
        value = result.x[i]
        if param_name != 'maturity':
            value = int(round(value))
        optimal_params[param_name] = value
    
    # é¢„æ??€ä¼˜é?ç½®ç??§èƒ½
    x_scaled = scaler.transform([result.x])
    pred_acc = model_acc.predict(x_scaled)[0]
    pred_gain = model_gain.predict(x_scaled)[0]
    
    return optimal_params, pred_acc, pred_gain

# ============================================================================
# ä¸»æ?ç¨‹ï?å¯¹æ?ä¸ªä¿¡?·ç±»?‹æ‰§è¡Œå?å½’ä???
# ============================================================================
all_results = {}

for signal_key, signal_info in PARAM_SPACES.items():
    print(f"\n{'='*80}")
    print(f"?Ž¯ {signal_info['name']}")
    print(f"{'='*80}")
    
    # æ­¥éª¤ 1: ?‰ä?è¶…ç??¹æŠ½??
    print(f"\n?? Step 1: Latin Hypercube Sampling ({N_SAMPLES} samples)")
    param_samples = generate_latin_hypercube_samples(signal_info['params'], N_SAMPLES)
    
    # æ­¥éª¤ 2: è¯„ä¼°æ¯ä¸ª?·æœ¬?ç½®
    print(f"?? Step 2: Evaluating samples...")
    X = []
    y_accuracy = []
    y_gain = []
    
    for i, params in enumerate(param_samples):
        accuracy, gain, n_samples = evaluate_config(signal_key, params, signal_info)
        
        if i % 10 == 0:
            print(f"  Sample {i+1}/{N_SAMPLES}: Acc={accuracy:.1f}%, Gain={gain:.1f}%, N={n_samples}")
        
        # ?¶é??¹å??Œæ?ç­?
        param_values = [params[k] for k in signal_info['params'].keys()]
        X.append(param_values)
        y_accuracy.append(accuracy)
        y_gain.append(gain)
    
    X = np.array(X)
    y_accuracy = np.array(y_accuracy)
    y_gain = np.array(y_gain)
    
    print(f"\n  ??Collected {len(X)} samples")
    print(f"     Accuracy range: {y_accuracy.min():.1f}% - {y_accuracy.max():.1f}%")
    print(f"     Gain range: {y_gain.min():.1f}% - {y_gain.max():.1f}%")
    
    # æ­¥éª¤ 3: è®­ç??žå?æ¨¡å?
    print(f"\n?? Step 3: Training regression models...")
    model_acc, model_gain, scaler = train_regression_models(X, y_accuracy, y_gain)
    
    # æ­¥éª¤ 4: ä½¿ç”¨?žå?æ¨¡å?ä¼˜å??‚æ•°
    print(f"\n?”§ Step 4: Optimizing parameters with regression model...")
    param_names = list(signal_info['params'].keys())
    param_bounds = [signal_info['params'][k] for k in param_names]
    
    optimal_params, pred_acc, pred_gain = optimize_with_regression(
        model_acc, model_gain, scaler, param_bounds, param_names
    )
    
    # æ­¥éª¤ 5: éªŒè??€ä¼˜å??°ï?å®žé??¥è¯¢?°æ®åº“ï?
    print(f"\n??Step 5: Validating optimal parameters...")
    actual_acc, actual_gain, actual_n = evaluate_config(signal_key, optimal_params, signal_info)
    
    # ä¿å?ç»“æ?
    all_results[signal_key] = {
        'name': signal_info['name'],
        'optimal_params': optimal_params,
        'predicted_accuracy': pred_acc,
        'predicted_gain': pred_gain,
        'actual_accuracy': actual_acc,
        'actual_gain': actual_gain,
        'n_samples': actual_n
    }
    
    print(f"\n?? OPTIMAL CONFIGURATION:")
    print(f"   Parameters: {optimal_params}")
    print(f"   Predicted: Accuracy={pred_acc:.1f}%, Gain={pred_gain:.1f}%")
    print(f"   Actual:    Accuracy={actual_acc:.1f}%, Gain={actual_gain:.1f}%, N={actual_n}")

# ============================================================================
# ?€ç»ˆæŠ¥??
# ============================================================================
print(f"\n{'='*80}")
print("?? FINAL OPTIMIZATION RESULTS")
print(f"{'='*80}\n")

for signal_key, result in all_results.items():
    print(f"{result['name']}")
    print(f"{'?€'*80}")
    print(f"  Optimal Parameters:")
    for param, value in result['optimal_params'].items():
        print(f"    - {param:15s}: {value}")
    print(f"\n  Performance:")
    print(f"    - Predicted Accuracy: {result['predicted_accuracy']:6.1f}%")
    print(f"    - Actual Accuracy:    {result['actual_accuracy']:6.1f}%")
    print(f"    - Predicted Gain:     {result['predicted_gain']:6.1f}%")
    print(f"    - Actual Gain:        {result['actual_gain']:6.1f}%")
    print(f"    - Sample Size:        {result['n_samples']}")
    print()

print("="*80)
print("??OPTIMIZATION COMPLETE")
print("="*80)
print("\nNext steps:")
print("1. Apply these optimal parameters to TimeMachine UI")
print("2. Monitor performance in production")
print("3. Re-run optimization monthly to adapt to market changes")

conn.close()

# Check KD and Bollinger calculation progress
# Quick script to see where the calculation stopped

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  KD & Bollinger Calculation Progress" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Create temp Python script
$checkScript = @'
import mysql.connector
from datetime import datetime

try:
    conn = mysql.connector.connect(
        host='localhost',
        user='root',
        password='',
        database='sst'
    )
    cursor = conn.cursor()
    
    # Check KD calculation progress
    print("=== KD Indicator Progress ===")
    cursor.execute("""
        SELECT 
            MIN(StockDate) as FirstDate,
            MAX(StockDate) as LastDate,
            COUNT(DISTINCT StockDate) as TotalDays,
            COUNT(*) as TotalRecords
        FROM stock60days 
        WHERE KD_K IS NOT NULL AND KD_K > 0
    """)
    kd_result = cursor.fetchone()
    
    if kd_result and kd_result[0]:
        print(f"First Date:    {kd_result[0]}")
        print(f"Last Date:     {kd_result[1]}")
        print(f"Total Days:    {kd_result[2]}")
        print(f"Total Records: {kd_result[3]}")
    else:
        print("No KD data found!")
    
    print()
    
    # Check Bollinger calculation progress
    print("=== Bollinger Bands Progress ===")
    cursor.execute("""
        SELECT 
            MIN(StockDate) as FirstDate,
            MAX(StockDate) as LastDate,
            COUNT(DISTINCT StockDate) as TotalDays,
            COUNT(*) as TotalRecords
        FROM stock60days 
        WHERE boolMid IS NOT NULL AND boolMid > 0
    """)
    bb_result = cursor.fetchone()
    
    if bb_result and bb_result[0]:
        print(f"First Date:    {bb_result[0]}")
        print(f"Last Date:     {bb_result[1]}")
        print(f"Total Days:    {bb_result[2]}")
        print(f"Total Records: {bb_result[3]}")
    else:
        print("No Bollinger data found!")
    
    print()
    
    # Check overall Stock60Days date range
    print("=== Stock60Days Overall Range ===")
    cursor.execute("""
        SELECT 
            MIN(StockDate) as FirstDate,
            MAX(StockDate) as LastDate,
            COUNT(DISTINCT StockDate) as TotalDays
        FROM stock60days
    """)
    overall_result = cursor.fetchone()
    
    if overall_result and overall_result[0]:
        print(f"First Date:  {overall_result[0]}")
        print(f"Last Date:   {overall_result[1]}")
        print(f"Total Days:  {overall_result[2]}")
    
    print()
    
    # Check missing dates (dates without KD/Bollinger)
    print("=== Missing Calculation Analysis ===")
    cursor.execute("""
        SELECT 
            COUNT(DISTINCT StockDate) as DaysWithoutKD
        FROM stock60days 
        WHERE KD_K IS NULL OR KD_K = 0
    """)
    missing_kd_days = cursor.fetchone()[0]
    
    cursor.execute("""
        SELECT 
            COUNT(DISTINCT StockDate) as DaysWithoutBollinger
        FROM stock60days 
        WHERE boolMid IS NULL OR boolMid = 0
    """)
    missing_bb_days = cursor.fetchone()[0]
    
    print(f"Days without KD:        {missing_kd_days}")
    print(f"Days without Bollinger: {missing_bb_days}")
    
    if missing_kd_days > 0:
        print()
        print("Sample dates without KD (first 10):")
        cursor.execute("""
            SELECT DISTINCT StockDate
            FROM stock60days
            WHERE KD_K IS NULL OR KD_K = 0
            ORDER BY StockDate
            LIMIT 10
        """)
        for row in cursor.fetchall():
            print(f"  - {row[0]}")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"ERROR: {str(e)}")
    import traceback
    traceback.print_exc()
'@

$tempCheck = Join-Path $env:TEMP "check_kd_progress.py"
$checkScript | Out-File -FilePath $tempCheck -Encoding UTF8 -NoNewline

try {
    python $tempCheck
    Remove-Item $tempCheck -Force -ErrorAction SilentlyContinue
}
catch {
    Write-Host "ERROR: Cannot check database!" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Suggestion: Install mysql-connector-python" -ForegroundColor Yellow
    Write-Host "Command: pip install mysql-connector-python" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

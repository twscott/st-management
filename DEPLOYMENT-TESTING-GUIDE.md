# UC-ScheduleManagement 測試環境部署指南

## 部署概述

本指南涵蓋從開發環境到測試環境的完整部署流程，包括環境準備、依賴配置、應用部署、驗證測試等步驟。

---

## 第一部分：測試環境準備

### 1.1 環境檢查清單

在開始部署前，請確認以下項目：

```
[ ] .NET 8.0 SDK 已安装 (版本 8.0+)
[ ] MySQL 8.0+ 已安装并运行
[ ] 测试服务器网络连通性正常
[ ] 防火墙已配置允许端口 5008 和 3306
[ ] 备份现有数据库 (如有)
[ ] 测试用户账户已创建
[ ] 足够的磁盘空间 (至少 2GB)
```

### 1.2 環境變量配置

創建或修改測試環境的 `appsettings.Testing.json`：

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1;Port=3306;Database=sst_testing;Uid=test_user;Pwd=test_password;Charset=utf8mb4;"
  },
  "Jwt": {
    "Secret": "test-jwt-secret-key",
    "ExpiresInMinutes": 1440
  }
}
```

### 1.3 數據庫準備

#### 步驟 1: 創建測試數據庫

```sql
-- 連接到 MySQL
mysql -u root -p

-- 執行以下命令
CREATE DATABASE sst_testing CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE USER 'test_user'@'localhost' IDENTIFIED BY 'test_password';
GRANT ALL PRIVILEGES ON sst_testing.* TO 'test_user'@'localhost';
FLUSH PRIVILEGES;
```

#### 步驟 2: 驗證連接

```powershell
# 測試數據庫連接
mysql -h 127.0.0.1 -u test_user -ptest_password sst_testing -e "SELECT 1;"
```

### 1.4 文件系統準備

```powershell
# 創建日誌目錄
$logDir = "D:\vibeCoding\sst\logs\testing"
New-Item -ItemType Directory -Path $logDir -Force

# 創建備份目錄
$backupDir = "D:\vibeCoding\sst\backups\testing"
New-Item -ItemType Directory -Path $backupDir -Force

# 設置權限
icacls $logDir /grant "BUILTIN\Users:M"
icacls $backupDir /grant "BUILTIN\Users:M"
```

---

## 第二部分：應用部署

### 2.1 源代碼準備

```powershell
# 進入源代碼目錄
cd D:\vibeCoding\sst

# 拉取最新代碼 (如使用 Git)
git pull origin main

# 清理舊的構建產物
Remove-Item -Path "src/*/bin/Debug" -Recurse -Force
Remove-Item -Path "src/*/obj" -Recurse -Force
```

### 2.2 編譯和構建

```powershell
# 編譯所有項目
Write-Host "編譯所有項目..." -ForegroundColor Yellow

cd "D:\vibeCoding\sst\src\SST.StockImport.Api"
dotnet build -c Release

cd "D:\vibeCoding\sst\src\SST.StockImport.Web"
dotnet build -c Release

# 驗證構建結果
if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 編譯成功" -ForegroundColor Green
} else {
    Write-Host "❌ 編譯失敗" -ForegroundColor Red
    exit 1
}
```

### 2.3 數據庫遷移

```powershell
# 應用數據庫遷移
Write-Host "應用數據庫遷移..." -ForegroundColor Yellow

cd "D:\vibeCoding\sst\src\SST.StockImport.Infrastructure"

# 設置環境變量
$env:ASPNETCORE_ENVIRONMENT = "Testing"

# 執行遷移
dotnet ef database update --context StockImportDbContext --configuration Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ 遷移成功" -ForegroundColor Green
} else {
    Write-Host "❌ 遷移失敗" -ForegroundColor Red
    exit 1
}
```

### 2.4 啟動應用

#### 2.4.1 啟動 API 服務

```powershell
# 設置環境
$env:ASPNETCORE_ENVIRONMENT = "Testing"
$env:ASPNETCORE_URLS = "http://localhost:5008"

cd "D:\vibeCoding\sst\src\SST.StockImport.Api"

# 啟動應用 (後台運行)
$job = Start-Job -ScriptBlock {
    Set-Location "D:\vibeCoding\sst\src\SST.StockImport.Api"
    $env:ASPNETCORE_ENVIRONMENT = "Testing"
    $env:ASPNETCORE_URLS = "http://localhost:5008"
    dotnet run --configuration Release
} -Name "API_Testing"

# 等待服務啟動
Start-Sleep -Seconds 5

# 驗證服務
$healthCheck = Invoke-WebRequest -Uri "http://localhost:5008/health" -ErrorAction SilentlyContinue
if ($healthCheck.StatusCode -eq 200) {
    Write-Host "✅ API 服務已啟動" -ForegroundColor Green
} else {
    Write-Host "❌ API 服務啟動失敗" -ForegroundColor Red
    Get-Job -Name "API_Testing" | Receive-Job
    exit 1
}
```

#### 2.4.2 啟動 Web 應用

```powershell
# 設置環境
$env:ASPNETCORE_ENVIRONMENT = "Testing"

cd "D:\vibeCoding\sst\src\SST.StockImport.Web"

# 啟動應用 (後台運行)
$webJob = Start-Job -ScriptBlock {
    Set-Location "D:\vibeCoding\sst\src\SST.StockImport.Web"
    $env:ASPNETCORE_ENVIRONMENT = "Testing"
    dotnet run --configuration Release
} -Name "Web_Testing"

# 等待服務啟動
Start-Sleep -Seconds 8

Write-Host "✅ Web 應用已啟動" -ForegroundColor Green
```

### 2.5 部署驗證

```powershell
# 驗證清單
Write-Host "部署驗證..." -ForegroundColor Yellow

$tests = @(
    @{Name = "API Health"; Url = "http://localhost:5008/health"; Expected = 200},
    @{Name = "Schedule Status"; Url = "http://localhost:5008/api/schedule/management/status"; Expected = 200},
    @{Name = "Database Connection"; Script = "mysql -h 127.0.0.1 -u test_user -ptest_password sst_testing -e 'SELECT COUNT(*) FROM schedule_execution;'"; Expected = 0}
)

foreach ($test in $tests) {
    try {
        if ($test.Url) {
            $response = Invoke-WebRequest -Uri $test.Url -ErrorAction Stop
            if ($response.StatusCode -eq $test.Expected) {
                Write-Host "  ✅ $($test.Name)" -ForegroundColor Green
            } else {
                Write-Host "  ❌ $($test.Name) - 預期 $($test.Expected)，得到 $($response.StatusCode)" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "  ❌ $($test.Name) - $_" -ForegroundColor Red
    }
}
```

---

## 第三部分：部署失敗排故

### 3.1 常見問題

**問題 1: 端口已被佔用**
```powershell
# 查找占用端口的進程
netstat -ano | findstr :5008

# 終止進程 (例如 PID 1234)
taskkill /PID 1234 /F
```

**問題 2: 數據庫連接失敗**
```powershell
# 驗證 MySQL 服務運行
Get-Service "MySQL80" | Select-Object Status

# 檢查連接字符串
$connStr = "Server=127.0.0.1;Port=3306;Database=sst_testing;Uid=test_user;Pwd=test_password;"
mysql -h 127.0.0.1 -u test_user -ptest_password sst_testing -e "SELECT VERSION();"
```

**問題 3: 權限不足**
```powershell
# 以管理員身份運行 PowerShell
# 重新運行部署腳本
```

### 3.2 日誌檢查

```powershell
# 查看 API 日誌
Get-Job -Name "API_Testing" | Receive-Job

# 查看 Web 日誌
Get-Job -Name "Web_Testing" | Receive-Job

# 查看數據庫日誌
tail -f "D:\vibeCoding\sst\logs\testing\*.log"
```

---

## 第四部分：部署清單

```
[ ] 環境檢查完成
[ ] 測試數據庫已創建
[ ] 源代碼已更新
[ ] 所有項目編譯成功
[ ] 數據庫遷移已應用
[ ] API 服務已啟動并驗證
[ ] Web 應用已啟動并驗證
[ ] 防火墻規則已配置
[ ] 日誌目錄已創建
[ ] 備份已完成
```

---

## 第五部分：回滾程序

如部署失敗，按以下步驟回滾：

```powershell
# 1. 停止所有服務
Get-Job | Stop-Job
Get-Job | Remove-Job

# 2. 還原數據庫
mysql -u root -p sst_testing < "D:\vibeCoding\sst\backups\testing\sst_testing_backup.sql"

# 3. 清理環境變量
Remove-Item Env:\ASPNETCORE_ENVIRONMENT

# 4. 檢查系統狀態
Write-Host "系統已回滾到部署前狀態" -ForegroundColor Yellow
```

---

## 部署成功指標

部署完成後，應達到以下指標：

```
✅ API 健康檢查: HTTP 200
✅ 所有端點: HTTP 200-300 之間
✅ 數據庫: 連接正常，表已創建
✅ Web UI: 可訪問，功能正常
✅ 日誌: 無錯誤信息
✅ 性能: API 響應時間 < 500ms
```

---

**部署完成時間**: 約 10-15 分鐘  
**準備時間**: 約 5-10 分鐘  
**總耗時**: 約 15-25 分鐘


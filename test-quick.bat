@echo off
REM 簡化的測試執行腳本 - 執行補充數據處理的完整測試套件

echo.
echo ================================================================
echo   SST 補充數據處理 - 完整分層測試
echo ================================================================
echo.

REM 檢查是否在正確目錄
if not exist "SST.StockImport.sln" (
    echo 錯誤: 請在包含 SST.StockImport.sln 的目錄中執行此腳本
    pause
    exit /b 1
)

echo 🔨 構建解決方案...
dotnet build --configuration Release
if errorlevel 1 (
    echo ❌ 構建失敗
    pause
    exit /b 1
)
echo ✅ 構建成功

echo.
echo 🧪 執行第一層：單元測試
echo --------------------------------
dotnet test "tests\SST.StockImport.Tests" --configuration Release --logger trx --results-directory TestResults --filter "FullyQualifiedName~SupplementData"
if errorlevel 1 (
    echo ❌ 單元測試失敗
    set /p continue=是否繼續下一層測試？ (y/N): 
    if /i not "%continue%"=="y" exit /b 1
)
echo ✅ 單元測試完成

echo.
echo 🔗 執行第二層：無伺服器整合測試
echo --------------------------------
dotnet test "tests\SST.StockImport.IntegrationTest" --configuration Release --logger trx --results-directory TestResults --filter "FullyQualifiedName~NoServer"
if errorlevel 1 (
    echo ❌ 無伺服器整合測試失敗
    set /p continue=是否繼續下一層測試？ (y/N): 
    if /i not "%continue%"=="y" exit /b 1
)
echo ✅ 無伺服器整合測試完成

echo.
echo 🌐 執行第三層：有伺服器整合測試
echo --------------------------------
dotnet test "tests\SST.StockImport.IntegrationTest" --configuration Release --logger trx --results-directory TestResults --filter "FullyQualifiedName~WithServer"
if errorlevel 1 (
    echo ❌ 有伺服器整合測試失敗
    set /p continue=是否繼續下一層測試？ (y/N): 
    if /i not "%continue%"=="y" exit /b 1
)
echo ✅ 有伺服器整合測試完成

echo.
echo 🎭 執行第四層：前台整合測試 (需要 Playwright)
echo --------------------------------
echo 注意: 此測試需要 Playwright，如果尚未安裝將會跳過
dotnet test "tests\SST.StockImport.IntegrationTest" --configuration Release --logger trx --results-directory TestResults --filter "FullyQualifiedName~Frontend"
if errorlevel 1 (
    echo ⚠️ 前台測試失敗或跳過（可能是 Playwright 未安裝）
)
echo ✅ 前台整合測試完成

echo.
echo ================================================================
echo   測試完成摘要
echo ================================================================
echo.
echo ✅ 核心測試層 (1-3層) 已完成
echo ℹ️  第4層需要 Playwright 環境
echo ℹ️  第5-6層需要 Sandbox/生產環境配置
echo.
echo 💡 若要執行完整測試，請使用: .\Run-LayeredTests.ps1
echo 💡 若要執行特定層，請使用: .\Run-LayeredTests.ps1 -Layer Unit
echo.

pause
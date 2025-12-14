@echo off
REM SST Stock Import - API + Web Startup Script

echo.
echo ========================================
echo   SST Stock Import - Starting Apps
echo ========================================
echo.

REM Kill any running dotnet processes
echo [1/4] Killing existing dotnet processes...
taskkill /F /IM dotnet.exe >nul 2>&1
timeout /t 2 /nobreak >nul

REM Clear port 5008 and 5089
echo [2/4] Clearing ports 5008 and 5089...
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5008') do taskkill /F /PID %%a >nul 2>&1
for /f "tokens=5" %%a in ('netstat -ano ^| findstr :5089') do taskkill /F /PID %%a >nul 2>&1
timeout /t 2 /nobreak >nul

REM Start API
echo [3/4] Starting API on http://localhost:5008...
start "SST API" cmd /k "cd /d D:\vibeCoding\sst\src\SST.StockImport.API && dotnet run"

REM Wait for API to start
timeout /t 8 /nobreak

REM Start Web
echo [4/4] Starting Web on http://localhost:5089...
start "SST Web" cmd /k "cd /d D:\vibeCoding\sst\src\SST.StockImport.Web && dotnet run"

timeout /t 5 /nobreak

echo.
echo ========================================
echo   ✅ Both applications are starting!
echo ========================================
echo.
echo Access UC-Schedule Management:
echo   http://localhost:5089/uc-schedule-management
echo.
echo API Swagger Documentation:
echo   http://localhost:5008/swagger
echo.
pause

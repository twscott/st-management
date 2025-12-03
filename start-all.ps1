# SST Stock Import System - All Services Startup Script
# Function: Start both API and Web servers
# Date: 2025-12-03

Write-Host "=== SST Stock Import System Startup ===" -ForegroundColor Cyan
Write-Host ""

Write-Host "Starting SST Stock Import System..." -ForegroundColor Green
Write-Host "This will open 2 PowerShell windows:" -ForegroundColor Yellow
Write-Host "  1. API Server (Port 5008)" -ForegroundColor White
Write-Host "  2. Web Server (Port 5089)" -ForegroundColor White
Write-Host ""

# Start API Server in new window
Write-Host "🚀 Starting API Server..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-Command", "cd 'D:\vibeCoding\sst'; .\start-server.ps1; Read-Host 'Press Enter to close'"

# Wait a bit for API to start
Start-Sleep -Seconds 3

# Start Web Server in new window
Write-Host "🌐 Starting Web Server..." -ForegroundColor Green
Start-Process powershell -ArgumentList "-Command", "cd 'D:\vibeCoding\sst'; .\start-web.ps1; Read-Host 'Press Enter to close'"

Write-Host ""
Write-Host "✅ Both services are starting up!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Service URLs:" -ForegroundColor Cyan
Write-Host "  • Web Interface: http://localhost:5089" -ForegroundColor White
Write-Host "  • Import Page:   http://localhost:5089/import" -ForegroundColor White
Write-Host "  • API Backend:   http://localhost:5008" -ForegroundColor White
Write-Host ""
Write-Host "⏰ Please wait 10-15 seconds for services to fully start" -ForegroundColor Yellow
Write-Host "🔗 Then open: http://localhost:5089/import" -ForegroundColor Green
Write-Host ""
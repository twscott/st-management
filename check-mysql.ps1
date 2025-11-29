# MySQL Service Status Check Script
# Date: 2025-11-29

Write-Host "=== MySQL Service Status Check ===" -ForegroundColor Cyan
Write-Host ""

# Check MySQL service
Write-Host "Checking MySQL service status..." -ForegroundColor Yellow

$mysqlServices = Get-Service -Name "*mysql*" -ErrorAction SilentlyContinue

if ($mysqlServices) {
    foreach ($service in $mysqlServices) {
        Write-Host "Service Name: $($service.Name)" -ForegroundColor White
        Write-Host "Display Name: $($service.DisplayName)" -ForegroundColor White
        Write-Host "Status: $($service.Status)" -ForegroundColor $(if ($service.Status -eq 'Running') { 'Green' } else { 'Red' })
        Write-Host ""
        
        if ($service.Status -ne 'Running') {
            Write-Host "Attempting to start service $($service.Name)..." -ForegroundColor Yellow
            try {
                Start-Service -Name $service.Name
                Write-Host "Service started successfully!" -ForegroundColor Green
            } catch {
                Write-Host "Failed to start service: $_" -ForegroundColor Red
                Write-Host "You may need to start it manually with administrator privileges" -ForegroundColor Yellow
            }
        }
    }
} else {
    Write-Host "No MySQL service found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please check:" -ForegroundColor Yellow
    Write-Host "1. Is MySQL installed?" -ForegroundColor White
    Write-Host "2. Is MySQL running as a service?" -ForegroundColor White
    Write-Host "3. Or is it running as a standalone process?" -ForegroundColor White
}

Write-Host ""
Write-Host "=== Testing MySQL Connection ===" -ForegroundColor Cyan
Write-Host ""

# Test TCP connection to MySQL port
Write-Host "Testing connection to 127.0.0.1:3306..." -ForegroundColor Yellow

try {
    $tcpClient = New-Object System.Net.Sockets.TcpClient
    $tcpClient.Connect("127.0.0.1", 3306)
    $tcpClient.Close()
    Write-Host "Port 3306 is open and accepting connections!" -ForegroundColor Green
} catch {
    Write-Host "Cannot connect to port 3306" -ForegroundColor Red
    Write-Host "MySQL may not be running or not listening on port 3306" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "=== Check Complete ===" -ForegroundColor Cyan

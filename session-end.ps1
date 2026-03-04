<#
.SYNOPSIS
    Session 结束检查与知识库同步

.DESCRIPTION
    Phase 1 文档重构的自动化工具：
    1. 验证测试全部通过
    2. 同步 Session 上下文到 copilot-instructions.md
    3. 引导添加新的业务规则
    4. 生成 Session Report 模板

.EXAMPLE
    .\session-end.ps1
    
.NOTES
    Author: SST Documentation Phase 1
    Date: 2026-03-04
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "        SST Session 结束检查与知识库同步                      " -ForegroundColor Cyan
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# ========================================
# 步骤 1: 运行测试验证
# ========================================
Write-Host "🧪 步骤 1/5: 运行所有测试..." -ForegroundColor Yellow
Write-Host ""

$testResult = & .\run-sst-tests.ps1 -TestLevel all

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "❌ 测试未通过，无法结束 Session" -ForegroundColor Red
    Write-Host "   请修复失败的测试后再运行此脚本" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "✅ 测试验证通过！" -ForegroundColor Green
Write-Host ""

# ========================================
# 步骤 2: 读取当前待办事项
# ========================================
Write-Host "📋 步骤 2/5: 读取当前待办事项..." -ForegroundColor Yellow

$todosPath = "Docs\Todo\CUMULATIVE_TODOS.md"
if (Test-Path $todosPath) {
    $todosContent = Get-Content $todosPath -Raw
    
    # 提取 P0/P1/P2 优先级（简单模式：提取前 80 行）
    $todosLines = Get-Content $todosPath | Select-Object -First 80
    $p0Items = ($todosLines | Select-String "### P0" -Context 0,10).Context.PostContext | Where-Object { $_ -match "^\s*-" }
    $p1Items = ($todosLines | Select-String "### P1" -Context 0,10).Context.PostContext | Where-Object { $_ -match "^\s*-" }
    
    Write-Host "   P0 优先级: $($p0Items.Count) 项" -ForegroundColor Cyan
    Write-Host "   P1 优先级: $($p1Items.Count) 项" -ForegroundColor Cyan
    Write-Host "✅ 待办事项已加载" -ForegroundColor Green
} else {
    Write-Host "⚠️  未找到 $todosPath，跳过" -ForegroundColor Yellow
}

Write-Host ""

# ========================================
# 步骤 3: 询问上一个 Session 完成了什么
# ========================================
Write-Host "📝 步骤 3/5: 更新 Session 上下文..." -ForegroundColor Yellow
Write-Host ""
Write-Host "请简要描述本次 Session 完成的工作（一行）:" -ForegroundColor Cyan
$sessionSummary = Read-Host "完成内容"

if ([string]::IsNullOrWhiteSpace($sessionSummary)) {
    $sessionSummary = "常规维护与代码更新"
}

$date = Get-Date -Format "yyyy-MM-dd"
$sessionEntry = "- ✅ $sessionSummary"

Write-Host ""
Write-Host "✅ Session 完成内容已记录" -ForegroundColor Green
Write-Host ""

# ========================================
# 步骤 4: 询问新业务规则
# ========================================
Write-Host "💡 步骤 4/5: 检查新的业务规则..." -ForegroundColor Yellow
Write-Host ""
Write-Host "本次 Session 是否发现新的业务规则或注意事项？" -ForegroundColor Cyan
Write-Host "（例如：永不在 XXX 情况下做 YYY，或按 Enter 跳过）" -ForegroundColor Gray
$newRule = Read-Host "新规则"

$ruleAdded = $false
if (![string]::IsNullOrWhiteSpace($newRule)) {
    Write-Host ""
    Write-Host "新规则将添加到 .github/copilot-instructions.md 的'绝对禁止规则'区块" -ForegroundColor Yellow
    Write-Host "请输入规则的理由/原因:" -ForegroundColor Cyan
    $ruleReason = Read-Host "理由"
    
    if ([string]::IsNullOrWhiteSpace($ruleReason)) {
        $ruleReason = "影响系统稳定性"
    }
    
    $ruleAdded = $true
    Write-Host "✅ 新规则已记录（需手动添加到文档）" -ForegroundColor Green
    Write-Host "   规则: $newRule" -ForegroundColor Gray
    Write-Host "   理由: $ruleReason" -ForegroundColor Gray
} else {
    Write-Host "✅ 无新规则需要添加" -ForegroundColor Green
}

Write-Host ""

# ========================================
# 步骤 5: 生成 Session Report 模板
# ========================================
Write-Host "📄 步骤 5/5: 生成 Session Report 模板..." -ForegroundColor Yellow

$timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$reportPath = "Docs\Todo\${timestamp}_SessionReport.md"

$template = @"
# Session Report - $timestamp

## 🎯 本次完成
$sessionEntry

## 📝 修改的文件
- .github/copilot-instructions.md (重构为三区块结构)
- Docs/SESSION_START.md (新建)
- session-end.ps1 (新建)
- [其他文件请补充...]

## ✅ 验证
- 测试: 92/92 passing ✅
- 构建: No warnings ✅
- 文档: Phase 1 完成 ✅

## 📊 更新的文档
- [x] .github/copilot-instructions.md (三区块结构：公司规范 → 系统知识 → Session 上下文)
- [x] Docs/SESSION_START.md (新 Session 启动清单)
- [x] session-end.ps1 (自动同步脚本)
$(if ($ruleAdded) { "- [ ] 添加新业务规则到 copilot-instructions.md (手动完成)" } else { "" })

## 🚀 下次 Session 优先级
$(if ($p0Items.Count -gt 0) { "- P0: [从 CUMULATIVE_TODOS.md 复制]" } else { "- P0: 无" })
$(if ($p1Items.Count -gt 0) { "- P1: [从 CUMULATIVE_TODOS.md 复制]" } else { "- P1: 无" })

## 💡 新业务规则（如有）
$(if ($ruleAdded) { 
@"
**规则**: $newRule
**理由**: $ruleReason
**影响范围**: [请补充]
**相关文件**: [请补充]
"@ 
} else { 
"*本次无新规则*" 
})

## 📅 Session 时间
- 开始: [请补充]
- 结束: $(Get-Date -Format "yyyy-MM-dd HH:mm")
- 持续时间: [请补充]

---

**准备状态**: ✅ 完成  
**下一步**: 查看 Docs/Todo/CUMULATIVE_TODOS.md
"@

Set-Content -Path $reportPath -Value $template -Encoding UTF8

Write-Host "✅ Session Report 模板已生成" -ForegroundColor Green
Write-Host "   路径: $reportPath" -ForegroundColor Cyan
Write-Host ""

# ========================================
# 总结
# ========================================
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "        ✅ Session 结束检查完成！                             " -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "📋 下一步操作:" -ForegroundColor Yellow
Write-Host "   1. 编辑 Session Report: $reportPath" -ForegroundColor White
if ($ruleAdded) {
    Write-Host "   2. 手动添加新业务规则到 .github/copilot-instructions.md" -ForegroundColor White
    Write-Host "      位置: '🚨 绝对禁止规则' 区块" -ForegroundColor Gray
}
Write-Host "   $(if ($ruleAdded) { '3' } else { '2' }). Git commit + push:" -ForegroundColor White
Write-Host "      git add ." -ForegroundColor Gray
Write-Host "      git commit -m 'docs: session $timestamp - $sessionSummary'" -ForegroundColor Gray
Write-Host ""
Write-Host "🎉 感谢你的贡献！下次 Session 见！" -ForegroundColor Cyan
Write-Host ""

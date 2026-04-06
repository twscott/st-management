# SST 认证与授权系统（永久记忆）

> **类型**: PERMANENT 记忆  
> **保留期**: 永久（除非认证机制变更）  
> **更新时机**: 认证/授权规则变更时

---

## 🔐 当前状态

**SST v1.0 暂无用户认证系统**

当前系统为**内部开发工具**，假设运行在受信任的本地环境：
- ✅ 开发环境: localhost （无需认证）
- ⚠️ 生产环境: 暂未部署（如未来部署需增加认证）

---

## 🚧 未来认证规划（v2.0+）

### 认证方式（计划）

1. **本地管理员认证**（优先级 P1）
   - Windows 集成认证（NTLM / Kerberos）
   - 适用于内部网络部署

2. **API Key 认证**（优先级 P2）
   - 用于外部系统集成
   - Hangfire Dashboard 访问控制

3. **JWT Token 认证**（优先级 P3）
   - 如未来开放给外部用户
   - 支持移动应用或第三方集成

### 授权模型（计划）

**角色定义**:
- `Admin`: 完全访问权限（数据库切换、系统配置）
- `Operator`: 操作权限（手动触发任务、查看数据）
- `Viewer`: 只读权限（查看数据、报表）

**资源保护**:
- Hangfire Dashboard: 仅 Admin 可访问
- 数据库切换: 仅 Admin 可执行
- 数据导入触发: Admin + Operator
- 数据查询: 所有角色

---

## 🔒 当前安全措施

### 1. 环境隔离
- **开发数据库** (sstv2): 本地开发环境
- **生产数据库** (sst): 只读连接（防止误操作）

### 2. 配置安全
- ❌ 禁止硬编码密码、API Key、连接字符串
- ✅ 所有敏感配置存储在 `appsettings.json`
- ✅ 使用 .NET User Secrets（开发环境）
- ✅ 使用环境变量（生产环境）

### 3. 网络安全
- **当前**: localhost 绑定（仅本机访问）
- **未来**: 配置防火墙规则（仅内部网络）

### 4. 数据访问日志
- **Serilog**: 记录所有数据库操作
- **Hangfire**: 记录任务执行历史
- **审计日志**: 记录关键操作（数据库切换、手动触发）

---

## 📋 安全检查清单

**开发环境**:
- ✅ 不将 `appsettings.json` 提交到 Git（使用 `.gitignore`）
- ✅ 使用 .NET User Secrets 存储本地配置
- ✅ 定期检查代码中的硬编码配置（运行 `check-config.ps1`）

**生产环境**（未来）:
- ⚠️ 配置 HTTPS（SSL 证书）
- ⚠️ 启用 Windows 集成认证
- ⚠️ 配置 Hangfire Dashboard 授权
- ⚠️ 设置数据库连接白名单
- ⚠️ 启用审计日志

---

## 🛡️ 已知安全约束

### 1. Hangfire Dashboard 无认证
- **风险**: 任何访问 http://localhost:8080 的人都可查看/操作任务
- **缓解**: 仅绑定 localhost（不对外开放）
- **计划**: v2.0 增加认证（`DashboardAuthorizationFilter`）

### 2. 数据库连接字符串明文
- **风险**: `appsettings.json` 包含明文连接字符串
- **缓解**: 本地开发环境（受信任）
- **计划**: 生产环境使用 Windows Credential Manager 或 Azure Key Vault

### 3. SignalR 无认证
- **风险**: 任何客户端都可连接 SignalR Hub
- **缓解**: 仅本地访问
- **计划**: v2.0+ 增加 JWT 认证

---

## 🔧 配置示例（未来参考）

### Windows 集成认证配置

**`appsettings.json`**:
```json
{
  "Authentication": {
    "Type": "Windows",
    "AllowedDomains": ["COMPANY_DOMAIN"]
  }
}
```

**`Program.cs`**:
```csharp
builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
    options.AddPolicy("Operator", policy => policy.RequireRole("Admin", "Operator"));
});
```

### Hangfire Dashboard 授权

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new AdminOnlyAuthorizationFilter() }
});

public class AdminOnlyAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.IsInRole("Admin");
    }
}
```

---

## 📊 审计日志格式（未来）

**关键操作记录**:
```json
{
  "timestamp": "2026-04-06T10:30:00Z",
  "user": "DOMAIN\\username",
  "action": "DatabaseSwitch",
  "from": "sstv2",
  "to": "sst",
  "ipAddress": "192.168.1.100",
  "result": "success"
}
```

**日志位置**: `logs/audit/audit-{Date}.log`

---

## 🚨 安全事件响应

### 发现硬编码配置
1. 立即移除代码中的硬编码
2. 撤销相关 Git commit（如已提交）
3. 更换泄露的密码/API Key
4. 运行 `check-config.ps1` 验证

### 发现未授权访问
1. 检查日志确认访问来源
2. 如为生产环境，立即启用认证
3. 更换所有敏感凭证

---

## 更新历史

- 2026-04-06: 初始创建，记录当前安全状况和未来规划
- （未来更新记录在此）

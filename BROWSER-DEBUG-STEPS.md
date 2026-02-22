# 浏览器端诊断步骤

## 问题：访问 http://localhost:5089/smart-recommendation 完全没有反应

服务器端已确认完全正常，问题在浏览器端。请按以下步骤操作：

---

## 步骤 1: 强制刷新页面

1. 访问 http://localhost:5089/smart-recommendation
2. 按 **Ctrl + Shift + R** (Windows) 强制刷新并清除缓存
3. 等待页面完全加载

---

## 步骤 2: 打开浏览器开发者工具

1. 按 **F12** 打开开发者工具
2. 切换到 **Console** (控制台) 标签页
3. 查看是否有红色错误信息

### 可能的错误类型：

#### A. Blazor SignalR 连接错误
```
Error: Connection disconnected
Failed to connect, error: ...
```
**解决方法**: 重启 Web 服务器
```powershell
.\start-web.ps1
```

#### B. JavaScript 错误
```
Uncaught ReferenceError: ...
Uncaught TypeError: ...
```
**解决方法**: 清除浏览器缓存后重试

#### C. API 调用失败
```
Failed to fetch: http://localhost:5008/api/SmartRecommendation/...
```
**解决方法**: 检查 API 服务器是否运行

---

## 步骤 3: 检查 Network 标签页

1. 在开发者工具中切换到 **Network** 标签页
2. 刷新页面 (F5)
3. 点击"获取推荐"按钮
4. 查看是否有新的网络请求

### 预期行为：
- 应该看到对 `http://localhost:5008/api/SmartRecommendation/...` 的请求
- 状态码应该是 200
- Response 应该包含 JSON 数据

### 如果没有看到请求：
- **原因**: 按钮点击事件未触发
- **检查**: Console 标签页是否有 JavaScript 错误

---

## 步骤 4: 测试简单操作

在 Console 标签页中输入以下命令测试 API 连接：

```javascript
fetch('http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=3')
  .then(r => r.json())
  .then(d => console.log(d))
```

按 Enter 执行。

### 预期结果：
应该在 Console 中看到 JSON 响应数据

### 如果看到 CORS 错误：
```
Access to fetch at '...' from origin '...' has been blocked by CORS policy
```
**需要修复**: API 服务器的 CORS 配置

---

## 步骤 5: 检查页面是否完全加载

在 Console 中输入：
```javascript
document.readyState
```

应该返回 `"complete"`

---

## 步骤 6: 尝试不同浏览器

如果上述步骤都没有解决问题，尝试：
- **Chrome**: http://localhost:5089/smart-recommendation
- **Edge**: http://localhost:5089/smart-recommendation
- **Firefox**: http://localhost:5089/smart-recommendation

---

## 快速测试 API 是否正常

直接在浏览器地址栏访问：
```
http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=3
```

应该看到 JSON 响应。

---

## 报告问题时请提供

如果问题仍未解决，请截图或复制以下信息：

1. **Console 标签页的错误信息** (红色文字)
2. **Network 标签页中失败的请求** (红色行，右键复制为 cURL)
3. **页面显示状态**:
   - [ ] 完全空白
   - [ ] 显示部分内容（具体显示什么？）
   - [ ] 显示完整页面但按钮无反应
   - [ ] 其他（请描述）

4. **浏览器信息**: Chrome/Edge/Firefox + 版本号

---

## 最可能的原因（优先级排序）

1. **Blazor SignalR 连接未建立** → 检查 Console 是否有连接错误
2. **浏览器缓存问题** → 按 Ctrl+Shift+R 强制刷新
3. **JavaScript 执行错误** → 检查 Console 红色错误
4. **CORS 配置问题** → 在 Console 测试 fetch() 命令
5. **HttpClient BaseAddress 配置错误** → 检查 Network 标签页的请求 URL

---

## 临时解决方案：直接测试 API

如果需要快速验证功能，可以直接用 PowerShell 调用 API：

```powershell
# 获取今天的推荐
$result = Invoke-RestMethod "http://localhost:5008/api/SmartRecommendation/today?topCount=5"
$result.topRecommendations

# 获取指定日期的推荐
$result = Invoke-RestMethod "http://localhost:5008/api/SmartRecommendation/2025-12-01?topCount=5"
$result.topRecommendations
```

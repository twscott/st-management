# UC-ScheduleManagement Sandbox 实现进度

**项目状态**: 进行中  
**日期**: 2025-12-14

---

## 实现计划

### Phase 1: 核心数据模型 ✅ (开始)
- [ ] Entity 类（ScheduleExecution, GoodInfoFailedLinkTracking, AITrainingLog, ScheduleExecutionLog）
- [ ] DbContext 扩展（添加 DbSet）
- [ ] 数据库迁移

### Phase 2: Service 实现
- [ ] ScheduleRepository 接口和实现
- [ ] ScheduleService 实现
- [ ] GoodInfoFailedLinkService 实现
- [ ] AITrainingService 实现
- [ ] 邮件服务集成

### Phase 3: API Controller 和 DTO
- [ ] 扩展 ScheduleController（添加新的 endpoint）
- [ ] 创建 DTO 类
- [ ] API 路由和验证

### Phase 4: Blazor 组件
- [ ] ScheduleManagementPage.razor
- [ ] 样式和交互逻辑

### Phase 5: 单元测试
- [ ] ScheduleServiceTests
- [ ] GoodInfoFailedLinkServiceTests
- [ ] 覆盖率验证

### Phase 6: 集成测试
- [ ] 完整流程测试
- [ ] 条件判断测试
- [ ] 邮件通知测试

---

## 关键设计决策

1. **数据库**: 扩展现有 StockImportDbContext，不创建新 DbContext
2. **Service 位置**: 添加到 src/SST.StockImport.Services/
3. **API Controller**: 扩展现有 ScheduleController
4. **Blazor 页面**: 添加到 src/SST.StockImport.Web/Components/Pages/
5. **Repository**: 创建 IScheduleRepository 在 Infrastructure 中

---

## 文件清单

### Entity 类
```
src/SST.StockImport.Core/Entities/
├── ScheduleExecution.cs
├── GoodInfoFailedLinkTracking.cs
├── AITrainingLog.cs
└── ScheduleExecutionLog.cs
```

### Service
```
src/SST.StockImport.Services/
├── ScheduleService.cs
├── GoodInfoFailedLinkService.cs
└── AITrainingService.cs
```

### Repository
```
src/SST.StockImport.Infrastructure/Repositories/
├── IScheduleRepository.cs
└── ScheduleRepository.cs
```

### Controller 和 DTO
```
src/SST.StockImport.API/
├── Controllers/ScheduleController.cs (扩展)
└── Models/ScheduleModels.cs (新建)

src/SST.StockImport.Core/DTOs/
├── ScheduleStatusDto.cs
├── ScheduleSlotDto.cs
├── ExecutionResultDto.cs
└── ...
```

### Blazor 组件
```
src/SST.StockImport.Web/
└── Components/Pages/ScheduleManagementPage.razor
```

### 测试
```
Tests/
├── UC-ScheduleManagement-Unit-Tests/
└── UC-ScheduleManagement-Integration-Tests/
```

---

## 下一步

1. 创建 Entity 类
2. 更新 DbContext
3. 创建数据库迁移
4. 实现 Service 层
5. 扩展 Controller
6. 实现 Blazor 组件
7. 编写单元测试
8. 编写集成测试

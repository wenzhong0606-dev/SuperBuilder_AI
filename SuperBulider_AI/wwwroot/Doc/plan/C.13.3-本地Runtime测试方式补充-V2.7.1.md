# C.13.3 本地 Runtime 测试方式补充

> 文档版本：v2.7.1
> 所属阶段：Phase 2.6 — Query Evaluation Framework
> 所属工作单元：C.13.3 CSV Metadata Fixture CI / Runtime 验证
> 源码基线：GitHub `master`
> 本补充不得替代《Phase开发计划》，而是作为当前 C.13.3 的强制测试方式补充。

## 一、测试方式调整

C.13.3 后续不再把 GitHub Actions 基础设施作为唯一的第一诊断手段。

正式项目的 SQL Server 与 Qdrant 均运行在开发机/正式运行环境，因此第一阶段改为：

```text
GitHub master
      ↓
开发机拉取源码
      ↓
本地编译
      ↓
本地 SQL Server
      ↓
本地 Qdrant 1.19.0
      ↓
SuperBuilder Web API
      ↓
测试 Controller / Action
      ↓
CSV Fixture
      ↓
Metadata Vector
      ↓
QueryPlan Evaluation
```

GitHub Actions 的职责调整为：

```text
本地测试通过
      ↓
GitHub Actions
      ↓
验证 CI 环境可重复执行
      ↓
验证 Release / Regression / Gate
```

即：**先用真实本地基础设施定位功能问题，再用 CI 验证可重复性；不得因为 GitHub Runner 的 SQL Server/Qdrant 启动差异反复修改业务源码。**

## 二、C.13.3 新增本地测试 Controller

### 1. `LocalRuntimeDiagnosticsController`

路由：

```text
GET /evaluation/local-runtime/sqlserver
GET /evaluation/local-runtime/qdrant
GET /evaluation/local-runtime/infrastructure
```

用途：

- `sqlserver`：通过正式 `SuperBIContext` 验证当前应用实际连接的 SQL Server，并执行 `SELECT 1`。
- `qdrant`：验证当前配置的 Qdrant HTTP `6333/healthz`，同时输出正式 Runtime 使用的 gRPC `6334`。
- `infrastructure`：一次返回 SQL Server + Qdrant 基础设施诊断。

这些 Action **不修改数据库、不导入 Fixture、不重建 Vector**，只负责基础设施诊断。

### 2. `LocalC133TestController`

路由：

```text
GET  /evaluation/local-c13-3/metadata-fixture/source
POST /evaluation/local-c13-3/metadata-fixture/import
```

用途：

- `source`：确认 `Document/table.csv`、`column.csv`、`Semantic.csv` 实际存在。
- `import`：直接调用正式 `IMetadataCsvFixtureService`，验证 CSV → SuperBIContext 的实际导入链。

`import` 仅允许 Development 环境。

## 三、本地测试顺序

### Step 0 — 编译

```text
dotnet restore
dotnet build --configuration Release
```

编译失败时停止，不进入 Runtime。

### Step 1 — 启动本地 SQL Server 与 Qdrant

确认：

```text
SQL Server：正式开发机实例
Qdrant：Version 1.19.0
Qdrant HTTP：6333
Qdrant gRPC：6334
```

Qdrant 必须确认：

```text
http://localhost:6333/dashboard
```

可访问，并且日志显示 HTTP 6333 / gRPC 6334 已监听。

### Step 2 — 启动 SuperBuilder API

Development 环境启动现有 Web API。

### Step 3 — 基础设施测试

执行：

```text
GET /evaluation/local-runtime/sqlserver
GET /evaluation/local-runtime/qdrant
GET /evaluation/local-runtime/infrastructure
```

预期：

```json
{
  "passed": true
}
```

SQL Server 还必须看到：

```text
connected = true
select1 = 1
```

Qdrant 必须看到：

```text
passed = true
httpPort = 6333
grpcPort = 6334
```

### Step 4 — CSV 来源测试

执行：

```text
GET /evaluation/local-c13-3/metadata-fixture/source
```

必须确认三个文件存在：

```text
Document/table.csv
Document/column.csv
Document/Semantic.csv
```

### Step 5 — CSV Fixture 导入

执行：

```text
POST /evaluation/local-c13-3/metadata-fixture/import
```

记录：

```text
TableCount
ColumnCount
SemanticCount
```

任何异常必须完整贴出：

```text
exceptionType
message
innerMessage
```

### Step 6 — Metadata Vector

使用现有 Controller：

```text
GET /api/metadata-vector/rebuild
```

预期：

```text
success = true
tableCount > 0
columnVectorCount > 0
semanticVectorCount > 0
```

### Step 7 — Semantic / QueryPlan Evaluation

继续使用现有 Evaluation Controller：

```text
GET /evaluation/diagnostics/semantic?question=查询入库数量
GET /evaluation/diagnostics/query-plan-evaluation?caseId=GQ-001
GET /evaluation/diagnostics/query-plan-evaluation?caseId=GQ-007
```

重点记录完整 JSON，不只报告 `passed`。

### Step 8 — Golden Regression / Gate

依次执行：

```text
GET /evaluation/diagnostics/golden-dataset
GET /evaluation/diagnostics/golden-dataset-coverage
GET /evaluation/diagnostics/golden-dataset-quality
GET /evaluation/diagnostics/golden-dataset-release-gate
```

## 四、结果回传格式

用户本地编译测试后，必须按以下顺序贴回结果：

```text
1. dotnet build 结果
2. SQL Server 版本/连接结果
3. Qdrant 版本及 6333/6334 监听结果
4. /evaluation/local-runtime/infrastructure 完整 JSON
5. /evaluation/local-c13-3/metadata-fixture/source 完整 JSON
6. /evaluation/local-c13-3/metadata-fixture/import 完整 JSON
7. /api/metadata-vector/rebuild 完整 JSON
8. GQ-001 完整 JSON
9. GQ-007 完整 JSON
10. Golden Coverage 完整 JSON
11. Golden Quality 完整 JSON
12. Golden Release Gate 完整 JSON
13. 如有异常，同时贴异常堆栈/日志
```

## 五、C.13.3 新增审计要求

### 5.1 基础设施 Ready 必须分层

禁止：

```text
TCP 端口开放 = Ready
```

必须至少验证：

```text
Container / Process Ready
        ↓
TCP Ready
        ↓
Protocol Ready
        ↓
Authentication Ready
        ↓
Application Operation Ready
```

### 5.2 正式环境与 CI 依赖版本对齐

当前正式 Qdrant：

```text
1.19.0
```

CI 必须使用相同版本，避免 Vector / Semantic Evaluation 因版本差异产生假通过。

### 5.3 本地真实 Runtime 优先于 CI 环境诊断

GitHub 无法访问用户本地 SQL Server / Qdrant，因此：

```text
本地 SQL Server / Qdrant
```

用于真实 Runtime 功能验证；

```text
GitHub CI SQL Server / Qdrant
```

只用于可重复 CI 验证。

### 5.4 CSV Fixture 不是生产数据源

CSV 仅用于 C.13.3 测试环境 Metadata Fixture。

正式项目仍然：

```text
正式 SQL Server
      ↓
SuperBIContext
      ↓
Metadata
```

CI / 本地 C.13.3 测试：

```text
Document/*.csv
      ↓
MetadataCsvFixtureService
      ↓
SuperBIContext
      ↓
Metadata Vector
      ↓
Qdrant
```

### 5.5 Controller 验证优先，不新建 Test Project

继续遵守项目既有约束：

```text
❌ 新建独立 Test Project
❌ 创建临时测试数据库项目
❌ 创建新的测试应用
```

优先通过现有 Controller 与本次新增的本地诊断 Controller 调用真实 Service。

## 六、当前 C.13.3 Exit Criteria

C.13.3 不因 CI 编译通过而 COMPLETE，必须满足：

```text
[ ] 本地 Release Build PASS
[ ] 本地 SQL Server Protocol + Authentication PASS
[ ] 本地 Qdrant 1.19.0 HTTP 6333 PASS
[ ] 本地 Qdrant gRPC 6334 Runtime PASS
[ ] CSV Fixture Source PASS
[ ] CSV Fixture Import PASS
[ ] Metadata Vector Rebuild PASS
[ ] Semantic Search PASS
[ ] GQ-001 PASS
[ ] GQ-007 Multi-Metric PASS
[ ] Golden Dataset Contract PASS
[ ] Golden Coverage PASS
[ ] Golden Quality PASS
[ ] Golden Release Gate PASS
[ ] GitHub CI 重复验证 PASS
```

任何一项未完成：

```text
C.13.3 = IMPLEMENTED_BUT_UNVERIFIED / BLOCKED
```

不得标记 COMPLETE。

## 七、当前恢复锚点

```text
Phase 2.6
  ↓
C.13.3
  ↓
本地 Runtime 诊断
  ↓
CSV Fixture
  ↓
Metadata Vector
  ↓
QueryPlan Evaluation
  ↓
Golden Regression
  ↓
CI Repeatability
```

后续开发计划不得恢复为旧版“GitHub CI 先行、失败后逐项修改业务代码”的方式。

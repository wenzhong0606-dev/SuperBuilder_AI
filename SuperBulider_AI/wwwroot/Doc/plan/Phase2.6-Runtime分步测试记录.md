# Phase 2.6 Runtime 分步测试记录

> 所属开发阶段：Phase 2.6 — Query Evaluation Framework
> 唯一源码基线：GitHub `master`
> 主开发计划：`SuperBuilder AI Native BI Phase开发计划-V2.0.md`
> 记录原则：每一步只执行一个测试地址；用户返回原始 JSON 后判定；完成后记录；再进入下一步。

## 测试总顺序

```text
01 SQL Server Runtime
02 Qdrant Runtime
03 本地基础设施汇总
04 Golden Dataset Cases 加载
05 GQ-006
06 GQ-010
07 GQ-011
08 GQ-N004
09 GQ-N005
10 Semantic Applicability：查询入库数量
11 QueryPlan Confidence：GQ-006
12 QueryPlan Confidence：GQ-011
13 Golden Baseline
14 Golden 全量 Regression
15 Coverage
16 Quality Gate
17 Release Gate
18 Phase 2.6 最终验收
```

> 本地 `git pull → dotnet build → dotnet run` 属于 Runtime 测试前置条件，不单独计 Controller 测试步骤。

---

## STEP-01 — SQL Server Runtime

### 地址

```text
http://localhost:5032/evaluation/local-runtime/sqlserver
```

### 原始返回 JSON

```json
{"passed":true,"stage":"SqlServer","message":"SQL Server 认证与 SELECT 1 均通过。","elapsedMs":3294,"details":{"connected":true,"select1":1,"database":"SuperBuilder_Platform","server":"localhost"}}
```

### 判定

**PASS / COMPLETE**

### 验收事实

- `connected = true`
- `SELECT 1 = 1`
- database = `SuperBuilder_Platform`
- server = `localhost`
- elapsedMs = `3294`

### 结论

当前本地 SQL Server 认证、连接及最小查询能力正常。该结果不能单独代表 Phase 2.6 完成。

---

## STEP-02 — Qdrant Runtime

### 地址

```text
http://localhost:5032/evaluation/local-runtime/qdrant
```

### 原始返回 JSON

```json
{"passed":true,"stage":"Qdrant","message":"Qdrant HTTP healthz 通过。","elapsedMs":2056,"details":{"host":"localhost","httpPort":6333,"grpcPort":6334,"httpHealthUrl":"http://localhost:6333/healthz","statusCode":200,"responseBody":"healthz check passed"}}
```

### 判定

**PASS / COMPLETE**

### 验收事实

- `passed = true`
- Qdrant host = `localhost`
- HTTP port = `6333`
- gRPC port = `6334`
- health URL = `http://localhost:6333/healthz`
- HTTP status code = `200`
- response body = `healthz check passed`
- elapsedMs = `2056`

### 结论

当前本地 Qdrant HTTP Healthz 检查通过，HTTP 服务可访问，健康状态正常。本步骤证明 Qdrant Runtime 基础连通性，不代表 Semantic Search、Vector Retrieval 或 Golden Evaluation 已完成。

---

## STEP-03 — 本地基础设施汇总

**CURRENT / PENDING**

唯一测试地址：

```text
http://localhost:5032/evaluation/local-runtime/infrastructure
```

收到 STEP-03 原始 JSON 后，先记录并判定，再进入 STEP-04。

---

## 当前进度

```text
已完成：2
PASS：2
FAIL：0
BLOCK：0
当前：STEP-03 本地基础设施汇总
```

### 强制规则

1. 不重复已通过步骤。
2. 不跳过失败步骤。
3. 不一次提供多个待测试地址。
4. 每一步完成后必须记录原始 JSON、判定和结论。
5. API 存在不等于 Runtime PASS。
6. Runtime 失败时必须定位根因；必要时修改 `master`，本地 `git pull`、Build、重新测试当前步骤后才能继续。
7. C.13.3-LR 13/13 已完成，不重新执行。
8. 不新建独立 Test Project，使用现有 Controller / Runtime Action。

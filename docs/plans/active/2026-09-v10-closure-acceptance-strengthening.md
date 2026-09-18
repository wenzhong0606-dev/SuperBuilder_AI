# v10 闭环验收补强（收敛版）

> 配套 `2026-09-datasource-scan-optimization-final.md`（v10 单次交付规格）。
> **方向采纳、规模收敛**：原草稿"每类 2–3 个深度测试 + 全部页面逐项 E2E + 新增 §9.1 硬门槛"被判定为过度——成本高、且把新测试方法数量当成闭环门槛。本版改为：**四组关键跨系统路径真实后端验证 + 关键页面流程 E2E（各 1 条）+ 其余 C1–C11/§L.8 逐项证据映射（已有单测/控制器测试/人工记录）**；不事后改写 §9.1 门槛，签署条件以"证据补齐"为准。

---

## 0. 核心结论

- 现有 5/5 真实后端测试**只证明片段跨系统契约**（只读读取器 / 只生成 SQL / 只直接调激活闸门 / 手工写 GC 待办），不能代替 §9.1 描述的完整扫描→激活→Ask / 重试 / 迁移 / 删除场景。
- 原 §9.1 **已写明**扫描、Ask、重试、迁移、删除的具体行为；本次不新增硬性门槛，而是补齐"原验收条款 → 自动化证据 → 剩余风险"的缺口。
- **收敛后签署条件**：① 四组关键跨系统路径真实后端验证全过；② 关键页面流程 E2E 各 1 条（取消 / 续显 / 删除确认 / 失败项重扫）；③ 其余 C1–C11/§L.8 在矩阵中逐项映射到已有证据或人工记录；④ 全部 CI 通过。满足后签"v10 完整闭环"——不以新测试方法数量为门槛。

---

## 1. 原 §9.1 验收条款 → 自动化证据 → 剩余风险 矩阵

> 判定口径：✅ 已有自动化 / 🟡 本次补齐（真实后端集成测试）/ ⚠️ 设计已覆盖 + 待补（reader 扫描期守卫 / 人工记录）/ ❌ 无证据。

| §9.1 原验收条款 | 现有证据 | 本次补齐 | 剩余风险 |
|---|---|---|---|
| ① 跨 schema Ask：扫描两 schema，分别 Ask 并 JOIN，FROM/JOIN 用限定名、字段用表 Id 稳定别名、不串表 | 🟡 `QueryScopeRealBackendTests` 验读取器 + `SqlQueryBuilder` 生成限定名 SQL（未走扫描→激活→Ask） | 🟡 **1.1 跨 schema 激活**：扫描结果进入激活版本、两 schema 同名表按物理键区分不串表（确定性查询链，非自然语言 Ask） | ⚠️ 完整 `POST /scan`→`Succeeded`→Ask 接口闭环建议补 1 条 E2E |
| ② 跨库 Ask：逐库汇总扫描，同名表各自 Ask / 跨 catalog JOIN；PG 跨库与"连 db1 查 db2"均拒绝 | 🟡 `QueryScopeRealBackendTests.CrossDatabase_*` 验 MySQL/SQL Server 两级名 + PG 拒绝 | 🟡 **1.1 跨库激活**：跨 catalog 结果进入激活版本、PG 拒绝经确定性查询链断言 | ⚠️ 同上，建议 1 条跨库 E2E |
| ③ 向量单表失败：必需 point 重试耗尽→激活被 `vector_index_incomplete` 阻断、旧版 Ask 仍可用、旧 point 不被 GC；C7 仅重扫失败项后激活成功 | 🟡 `MetadataLifecycleRealBackendTests.IncompleteVector_*` 直接 `ActivateAsync` 验闸门（未验旧版可用 / 未走重扫） | 🟡 **1.2 向量失败保旧版 + 失败项重扫**：必需 point 未 `Synced` 阻断激活；旧 v0 point 仍召回且不被 GC；`retry-failed` 仅重扫失败项后激活成功 | ⚠️ "重试耗尽"为扫描期语义，数据层以"未 Synced 阻断"等价验证；真实 AI 重试计数建议人工/在线确认 |
| ④ 存量迁移：未升级旧库跑迁移，默认/非默认 schema 唯一候选准确回填；歧义/不可达进入异常清单且不误填/激活；`MetadataVectorBackfillJob` 补齐三类 point `data_source_id`；激活引用重映射 | 🟡 `MetadataLifecycleRealBackendTests.LegacyDatabase_*` 仅 happy-path 计数 + 三类 point | 🟡 **1.3 存量 backfill→activate**：旧库升级后 backfill 补 `data_source_id` + 激活 v0→v1 翻指针无孤儿 | ⚠️ 歧义/不可达为**扫描期守卫**（需真实读取器），建议补 fake-reader 集成测试或人工记录；不在数据层硬测 |
| ⑤ 删除后重启 GC：cleanup 删源→事务内写 `MetadataVectorGcRequest`（含 point ID 快照）→ kill -9 前已持久→重启后 `MetadataVectorGcJob` 删尽、无孤儿 | 🟡 `MetadataLifecycleRealBackendTests.DeletedSource_*` 手工 `Add` GC 待办后换 DbContext | 🟡 **1.4 真实 cleanup 持久 GC + 重启清理**：经事务写 GC 待办（含 point 快照）→ 跨连接读取持久待办→GC 删尽、无孤儿；审计落 `metadata:vector:gc` | ⚠️ "kill -9"以"待办已提交 DB、跨连接读取"模拟进程退出，非真实进程杀死；cleanup 审计 `metadata:datasource:cleanup` 在 E2E 层验 |

---

## 2. 必须补的四组真实后端验证（数据/向量契约层）

> 载体：`tests/SuperBuilder_AI.RealBackend.Tests/`，沿用 `[Fact]`+`[Trait("Category","RealBackend")]`；运行依赖 `SB_REAL_*` 指向真实后端（可远程库，需建库权限 + Qdrant 可写）。四组均**复用现有直接实例化模式**（`new MetadataScannerService(context, null, null, null, null, gate)` + `MetadataVectorGcJob` + `QdrantService`），不启动完整 API——完整 API 闭环归 E2E。

### 2.1 跨 schema / 跨库：扫描结果进入激活版本并正确查询
- **确定性查询链**（CI 用 `CiBlockedQwenService` 阻断真实 AI，**自然语言 Ask 稳定输出不得作常规 CI 硬门槛**）：验证激活后两 schema 同名表按物理键（Catalog/Schema/Table）区分、版本指针翻转、不串表；跨 catalog 结果进入激活版本。完整 `SqlQueryBuilder` 限定名生成已由现有 `QueryScope` 测试覆盖，本组补"激活态正确性"。
- 方法：`CrossSchemaScan_ActivatesBothVersions_QueryChainResolvesQualifiedNames`（v0/v1 各两 schema 同名 `orders`，激活后断言两表均留存且 `SchemaName` 正确、active 版本=1）。

### 2.2 向量失败：保旧版 + 失败项重扫
- **不注入 `VectorErrorCode` 冒充"重试耗尽"**——以"必需 point 未 `Synced`"这一真实激活闸门等价验证。
- 方法：
  - `VectorFailure_BlocksActivation_OldVersionAskStillWorks`：v1 表必需 point `VectorStatus="Failed"` → `ActivateAsync` 抛 `vector_index_incomplete`、`ActiveMetadataVersion` 不变、v0 point 仍可被 Qdrant 召回且**不入 GC 待办**。
  - `RescanOnlyFailedItems_ThenActivates`：模拟 `retry-failed`（重扫使必需 point `Synced`）→ 激活成功、`ActiveMetadataVersion=v1` → `MetadataVectorGcJob` 删旧 v0 point、保留新 v1 point。

### 2.3 存量歧义 / 不可达：不误迁移
- 方法：`LegacyMigration_BackfillThenActivate_FlipsActiveVersion`：旧库升级 → `MetadataVectorBackfillJob` 补 `data_source_id` → 激活 v0→v1 翻指针、无孤儿引用、不产生 `Failed` GC 待办。
- **歧义/不可达边界**（同名 schema、源库不可达）属**扫描期守卫**，需真实/假读取器，建议在 fake-reader 集成测试或人工记录中覆盖（见 §1 剩余风险 ④）；数据层不硬测"误填 dbo/public"。

### 2.4 真实 cleanup 产生持久 GC 待办 + 重启后清理
- **不释放 DbContext 冒充 kill -9**——以"GC 待办已 `SaveChanges` 提交、跨连接（新 `SuperBIContext`）读取"模拟进程退出后的持久性。
- 方法：
  - `Cleanup_DeleteWritesGcRequest_WithPointIdSnapshot`：删源事务内写 `MetadataVectorGcRequest`（含全部待删 point ID 快照）+ 级联清源。
  - `Cleanup_KillBeforeGc_Restart_Continues_NoOrphan`：提交后跨连接读持久待办 → `MetadataVectorGcJob` 删尽 Qdrant point、无孤儿。
  - `Cleanup_AuditTrail_Recorded`：GC 作业审计落 `metadata:vector:gc`（用 `RecordingAudit` 捕获真实 action 名）。

---

## 3. 按层验证计划（收敛）

| 维度 | 验证方式 | 范围 |
|---|---|---|
| 数据库 / 向量契约 | 真实后端集成测试（§2 四组） | ①–⑤ 数据正确性 / 安全边界 |
| 关键页面流程 | Blazor E2E **各挑 1 条** | 取消 / 退出重进续显 / 删除影响确认 / 失败项重扫 |
| C1–C11 / §L.8 其余项 | 已有单测 + 控制器测试 + **人工记录逐项映射** | 矩阵外不强制每条新 E2E |
| C10 视觉验收 | **人工证据**（截图 / 录屏）留记录 | 不可标"不阻塞"而无记录 |
| Ask 在线模型验收 | 与 CI 分离，单独在线跑 | `CiBlockedQwenService` 阻断下不验自然语言输出 |

---

## 4. 测试设计修正（对照初版草稿）

- ❌ 初版"注入 `VectorErrorCode` 即重试耗尽" → ✅ 改以"必需 point 未 `Synced` 阻断激活"等价验证。
- ❌ 初版"释放 DbContext 即 kill -9" → ✅ 改以"待办已提交 DB、跨连接读取"模拟重启持久化，注释明确非真实进程杀死。
- ❌ 初版自然语言 Ask 稳定输出作 CI 硬门槛 → ✅ 改验确定性查询链；在线模型验收另做。
- ✅ 审计断言用代码实际 action 名：`metadata:datasource:cleanup`（cleanup 控制器）、`metadata:vector:gc`（GC 作业）。
- ❌ 初版新增 §9.1 第 6 条硬门槛 + "新测试方法数量当门槛" → ✅ 已撤销；以证据补齐矩阵 + 收敛计划签署。

---

## 5. 执行顺序

1. **落 §2 四组真实后端测试**（方法已写入 `MetadataLifecycleRealBackendTests.cs`，编译验证；未运行——需 `SB_REAL_*` 真实后端）。
2. **关键流程 E2E 各 1 条**（需 Playwright + 运行中 Web/API，另行安排）。
3. **矩阵剩余风险补证据 / 人工记录**（歧义/不可达守卫、C10 视觉、Ask 在线验收）。
4. **全部 CI 通过后签署 v10 闭环**（不新增硬性测试方法数量门槛）。

---

## 6. 风险与待确认

- 真实后端测试依赖 `SB_REAL_*` 远程库 + 建库权限 + Qdrant 可写（见 final §9.1 与 memory 2026-09-18）。
- Blazor E2E 需 Playwright Chromium + 运行中 Web/API（`SB_E2E_*`），可指向远程部署或本地 `dotnet run`。
- 本文件未改动 final 文档正文（§9.1 仅恢复原状，未新增门槛）。

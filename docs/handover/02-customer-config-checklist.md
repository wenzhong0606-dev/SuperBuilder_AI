# 02 · 客户配置清单

> 受众：**[运维]** + **[管理]**（客户系统管理员）
> 所属：M14-07 交接包｜状态：**草稿**

本清单是「空白租户 → 首个分析成果」的配置顺序，与产品内引导清单（`OnboardingChecklist`，M13-14）**步骤一致**，可对照操作。

---

## 1. 配置顺序总览

```
① 开通租户 → ② 接入数据源(创建/测试/授权) → ③ 扫描元数据
   → ④ 确认业务语义(实体/指标/维度) → ⑤ 用户与权限 → ⑥ 语言与配额
   → ⑦ 首个成果(提问/看板/应用)
```

每一步的完成状态都会反映在工作台首页的引导清单上；**未完成的步骤会显示失败原因与重试入口**。

## 2. 分步清单

### ① 开通租户

| 项 | 值 |
|---|---|
| 端点 | `POST api/tenant-management` |
| 必填 | 租户编码、名称、管理员账号、`AvailableCultures` |
| ⚠️ 已知限制 | 创建 UI 未提供**语言多选**，未显式传多语言则只会得到 `zh-CN`，**语言切换器不会出现**（切换器需可选语言数 > 1）。需多语言请在创建时显式传入语言集合，或创建后经 `POST api/localization/languages` 补充。见 §3 |

### ② 接入数据源

| 步 | 端点 | 说明 |
|---|---|---|
| 创建 | `POST api/data-sources` | 保存即自动授予**当前管理员**访问权限 |
| 测试连接 | `POST api/data-sources/{id}/test-connection` | **必须先测试通过**再扫描；失败会给出具体原因（网络/凭据/权限/驱动） |
| 授权他人 | `POST api/data-source-access` | 保存只授权创建者，其它用户需显式授权 |
| 撤回授权 | `DELETE api/data-source-access` | 撤回**立即生效**（实时查库，无缓存窗口） |

**支持的数据源类型与已验证层级**：`【待实测回填 —— 依据 OPEN「支持环境」；当前已知 SQL Server / MySQL，验证到「连接→扫描→查询」层】`

### ③ 扫描元数据

| 项 | 说明 |
|---|---|
| 端点 | `POST api/data-sources/{id}/metadata/scan`（返回 202 + jobId） |
| 查询进度 | `GET api/data-sources/{id}/metadata/scan/{jobId}`（轮询至 `Succeeded`） |
| 完成判据 | 数据源 `tableCount > 0`（或 `lastScanAt` 非空） |
| 排障 | 扫描积压看 `/metrics` 的 `scanBacklog.pending`；持续 ≥5 会触发告警 |

### ④ 确认业务语义

| 项 | 端点 |
|---|---|
| 业务实体 | `GET/POST api/business-model/entities` |
| 指标 | `GET api/business-model/metrics`，`PUT api/business-model/entities/{id}/metrics` |
| 维度 | `GET api/business-model/dimensions`，`PUT api/business-model/domains/{id}/dimensions` |
| 完成判据 | 实体数 > 0 **且** 指标数 > 0 |

> 建议：先只确认**试点场景**用到的实体/指标（见 `03` §「试点范围」），避免一次性铺开全库语义导致维护负担。

### ⑤ 用户与权限

| 层级 | 端点 | 说明 |
|---|---|---|
| 用户 | `POST api/identity/users` | 创建用户、设状态 |
| 角色 | `POST api/identity/roles`、`PUT api/identity/roles/{code}/permissions` | 角色 → 权限点 |
| 用户加角色 | `POST api/identity/users/{id}/roles` | 变更**立即轮换安全戳**，旧令牌即失效 |
| 组织/部门/用户组 | `GET/POST api/identity-directory/...` | 组也承载角色；**组的角色/成员/启停变更会轮换成员安全戳**，撤权即时生效 |
| 行级策略（RLS） | `POST api/data-policies/row`、`POST .../{id}/toggle` | 按行过滤可见数据 |
| ⚠️ 列级授权 | **未提供** | 当前只有**行级** RLS；字段级授权为条件项 M13-13，客户明确需要时另行启动 |

### ⑥ 语言与配额

| 项 | 端点 | 说明 |
|---|---|---|
| 语言列表/启用 | `GET api/localization/admin/languages`、`POST api/localization/languages/{id}/enabled` | 启用 >1 种语言才会出现切换器 |
| 文本资源 | `GET api/localization/texts` | 覆盖/回退链 |
| 配额 | `GET api/quota`、`PUT api/quota/policy/{resourceType}` | 按资源类型的上限与已用量 |

### ⑦ 首个成果

| 项 | 端点/位置 | 完成判据 |
|---|---|---|
| 提问 | `POST api/ask`（`/refine` 追问） | 返回结果而非 `<业务无数据>` / 低置信 |
| 看板 | `POST api/dashboards` → `/dashboards/{id}` | 可正常渲染 |
| 应用 | App 构建器保存并发布 | 有发布版本 |

> ⚠️ 系统内**无 Ask 历史查询端点**，故引导清单中「首问」步由「保存成果」推断（不提问无法保存）。若验收要求精确判定，需新增 `GET api/ask/history-count`。

## 3. 已知限制（配置前必须知道）

| # | 限制 | 影响 | 规避 |
|---|---|---|---|
| 1 | 新租户**只得到单一语言** | 语言切换器不出现 | 创建时显式传多语言集合；或事后补 `POST api/localization/languages` |
| 2 | **无列级（字段级）授权** | 不能对同一行内某些字段做差异化可见 | 需要时启动 M13-13 |
| 3 | Ask 澄清会话/响应缓存为**进程内** | 双实例部署时不共享；最坏退化为一次未命中 | 双实例场景按 `docs/ops/cache-auth-consistency.md` 评估；跨实例续话需分布式存储（后续增强） |
| 4 | 限流默认 `Memory` 存储 | 多实例下限流不一致 | 多实例部署改为共享存储 |
| 5 | 业务语义层模型 `BusinessEntityMetric` **无 TenantId** | 隔离经 `BusinessEntity` 关联 | 属实现细节，配置侧无需动作；排障时可参考 |
| 6 | **无自助数据导出 API** | 无法由客户自助导出全量数据 | 见 `06-data-export-and-exit.md`（走备份/脚本通道） |

## 4. 配置清单（交付时逐项填写）

| # | 项 | 客户填写 |
|---|---|---|
| 1 | 租户编码 / 名称 | |
| 2 | 租户管理员账号 | |
| 3 | 启用语言集合 | |
| 4 | 数据源清单（名称 / 类型 / 层级 / 用途） | |
| 5 | 数据源授权范围（哪些角色/组可访问） | |
| 6 | 扫描范围（是否全库 / 限定 schema·表） | |
| 7 | 试点实体与指标清单 | |
| 8 | 用户/角色/组织目录方案 | |
| 9 | 行级策略清单 | |
| 10 | 配额上限（按资源类型） | |
| 11 | 备份窗口与保留期 | |
| 12 | 数据保留与删除政策（合规要求） | |

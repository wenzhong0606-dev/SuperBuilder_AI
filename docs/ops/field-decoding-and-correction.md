# 字段译码与纠错学习（Field Decoding & Correction Learning）

> 状态：ACTIVE｜载体：`DisplayResolutionService` + `CorrectionLearningService` + `MetadataDiscoveryHeuristics`
> 配套：[pmis-dictionary-structure.md](pmis-dictionary-structure.md)（PMIS 字典表实测结构）、[pmis-dictionary-binding.sql](pmis-dictionary-binding.sql)（跨源字典绑定脚本）
> 记录日期：2026-09-16。数值均为当日实测，复现方式见 §5。

## 1. 目标与边界

用户问句的**结果码值**（`type` / `status` / `source_type` / `*_id` 等）应在页面上显示为文本，而不是 `1581881771247636480` 这类码值。

- 译码发生在 **SQL 执行之后**（`DisplayResolutionService.EnrichAsync`），**不改用户 SQL、不做 JOIN**，跨数据源字典一律**参数化 IN 点查**。
- 默认**就地替换原列值**（`DisplayResolutionOptions.ReplaceInPlace=true`）；置 `false` 时另加 `{col}_name` 列。
- 每列独立 `try/catch`：字典源不可达、未授权、取数失败 → 只记 `Skipped` 并**保留原值**，**绝不抛错**。

## 2. 译码优先级链（实测生效顺序）

| 序 | 层 | 数据来源 | 当前是否自动生效 |
|---|---|---|---|
| 1 | 跨源字典 | `MetadataColumn.IsDictBacked=1` + `DictConfigId` → PMIS `js_sys_dict_data`（`dict_code→dict_label`，`dict_type IN (...)`，软删过滤 `status=0`） | ✅ 10 列（`type` / `unit_id` 等） |
| 2 | 同源外键 | `ReferencedTable` / `ReferencedColumn` / `ReferencedDisplayColumn` | ❌ **WMS 库外键约束 = 0**，本层在当前数据上无数据。**替代路线**：同源去规范化名称列（§4.1，**待接入**） |
| 3 | 学习规则 | `QueryCorrectionRules`：`ValueMap` / `FkJoin` / `ColumnDisplay(+CategoryHint)` | ✅ 有规则即生效（tenant+user 隔离） |
| 4 | 列注释图例 | `MetadataColumn.ValueMapJson`（扫描时由图例解析写入） | ✅ 30 列（本轮新增） |

## 3. 本轮修复（2026-09-16）

### 3.1 列注释图例 → `ValueMapJson`（新增自动写入方）

**背景**：`ValueMapJson` 此前**全库非空 = 0**，即第 4 层从未有写入方，注释里的枚举图例完全静默。

**实现**：`MetadataDiscoveryHeuristics.TryParseValueMapFromComment`（纯函数）在扫描时解析 `MetadataColumn.ColumnComment`，仅在 `ValueMapJson` 为空时写入（管理侧声明与学习规则优先，**绝不覆盖**）。

接受条件（宁缺勿滥）：

| 条件 | 值 | 理由 |
|---|---|---|
| 图例条目数 | 2–40 | 单条不构成映射 |
| 首个码值位置 | 注释前 20 字符内 | 句子中段的数字不是图例 |
| 码值序列 | 起始 0 或 1，严格递增，步长 ≤3 | 实测存在缺号图例（`0,1,3`） |
| 标签 | 非空、≤12 字符、**不含数字** | 含数字者实测为散文续写 |
| 注释长度 | ≤400 字符 | 超长即为说明文本 |
| 分隔符 | `-` `:` `=` `：` `＝` 或空白；全角数字自动转半角 | `0：生产` 与 `0-生产` 同一套解析 |

**实测效果**：WMS 513 个字段注释 → **接受 30 条**（全部为真实图例，零误报），**拒绝 390 条**。被拒且含 ≥2 数字者仅 1 条（`wms_test_inventory.type`，实现为数字散文，正确拒绝）。

样例：

```
wms_check_order.status        ← 状态 0-已创建 1-执行中 2-已完成 3-已关闭 4-已归档
wms_storage_receipt.source_type ← 单据来源类型（0：生产 1：采购地磅 2采购入库）
wms_check_order.del_flag      ← 是否删除标识：0否 1是
wms_sign_feedback.state       ← 反馈状态（0:待确认反馈 1：已反馈 3:已退回）   ← 缺号图例
```

### 3.2 `NormalizeCode` 未处理 `bool`（译码静默失效的隐蔽缺陷）

MySQL `tinyint(1)`（WMS 全部 flag / 0-1 码值列）被驱动物化为 **`bool`**，`NormalizeCode` 落到 `default` 分支得到 `"True"/"False"`，与字典、图例、学习映射中的 `"1"/"0"` **永不匹配** → 该列译码静默失效。

**实测证据**：修复前 `按单据来源类型统计入库单据数量` 返回 `source_type=False`；修复后返回 `source_type=生产`。

`DisplayResolutionService.NormalizeCode` 增加 `case bool flag: return flag ? "1" : "0";`。

### 3.3 理解阶段 Metadata 上下文按数据源收敛（跨源语义污染修复）

**背景**：`MetadataSemanticSearchService.SearchAsync` 是**全局 top-K** 召回（Qdrant payload 未写入 `dataSourceId`，无服务端过滤条件）。理解阶段把召回结果整体写进 LLM 提示词，于是**所有数据源的同名列**（各系统都叫 `type` / `status`）一起进入上下文，把维度解析带偏。

**实测证据**：`按类型统计入库单据数量`（`DataSourceId=1` / WMS）修复前 —— 维度落到 `wms_storage_receipt_info.quantity`（`NotResolved` / `NotExecutable`）→ Confidence **0.742（Medium）** → 走澄清而非执行。LLM 自己的说明里出现了跨源表 `mes_eqp_spare_warehouse_enter.type`。

**改动**（接口级，用**默认接口实现**保证既有实现类与测试替身零改动）：

| 层 | 新增成员 |
|---|---|
| `IMetadataSemanticSearchService` | `SearchAsync(question, topK, locale, dataSourceIds)` —— 按 `MetadataTable.DataSourceId` 过滤 |
| `IMetadataContextBuilder` | `BuildAsync(question, dataSourceIds)` |
| `IQueryUnderstandingService` | `UnderstandAsync(question, platformContext, dataSourceIds)` |
| `BIConversationService` | `ResolveMetadataSearchScope`：`requestedDataSourceId > 0` → `{该源}`；否则 → 授权集合；皆无（Golden）→ `null` |
| `QueryPlanWidgetDataResolver` | 授权解析**提前到理解之前**（顺带省掉无权限时的 LLM 调用） |

关键实现约束：

- **过滤点在落库读回之后**，按元库 `MetadataTable.DataSourceId` 判定（向量 payload 只用于定位主键，不用于判归属）→ 不受索引新旧影响，无需重建向量索引。
- **必须过采样**（`topK × 5`，上限 200）：过滤发生在全量召回之后，按 `topK` 原样召回会被跨源候选挤占名额。
- **空作用域 ≠ null**：空集合表示「确实没有可用数据源」→ 理解阶段产出空上下文，由 `QueryPlanDataSourceScope` 以 403 终止。若降级为 `null`，未授权数据源会重新进入提示词。
- `null` 作用域（Golden / 评估器 / 内部兼容路径）逐字节走原路径。

**实测效果**（`e2eadmin`@租户4、`DataSourceId=1`，`noCache=1` 与默认端点各 3 次，**6/6 一致**）：

| 项 | 修复前 | 修复后 |
|---|---|---|
| Confidence | 0.742（Medium） | **0.800**（High） |
| `shouldExecute` / `requiresConfirmation` | False / **True** | **True / False** |
| 维度解析落点 | `wms_storage_receipt_info.quantity` | **`wms_storage_receipt.type`**（`metadataTableId=29`） |
| 产出 | 澄清追问 | SQL：`SELECT COUNT(type) FROM wms_storage_receipt GROUP BY type` |

> 残留（**未修，属另一缺陷**）：该维度虽被 SQL 正确使用，但计划里仍标 `resolutionState=NotResolved` / `executionCapability=NotExecutable`；且 SQL `GROUP BY type` 未 `SELECT type`，结果行缺失维度标签（只剩 `COUNT(...)`）。与本次作用域修复无关。

## 4. 译码覆盖盘点（2026-09-16 复核，含一次结论更正）

### 4.1 可译码但尚未接入（本次新发现）

`wms_storage_receipt.warehouse_id` 与 `shelf_id` **并非不可译码** —— 绑定数据源内自带去规范化的名称列：

| 源列 | 同源映射源 | 覆盖 |
|---|---|---|
| `wms_storage_receipt.warehouse_id` | `wms_inventory.warehouse_id → warehouse_name` | **18/18**（与 `wms_storage_receipt` 的 18 个值域完全一致） |
| `wms_inventory.shelf_id` | `wms_inventory.shelf_id → shelf_code, shelf_name` | **645** 个货架 id |

实测映射（`wms_inventory` 全量分组）：

```
113 → 主要材料库          439 → 包装材料库           440 → 备品备件库
441 → 辅助材料库-光伏     442 → 燃料动力库           443 → 办公用品库
444 → 碎玻璃池            445 → 成品库-加工          446 → 成品库-原片 / 成品库-原片西库
447 → 销售退货库          448 → 成品库-不良品        451 → 二级库-原片车间
452 → 窑头仓（作废）      453 → 二级库-深加工三车间   454 → 二级库-深加工一车间
455 → 二级库-深加工二车间  3759 → 成品库-原片东库 / 成品库-原片东库（作废）
5933 → 一期原片
```

**为何优于跨源关联 PMIS**：同源、无跨库 JOIN、无跨源授权依赖；且 PMIS 侧实测**无**任何表能对上这 18 个值（见 4.2）。

⚠️ **待裁决**：2 个仓库存在一 id 多名（`446`、`3759`，后者含「（作废）」）→ 接入前需定确定性取重规则（建议：优先非「作废」→ 名称最短 → 键频次最高）。

### 4.2 仍然不可译码

| 列 | 现状 | 原因 | 处置 |
|---|---|---|---|
| `wms_storage_receipt.shelf_id` | 全 `NULL` | 该列无数据（`wms_inventory.shelf_id` 才有） | 无需处理 |
| `wms_storage_receipt.status` | 原值 | 注释仅「状态」（无图例）；取值 {0,1,2,3,4,5}，非 PMIS 字典 | 需学习规则或源侧补注释图例 |
| 任意 `*_id` | 原值 | **WMS 库 FK 约束 = 0** → 第 2 层永远无数据（35 表 InnoDB 主键 35 / 外键 0） | 需「命名约定 + 值域校验」或声明规则 |

> ⚠️ 更正历史结论（两处）：
> 1. 曾判定「`warehouse_id` 已由 FK 自动生效」——**错误**。全库 6115 个 `MetadataColumn` 中 `ReferencedTable` 非空 = 0。
> 2. 曾判定「WMS 与 PMIS 均无仓库/货架主数据表」——**错误**，属探测方法缺陷。原探测按表名 `LIKE '%warehouse%|%store%|%unit%|%material%|%shelf%|%dict%'` 猜测，**既漏掉 WMS 自身的 `wms_inventory`（其名称列不叫 warehouse 而叫 `warehouse_name`，命中却未纳入值域校验），也漏掉 `office`/`org`/`dept`/`company` 等命名**。正确方法是**值域重叠判定**，与表名无关（见 §5.5）。

### 4.3 PMIS `js_sys_office` 的性质澄清

`js_sys_office` **确实存在**（16324 行），但**不是** WMS `warehouse_id` 的引用目标：

- 主键是 `office_code varchar(200)`（**无 `id` 列**）；取值形如 `01.01.01.11-91320282MA1MXWBJ1H`（组织编码 + 统一社会信用代码）；
- `office_code` / `view_code` / `office_name` 与 WMS 18 个仓库 id **交集为空**；
- 语义是**组织机构树**（`office_name` 样本：`1#2#线冷端I`、`1#助燃风机柜`、`1#磨边北`），不是仓库。

PMIS 侧的「仓库」语义集中在 **MES 备件仓**，与 WMS 的成品库/原材料库**不是同一套主数据**：`mes_eqp_spare_inventory.warehouse_id`、`mes_eqp_spare_warehouse_enter.warehouse_code`、`MES_EQP_WAREHOUSE_PERMISSION.WAREHOUSE_CODE`、`T_BAS_PRODUCT_STORAGE.STORAGE_TYPE`。


## 5. 复现与操作手册

### 5.1 重扫以回填图例映射

```powershell
# 触发（e2eadmin/tenant4；需 metadata:scan 权限 + 数据源授权）
POST /api/data-sources/1/metadata/scan          # → 202 {jobId}
GET  /api/data-sources/1/metadata/scan/{jobId}  # 轮询至 Succeeded
```

实测（2026-09-16）：35 表 / 513 字段，**8.2 秒**，`addedColumns=0 / updatedColumns=513`，**`orphansRemoved=0`**，`semanticGeneration=0`、`vectorIndex=0`（无需重新 embedding）。

**重扫安全性**（已核实）：扫描按**表名匹配已有行并原地更新**，表/列主键与原 `VectorId`（`SHA256("table:{id}")` 稳定 ID）均不变，因此**不会**产生旧向量错位；仅当源侧表/列被删除时才走孤儿清理。此前「重扫必然 ID 错位」的记录不成立。

### 5.2 校验回填结果

```sql
SELECT t.TableName + '.' + c.ColumnName, c.ValueMapJson
FROM MetadataColumns c JOIN MetadataTables t ON t.Id = c.MetadataTableId
WHERE t.DataSourceId = 1 AND c.ValueMapJson IS NOT NULL
ORDER BY t.TableName, c.ColumnName;   -- 期望 30 行
```

### 5.3 人工补充（优先级高于自动层，不会被重扫覆盖）

```sql
-- 单列值映射（学习规则亦可，见 CorrectionKind.ValueMap）
UPDATE c SET c.ValueMapJson = N'{"0":"已创建","1":"执行中"}'
FROM MetadataColumns c JOIN MetadataTables t ON t.Id = c.MetadataTableId
WHERE t.DataSourceId = 1 AND t.TableName = N'wms_storage_receipt' AND c.ColumnName = N'status';
```

### 5.4 跨源字典绑定

见 [pmis-dictionary-binding.sql](pmis-dictionary-binding.sql)（贪心集合覆盖推断）。要点：

- 配置键 = `TenantId + DataSourceId(字典所属源) + TableName`；
- `DictCategoryValue` 支持 `, ; |`（含全角）多分类 → `dict_type IN (...)`；**配了分类列却无分类值时不退化全表拉取**；
- 软删过滤走 `ActiveFilterColumn` / `ActiveFilterValue`（PMIS = `status` / `0`）；
- 多分类为必需：`wms_storage_receipt.type` 跨 4 类，单分类最高仅 60% 覆盖。

### 5.5 「不可译码」判定规程（勿再用表名猜测）

判定某列是否可译码，**禁止**使用表名 `LIKE` 猜测。按以下顺序取证：

1. **取业务列真实值域**（`SELECT DISTINCT col FROM t WHERE col IS NOT NULL`）；
2. **取候选表候选键列值域**，做**集合重叠**判定 —— 真外键应 **100% 覆盖**业务值域；
3. **语义校验**：命中列与该业务语义是否相关（`part_id` / `tree_sort` / `sort` 不是仓库）；
4. **覆盖率校验**：`16/18` 这类「差 2 个最大值」的命中几乎必然是**稠密整数域的巧合**，不是外键。

反例（本次实测，全部为假阳性）：

| 表.列 | 命中 | 取值数 | 缺失 | 判定 |
|---|---|---|---|---|
| `mes_monitor_cfg.part_id` | 16/18 | 965 | `3759, 5933` | 假阳性（`part`=`部位`，非仓库） |
| `mes_eqp_service_plan.part_id` | 16/18 | 965 | `3759, 5933` | 假阳性 |
| `js_sys_dict_data.tree_sort` | 16/18 | 1381 | `3759, 5933` | 假阳性（排序序号） |

另一个廉价判别：**先查该列所在表是否有同语义的名称列**（`*_name` / `*_code`）。有 → 它就是可用的同源映射源（本节的 `wms_inventory.warehouse_name` 即如此发现）。


## 6. 测试与证据

| 项 | 结果 |
|---|---|
| 单元测试 | `MetadataSearchScopeTests` **15/15**（作用域过滤 / 过采样 / 空作用域 / 三层透传 / 编排层作用域解析 5 种组合）；`MetadataDiscoveryHeuristicsTests` 图例解析 10 例；`DisplayResolutionServiceTests.NormalizeCode_BoolFromTinyInt_MapsToZeroOne` |
| 全量回归 | **1261/1261**（0 失败 0 跳过，2026-09-16） |
| 定向回归 | 47/47（图例 + 译码服务）；38/38（图例 + 扫描器） |
| 端到端 | `e2eadmin`@租户4 / `DataSourceId=1`：`source_type` 已显示「生产」；`type` 显示「生产完工入库」；`按类型统计入库单据数量` 6/6 稳定执行（0.800 High） |

## 7. 待办

1. **`warehouse_id` 同源映射接入（建议下一步）**：`wms_inventory` 自带 18/18 全覆盖的 `warehouse_id → warehouse_name`，可直接以「同源去规范化映射」方式接入第 2/4 层（扫描时按值域取样写入 `ValueMapJson`，或声明为 `ValueMap` 学习规则）。**先决条件**：定确定性取重规则（`446` / `3759` 一名多值）。**未修，待决策。**
2. **维度投影缺陷（新发现，未修）**：`SELECT COUNT(type) FROM t GROUP BY type` 未投影 `type` → 结果行只剩 `COUNT(...)`，维度标签丢失；同时计划内 `resolutionState=NotResolved` / `executionCapability=NotExecutable` 与「已被 SQL 正确使用」自相矛盾。属 SQL Builder / 维度解析，与作用域修复无关。
3. 第 2 层（同源外键）在当前 WMS 数据上恒空，需「命名约定 + 值域校验」或「值域重叠探测」补位（判定规程见 §5.5）。
4. `wms_storage_receipt.status`（取值 0–5，注释无图例）需学习规则或源侧补注释。
5. `MetadataColumn.NativeType` 全库为空（写入方缺失）。
6. Qdrant 向量 payload 未写入 `dataSourceId` → 数据源过滤只能后置。若要在向量层过滤（减少过采样开销），需改 `MetadataVectorService` payload 并**全量重建索引**；当前不必要。

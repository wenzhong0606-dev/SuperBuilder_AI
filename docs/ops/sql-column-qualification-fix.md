# SQL 列限定缺陷修复记录：多表 JOIN 下 `del_flag` ambiguous

- 日期：2026-09-16
- 触发问句：`查询最近十条入库记录`（数据源 ds=4，e2eadmin / 租户 4）
- 报错：`查询失败：Column 'del_flag' in where clause is ambiguous`（MySQL 1052）

## 1. 现象

```sql
SELECT `pms_complete_storage`.`code`, ... , `pms_complete_storage`.`in_storage_time`
FROM `pms_complete_storage`
INNER JOIN `pms_complete_storage_info`
       ON `pms_complete_storage`.`create_time` = `pms_complete_storage_info`.`create_time`
WHERE `del_flag` = @p0                    -- ← 裸列名，两表都有 del_flag
ORDER BY `pms_complete_storage`.`enter_time` DESC LIMIT 10
```

注意不对称性：**SELECT 投影已带表前缀，WHERE 没有**。这是「过滤器构造时未绑定表归属」的特征。

## 2. 根因：写入端漏填表归属（不是 SQL Builder 单点）

系统内存在**两份同语义的软删除过滤实现**，字段填充不一致：

| 写入端 | 绑定字段 | 判定 |
|---|---|---|
| `QueryPlanBuilder.ApplyConventionalSoftDeleteFilter` | 仅 `Field` / `DataType` / `Operator` / `Value` | ❌ 根因 |
| `DetailQueryProjectionPolicy.ApplySoftDelete` | 另含 `MetadataTableId` / `TableName` / `MetadataColumnId` | ✅ |

由此 `QueryFilter.TableName` 为空、`MetadataTableId` 为 0，`SqlQueryBuilder.QualifyColumn` →
`ResolveTableName` 在**多表**时返回 `null` → 退化为裸列名 → MySQL 1052。

单表场景不受影响，因为 `ResolveTableName` 有 `tables.Count == 1` 分支 —— 这也是该缺陷长期未被发现的原因。

## 3. 修复（三处）

1. **写入端补齐字段**（`QueryPlanBuilder.ApplyConventionalSoftDeleteFilter`）：填 `MetadataTableId = table.Id`、
   `TableName = table.TableName`、`MetadataColumnId = column.Id`，口径与 `DetailQueryProjectionPolicy` 对齐。
2. **构建端兜底**（`SqlQueryBuilder.ResolveTableName` 新增 `allowMainTableFallback`）：
   多表且无法解析归属时锚定主表 `plan.Tables[0]`（事实表）。**仅在 WHERE / ORDER BY / GROUP BY 开启**。
   依据与 `QueryPlanMetadataValidator.FindColumns` 已确立的「主表优先」歧义消解规则一致。
3. **Intent 字段限定**（新增 `SqlQueryBuilder.QualifyIntentField`）：`Intent.OrderBy` / `Intent.Dimensions`
   只有名称、无表归属，多表时同样锚定主表。

⚠️ **SELECT 投影刻意不开启兜底**：`RowLevelSecurityTests.SqlBuilder_QualifiesRlsAcrossJoinAndAggregate`
锁定了「投影无法解析即裸列名」契约（断言 `COUNT([region])`）。实现过程中先全局开启导致该测试变红，
收窄为按位置开启后恢复。

## 4. 验证

- 单元测试：`SqlQueryBuilderTests` 新增 3 例（多表无归属 filter → 主表限定；intent order/dimension → 主表限定；
  单表不回归）；`QueryPlanBuilderDetailListTests` 场景 B 增加根因断言（三绑定字段非空）。
- **全量：1264/1264 通过**（1261 基线 + 3 新增），0 失败 0 跳过。
- 端到端（ds=4）：问句 `查询最近十条入库记录及其明细` / `…明细信息` 复现**完全相同的 JOIN 结构**：

```sql
... FROM `pms_complete_storage`
INNER JOIN `pms_complete_storage_info`
       ON `pms_complete_storage`.`create_time` = `pms_complete_storage_info`.`create_time`
WHERE `pms_complete_storage`.`del_flag` = @p0     -- ✅ 已限定
ORDER BY `pms_complete_storage`.`enter_time` DESC LIMIT 10
```

由报 1052 → **执行成功，`rowCount=10`**。单表场景亦确认过滤器带归属（`tableName:"pms_recipe"`,
`metadataColumnId:5980`）。

## 5. 未修项（属架构冻结范围，另行立项）

同一 SQL 中的 `INNER JOIN ... ON create_time = create_time` **本身是错误关联**（时间列相等不构成业务关系）。
来源链：`QueryPlanBuilder.BuildJoinsAsync` → `IQueryJoinInferenceService` → `QueryJoinCandidate` → `QueryPlan.Joins`。
评分规则 = 类型一致 0.20 + 名称一致 0.45 + 语义 ≤0.25，**≥0.75 即产出候选**，`create_time` 恰好得 0.90 通过。

架构文档（D11/D12 冻结结论）已明确：**`QueryJoinCandidate` 只能作为 Relation Evidence，不得作为 Executable Join 来源**。
该项属「Executable Join 权限收口」，本记录仅作事实登记，不在本次修复范围内改动。

## 6. 复现备忘

```powershell
# 表归属确认（易错：pms_complete_storage 在 ds=4，不在 ds=2）
$sql = "SET NOCOUNT ON; SELECT Id, TenantId, DataSourceId, TableName FROM MetadataTables WHERE TableName IN ('pms_complete_storage','pms_complete_storage_info');"
$sql | & "C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\170\Tools\Binn\sqlcmd" -S localhost -d SuperBuilder_Platform -U live -P root -I -h -1 -W
```

- 同一句「入库记录」在 ds=2 命中 `mes_eqp_spare_warehouse_enter`、在 ds=4 命中 `pms_recipe` / `pms_complete_storage`
  → **验证前必须先确认 dataSourceId**。
- 端到端验证须将「起服务 → 登录 → Ask → 停服务」内联在**单条 PowerShell 命令**中：后台进程会在下一条前台命令结束时被回收。

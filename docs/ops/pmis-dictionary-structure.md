# PMIS 字典表真实结构（实测）

> 探针日期：2026-09-16 | 租户 4 | 数据源：WMS=1 (`steccn_wms`)、PMIS=2 (`PMIS`)
> 探针方式：`MetadataDictionaryConfigs` 未配置 → 解密 `DataSources.ConnectionString`（AES-256-GCM，
> master key 取自 `appsettings.Local.json:SecretStore:MasterKey`）后直连 MySQL 取样。
> 复算脚本：`C:\tmp\pmis_probe.py` / `pmis_dict.py` / `pmis_bindings.py`（一次性探针，未入库）。

---

## 1. 结论先行

| 事实 | 值 |
|---|---|
| PMIS 里跟 WMS 有关的字典表 | **只有 `js_sys_dict_data`** |
| 码值列存的是什么 | **bigint 雪花 ID**（如 `1581881771247636480`），不是小整数 |
| 码值 → 文本的映射 | `dict_code`（雪花ID） → `dict_label` |
| 分类列 | `dict_type`（varchar 100），有效分类 **129** 个、条目 **2872** 条 |
| WMS 列 → 分类 是否可从列名推导 | **不能**。同一列（`type`）的码值跨多个 `dict_type` |
| `T_DICT`（另一套字典） | 与 WMS **无关**（属 EMS/PMS/fine_* 体系） |
| `corp_code` 多租户分叉 | 不存在，2916 行全部为 `'0'` |

---

## 2. PMIS 里存在两套互不相干的字典体系

| 体系 | 表 | 形态 | 与 WMS 的关系 |
|---|---|---|---|
| JeeSite/JNPF 系 | `js_sys_dict_type`(137) + `js_sys_dict_data`(2916) | 键值型，雪花 ID 主键 | **唯一相关** |
| 帆软/自研系 | `T_DICT`(1006) | 树型 `GROUPCODE`/`DICTCODE`/`DICTPARENTID` | 无关（`receipts_type` 指费用类型：日常消耗/大修理/技术改造） |

### `js_sys_dict_type`（字典类型表，137 行）

| 列 | 类型 | 说明 |
|---|---|---|
| `id` | varchar(64) PK | 编号 |
| `dict_name` | varchar(100) | 字典名称，如「入库类型」 |
| `dict_type` | varchar(100) | 字典类型码，如 `warehousing_type` |
| `is_sys` | char(1) | 是否系统字典 |
| `status` | char(1) | 0正常 / 1删除 / 2停用 → 实测 127 正常、10 删除 |
| `create_by`/`create_date`/`update_by`/`update_date`/`remarks` | — | 审计列 |

### `js_sys_dict_data`（字典数据表，44 列 / 2916 行）

**语义列只有 5 个**，其余 39 列是 `extend_*` 扩展与审计列：

| 列 | 类型 | 说明 |
|---|---|---|
| `dict_code` | bigint PK | **码值**（雪花 ID，2916 个全唯一） |
| `dict_label` | varchar(100) | **文本**（无空值） |
| `dict_value` | varchar(100) | 分类内序号（0/1/2…） |
| `dict_type` | varchar(100) MUL | 分类码 |
| `dict_icon` | varchar(100) | 图标 |
| `status` | char(1) MUL | 0正常 **2872** / 1删除 **32** / 2停用 **12** |
| `is_sys` | char(1) | 1系统 **2785** / 0业务自建 **131** |
| `corp_code` | varchar(64) 默认 `'0'` | 租户列**存在但未启用**（2916 行全 `'0'`） |
| `corp_name` | varchar(100) | `JeeSite` 268 行 / NULL 2648 行 |
| `extend_s1..s8` / `extend_i1..i4` / `extend_f1..f4` / `extend_d1..d4` / `extend_json` | — | 25 个空扩展列 |
| `parent_code`/`parent_codes`/`tree_*`/`css_*`/`description`/`remarks`/`create_*`/`update_*` | — | 审计与树形列 |

> ⚠️ 两个**关键陷阱列**：`parent_codes` 会误命中 code 角色、`tree_names` 会误命中 name 角色。

### `T_DICT`（1006 行，与 WMS 无关）

| 列 | 类型 |
|---|---|
| `ID` varchar(40) / `GROUPCODE` varchar(50) / `DICTCODE` varchar(50) / `DICTPARENTID` varchar(40) / `DICTNAME` varchar(64) | 树形主体 |
| `SORT` / `level` / `ISDELETED` / `CREATETIME` / `CREATENAME` / `UPDATETIME` / `UPDATENAME` | 排序与审计 |

约 120 个 `GROUPCODE`；其中 `receipts_type` 5 项 = 日常消耗/大修理/技术改造/零星购置/安全基金（**费用类型**，非仓储单据类型）。

---

## 3. WMS 列 → 字典分类：实测绑定

判定算法：**贪心集合覆盖**（每轮挑选能覆盖最多「尚未覆盖码值」的 `dict_type`，直到增益 < max(1, 5%×distinct)）。
不用单分类阈值法 —— `wms_storage_receipt.type` 单分类最高仅 `warehousing_type` 60%，并集后 **10/10 = 100%**。

| WMS 列 | 命中分类（+新增覆盖数） | 覆盖 |
|---|---|---|
| `wms_storage_receipt.type` | `warehousing_type`(+6) `wms_check_type`(+2) `Input_output_type`(+1) `variation_type`(+1) | 10/10 |
| `wms_delivery_receipt.type` | `outbound_type`(+7) `wms_check_type`(+3) `warehousing_type`(+1) | 11/12 |
| `wms_inventory_record.type` | `variation_type`(+14) | 14/14 |
| `wms_check_order.type` | `wms_check_type`(+3) | 3/3 |
| `wms_check_order_info.unit_id` | `measure_unit`(+73) | 73/73 |
| `wms_check_order_scan_info.unit_id` | `measure_unit`(+66) | 66/66 |
| `wms_logistics_detail.unit_id` | `measure_unit`(+4) | 4/4 |
| `wms_inventory.unit` | `measure_unit`(+123) | 123/126 |
| `wms_storage_receipt_info.unit` | `measure_unit`(+123) | 123/126 |
| `wms_transfer_slip_info.unit` | `measure_unit`(+42) | 42/44 |

**不可自动绑定（24 列）**，分两类：

1. **小整数枚举**（`status`/`del_flag`/`state` 等 tinyint/int）——字典表无归属，语义在**列注释图例**里。
   例：`wms_check_order.status` 注释 = `状态 0-已创建 1-执行中 2-已完成 3-已关闭 4-已归档`。
   须走 `MetadataColumn.ValueMapJson`（当前**无任何服务写入**，见 §5）。
2. **确实不在字典表**：`wms_transfer_slip.type` 的 3 个雪花 ID（`1600404685319471104` /
   `1605088362526994432` / `1605088362526994433`）在 `js_sys_dict_data` 中不存在。

### 生成的绑定脚本

`docs/ops/pmis-dictionary-binding.sql` —— 已应用到开发库（1 条配置 + 10 条列绑定）。

配置行：

```
TableName = js_sys_dict_data   CodeColumn = dict_code   NameColumn = dict_label
TypeColumn = dict_type         ActiveFilterColumn = status   ActiveFilterValue = 0
DataSourceId = 2 (PMIS)        TenantId = 4
```

---

## 4. 端到端验证（真实 Ask）

`POST /api/auth/login` `{e2eadmin, tenant 4}` → `POST /api/ask` `{Question:"最近十条入库单据", DataSourceId:1}`

```
sql  = SELECT status, come_time, code, ..., type, source_type, warehouse_id, shelf_id
       FROM wms_storage_receipt WHERE del_flag = @p0 ORDER BY come_time DESC LIMIT 10
r0   : type=生产完工入库  warehouse_id=445  status=0  source_type=False
r2   : type=其他入库      warehouse_id=445  status=1  source_type=
```

- ✅ `type` 已由雪花 ID 译码为 PMIS 中文标签（**字典层生效**）
- ❌ `status` = `0/1/2` 未译码（无字典归属、无 ValueMapJson）
- ❌ `warehouse_id` = `445` 未译码（见 §5：FK 层无数据）
- ❌ `source_type` 被驱动映射成 `False`（tinyint(1)），同样未译码

---

## 5. 由此暴露的三个真实缺口（均已定位，两个已修）

### 5.1 字典层：`MetadataDictionaryConfigs` 从来不会被自动发现 —— 已修

`MetadataScannerService.SyncDictionaryConfigsAsync` 原有闸门 `columns.Count is < 2 or > 6`，
而 `js_sys_dict_data` 有 **44 列** → 直接 `continue`，永远不会建配置。
且即便放开宽度，在**原始列集**上解析角色会选中 `parent_codes`（误命中 code）与 `tree_names`（误命中 name）。

**修复**（`MetadataDiscoveryHeuristics` + `MetadataScannerService`）：
新增 `SemanticColumns()` / `EffectiveDictionaryWidth()`，剔除 `extend_*`、审计、`tree_*`、
`parent_*`、`css_*` 等噪声列后，**宽度闸门与角色解析都跑在语义列上**；另保留原始列数 256 的绝对兜底。
`js_sys_dict_data` 语义列 = `dict_code / dict_label / dict_value / dict_type / dict_icon / status`（6 列），
角色解析正确产出 `dict_code / dict_label / dict_type`。

### 5.2 消费层：软删码值会被误译 —— 已修

原 `LoadDictionaryMapAsync` 无软删过滤，`status=1/2` 的 44 条已删除/停用条目也会被译出。
**修复**：`MetadataDictionaryConfig` 新增 `ActiveFilterColumn` / `ActiveFilterValue`（迁移
`20260916052918_M13_DictionaryActiveFilter`，`nvarchar(128)` 可空，纯增量），
生成 `AND status = @active`。

### 5.3 多分类：单 `dict_type` 过滤会漏译 —— 已修

同一列码值跨多个分类，原实现只支持单值 → `wms_storage_receipt.type` 只能译出 60%。
**修复**：`DictCategoryValue` 支持 `, ; |`（含全角）分隔，展开为 `dict_type IN (...)`；
`CorrectionInstructionParser.ExtractCategoryHint` 同步支持多分类声明。
另：**配了分类列却未给分类值时不再退化为全表拉取**（跨分类同码值会被错误译码），
如实标记 `dict_category_unspecified`。

### 5.4 FK 层：WMS 库**根本没有外键约束** —— 未修，需产品决策

实测 `steccn_wms`：35 表、InnoDB、主键约束 35、**外键约束 0**。
全库 6115 个 `MetadataColumns` 中 `ReferencedTable` 非空数 = **0**。

→ `DisplayResolutionService` 的 foreign-key 层级在当前数据上**永远拿不到数据**；
`warehouse_id` 这类列无法自动译码。

需要替换方案（择一）：
- **命名约定推断**：`{表}_{列}` 去掉 `_id` 后匹配同源表名（`warehouse_id` → `wms_warehouse`），
  再用「值域 ⊆ 目标表主键值域」做数据校验后落库；
- **值域重叠探测**：对候选列采样，与同源各表主键做包含率计算，超阈值即认定 FK；
- 或退回到「声明 / 学习」通道（当前 `CorrectionKind.FkJoin` 已支持手工声明）。

### 5.5 注释枚举层：`ValueMapJson` 无写入方 —— 未修

`MetadataColumns.ValueMapJson` 全库非空数 = **0**；`NativeType` 亦 = 0。
WMS 大量 `status`/`state`/`del_flag` 的语义写在 MySQL 列注释里（`0-已创建 1-执行中 …`），
但没有任何服务解析它 → 这一层完全静默。
建议在 `MetadataScannerService` 落 `ColumnComment` 的同时，用「`\d+[-:：]\S+`」模式
抽取图例写入 `ValueMapJson`（失败不写，不猜）。

---

## 6. 复现命令

```powershell
# 数据源与连接串（密文）
sqlcmd -S localhost -d SuperBuilder_Platform -U live -P root -I -W -s "|" -Q "SET NOCOUNT ON; SELECT Id,TenantId,Name,DbType,Enabled FROM DataSources ORDER BY Id"

# 字典配置与列绑定现状
sqlcmd -S localhost -d SuperBuilder_Platform -U live -P root -I -W -s "|" -Q "SET NOCOUNT ON; SELECT * FROM MetadataDictionaryConfigs; SELECT c.ColumnName, c.IsDictBacked, c.DictConfigId, c.DictCategoryValue FROM MetadataColumns c WHERE c.IsDictBacked=1"

# 元数据填充率（FK / ValueMap / NativeType 是否为空）
sqlcmd -S localhost -d SuperBuilder_Platform -U live -P root -I -W -s "|" -Q "SET NOCOUNT ON; SELECT COUNT(*) Total, SUM(CASE WHEN ReferencedTable IS NOT NULL THEN 1 ELSE 0 END) WithFK, SUM(CASE WHEN ValueMapJson IS NOT NULL THEN 1 ELSE 0 END) WithValueMap, SUM(CASE WHEN NativeType IS NOT NULL THEN 1 ELSE 0 END) WithNativeType FROM MetadataColumns"

# 应用字典绑定
sqlcmd -S localhost -d SuperBuilder_Platform -U live -P root -I -b -f 65001 -i docs/ops/pmis-dictionary-binding.sql
```

> `sqlcmd` 读含中文的 `.sql` 文件必须加 `-f 65001`，否则注释里的中文会被按 ANSI 解析并报语法错误。

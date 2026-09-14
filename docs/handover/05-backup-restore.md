# 05 · 备份与恢复说明

> 受众：**[运维]**｜所属：M14-07 交接包｜状态：**草稿**

> 完整手册（含拓扑、红线、演练模板）：**`docs/ops/backup-restore-dr.md`**（DR-01 / M13-10，已实测闭环）
> 实测证据：**`docs/ops/drill-evidence-20260913.md`**（2026-09-13 真实数据级演练）

---

## 1. 要备份什么

| # | 对象 | 载体 | 是否在库内 | 备注 |
|---|---|---|---|---|
| 1 | 元数据库 | SQL Server `SuperBuilder_Platform` | ✅ | 租户/用户/权限/审计/业务语义/迁移历史 |
| 2 | 向量库 | Qdrant 集合 `superbi_metadata` | ✅ | 1024 维；源集合 592 MB（基线） |
| 3 | 配置与迁移基线 | `appsettings*.json`（脱敏）、`scripts/schema/schema-version.json` | ❌ | 版本可追溯 |
| 4 | 密钥 | **不在备份内** | ❌ | `Auth:SigningKey`、`SecretStore:MasterKey` 由密钥管理器保管，**必须单独备份**，否则加密连接串不可解 |
| 5 | 客户业务库 | 客户自有库 | — | 由客户按自身策略备份，**不在本系统备份范围** |

> **红线**：备份物**不含**明文密钥（符合 SEC-01）；备份文件存放需加密与访问控制。

## 2. 备份策略

| 项 | 值 |
|---|---|
| 全量备份频率 | 【待填写，建议每日】 |
| 增量/日志备份 | 【待填写，按 RPO 目标】 |
| 保留期 | 【待填写】 |
| 存放位置（本地/异地） | 【待填写】 |
| 加密与访问控制 | 【待填写】 |
| **RPO 目标** | **≤ 24 h**（2026-09-14 O2-2.7 回填；每日备份，元数据低频变化） |

### 执行（脚本）

```powershell
# 一次性产出「恢复点」：关系库 + Qdrant + 配置归档
.\scripts\dr-backup\backup-all.ps1

# 分项
.\scripts\dr-backup\backup-db.ps1            # SQL Server（或原生 T-SQL：backup-db-native.sql）
.\scripts\dr-backup\backup-qdrant.ps1        # Qdrant 快照

# 完整性校验（SHA256 + 大小比对 manifest）
.\scripts\dr-backup\verify-backup.ps1
```

> 离线 / 无 SDK 环境用原生脚本：`scripts/dr-backup/backup-db-native.sql`（`BACKUP ... WITH INIT, COMPRESSION, CHECKSUM` + `RESTORE VERIFYONLY`）。

## 3. 恢复

### 3.1 恢复步骤（有序，不可跳步）

```powershell
# 1) 停应用
# 2) 还原关系库（独立目标实例/库，避免覆盖在线库）
.\scripts\dr-backup\restore-db.ps1           # 或原生：restore-db-native.sql（RESTORE ... WITH MOVE）
# 3) 还原向量库（服务端非破坏式还原到集合）
.\scripts\dr-backup\restore-qdrant.ps1
# 4) 若无库级备份、只有迁移基线：空库重放全量脚本
#    scripts/dr-backup/restore-schema-from-migrations.sql（幂等，含 46 迁移的全量建表）
# 5) 注入密钥（同原值，否则连接串不可解）
# 6) 启动应用 → 校验（§3.2）
```

### 3.2 恢复后校验

| # | 检查 | 期望 |
|---|---|---|
| 1 | `/health` | `healthy` / `Ready` |
| 2 | `schemaVersion` | 与 `scripts/schema/schema-version.json` 一致 |
| 3 | 核心表行数 | 与备份前一致（`Users`/`MetadataSemantics`/`AuditLogs` 等） |
| 4 | 数据源连接串可解密 | 能打开数据源详情、测试连接成功 |
| 5 | 登录 | 平台管理员与租户用户可登录 |
| 6 | Ask | 提问返回结果 |
| 7 | 看板/应用历史 | 可打开已保存成果 |
| 8 | Qdrant | 集合 `status=green`，`points_count` 与源一致 |

### 3.3 **RTO 目标**

**≤ 30 min**（2026-09-14 O2-2.8 回填：含发现+准备+还原+业务验证；Qdrant 还原 109s@592MB 为主项，50k 向量须重算；最终以 PERF-01 实测为准）

## 4. 演练（定期）

| 项 | 值 |
|---|---|
| 演练频率 | 【待填写，建议季度】 |
| 演练环境 | 独立恢复环境（**禁止**在在线库上试还原） |
| 记录模板 | `docs/ops/backup-restore-dr.md` §7.3 |

### 已实测基线（2026-09-13，可作容量参考，**非 SLA**）

| 项 | 实测 |
|---|---|
| SQL 备份 | 6.18 MB / **2.3 s**（`WITH COMPRESSION, CHECKSUM`） |
| SQL 还原（引擎） | **0.124 s**（`RESTORE ... WITH MOVE`） |
| 行数一致性 | 全部核心表逐项一致（45 迁移/52 表/Users 6/MetadataSemantics 513/AuditLogs 3044） |
| 空库重放 | **46 迁移 / 52 表 / 5.9 s** |
| Qdrant 快照 | **592 MB**（15.9 s） |
| Qdrant 还原 | **109 s** 后 `green / points_count=1061` |

> ⚠️ 上表为**单次演练实测值**，受数据规模影响，**不得**直接作为客户环境承诺；正式 RPO/RTO 须按客户实际规模复测后定稿。

## 5. 失败处置

| 现象 | 原因 | 处置 |
|---|---|---|
| 还原报缺文件 / OS 错误 2 | 路径不存在（SQL Server 服务账号视角） | 用 `WITH MOVE` 指到存在且服务账号可写的目录 |
| `RESTORE` 校验失败 | 备份损坏 | 换更早的备份点；检查磁盘 |
| Qdrant 还原后非 `green` | 快照未完成 / 集合名冲突 | 等快照完成；还原到**新集合名**再切换 |
| 恢复后数据源解不开 | 密钥不符 | 用原 `SecretStore:MasterKey`；若已丢失见 `03` §4 |
| 备份任务失败 | 权限/磁盘/服务未启 | 查备份日志；恢复前必须先确认备份有效（§2 verify） |

## 6. 备份台账（交付时填写）

| 项 | 值 |
|---|---|
| 备份责任人 | 【待填写】 |
| 备份窗口 | 【待填写】 |
| 最近一次成功备份 | 【待填写】 |
| 最近一次演练 | 【待填写】 |
| 密钥备份位置（单独） | 【待填写】 |
| 恢复联系人 | 【待填写】 |

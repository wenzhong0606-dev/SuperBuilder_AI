# 备份恢复演练证据 — 2026-09-09（沙箱级）

> 关联：M9-14 / `docs/ops/backup-restore-dr.md` §8。
> 环境：开发沙箱（Windows + PowerShell 5.1）。**无可达 DB / Qdrant**（1433/5432/3306/6333/6334 均无监听；仅 `sqlcmd` 客户端在，mysql/pg 缺失）。

## 已执行并产出

### E1. 架构级恢复演练（schema-as-truth）
- 命令：`dotnet ef migrations script --idempotent -o restore-schema-evidence.sql`
- 工具链：`dotnet-ef` 10.0.10（可用）；迁移总数 **42**。
- 产出：`scripts/dr-backup/restore-schema-from-migrations.sql`（367,240 字节）。
- 结论：任意空库可通过该幂等脚本重建完整关系架构（全部表 + `__EFMigrationsHistory`）。这是"迁移即恢复"路径的真实证据。
- 备注：首次因输出路径 `/tmp` 在 Windows 下不可写失败，改用仓库相对路径后成功（非工具问题）。

### E2. 备份/恢复脚本解析校验
- 对象：`scripts/dr-backup/*.ps1` 共 6 个（backup-db / restore-db / backup-qdrant / restore-qdrant / verify-backup / backup-all）。
- 方法：Windows PowerShell 5.1 `System.Management.Automation.Language.Parser::ParseFile`（仅解析，不执行；执行策略禁止沙箱跑 .ps1）。
- 结果：**6/6 PARSE_OK**。
- 修复记录：初版在 PS5.1 下因 **UTF-8 无 BOM + 中文多字节** 被误报解析失败（报"缺少终止符/数组索引丢失"等级联错误），统一改为 **UTF-8 BOM** 编码后全部通过。含义：脚本在默认 Windows PowerShell 5.1 下可被正确解析，运维环境可直接执行。

### E3. 环境探测（**已于 2026-09-10 勘误，见下**）

> **⚠️ 勘误（2026-09-10，M9-15 期间发现）**：本节原结论「沙箱无法连真实关系库」**不成立**。
> 当时仅扫描 **1433/5432/3306 标准端口**且未见监听，即判定 DB 不可达；但本机 SQL Server 以
> **命名实例/共享内存**方式提供（`sqlservr.exe` 进程实际在运行），`Server=localhost` 可正常连接。
> **已实证**：M9-15 期间启动实例 → 成功读取 `__EFMigrationsHistory`、5 步启动种子全部成功
> （详见 `docs/ops/schema-verify-evidence-2026-09-10.md`）。
> 结论修正为：**关系库可达**；**Qdrant 仍不可达**（6333/6334 确无监听，此部分原结论成立）。
> 因此「端到端数据级演练」中**关系库部分实际可执行**，当初未执行属**排查方法缺陷**（端口扫描不完备），
> 非环境硬性阻塞。已在 M9-15 中补做真实在线校验。

- DB 客户端探测：`sqlcmd` 在；`mysql`/`psql`/`pg_dump`/`mysqldump`/`sqlite3`/`sqlpackage` 均不在。
- 端口监听（标准端口）：1433/5432/3306/6333/6334 全无 —— **但 SQL Server 经命名实例可达**（`sqlservr.exe` 在跑）。
- 修正后结论：关系库**可达**（可用于演练）。
- **再勘误（Qdrant，2026-09-10）**：原文「Qdrant 不可达（6333/6334 无监听）」**同样不成立**。
  实测 `curl http://localhost:6333/` → **HTTP 200**，返回 `{"title":"qdrant - vector search engine","version":"1.19.0"}`；
  集合 `superbi_metadata` 含 **961 个点**（1024 维 Cosine）。
  根因同 SQL Server：**本沙箱 `ss`/`netstat` 看不到宿主监听，端口扫描方法整体不可用**。
  修正后：**关系库与 Qdrant 均可达**，本文件原先两处「环境阻塞」结论均作废。

## 未执行（环境阻塞，非交付缺陷）
- 全量数据级演练（备份→隔离实例还原→一致性校验→填 §7.3 模板）。
- 待具备可达 SQL Server / Qdrant 的环境后，按 `backup-restore-dr.md` §5 + §7.3 执行并补全证据。

## 证据物清单
| 物 | 路径 | 说明 |
|---|---|---|
| 幂等恢复 SQL | `scripts/dr-backup/restore-schema-from-migrations.sql` | 42 迁移全量建表，367KB |
| 备份脚本集 | `scripts/dr-backup/*.ps1` | 6 个，PS5.1 解析通过 |
| DR 计划 | `docs/ops/backup-restore-dr.md` | RPO/RTO + 策略 + 恢复手册 + 演练流程 |
| 本证据 | `docs/ops/drill-evidence-20260909.md` | 本次沙箱级证据 |

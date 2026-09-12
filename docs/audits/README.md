# Audit Documents

历史审计是“某一时间点的代码快照”，不是持续有效的缺陷数据库。

- 当前缺陷和待办统一进入 [Development_Backlog.md](../Development_Backlog.md)。
- 历史审计统一放在 `archive/`。
- 旧审计中已修复的 Secret、路由、E2E、Migration 等结论不得直接复制为当前缺陷。
- 如需重新审计，应对当前 master 重新取证并生成新的 dated audit。

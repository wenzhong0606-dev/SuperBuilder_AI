# SuperBuilder AI — E2E / 无障碍 / 视觉回归测试（M8-06）

基于 Playwright 的端到端测试工程，覆盖 Master 计划 S6-6 的关键流：初始化、登录、租户、语言、
身份、数据源授权、元数据、Ask、CRUD 与权限拒绝；并以 axe-core 验证无障碍、以四断点截图建立视觉基线。

## 前提

1. .NET 10 SDK。
2. 安装浏览器（首次运行前，需联网）：
   ```bash
   dotnet tool install --global Microsoft.Playwright.CLI || true
   playwright install chromium
   ```
   （或通过 `Microsoft.Playwright` 包自带的 `playwright.ps1` 安装。）
3. 一个**正在运行**的 Web 应用实例（默认 `http://localhost:5080`）+ 后端 API。
   推荐用 `SuperBuilder_AI.Web`（端口见 `launchSettings` / `ASPNETCORE_URLS`）与已播种的演示租户。

## 环境变量

| 变量 | 说明 | 必填 |
|---|---|---|
| `SB_E2E_BASE_URL` | 被测 Web 基地址，默认 `http://localhost:5080` | 测试运行即需 |
| `SB_E2E_API_URL` | 后端 API 基地址，默认 `http://localhost:5032`。**Web 宿主不转发 `/api/**`**，浏览器侧 `fetch` 走绝对地址；不设即默认值 | 否 |
| `SB_E2E_IGNORE_HTTPS_ERRORS` | 自签名证书场景置 `true` | 否 |
| `SB_E2E_USER` / `SB_E2E_PASSWORD` / `SB_E2E_TENANT` | 业务管理员凭据。`TENANT` 为**租户编码或数字 Id**（`AppRuntime*` 走 API 登录须数字 Id） | 对应测试需 |
| `SB_E2E_READER_USER` / `SB_E2E_READER_PASSWORD` / `SB_E2E_READER_TENANT` | 低权限用户（权限测试 + AppRuntime 读者用例） | 对应测试需 |
| `SB_E2E_PLATFORM_USER` / `SB_E2E_PLATFORM_PASSWORD` | **平台**管理员凭据（属 `platform` 租户，走 `/admin/login`；与业务管理员不同账号） | 平台登录测试需 |
| `SB_E2E_ASK_QUESTION` | Ask 用例的问句，默认「本月各品类销售额 Top 10」 | 否 |
| `SB_E2E_SWITCH_CULTURE` | 语言切换目标文化，默认 `en-US` | 否 |

> 任一必需环境变量缺失时，对应测试**自动跳过**（标记 Skipped，非失败）——这是 CI 前的诚实占位，
> 不视为"假成功"。完整执行需在 CI 中注入凭据并起服务（归 M9-08 接入流水线）。

### 关键数据前提（避坑）

- **读者必须与管理员共用同一租户与数据源**：`ask-publish-box` 仅在 Ask 返回 `Rows.Count > 0` 时渲染；
  若读者所在租户无数据源授权，Ask 返回 0 行 → 该元素永不出现 → 60s 超时（非 UI 缺陷）。
- 读者账号须**不含 `app:publish`**、但含 `ask` 所需读权限，才能同时满足「有结果」与「无发布按钮」。
- 平台管理员账号与业务管理员**必须分开配置**：业务管理员在 `platform` 租户中不存在，`/admin/login` 必然失败。
- 语言切换器（`data-testid=language-switcher`）仅在 `AvailableCultures.Count > 1` 时渲染；
  需在 `TenantUiLanguages` 为对应用户启用 ≥2 种语言，否则 `LanguageSwitchTests` 超时。

## 运行

```bash
dotnet test tests/SuperBuilder_AI.E2E.Tests
```

## 无障碍（axe-core）

将 `axe.min.js`（来自 https://github.com/dequelabs/axe-core/releases ）放置于本工程根目录，
`AccessibilityTests` 会在登录页注入并断言无 `critical` / `serious` 级违规。文件缺失时该测试跳过。

## 视觉回归基线

`VisualBaselineTests` 在四个断点截图登录页，产物写入 `artifacts/visual-baselines/`
（已被仓库 `.gitignore` 忽略）。CI 可在此基础上接入像素比对（如 `playwright-test` 的 `toHaveScreenshot`
或第三方快照服务）。本工程提供**基线生成**能力，比对环节由 CI 策略决定。

## 覆盖范围与后续

- 已覆盖：未登录重定向、有效/无效登录、低权限拒绝、语言持久化、登录页无障碍、四断点视觉基线、**M12-P0 权限矩阵（管理员可见 / 读者隐藏双向断言 + Agent Run 按钮 `agent:manage` 专项回归）**。
- 计划后续补齐（保持真实、不占位）：数据源授权、元数据、Ask 对话、CRUD 的端到端用例；以及 CI 浏览器矩阵。

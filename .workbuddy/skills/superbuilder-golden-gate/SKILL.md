---
name: superbuilder-golden-gate
agent_created: true
description: This skill should be used when verifying a change to the SuperBuilder AI Native BI (.NET 10) codebase against the hard "Golden 18/18" regression gate (GET /evaluation/golden-runtime/run). It covers the constrained sandbox quirks that otherwise block the run: SQL Server TCP/1433 disabled (use shared-memory `Server=.` override), curl body-write failure (use the bundled urllib fetcher), and Qwen chat-model fallback when the configured model hits quota/403 or is unsuitable. Trigger before committing any change touching src/Application/BI, src/Domain/{Metadata,BiQuery}, src/Infrastructure/{Vector,Database}, Resolution, or any P-stage exit.
---

# SuperBuilder Golden 18/18 Gate — Verification Workflow

## Overview

Every P-stage (and any change to the gated query pipeline) must pass the **Golden 18/18** regression gate. The gate is a single HTTP endpoint that runs 18 canned BI query cases end-to-end through the real runtime (LLM + Qdrant + SQL) and asserts each case's `expectedOutcome`. A change is only shippable when the run returns `decision=PASS` with `passedCases=18/18`, `failedGates=[]`, `overallPassRate=1.0`.

This skill encodes the sandbox-specific workarounds needed to actually execute that endpoint here, plus how to read a (failing) result to tell a **code regression** from an **external fault** (Qwen quota/OCR model).

## When to use

- About to commit any change to `src/Application/BI`, `src/Domain/{Metadata,BiQuery}`, `src/Infrastructure/{Vector,Database}`, `Resolution`, or any P-stage exit criteria.
- The dashboard says "run Golden" but the service won't start, the curl hangs/fails, or the run BLOCKs.
- Diagnosing a `decision=BLOCK`: is it your code or the LLM?

## Workflow

### 1. Build + unit tests (fast pre-gate)
```bash
cd SuperBuilder_AI
"/c/Program Files/dotnet/dotnet.exe" build -v q -nologo
cd ../tests/SuperBuilder_AI.Tests
"/c/Program Files/dotnet/dotnet.exe" test --no-build -v q -nologo
```
Green build (0 error) + 4/4 tenant-filter tests is required but NOT sufficient — the Golden gate is the real gate.

### 2. Start the service (shared-memory SQL override)
In this sandbox **SQL Server TCP/1433 is disabled** (`HKLM\...\SuperSocketNetLib\Tcp\Enabled=0`, requires admin to fix). Start the service with a **shared-memory** connection override so it can still reach `SuperBuilder_Platform`:

```bash
cd SuperBuilder_AI
export ASPNETCORE_URLS="http://localhost:5032"
export ConnectionStrings__DefaultConnection="Server=.;Database=SuperBuilder_Platform;User Id=live;***REMOVED***;TrustServerCertificate=True;"
"/c/Program Files/dotnet/dotnet.exe" bin/Debug/net10.0/SuperBuilder_AI.dll > /c/tmp/sb_service.log 2>&1 &
```

**Critical:** the override must keep the DB name, SQL credentials, and `TrustServerCertificate=True`. A bare `Server=.` drops those and triggers an SSPI/encryption handshake error (`Error Number:-2146893019`). Verify startup with:
```bash
curl -s -m 20 -o /dev/null -w "HTTP %{http_code}\n" http://localhost:5032/api/tenant-management
```
200 = shared-memory DB works.

### 2b. Pre-flight: Qdrant liveness (do NOT skip)
Golden additionally needs **Qdrant** (semantic vector search) on REST 6333 / gRPC 6334:
```bash
curl -s -m 5 -o /dev/null -w "Qdrant REST: HTTP %{http_code}\n" http://localhost:6333/collections
```

**Qdrant is a frequent silent failure.** If it returns `000`/unreachable (process exited), **all 18 cases ERROR** with `Semantic Applicability 执行异常：Status(StatusCode="Unavailable", Detail="Error connecting to subchannel.")` — alarming-looking but pure infrastructure, unrelated to Qwen and unrelated to code. Restart it from a neutral writable CWD (Qdrant refuses to start from protected dirs):
```bash
cd /c/tmp/qdrant_run && "/c/Users/ThinkPad  X1/Downloads/qdrant-x86_64-pc-windows-msvc/qdrant.exe" > /c/tmp/qdrant_run_restart.log 2>&1 &
```
Confirm the index survived: collection `superbi_metadata` must report **961 points / status green / dim 1024**. If points are missing, re-scan metadata to rebuild the vector index before Golden can pass.

### 3. Run the Golden gate (bundled fetcher)
The endpoint buffers the full response until all 18 cases finish (~300–360s). A plain `curl -o file` frequently **fails to write the body (exit 23)** even on HTTP 200. Use the bundled reliable fetcher:

```bash
cd /c/tmp && python <skill>/scripts/fetch_golden.py 5032 C:/tmp/golden_run.json
```
Run it in the background and block-wait; it prints a summary (decision, passedCases, pass rates, ERROR buckets) and saves the full JSON. Exit code is non-zero if `decision != PASS`, so a caller can branch on `$?`.

### 4. Read the result — code regression vs external fault
Parse the saved JSON or the printed summary:
- `decision=PASS`, `passedCases=18`, `failedGates=None`, `overallPassRate=1.0` → **gate GREEN**, safe to commit.
- `decision=BLOCK` with a majority of `ERROR` cases whose `reason` contains `403` / `quota` / `forbidden` → **external Qwen fault, NOT a code regression**. Confirm by probing the chat endpoint directly (see "Qwen model fallback" below).
- `decision=BLOCK` with **all** cases ERRORing on `Unavailable` / `Error connecting to subchannel` → **Qdrant is down**, not a code regression. Restart Qdrant (see step 2b) and re-run.
- `decision=BLOCK` with `0` Qwen errors but many positive cases `FAIL @ Decision Gate` → model produced wrong query plans. If the Gate path uses an **untouched overload** (e.g. a parameterless `UnderstandAsync(question)`), this is a **model-capability mismatch, still not a code regression** — but it does block the gate.

### 5. Qwen model fallback (unblocking the gate)
The configured chat model can fail the gate for two external reasons:
1. **`AllocationQuota.FreeTierOnly` / 403** — the model's free quota is exhausted. `qwen3.7-plus-2026-05-26` hits this. The **embeddings** endpoint keeps working, and `qwen-plus` / `qwen-turbo` / `qwen3.5-plus` still return 200.
2. **Wrong model class** — `qwen3.5-ocr` returns 200 but is an **OCR-only** model, useless for general BI intent understanding → 11/18 positive cases fail the Decision Gate. Never use an OCR model for Golden.

To diagnose, probe directly (key read from `appsettings.json` `Qwen:ApiKey`):
```bash
KEY=$(grep -A1 '"Qwen"' appsettings.json | grep -m1 ApiKey | sed -E 's/.*"ApiKey": *"([^"]+)".*/\1/')
curl -s -m 25 -H "Authorization: Bearer $KEY" -H "Content-Type: application/json" \
  -d '{"model":"qwen-plus","messages":[{"role":"user","content":"hi"}],"max_tokens":5}' \
  https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions -o /tmp/p.json -w "HTTP %{http_code}\n"
```
Pick a **general chat model** that returns 200. The project's tuned model is `qwen3.7-plus` (guaranteed 18/18 once quota restored); `qwen-plus` is the closest general fallback. Switch via `Qwen:Model` in `appsettings.json`, **restart the service** (it loads config at startup), then re-run step 3.

## Notes / gotchas
- The Golden gate runs the **runtime** (live LLM + Qdrant), so run-to-run jitter from Qwen rate limits is expected; 3 transient 403/ERROR on negative cases with `expectedOutcomeSatisfied=true` still counts as PASS.
- `dotnet build` emits ~10 pre-existing nullable warnings in files outside the changed set — do not treat them as regressions; check the warnings are not in the files you touched.
- **`dotnet build` fails with MSB3027/MSB3021 ("file locked by .NET Host")** — the running service holds `bin/Debug/net10.0/SuperBuilder_AI.dll`. This happens every time you edit code while the service is up. Stop it first, then build:
  ```powershell
  Get-CimInstance Win32_Process | Where-Object { $_.CommandLine -like "*SuperBuilder_AI.dll*" } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
  ```
  Working loop: **stop service → build → (ef migrations if needed) → start service → run Golden**.
- **EF tools order matters**: `ef database update --no-build` runs against the assembly compiled *before* the migration was generated, producing `PendingModelChangesWarning`. Correct sequence: `build` → `ef migrations add` → `build` again → `ef database update`.
- Migration commands must override the connection string (TCP is disabled): append `--connection "Server=.;Database=SuperBuilder_Platform;User Id=live;***REMOVED***;TrustServerCertificate=True;"`.
- Each P-stage exit = `Golden 18/18`. Keep `Evaluation/Golden/query-plan-golden-v1.json` (the contract) untouched.

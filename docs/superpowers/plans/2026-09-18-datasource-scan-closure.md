# Data Source Scan Closure Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the remaining v10 scan lifecycle gaps with table-level vector failures, safe old-version row GC, and real-backend acceptance evidence.

**Architecture:** The activation gate reports physical table keys for incomplete vectors, and the hosted worker persists them through the existing failure recorder. A separate idempotent maintenance job deletes inactive metadata versions only when no live external references remain. Real-backend tests exercise the cross-system contracts without replacing existing fast unit tests.

**Tech Stack:** .NET 10, EF Core, xUnit, SQL Server, MySQL, PostgreSQL, Qdrant.

**Spec:** `docs/plans/active/2026-09-datasource-scan-optimization-final.md`

## Global Constraints

- Preserve the active version and old vectors on a failed or cancelled scan.
- Default orphan handling remains Block.
- Real MySQL, SQL Server, PostgreSQL and Qdrant are mandatory for final v10 acceptance.

---

## Task 1: Table-level vector failure contract

**Files:** `MetadataScannerService.cs`, `MetadataActivationBlockedException.cs`, `MetadataScanHostedService.cs`, scanner and controller tests.

- [x] Add failing tests that activation returns every physical table with an incomplete required point.
- [x] Persist those failures before staging cleanup; permit retry-failed to select them.
- [x] Verify old active pointer remains unchanged and run focused tests (real vector retention awaits Task 3).

## Task 2: Old-version relational GC

**Files:** new `MetadataVersionGcJob.cs`, `MetadataVectorMaintenanceHostedService.cs`, DI registration, GC tests.

- [x] Add failing tests for old-version deletion and reference-protected retention.
- [x] Delete semantic, column and table rows in a transaction, guarded by active-version and reference checks; register the job in the maintenance cycle.
- [x] Verify idempotency and run focused tests.

## Task 3: Real-backend acceptance

**Files:** real-backend tests, `.github/workflows/dotnet-build.yml` or a dedicated workflow.

- [ ] Establish four real service fixtures and the five §9.1 scenarios.
- [ ] Run them against real services, record results and resolve failures.
- [ ] Run the full unit suite; claim v10 closure only after all five scenarios pass.

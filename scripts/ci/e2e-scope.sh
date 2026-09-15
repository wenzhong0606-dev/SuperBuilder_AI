#!/usr/bin/env bash
set -euo pipefail

files_file="${1:?changed-files path required}"
filter=""

if grep -Eq '^(\.github/workflows/dotnet-build\.yml|SuperBuilder_AI/src/Api/|tests/SuperBuilder_AI\.E2E\.Tests/)' "$files_file"; then
  filter='FullyQualifiedName~E2ETests'
elif grep -Eq '^(SuperBuilder_AI/src/Application/Metadata/|SuperBuilder_AI/src/Infrastructure/Database/|SuperBuilder_AI/src/Infrastructure/Llm/|SuperBuilder_AI/src/Domain/Metadata/)' "$files_file"; then
  filter='FullyQualifiedName~DataSourceScanE2ETests'
elif grep -Eq '^(SuperBuilder_AI/src/Application/(Auth|Identity)/|SuperBuilder_AI/src/Domain/(Identity|Security)/)' "$files_file"; then
  filter='FullyQualifiedName~PermissionMatrixE2ETests|FullyQualifiedName~RlsIsolationE2ETests|FullyQualifiedName~PlatformLoginE2ETests'
elif grep -Eq '^(SuperBuilder_AI\.Web/|SuperBuilder_AI\.Components/)' "$files_file"; then
  filter='FullyQualifiedName~PlatformLoginE2ETests|FullyQualifiedName~DashboardRenderE2ETests'
elif grep -Eq '^(SuperBuilder_AI/src/Application/(AppBuilder|BiQuery)/)' "$files_file"; then
  filter='FullyQualifiedName~AppRuntimeE2ETests|FullyQualifiedName~DashboardRenderE2ETests'
fi

printf '%s' "$filter"

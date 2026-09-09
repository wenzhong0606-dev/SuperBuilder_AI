import os, re, subprocess, sys

REPO = r"C:\developer\GIT\SuperBuilder_AI"
SRC = os.path.join(REPO, "SuperBuilder_AI", "src")
EXCLUDE_DIRS = {"bin", "obj", ".git"}

def read_file(path):
    data = open(path, "rb").read()
    had_bom = data[:3] == b"\xef\xbb\xbf"
    if had_bom:
        data = data[3:]
    text = data.decode("utf-8")
    return text, had_bom

def write_file(path, text, had_bom):
    data = text.encode("utf-8")
    if had_bom:
        data = b"\xef\xbb\xbf" + data
    with open(path, "wb") as f:
        f.write(data)

def walk_cs(root):
    for base, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
        for fn in files:
            if fn.endswith(".cs"):
                yield os.path.join(base, fn)

def apply_namespace(text, old, new):
    # 仅匹配 namespace 声明行（{ 或 ; 结尾），避免误伤 qualified 引用
    return re.sub(r'namespace\s+' + re.escape(old) + r'(\s*[\{;])',
                  r'namespace ' + new + r'\1', text)

def apply_using(text, old, new):
    text = text.replace("using " + old + ";", "using " + new + ";")
    text = text.replace("using static " + old + ";", "using static " + new + ";")
    return text

def apply_qualified(text, old, new):
    return text.replace(old + ".", new + ".")

# ---- 全局唯一（可全仓安全替换）的游离命名空间 ----
global_pairs = [
    ("SuperBuilder_AI.Application.BiQuery", "SuperBuilder_AI.Services.BI"),
    ("SuperBuilder_AI.Application.Metadata", "SuperBuilder_AI.Services"),
    ("SuperBuilder_AI.Configuration", "SuperBuilder_AI.Application.Common.Options"),
    ("SuperBuilder_AI.Services.Database", "SuperBuilder_AI.Infrastructure.Database"),
    ("SuperBuilder_AI.Migrations", "SuperBuilder_AI.Infrastructure.Persistence.Migrations"),
    ("SuperBuilder_AI.src.Infrastructure.Persistence.Migrations", "SuperBuilder_AI.Infrastructure.Persistence.Migrations"),
]

# ---- 路径精确编辑（old_ns 与保留命名空间共享，必须只改这些文件）----
specific_files = {
    os.path.join(SRC, "Domain", "BiQuery", "QueryJoinInferenceService.cs"): ("SuperBuilder_AI.Services.BI", "SuperBuilder_AI.Models.BI"),
    os.path.join(SRC, "Domain", "BiQuery", "QueryPlanDecisionGate.cs"): ("SuperBuilder_AI.Services.BI", "SuperBuilder_AI.Models.BI"),
    os.path.join(SRC, "Domain", "BiQuery", "QueryPlanMetadataValidator.cs"): ("SuperBuilder_AI.Services.BI", "SuperBuilder_AI.Models.BI"),
    os.path.join(SRC, "Domain", "BiQuery", "QuerySemanticValidator.cs"): ("SuperBuilder_AI.Services.BI", "SuperBuilder_AI.Models.BI"),
    os.path.join(SRC, "Domain", "SharedKernel", "FlexibleStringListConverter.cs"): ("SuperBuilder_AI.Models.BI", "SuperBuilder_AI.Models"),
}

repo_wide_changed = 0
src_ns_changed = 0

# 1) 全局唯一对在 SRC 内改 namespace 行；全仓（含 test/RCL/Web）改 using + qualified
for old, new in global_pairs:
    # 全仓扫描
    for fp in walk_cs(REPO):
        text, bom = read_file(fp)
        orig = text
        # 若该文件位于 SRC，改 namespace 行
        if fp.startswith(SRC + os.sep):
            text = apply_namespace(text, old, new)
        text = apply_using(text, old, new)
        text = apply_qualified(text, old, new)
        if text != orig:
            write_file(fp, text, bom)
            repo_wide_changed += 1
            if fp.startswith(SRC + os.sep):
                src_ns_changed += 1

# 2) 路径精确文件：只改 namespace 行 + 该文件内的 using（不改 qualified，避免误伤对保留命名空间的引用）
for fp, (old, new) in specific_files.items():
    if not os.path.exists(fp):
        print("WARN 未找到精确文件:", fp)
        continue
    text, bom = read_file(fp)
    orig = text
    text = apply_namespace(text, old, new)
    text = apply_using(text, old, new)
    if text != orig:
        write_file(fp, text, bom)
        src_ns_changed += 1
        print("  精确编辑:", os.path.relpath(fp, REPO))
    else:
        print("  (无变化):", os.path.relpath(fp, REPO))

print(f"\n全仓改动文件数: {repo_wide_changed}")
print(f"其中 SRC 内命名空间声明改动: {src_ns_changed}")

# 3) 校验：重跑命名空间提取，确认目标游离命名空间已不存在
NS_RE = re.compile(r'^\s*namespace\s+([A-Za-z0-9_.]+)\s*[{;]', re.M)
remaining = {}
for fp in walk_cs(SRC):
    data = open(fp, "rb").read()
    if data[:3] == b"\xef\xbb\xbf":
        data = data[3:]
    text = data.decode("utf-8", "replace")
    if "auto-generated" in text[:400] or fp.endswith(".Designer.cs"):
        continue
    m = NS_RE.search(text)
    if m and m.group(1) in {p[0] for p in global_pairs} | {s[0] for s in specific_files.values()}:
        remaining.setdefault(m.group(1), 0)
        remaining[m.group(1)] += 1
print("\n残留待消除命名空间:", remaining if remaining else "无（全部已归一）")

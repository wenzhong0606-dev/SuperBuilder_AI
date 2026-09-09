import os, re

REPO = r"C:\developer\GIT\SuperBuilder_AI"
EXCLUDE = {"bin", "obj", ".git"}

def read_file(path):
    data = open(path, "rb").read()
    bom = data[:3] == b"\xef\xbb\xbf"
    if bom: data = data[3:]
    return data.decode("utf-8"), bom

def write_file(path, text, bom):
    d = text.encode("utf-8")
    if bom: d = b"\xef\xbb\xbf" + d
    with open(path, "wb") as f: f.write(d)

# 匹配普通 using 指令：using X; （单行，X 为命名空间，不含 static/别名/分号内复杂形式）
USING_RE = re.compile(r'^(\s*)using\s+([A-Za-z0-9_.]+)\s*;\s*$', re.M)

changed = 0
for base, dirs, files in os.walk(REPO):
    dirs[:] = [d for d in dirs if d not in EXCLUDE]
    for fn in files:
        if not fn.endswith(".cs"): continue
        fp = os.path.join(base, fn)
        text, bom = read_file(fp)
        seen = set()
        out_lines = []
        modified = False
        for line in text.split("\n"):
            m = USING_RE.match(line)
            if m:
                ns = m.group(2)
                if ns in seen:
                    modified = True
                    continue  # 丢弃重复 using
                seen.add(ns)
            out_lines.append(line)
        if modified:
            write_file(fp, "\n".join(out_lines), bom)
            changed += 1

print(f"去重 using 完成，修改文件数: {changed}")

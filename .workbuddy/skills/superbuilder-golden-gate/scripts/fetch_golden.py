"""SuperBuilder Golden 18/18 gate fetcher.

Usage:
    python fetch_golden.py [port] [output_json]

Defaults: port=5032, output_json=C:/tmp/golden_run.json

Buffers the full response (the endpoint runs 18 cases end-to-end and
buffers ~300-360s), prints a summary, and saves the full JSON.
Exit code is non-zero when decision != PASS, so a caller can branch on $?.
"""
import sys
import os
import json
import urllib.request
from pathlib import Path

PORT = "5032"
OUTPUT = "C:/tmp/golden_run.json"

args = sys.argv[1:]
if len(args) >= 1:
    PORT = args[0]
if len(args) >= 2:
    OUTPUT = args[1]

# 路径健壮性：Windows 下 Git-Bash 习惯写 `/c/tmp/x.json`，Python 会把它当成
# 当前盘根下的 `C:\c\tmp\x.json` 而 FileNotFoundError。归一化为 `C:/tmp/x.json`。
if os.name == "nt" and OUTPUT.startswith("/") and len(OUTPUT) >= 3 and OUTPUT[2] == "/":
    OUTPUT = OUTPUT[1].upper() + ":" + OUTPUT[2:]
OUTPUT = str(Path(OUTPUT))

URL = f"http://localhost:{PORT}/evaluation/golden-runtime/run"


def main():
    try:
        req = urllib.request.Request(URL)
        with urllib.request.urlopen(req, timeout=540) as resp:
            body = resp.read().decode("utf-8")
    except Exception as e:  # noqa: BLE001
        print("FETCH ERROR:", repr(e))
        sys.exit(2)

    data = json.loads(body)
    decision = data.get("decision")
    sc = data.get("scorecard") or {}
    print("decision:", decision)
    print(
        "passedCases:", sc.get("passedCases"), "total:", sc.get("total"),
        "failedGates:", data.get("failedGates"),
    )
    print(
        "overallPassRate:", sc.get("overallPassRate"),
        "positivePassRate:", sc.get("positivePassRate"),
    )
    cases = data.get("cases") or []
    errs = [c for c in cases if c.get("decision") == "ERROR"]
    print(
        "cases:", len(cases), "ERRORs:", len(errs),
        "PASS:", sum(1 for c in cases if c.get("decision") == "PASS"),
    )
    for c in errs[:6]:
        print("  ERR", c.get("id"), ":", str(c.get("reason") or c.get("error"))[:150])

    try:
        parent = os.path.dirname(OUTPUT)
        if parent:
            os.makedirs(parent, exist_ok=True)
        with open(OUTPUT, "w", encoding="utf-8") as f:
            f.write(body)
        print("BODY_SAVED", OUTPUT)
    except Exception as e:  # noqa: BLE001
        print("SAVE ERROR:", repr(e))

    sys.exit(0 if decision == "PASS" else 1)


if __name__ == "__main__":
    main()

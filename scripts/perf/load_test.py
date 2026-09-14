#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
PERF-01 负载测试脚本 (M13-12)
================================
直接打真实环境 API，并发执行并采集 P50/P95/P99、失败率、吞吐(RPS)。
不依赖任何测试工程，真实部署环境 `pip install requests` 即可运行。

【端点与请求体必须按你真实环境调整】
- 下方 ENDPOINTS 为占位路径，请对照你真实 API（如 AskController、查询接口、登录接口）修改。
- ASK_BODY / QUERY_BODY / LOGIN_BODY 的字段名按真实契约调整。
- <TENANT> / <USER> / <PASS> 等占位符运行时由参数或默认值替换。

用法示例：
  pip install requests
  # 先登录拿 token：
  python load_test.py --base-url https://api.example.com --username admin --password xxx \
      --scenario login --requests 20 --concurrency 1,5,10,20
  # 用 token 跑 ask 场景：
  python load_test.py --base-url https://api.example.com --token <JWT> \
      --scenario ask --requests 200 --concurrency 1,5,10,20 --tenant <TENANT>
"""
import argparse
import concurrent.futures as cf
import sys
import time

try:
    import requests
except ImportError:
    sys.exit("请先安装依赖: pip install requests")

# ============ 按真实环境调整 ============
ENDPOINTS = {
    "ask":   "/api/ask",          # Ask 自然语言问答（真实路径以你环境为准）
    "query": "/api/query",         # 实时查询计划执行
    "login": "/api/auth/login",    # 登录鉴权
}

# 请求体模板（字段名按真实契约调整）
def ask_body(q, tenant):
    return {"question": q, "tenantId": tenant}

def query_body(q, tenant):
    return {"query": q, "tenantId": tenant}

def login_body(username, password):
    return {"username": username, "password": password}

# 默认问句池（库存/入库类，匹配 O3 试点口径；按真实元数据改）
DEFAULT_QUESTIONS = [
    "昨日各仓库入库数量是多少",
    "当前库存总量最大的前十个物料",
    "近七天入库趋势如何",
    "各仓库库龄分布是怎样的",
]

DEFAULT_HEADERS = {"Content-Type": "application/json"}


def login(base_url, username, password):
    r = requests.post(base_url + ENDPOINTS["login"],
                       json=login_body(username, password),
                       headers=DEFAULT_HEADERS, timeout=30)
    r.raise_for_status()
    j = r.json()
    # 按真实响应字段取 token
    return j.get("token") or j.get("accessToken") or j.get("data", {}).get("token")


def do_request(base_url, scenario, token, question, tenant, username, password):
    if scenario == "login":
        url = base_url + ENDPOINTS["login"]
        body = login_body(username, password)
        headers = DEFAULT_HEADERS
    else:
        url = base_url + ENDPOINTS[scenario]
        body = ask_body(question, tenant) if scenario == "ask" else query_body(question, tenant)
        headers = DEFAULT_HEADERS.copy()
        if token:
            headers["Authorization"] = f"Bearer {token}"
    t0 = time.perf_counter()
    try:
        r = requests.post(url, json=body, headers=headers, timeout=60)
        dt = time.perf_counter() - t0
        return dt, r.status_code < 400, r.status_code
    except Exception as e:  # 网络/超时等
        return time.perf_counter() - t0, False, str(e)


def run_scenario(base_url, scenario, token, concurrency, n_requests, questions, tenant, username, password):
    latencies, failures, codes = [], 0, {}
    q_pool = questions + [questions[0]]  # 保证可循环
    idx = [0]

    def worker(_):
        q = q_pool[idx[0] % len(q_pool)]
        idx[0] += 1
        return do_request(base_url, scenario, token, q, tenant, username, password)

    with cf.ThreadPoolExecutor(max_workers=concurrency) as ex:
        futures = [ex.submit(worker, i) for i in range(n_requests)]
        for f in cf.as_completed(futures):
            dt, ok, code = f.result()
            if ok:
                latencies.append(dt)
            else:
                failures += 1
            codes[str(code)] = codes.get(str(code), 0) + 1
    return latencies, failures, codes


def pct(data, p):
    if not data:
        return 0.0
    s = sorted(data)
    k = (len(s) - 1) * p
    f = int(k)
    c = min(f + 1, len(s) - 1)
    return s[f] + (s[c] - s[f]) * (k - f)


def main():
    ap = argparse.ArgumentParser(description="PERF-01 负载测试")
    ap.add_argument("--base-url", required=True, help="真实 API 基址，如 https://api.example.com")
    ap.add_argument("--token", default=None, help="JWT；省略则 login 场景或 --username/--password 获取")
    ap.add_argument("--username", default=None)
    ap.add_argument("--password", default=None)
    ap.add_argument("--scenario", required=True, choices=["ask", "query", "login"])
    ap.add_argument("--concurrency", default="1,5,10,20", help="逗号分隔的并发梯度")
    ap.add_argument("--requests", type=int, default=200, help="总请求数")
    ap.add_argument("--tenant", default="<TENANT>", help="租户 ID")
    args = ap.parse_args()

    token = args.token
    if not token and args.scenario != "login":
        if not (args.username and args.password):
            sys.exit("非 login 场景需 --token，或提供 --username/--password 登录获取")
        token = login(args.base_url, args.username, args.password)

    print(f"# Scenario={args.scenario}  TotalRequests={args.requests}")
    for c in [int(x) for x in args.concurrency.split(",") if x.strip()]:
        t0 = time.perf_counter()
        lat, fail, codes = run_scenario(
            args.base_url, args.scenario, token, c, args.requests,
            [q.replace("<TENANT>", args.tenant) for q in DEFAULT_QUESTIONS],
            args.tenant, args.username, args.password)
        wall = time.perf_counter() - t0
        total = len(lat) + fail
        rps = total / wall if wall > 0 else 0.0
        print(f"--- concurrency={c} ---")
        print(f"  P50={pct(lat,0.50)*1000:7.0f} ms   P95={pct(lat,0.95)*1000:7.0f} ms   P99={pct(lat,0.99)*1000:7.0f} ms")
        print(f"  failures={fail}/{total}   fail_rate={fail/total*100 if total else 0:.1f}%   RPS={rps:.1f}")
        print(f"  status_codes={codes}")


if __name__ == "__main__":
    main()

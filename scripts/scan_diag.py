#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
SuperBuilder_AI 元数据扫描诊断脚本（独立于 Playwright / 浏览器）。

追踪一次元数据扫描的真实进度，重点监控「向量索引阶段」(vectorsProcessed/vectorsTotal)，
并自动识别两类故障：
  1) API 不可达 / 进程崩溃（连接拒绝、502、5xx）—— 对应之前「向量 295/1230 卡住」的根因；
  2) 进度卡住（连续 N 次轮询 stage+向量进度+百分比均不变，且 status 仍为 Running）。

两种用法：
  A. 监控已有 job（只读，安全，不触发任何写操作）：
     python3 scan_diag.py --base http://localhost:5032 --tenant-id 4 \
         --username e2eadmin --password '***' --data-source-id 5 --job-id 123
  B. 触发新扫描并监控（写操作：会创建 job 并消耗 LLM / 向量配额）：
     ... （同上） --trigger

退出码：
  0 = 终态 Succeeded
  2 = 终态 Failed，或超时且判定卡住
  3 = 超时且判定为 API 不可达 / 崩溃
  1 = 参数错误或登录 / 触发失败

所有参数均可由同名环境变量提供（SB_API_URL / SB_TENANT_ID / SB_USER / SB_PASSWORD /
SB_DATA_SOURCE_ID / SB_JOB_ID），命令行优先。
"""

import argparse
import json
import os
import sys
import time
from urllib import request, error as urlerror

TERMINAL = {"Succeeded", "Failed"}


def http_json(method, url, token=None, body=None, timeout=20):
    """发 JSON 请求，返回 (code, parsed)。code 为 None 表示连接层失败（API 不可达）。"""
    headers = {"Accept": "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = request.Request(url, data=data, headers=headers, method=method)
    try:
        with request.urlopen(req, timeout=timeout) as r:
            raw = r.read().decode("utf-8", errors="replace")
            return r.status, (json.loads(raw) if raw else None)
    except urlerror.HTTPError as e:
        raw = e.read().decode("utf-8", errors="replace")
        try:
            txt = json.loads(raw)
        except Exception:
            txt = raw
        return e.code, txt
    except urlerror.URLError as e:
        # 连接层失败：API 崩溃 / 未启动 / 网络不通
        return None, {"error": str(getattr(e, "reason", e))}


def login(base, tenant_id, username, password):
    code, body = http_json(
        "POST",
        base.rstrip("/") + "/api/auth/login",
        body={"username": username, "tenantId": tenant_id, "password": password},
    )
    if code != 200 or not isinstance(body, dict) or not body.get("token"):
        raise SystemExit(f"[FATAL] 登录失败 HTTP {code}: {body}")
    return body["token"], body


def fmt_vec(pd):
    vp, vt = pd.get("vectorsProcessed"), pd.get("vectorsTotal")
    sp, st = pd.get("semanticsProcessed"), pd.get("semanticsTotal")
    tp, td = pd.get("tablesProcessed"), pd.get("tablesDiscovered")
    return (
        f"vec={vp}/{vt} sem={sp}/{st} tbl={tp}/{td} "
        f"msg={pd.get('stageMessage')}"
    )


def main():
    ap = argparse.ArgumentParser(description="SuperBuilder 元数据扫描诊断器")
    ap.add_argument("--base", default=os.getenv("SB_API_URL", "http://localhost:5032"))
    ap.add_argument("--tenant-id", type=int, default=int(os.getenv("SB_TENANT_ID", "0") or 0))
    ap.add_argument("--username", default=os.getenv("SB_USER"))
    ap.add_argument("--password", default=os.getenv("SB_PASSWORD"))
    ap.add_argument("--data-source-id", type=int,
                    default=int(os.getenv("SB_DATA_SOURCE_ID", "0") or 0))
    ap.add_argument("--job-id", type=int, default=int(os.getenv("SB_JOB_ID", "0") or 0))
    ap.add_argument("--trigger", action="store_true", help="触发一次新扫描并监控（写操作）")
    ap.add_argument("--interval", type=float, default=5.0, help="轮询间隔（秒）")
    ap.add_argument("--stall-threshold", type=int, default=18,
                    help="连续多少次无进度判为卡住（默认 18 ≈ interval*18 秒）")
    ap.add_argument("--timeout", type=float, default=3600.0, help="最长监控时长（秒）")
    ap.add_argument("--out", default=None, help="将带时间戳的诊断日志写到该文件")
    args = ap.parse_args()

    if not args.username or not args.password or args.tenant_id <= 0:
        raise SystemExit("[FATAL] 需提供 --tenant-id / --username / --password")
    if args.data_source_id <= 0:
        raise SystemExit("[FATAL] 需提供 --data-source-id")
    if not args.job_id and not args.trigger:
        raise SystemExit("[FATAL] 需指定 --job-id（监控已有）或 --trigger（触发新扫描）")

    log_f = open(args.out, "a", encoding="utf-8") if args.out else None

    def log(line):
        ts = time.strftime("%Y-%m-%d %H:%M:%S")
        s = f"[{ts}] {line}"
        print(s)
        if log_f:
            log_f.write(s + "\n")
            log_f.flush()

    token, me = login(args.base, args.tenant_id, args.username, args.password)
    log(f"登录成功 user={me.get('username')} tenant={me.get('tenantId')} "
        f"perms={len(me.get('permissions') or [])}")

    ds = args.data_source_id
    if args.job_id:
        job_id = args.job_id
        log(f"监控已有 jobId={job_id} dataSourceId={ds}")
    else:
        code, body = http_json(
            "POST", f"{args.base.rstrip('/')}/api/data-sources/{ds}/metadata/scan",
            token=token)
        if code != 202 or not isinstance(body, dict) or "jobId" not in body:
            raise SystemExit(f"[FATAL] 触发扫描失败 HTTP {code}: {body}")
        job_id = body["jobId"]
        log(f"已触发扫描，jobId={job_id}（写操作：已创建扫描任务）")

    url = f"{args.base.rstrip('/')}/api/data-sources/{ds}/metadata/scan/{job_id}"
    last_sig = None
    stall = 0
    api_down_since = None
    api_down_total = 0.0
    api_down_events = 0
    start = time.time()

    try:
        while True:
            elapsed = time.time() - start
            if elapsed > args.timeout:
                if api_down_since is not None:
                    log(f"TIMEOUT：监控 {elapsed:.0f}s，且当前 API 不可达")
                    sys.exit(3)
                if stall >= args.stall_threshold:
                    log(f"TIMEOUT：监控 {elapsed:.0f}s，判定卡住（连续 {stall} 次无进度）")
                    sys.exit(2)
                log(f"TIMEOUT：监控 {elapsed:.0f}s，未达终态")
                sys.exit(3)

            code, body = http_json("GET", url, token=token)
            if code is None or (isinstance(code, int) and code >= 500):
                if api_down_since is None:
                    api_down_since = time.time()
                    api_down_events += 1
                    reason = body.get("error") if isinstance(body, dict) else body
                    log(f"!! API 不可达 (code={code}): {reason}")
                time.sleep(args.interval)
                continue
            if api_down_since is not None:
                dur = time.time() - api_down_since
                api_down_total += dur
                log(f"API 恢复，本次不可达持续 {dur:.1f}s（累计 {api_down_total:.1f}s / {api_down_events} 次）")
                api_down_since = None

            if not isinstance(body, dict):
                log(f"响应非预期: {body}")
                time.sleep(args.interval)
                continue

            status = body.get("status")
            stage = body.get("stage")
            pct = body.get("progressPercent")
            pd = body.get("progressDetails") or {}
            log(f"status={status} stage={stage} pct={pct}%  {fmt_vec(pd)}")

            if status in TERMINAL:
                err = body.get("errorCode")
                msg = body.get("errorMessage")
                log(f"==> 终态: {status}  errorCode={err}  errorMessage={msg}")
                if api_down_total > 0:
                    log(f"汇总：API 不可达累计 {api_down_total:.1f}s / {api_down_events} 次")
                sys.exit(0 if status == "Succeeded" else 2)

            sig = (stage, pd.get("vectorsProcessed"), pct)
            if status == "Running":
                if sig == last_sig:
                    stall += 1
                    if stall == args.stall_threshold:
                        log(f"!! 疑似卡住：连续 {stall} 次 stage/向量进度/百分比无变化")
                        log(f"   最后快照: stage={stage} vec={pd.get('vectorsProcessed')}/"
                            f"{pd.get('vectorsTotal')} pct={pct}%  "
                            f"API不可达累计={api_down_total:.1f}s")
                else:
                    stall = 0
                last_sig = sig

            time.sleep(args.interval)
    finally:
        if log_f:
            log_f.close()


if __name__ == "__main__":
    main()

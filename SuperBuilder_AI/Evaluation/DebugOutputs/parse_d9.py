import json

path = 'golden_result_d9.json'
with open(path, 'r', encoding='utf-8') as f:
    raw = f.read()
# strip trailing curl -w status line if present
last_brace = raw.rfind('}')
data = json.loads(raw[:last_brace + 1])

sc = data.get('scorecard', {})
print("=== SCORECARD ===")
print(json.dumps(sc, ensure_ascii=False, indent=1))

print("\n=== PER-CASE SUMMARY ===")
for c in data.get('cases', []):
    print(f"{c.get('caseId'):10} {c.get('category'):10} passed={str(c.get('passed')):5} stage={c.get('stage'):24} conf={c.get('confidenceLevel')}/{c.get('confidenceScore')} :: {c.get('reason')}")

fail_ids = ['GQ-003', 'GQ-005', 'GQ-006', 'GQ-008', 'GQ-009', 'GQ-010']
print("\n=== FAILING CASE DETAIL (validation + evaluation diagnostics) ===")
for c in data.get('cases', []):
    if c.get('caseId') in fail_ids:
        print(f"\n##### {c.get('caseId')} :: {c.get('question')}")
        print(f"  stage={c.get('stage')} passed={c.get('passed')} reason={c.get('reason')}")
        vd = c.get('validationDiagnostics')
        if vd:
            print(f"  [Validation] passed={vd.get('validationPassed')} errors={vd.get('errorCount')} warnings={vd.get('warningCount')}")
            print(f"  [Repair] status={vd.get('repairStatus')} attempts={vd.get('repairAttempts')} stop={vd.get('repairStopReason')}")
            for e in vd.get('errors', []):
                print(f"    ERR code={e.get('code')} field={e.get('field')} type={e.get('type')} msg={e.get('message')} colId={e.get('metadataColumnId')} tblId={e.get('metadataTableId')}")
            for w in vd.get('warnings', []):
                print(f"    WARN code={w.get('code')} field={w.get('field')} msg={w.get('message')}")
        ed = c.get('evaluationDiagnostics')
        if ed:
            print(f"  [Evaluation] passed={ed.get('passed')}")
            for sec in ['intent', 'metrics', 'dimensions', 'filters', 'tables', 'joins', 'queryShape', 'bindingConsistency']:
                s = ed.get(sec)
                if s:
                    print(f"    {sec}: passed={s.get('passed')} score={s.get('score')} detail={s.get('detail')}")
            se = ed.get('semanticEvidence')
            if se:
                print(f"    semanticEvidence: passed={se.get('passed')} detail={se.get('detail')}")

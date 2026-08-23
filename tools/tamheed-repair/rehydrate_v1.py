"""Move values from custom_attributes.v1 into the typed columns they belong in.

The migration parked values it could not map into the v1 provenance blob and left the
typed column NULL. The data is not lost, but every consumer of the typed schema sees an
empty column and no gate notices.

CRITICAL - guard G1: the emitted payloads NEVER include `custom_attributes`. Sending it
REPLACES the whole JSON, destroying the {"v1": ...} blob that is the only copy of the
data being recovered. entity_upsert leaves omitted columns untouched
(tamheed_server.py:262), so omission is both safe and required.

Every payload carries its table's NOT-NULL-no-default columns, because the INSERT half
of the upsert evaluates NOT NULL before conflict resolution.

Idempotent: re-running produces identical payloads.

Read-only. Emits JSON for review; performs no MCP calls.

    python rehydrate_v1.py --outdir reports/
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parents[2]
DATA = ROOT / "tamheed-package" / "data"

# v1 "Status" prose -> deferred_work.status. The v1 cells are multi-sentence prose
# beginning with "**Done ..." / "**Done in-repo ...", not clean enum values.
DEFERRED_STATUS = (
    ("won't-do", "Won't-do"),
    ("done", "Done"),
    ("in progress", "Activated"),
    ("scheduled", "Scheduled"),
    ("open", "Open"),
)

# v1 "Status" -> risks.risk_state. The migration wrote this into lifecycle_status and
# left the purpose-built risk_state at its schema default, so Closed and Accepted risks
# both read as open.
RISK_STATE = {
    "open": "open", "monitoring": "open", "mitigated": "mitigated",
    "closed": "retired", "accepted": "accepted",
}


def load(family: str) -> list[dict]:
    path = DATA / f"{family}.jsonl"
    if not path.exists():
        return []
    with path.open(encoding="utf-8") as fh:
        return [json.loads(line) for line in fh if line.strip()]


def v1(row: dict) -> dict:
    raw = row.get("custom_attributes")
    if not raw:
        return {}
    try:
        payload = json.loads(raw)
    except (TypeError, ValueError):
        return {}
    got = payload.get("v1")
    return got if isinstance(got, dict) else {}


def clean(value) -> str:
    """Strip markdown emphasis so a prose status cell can be matched."""
    return re.sub(r"[*`]", "", str(value or "")).strip()


def rehydrate_tests(rows: list[dict]) -> list[dict]:
    out = []
    for row in rows:
        kind = clean(v1(row).get("Type"))
        if kind and not row.get("kind"):
            out.append({"type": "test", "id": row["id"], "title": row["title"],
                        "kind": kind})
    return out


def rehydrate_kpis(rows: list[dict]) -> list[dict]:
    out = []
    for row in rows:
        source = v1(row)
        # Two v1 shapes merged into one family: 5 rows carry Measurement, 16 Cadence.
        measure = clean(source.get("Measurement")) or clean(source.get("Cadence"))
        if measure and not row.get("measure"):
            out.append({"type": "kpi", "id": row["id"], "title": row["title"],
                        "measure": measure})
    return out


def rehydrate_deferred(rows: list[dict]) -> list[dict]:
    out = []
    for row in rows:
        source = v1(row)
        status_text = clean(source.get("Status")).lower()
        status = next((mapped for needle, mapped in DEFERRED_STATUS
                       if needle in status_text), "Open")
        trigger = clean(source.get("Trigger to activate"))
        legacy = clean(source.get("#"))

        title = row["title"]
        # Surface the true v1 id: the migration renumbered D-nn -> DW-NNN by row
        # position over an unsorted source, so DW-015 is D-16, DW-019 is D-15, etc.
        # Guarded so a re-run does not double-prefix.
        if legacy and not title.startswith(legacy):
            title = f"{legacy} — {title}"

        payload = {
            "type": "deferred-work", "id": row["id"], "title": title,
            "severity": row.get("severity") or "medium",   # NOT NULL, no v1 source
            "status": status,
        }
        if trigger and not row.get("activation_trigger"):
            payload["activation_trigger"] = trigger
        out.append(payload)
    return out


def rehydrate_risks(rows: list[dict]) -> list[dict]:
    out = []
    for row in rows:
        status = clean(v1(row).get("Status")).lower()
        state = next((v for k, v in RISK_STATE.items() if status.startswith(k)), None)
        if state and row.get("risk_state") != state:
            out.append({"type": "risk", "id": row["id"], "title": row["title"],
                        "risk_state": state})
    return out


FAMILIES = {
    "tests": rehydrate_tests,
    "kpis": rehydrate_kpis,
    "deferred_work": rehydrate_deferred,
    "risks": rehydrate_risks,
}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--outdir", default="reports")
    args = ap.parse_args()
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

    outdir = pathlib.Path(args.outdir)
    outdir.mkdir(parents=True, exist_ok=True)
    print("# v1 -> typed-column rehydration payloads\n")

    total = 0
    for family, transform in FAMILIES.items():
        rows = load(family)
        payloads = transform(rows)
        total += len(payloads)
        (outdir / f"rehydrate-{family}-payloads.json").write_text(
            json.dumps(payloads, indent=2, ensure_ascii=False), encoding="utf-8")

        leaked = [p["id"] for p in payloads if "custom_attributes" in p]
        print(f"## {family} — {len(payloads)} of {len(rows)} rows\n")
        print(f"- guard G1 (no `custom_attributes` in any payload): "
              f"**{'PASS' if not leaked else 'FAIL ' + str(leaked)}**")
        changed = sorted({k for p in payloads for k in p if k not in ("type", "id")})
        print(f"- columns written: {', '.join(f'`{c}`' for c in changed) or '—'}\n")

        if family == "deferred_work":
            counts: dict[str, int] = {}
            for payload in payloads:
                counts[payload["status"]] = counts.get(payload["status"], 0) + 1
            print("| status | rows |")
            print("|---|---|")
            for status, count in sorted(counts.items()):
                print(f"| {status} | {count} |")
            print()
        elif payloads:
            for payload in payloads[:4]:
                extra = {k: v for k, v in payload.items()
                         if k not in ("type", "id", "title")}
                print(f"- `{payload['id']}` → {extra}")
            if len(payloads) > 4:
                print(f"- … {len(payloads) - 4} more")
            print()

    print(f"**Total rows to upsert: {total}**")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

"""Reconciliation profiler for the Tamheed package.

Detects the two damage classes that row-level checks (counts, gates, identifier_gaps)
structurally cannot see:

  1. typed-column starvation - a column at ~100% NULL whose value sits in custom_attributes.v1
  2. silent truncation       - a spike of field lengths at exactly a cap (120 / 200)

Run before the repair to capture a baseline, and after each task to prove no column's
null rate rose and that custom_attributes was never clobbered.

    python profile_nulls.py                 # markdown to stdout
    python profile_nulls.py --json out.json # machine-readable, for diffing runs

Read-only. Never writes to the package.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import sys

# ponytail: caps are hardcoded because they are hardcoded in migrate.py too
# (_clean_line -> [:120], plan.add -> title[:200]). If the plugin changes them, change here.
CAPS = (120, 200)
DATA = pathlib.Path(__file__).resolve().parents[2] / "tamheed-package" / "data"

# Columns whose NULL-ness carries no information in a freshly-migrated package:
# lifecycle/telemetry slots that only fill in as execution happens. Listing them
# separately is the difference between a 105-row wall of noise and a real signal.
LIFECYCLE_COLUMNS = frozenset({
    "disposition", "disposition_reason_ref", "superseded_by", "retired_in",
    "last_referenced", "discharged_by", "fixed_by", "found_in", "resolved_by",
    "recorded_at", "occurred_at", "due", "template_ref",
})


def load(path: pathlib.Path) -> list[dict]:
    with path.open(encoding="utf-8") as fh:
        return [json.loads(line) for line in fh if line.strip()]


def v1_keys(row: dict) -> set[str]:
    """Keys carried in the migration's raw v1 provenance blob, if any."""
    raw = row.get("custom_attributes")
    if not raw:
        return set()
    try:
        payload = json.loads(raw)
    except (TypeError, ValueError):
        return set()
    v1 = payload.get("v1")
    return set(v1) if isinstance(v1, dict) else set()


def is_empty(value) -> bool:
    return value is None or (isinstance(value, str) and not value.strip())


def profile(rows: list[dict]) -> dict:
    """Per-field null rate, length histogram at each cap, and the v1 shadow key set."""
    fields = list(rows[0]) if rows else []
    shadow = set().union(*(v1_keys(r) for r in rows)) if rows else set()
    out = {"rows": len(rows), "v1_keys": sorted(shadow), "fields": {}}

    for field in fields:
        values = [r.get(field) for r in rows]
        nulls = sum(1 for v in values if is_empty(v))
        lengths = [len(v) for v in values if isinstance(v, str)]
        entry = {
            "null": nulls,
            "null_pct": round(100 * nulls / len(rows), 1) if rows else 0.0,
            "max_len": max(lengths, default=0),
            # custom_attributes is a JSON blob; a blob that happens to be 120 chars
            # is not a truncation. Excluding it removes the only false-positive class.
            "at_cap": {} if field == "custom_attributes"
            else {cap: sum(1 for n in lengths if n == cap) for cap in CAPS},
        }
        # Report EVERY fully-NULL typed column. The v1 name-match is a hint, not a
        # filter - 'kind'<-'Type' and 'activation_trigger'<-'Trigger to activate'
        # share no substring, so filtering on the heuristic hides real starvation.
        if nulls == len(rows) and rows and field != "custom_attributes":
            entry["fully_null"] = True
            entry["lifecycle"] = field in LIFECYCLE_COLUMNS
            entry["starved_by"] = [
                k for k in shadow
                if field.replace("_", " ").lower() in k.lower()
                or k.lower().replace(" ", "_") == field.lower()
            ]
        out["fields"][field] = entry
    return out


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--json", metavar="PATH", help="also write machine-readable JSON here")
    args = ap.parse_args()

    # Windows consoles default to cp1252 and mangle the report's box/bullet glyphs.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]

    if not DATA.is_dir():
        print(f"package data not found: {DATA}", file=sys.stderr)
        return 1

    report = {}
    for path in sorted(DATA.glob("*.jsonl")):
        rows = load(path)
        if rows:
            report[path.stem] = profile(rows)

    total_rows = sum(r["rows"] for r in report.values())
    starved, lifecycle_null, truncated = [], [], []
    ca_present = {}

    for family, prof in report.items():
        for field, stat in prof["fields"].items():
            if stat.get("fully_null"):
                target = lifecycle_null if stat.get("lifecycle") else starved
                target.append((family, field, stat["starved_by"]))
            for cap, count in stat["at_cap"].items():
                if count:
                    truncated.append((family, field, cap, count, prof["rows"]))
        ca = prof["fields"].get("custom_attributes")
        if ca:
            ca_present[family] = prof["rows"] - ca["null"]

    print(f"# Tamheed package profile\n")
    print(f"- families: **{len(report)}**  ·  rows: **{total_rows}**\n")

    print(f"## Fully-NULL CONTENT columns — the signal ({len(starved)})\n")
    print("Columns carrying meaning that were left entirely empty. Where a "
          "`custom_attributes.v1` key looks like the source it is named, but the "
          "name-match is a hint only: `kind`←`Type` and "
          "`activation_trigger`←`Trigger to activate` share no substring.\n")
    if starved:
        print("| family | NULL column | likely source in `custom_attributes.v1` |")
        print("|---|---|---|")
        for family, field, keys in starved:
            hint = ", ".join(f"`{k}`" for k in keys) if keys else "—"
            print(f"| {family} | `{field}` | {hint} |")
    else:
        print("_none_")

    print(f"\n<details><summary>Fully-NULL lifecycle columns — expected, "
          f"no signal ({len(lifecycle_null)})</summary>\n")
    print("Telemetry/lifecycle slots that only fill in as execution happens "
          "(`disposition`, `last_referenced`, `superseded_by`, …). Listed for "
          "completeness so the signal table above stays readable.\n")
    for family, field, _ in lifecycle_null:
        print(f"- `{family}.{field}`")
    print("\n</details>")

    print("\n## Length spikes at a cap (silent truncation)\n")
    if truncated:
        print("| family | field | cap | at cap | of rows |")
        print("|---|---|---|---|---|")
        for family, field, cap, count, rows in sorted(
            truncated, key=lambda t: -t[3]
        ):
            print(f"| {family} | `{field}` | {cap} | **{count}** | {rows} |")
    else:
        print("_none_")

    print("\n## custom_attributes canary (must never decrease)\n")
    print("| family | rows with provenance |")
    print("|---|---|")
    for family, count in sorted(ca_present.items()):
        print(f"| {family} | {count} |")

    if args.json:
        pathlib.Path(args.json).write_text(
            json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8"
        )
        print(f"\n_JSON written to {args.json}_", file=sys.stderr)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

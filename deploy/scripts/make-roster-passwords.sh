#!/usr/bin/env bash
# Fill the `password` column of a committee roster CSV. WORKSTATION ONLY — never run this on a box.
#
#   bash deploy/scripts/make-roster-passwords.sh /path/to/acmp-users.csv
#
# The roster is the operator's own file (username, first name, last name, email, role). This adds a
# per-person password so credentials can be handed over privately. seed-users.sh then reads the same
# file, so the two never disagree about who gets which password.
#
# ONE PASSWORD PER UNIQUE USERNAME, NOT PER ROW. A person holding several roles occupies several
# rows — in the real roster `ahammo` occupies four (Secretary, Reviewer, Auditor, Administrator) but
# is ONE Keycloak account. A per-row generator would mint four passwords for it, the last write
# would win in Keycloak, and the operator would hand out three passwords that simply do not work.
#
# EXISTING VALUES ARE PRESERVED. Re-running must never rotate a password that has already been
# delivered to a person — that would lock them out with no signal to anyone. Only blank cells are
# filled, so adding a person later is safe.
#
# CHARACTER SET EXCLUDES 0/O and 1/l/I. These are typed by hand from a printed or messaged string,
# and the realm has bruteForceProtected=true, so an ambiguous glyph does not cause a typo — it
# causes a temporary account lockout that looks like a broken deployment.
#
# NOTHING IS PRINTED. The generated secrets go to the file and nowhere else; the summary reports
# counts only, so this is safe to run with the output visible.
set -euo pipefail

SRC="${1:-}"
[ -n "$SRC" ] || { echo "usage: make-roster-passwords.sh <roster.csv>" >&2; exit 2; }
[ -f "$SRC" ] || { echo "roster not found: $SRC" >&2; exit 2; }
command -v python >/dev/null 2>&1 || { echo "python is required" >&2; exit 2; }

PYTHONIOENCODING=utf-8 python - "$SRC" <<'PY'
import csv, io, os, secrets, sys

# Ambiguity is the enemy here, not entropy: 22 letters + 8 digits over 16 chars is ~78 bits, far
# more than a temporary credential that forces a change on first use needs.
ALPHABET = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789"
LENGTH = 16
PW_COL = "password"

path = sys.argv[1]
raw = open(path, "rb").read()
newline = "\r\n" if b"\r\n" in raw else "\n"
# Preserve the BOM if the file had one. Excel writes UTF-8 CSVs with a BOM and uses it to pick the
# encoding on open; silently dropping it is an unrequested change to someone else's file, and this
# project already learned today (re-pinning the SSM env) that "improving" a format in passing is how
# an untested change rides along inside an unrelated one.
bom = raw.startswith(b"\xef\xbb\xbf")
text = raw.decode("utf-8-sig")

rows = list(csv.reader(io.StringIO(text)))
rows = [r for r in rows if any(c.strip() for c in r)]      # drop blank trailing lines
if not rows:
    sys.exit("roster is empty")

header = [h.strip() for h in rows[0]]
body = rows[1:]

# Positional parse with a header assertion. The real file's header is "username,first name,last
# name,email,role" — spaces and all — so matching on exact names is safer than guessing.
expected = ["username", "first name", "last name", "email", "role"]
if [h.lower() for h in header[:5]] != expected:
    sys.exit(f"unexpected header: {header[:5]!r}\n  expected: {expected!r}")

if PW_COL in [h.lower() for h in header]:
    pw_idx = [h.lower() for h in header].index(PW_COL)
else:
    pw_idx = len(header)
    header.append(PW_COL)

def make() -> str:
    # Require all three classes so the value stays acceptable if a password policy is added later.
    while True:
        v = "".join(secrets.choice(ALPHABET) for _ in range(LENGTH))
        if (any(c.isupper() for c in v) and any(c.islower() for c in v)
                and any(c.isdigit() for c in v)):
            return v

by_user: dict[str, str] = {}
kept = 0

# Pass 1 — adopt any password already present, so a re-run cannot rotate a delivered credential.
for r in body:
    if len(r) > pw_idx and r[pw_idx].strip():
        u = r[0].strip()
        existing = r[pw_idx].strip()
        if u in by_user and by_user[u] != existing:
            sys.exit(f"conflicting existing passwords for '{u}' — refusing to guess which is live")
        by_user[u] = existing
        kept += 1

# Pass 2 — fill the gaps, keyed by username.
out = []
for r in body:
    r = list(r) + [""] * (pw_idx + 1 - len(r))
    u = r[0].strip()
    if not u:
        continue
    if any("," in c for c in r):
        sys.exit(f"field contains a comma for '{u}' — refusing to write a file I would corrupt")
    if u not in by_user:
        by_user[u] = make()
    r[pw_idx] = by_user[u]
    out.append(r)

buf = io.StringIO()
w = csv.writer(buf, lineterminator=newline)
w.writerow(header)
w.writerows(out)
open(path, "w", encoding="utf-8-sig" if bom else "utf-8", newline="").write(buf.getvalue())

print(f"rows        : {len(out)}")
print(f"unique users: {len(by_user)}")
print(f"preserved   : {kept} row(s) already had a password")
print(f"generated   : {len(by_user) - len({r[0].strip() for r in body if len(r) > pw_idx and r[pw_idx].strip()})} new")
print(f"line ending : {'CRLF' if newline == chr(13)+chr(10) else 'LF'} (preserved)")
PY

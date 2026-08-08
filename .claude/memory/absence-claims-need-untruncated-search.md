---
name: absence-claims-need-untruncated-search
description: Never claim something is ABSENT from a log/codebase based on a truncated search — `tail`/`head`/`-m` in the pipeline invalidates the claim.
metadata:
  node_type: memory
  type: feedback
---

A claim that something is **absent** is never supportable by a search that was truncated.
If `tail`, `head`, `-m`, or a `head_limit` appears in the pipeline that establishes the
claim, the claim is "I did not find it in the part I looked at" — not "it is not there".

**Why:** on 2026-08-09 I recorded in DEF-028 that `GET /api/members` "appears NOWHERE in
the access log for the run", and offered it as the first thread for the next reader to
pull. The grep behind it was truncated to its last eight matching lines, and all eight
were a frequently-polling `POST /api/members/me` that crowded everything else out. The
`GET` happens perfectly normally. A false lead sat in the permanent governance record and
would have cost the next person hours.

This is the **second** occurrence of the same shape in this project — [[tamheed-data-repair]]'s
sibling PE-183 already recorded that "searching for the wording you remember writing finds
the entries you remember writing", after a one-phrase grep found two of three carriers of a
retracted claim. Recording it as a note did not make it stick; it is a rule now.

**How to apply:**
- To prove absence, run the search **unbounded** and report the total match count.
- Prefer `grep -c` (a count) over `grep | tail` (a sample) when the question is "does X occur".
- If output really is too large, narrow with a *predicate* (time window, path filter), never
  with a positional cut — a predicate preserves the meaning of zero.
- If you cannot search exhaustively, write "I did not check", not "it does not appear".

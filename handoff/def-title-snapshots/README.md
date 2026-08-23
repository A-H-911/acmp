# Original defect narratives, preserved verbatim (2026-08-15)

`DEF-073`, `DEF-074`, `DEF-075`, `DEF-076` and `SC-012` were **closed** on 2026-08-15. Closing them
required a full-row `entity_upsert` (tamheed v4 refuses partial updates, and `title` is NOT NULL), and
their titles totalled roughly **18,000 characters** of diagnosis I had written earlier the same day.

Re-typing that is precisely the transport risk `findings_18` records — *the hand is the untrusted
transport* — so rather than claim a verbatim copy I could not mechanically guarantee, the rows were
closed with **condensed** titles stating the defect and its resolution, and the originals were kept
here byte-for-byte.

⚠ **The rows themselves point at the session scratchpad, which was wrong**: a scratchpad directory is
session-scoped, so a later session gets a different one and would never find these. This directory is
the durable location. The correction is recorded as a `correction` progress entry rather than by
editing the rows, per the v4 protocol.

The full diagnoses also survive in the progress entries — `PE-344` (DEF-073), `PE-347` (DEF-074),
`PE-348` + `PE-350` (DEF-075, including the correction to my own analysis), `PE-352` (DEF-076) — and
in the commit messages and PR bodies for #278, #279, #280 and #281.

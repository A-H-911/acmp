
_JSON written to tools/tamheed-repair/out/baseline-null-profile.json_
# Tamheed package profile

- families: **25**  ·  rows: **2436**

## Fully-NULL CONTENT columns — the signal (42)

Columns carrying meaning that were left entirely empty. Where a `custom_attributes.v1` key looks like the source it is named, but the name-match is a hint only: `kind`←`Type` and `activation_trigger`←`Trigger to activate` share no substring.

| family | NULL column | likely source in `custom_attributes.v1` |
|---|---|---|
| acceptance_criteria | `slice_id` | — |
| acceptance_criteria | `source_kind` | — |
| acceptance_criteria | `source_span` | — |
| adrs | `source_kind` | — |
| adrs | `source_span` | — |
| assumptions | `source_kind` | — |
| assumptions | `source_span` | — |
| constraints | `source_kind` | — |
| constraints | `source_span` | — |
| decisions | `source_kind` | — |
| decisions | `source_span` | — |
| deferred_work | `activation_trigger` | — |
| deferred_work | `invariant_at_stake` | — |
| dependencies | `owner` | — |
| dependencies | `source_kind` | — |
| dependencies | `source_span` | — |
| invariants | `source_kind` | — |
| invariants | `source_span` | — |
| kpis | `measure` | `Measurement` |
| kpis | `source_kind` | — |
| kpis | `source_span` | — |
| open_questions | `source_kind` | — |
| open_questions | `source_span` | — |
| packages | `entry_point` | — |
| phases | `objective` | — |
| phases | `exit_criteria` | — |
| phases | `source_kind` | — |
| phases | `source_span` | — |
| prompts | `phase_id` | — |
| risks | `source_kind` | — |
| risks | `source_span` | — |
| stakeholders | `role` | `Stakeholder / role` |
| stakeholders | `source_kind` | — |
| stakeholders | `source_span` | — |
| tests | `kind` | — |
| tests | `source_kind` | — |
| tests | `source_span` | — |
| wbs_items | `phase_id` | — |
| wbs_items | `slice_id` | — |
| wbs_items | `effort` | — |
| wbs_items | `source_kind` | — |
| wbs_items | `source_span` | — |

<details><summary>Fully-NULL lifecycle columns — expected, no signal (63)</summary>

Telemetry/lifecycle slots that only fill in as execution happens (`disposition`, `last_referenced`, `superseded_by`, …). Listed for completeness so the signal table above stays readable.

- `acceptance_criteria.disposition`
- `acceptance_criteria.disposition_reason_ref`
- `acceptance_criteria.superseded_by`
- `acceptance_criteria.retired_in`
- `acceptance_criteria.last_referenced`
- `adrs.disposition`
- `adrs.disposition_reason_ref`
- `adrs.superseded_by`
- `adrs.last_referenced`
- `assumptions.disposition`
- `assumptions.disposition_reason_ref`
- `assumptions.last_referenced`
- `audit_verdicts.recorded_at`
- `audit_verdicts.last_referenced`
- `constraints.disposition`
- `constraints.disposition_reason_ref`
- `constraints.last_referenced`
- `decisions.disposition`
- `decisions.disposition_reason_ref`
- `decisions.last_referenced`
- `deferred_work.last_referenced`
- `dependencies.disposition`
- `dependencies.disposition_reason_ref`
- `dependencies.last_referenced`
- `document_sections.last_referenced`
- `entity_types.template_ref`
- `invariants.disposition`
- `invariants.disposition_reason_ref`
- `invariants.last_referenced`
- `kpis.disposition`
- `kpis.disposition_reason_ref`
- `kpis.last_referenced`
- `milestones.due`
- `milestones.disposition`
- `milestones.disposition_reason_ref`
- `milestones.last_referenced`
- `narrative_documents.last_referenced`
- `open_questions.resolved_by`
- `open_questions.disposition`
- `open_questions.disposition_reason_ref`
- `open_questions.last_referenced`
- `phases.disposition`
- `phases.disposition_reason_ref`
- `phases.retired_in`
- `phases.last_referenced`
- `prompts.last_referenced`
- `requirements.disposition`
- `requirements.disposition_reason_ref`
- `requirements.retired_in`
- `requirements.last_referenced`
- `risks.discharged_by`
- `risks.disposition`
- `risks.disposition_reason_ref`
- `risks.last_referenced`
- `stakeholders.disposition`
- `stakeholders.disposition_reason_ref`
- `stakeholders.last_referenced`
- `tests.disposition`
- `tests.disposition_reason_ref`
- `tests.last_referenced`
- `wbs_items.disposition`
- `wbs_items.disposition_reason_ref`
- `wbs_items.last_referenced`

</details>

## Length spikes at a cap (silent truncation)

| family | field | cap | at cap | of rows |
|---|---|---|---|---|
| wbs_items | `title` | 120 | **133** | 155 |
| requirements | `title` | 120 | **78** | 218 |
| requirements | `title` | 200 | **78** | 218 |
| requirements | `statement` | 120 | **78** | 218 |
| acceptance_criteria | `title` | 120 | **74** | 74 |
| constraints | `title` | 200 | **15** | 15 |
| risks | `title` | 200 | **11** | 11 |
| invariants | `title` | 200 | **9** | 14 |
| assumptions | `title` | 200 | **8** | 16 |
| dependencies | `title` | 200 | **3** | 11 |
| open_questions | `title` | 200 | **3** | 58 |
| adrs | `title` | 120 | **2** | 33 |
| decisions | `title` | 200 | **1** | 28 |
| packages | `mvp_definition` | 200 | **1** | 1 |
| requirements | `statement` | 200 | **1** | 218 |

## custom_attributes canary (must never decrease)

| family | rows with provenance |
|---|---|
| acceptance_criteria | 74 |
| adrs | 33 |
| assumptions | 16 |
| audit_verdicts | 74 |
| constraints | 15 |
| decisions | 28 |
| deferred_work | 23 |
| dependencies | 11 |
| document_sections | 0 |
| entity_types | 0 |
| invariants | 14 |
| kpis | 21 |
| milestones | 6 |
| narrative_documents | 52 |
| open_questions | 58 |
| packages | 1 |
| phases | 4 |
| prompts | 0 |
| requirements | 218 |
| risks | 11 |
| stakeholders | 6 |
| tests | 50 |
| wbs_items | 20 |

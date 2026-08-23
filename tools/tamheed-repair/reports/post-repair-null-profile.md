# Tamheed package profile

- families: **29**  ·  rows: **2611**

## Fully-NULL CONTENT columns — the signal (31)

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
| deferred_work | `invariant_at_stake` | — |
| dependencies | `owner` | — |
| dependencies | `source_kind` | — |
| dependencies | `source_span` | — |
| invariants | `source_kind` | — |
| invariants | `source_span` | — |
| kpis | `source_kind` | — |
| kpis | `source_span` | — |
| open_questions | `source_kind` | — |
| open_questions | `source_span` | — |
| packages | `entry_point` | — |
| prompts | `phase_id` | — |
| risks | `source_kind` | — |
| risks | `source_span` | — |
| stakeholders | `role` | `Stakeholder / role` |
| stakeholders | `source_kind` | — |
| stakeholders | `source_span` | — |
| tests | `source_kind` | — |
| tests | `source_span` | — |
| wbs_items | `slice_id` | — |
| wbs_items | `source_kind` | — |
| wbs_items | `source_span` | — |

<details><summary>Fully-NULL lifecycle columns — expected, no signal (72)</summary>

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
- `defects.found_in`
- `defects.fixed_by`
- `defects.last_referenced`
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
- `progress_entries.last_referenced`
- `prompts.last_referenced`
- `requirements.disposition`
- `requirements.disposition_reason_ref`
- `requirements.retired_in`
- `requirements.last_referenced`
- `risks.discharged_by`
- `risks.disposition`
- `risks.disposition_reason_ref`
- `risks.last_referenced`
- `scope_changes.last_referenced`
- `slices.disposition`
- `slices.disposition_reason_ref`
- `slices.retired_in`
- `slices.last_referenced`
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
| decisions | `title` | 200 | **1** | 29 |
| packages | `mvp_definition` | 200 | **1** | 1 |
| progress_entries | `entry` | 120 | **1** | 125 |
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
| defects | 13 |
| deferred_work | 23 |
| dependencies | 11 |
| document_sections | 0 |
| entity_types | 0 |
| invariants | 14 |
| kpis | 21 |
| milestones | 6 |
| narrative_documents | 54 |
| open_questions | 58 |
| packages | 1 |
| phases | 4 |
| progress_entries | 125 |
| prompts | 0 |
| requirements | 218 |
| risks | 11 |
| scope_changes | 0 |
| slices | 0 |
| stakeholders | 6 |
| tests | 50 |
| wbs_items | 20 |

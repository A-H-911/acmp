# ADR-0003: Microsoft SQL Server as the Single Datastore

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP needs a datastore for transactional data (topics, votes, decisions, actions, audit), search (full-text across topics/docs/transcripts), reporting/dashboard analytics, and traceability graph traversal. The question is whether one datastore is sufficient or whether a second (search engine, analytics DB, graph DB) is needed from the start.

## Decision Drivers

- CON-001: self-contained deployment; each additional datastore adds an operational container, backup target, data-consistency boundary, and monitoring obligation.
- SQL Server is the organizational mandate for the ACMP instance; ACMP runs its own DB instance (not a shared cluster).
- Scale is ≤20 users, low traffic. There is no workload that SQL Server cannot handle at this scale.
- Reporting load can be served by columnstore indexes on read models — no separate analytics DB needed.
- Full-text search covers topics, documents, and transcripts adequately at this scale. FTS weakness on fuzzy/typo/autocomplete is acceptable for a 20-user internal tool.
- Graph traversal (traceability) is achievable via recursive CTEs / graph tables in SQL Server without a dedicated graph DB.
- Adding a second datastore (e.g., OpenSearch for search, PostgreSQL for something else) splits the backup/restore story, complicates migrations, and doubles the operational surface — unjustified without evidence of SQL Server being insufficient.

## Considered Options

1. **SQL Server only** — transactional + columnstore reporting + FTS + graph traversal in one engine.
2. **SQL Server + OpenSearch** — SQL Server for transactional; OpenSearch for full-text/faceted search.
3. **SQL Server + Redis** — add Redis for caching and session.
4. **SQL Server + PostgreSQL** — no rationale given the org already mandates SQL Server and the team knows EF Core with SQL Server.

## Decision Outcome

Chosen option: "SQL Server only", because at ≤20 users and low traffic, SQL Server's FTS, columnstore indexes, native JSON support, and recursive CTE graph traversal are demonstrably sufficient without a second datastore. Adding OpenSearch or any other engine introduces a consistency boundary (dual-write or CDC), an additional container, and additional backup complexity — costs that are not justified by any measured requirement. If FTS is outgrown (evidence: unacceptably slow queries or search quality complaints at scale), the platform will stand up its own self-hosted OpenSearch container (app-owned, never the org's ELK cluster). [Basis: §5.6 — airbyte/influxdata/medium comparisons; "From ElasticSearch back to SQL Server" (mauridb)]

### Consequences

- Good: one backup/restore target; one migration pipeline; one monitoring obligation; no dual-write complexity; simpler disaster-recovery; native JSON column support handles Tarseem spec storage and flexible metadata.
- Bad / trade-off: SQL Server FTS is weaker than Elasticsearch on fuzzy matching, typo tolerance, and autocomplete. Arabic word-breaking quality must be validated (see Validation). If search requirements grow, migration to a hybrid approach (SQL + self-hosted OpenSearch) requires adding a dual-write outbox or CDC — this is non-trivial to retrofit.

## Validation

- Validate SQL Server Arabic FTS word-breaker during Phase 1 development: run representative Arabic queries against a test dataset; verify recall is acceptable before launch. If the word-breaker is inadequate, raise OQ-### and evaluate self-hosted OpenSearch ahead of Phase 2 rather than at GA.
- Confirm columnstore index build times and query latency are acceptable for dashboard queries in Phase 2 load testing (simulated reporting queries on realistic data volumes).
- Monitor query performance from day 1 via query store; establish baseline before any optimization.

## Links / Notes

- SQL Server FTS sufficiency analysis: §5.6 of `.context/brief-digest.md`.
- No second DB in v1 is a hard constraint, not a recommendation — revisit only with measured evidence of SQL Server being insufficient.
- Tarseem JSON specs are stored as `NVARCHAR(MAX)` / JSON columns with the spec hash for traceability (see ADR-0006).
- The SQL Server instance is ACMP-owned: it runs in the Docker Compose stack alongside the ACMP app.
- Related: ADR-0001 (modular monolith — single schema), ADR-0008 (traceability via SQL graph), ADR-0011 (FTS), ADR-0014 (Hangfire uses ACMP SQL).

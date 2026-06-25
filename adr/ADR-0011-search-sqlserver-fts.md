# ADR-0011: SQL Server Full-Text Search in v1; Self-Hosted OpenSearch if Outgrown

- Status: Accepted
- Date: 2026-06-24
- Deciders: Architecture Committee (secretary-confirmed)

## Context and Problem Statement

ACMP needs full-text search across topics (title, description, scope), documents (meeting minutes, ADRs, architecture invariants), and (in later phases) meeting transcripts. Search quality requirements include English and Arabic text matching. The decision is which search technology to adopt for v1 and what the upgrade path looks like.

## Decision Drivers

- SQL Server is the sole permitted datastore in v1 (ADR-0003). Adding OpenSearch or Elasticsearch as a second datastore in v1 adds a dual-write outbox, a second backup target, and a second operational container — unjustified at ≤20 users and low traffic.
- SQL Server Full-Text Search (FTS) provides stemming, stopwords, ranked relevance (`CONTAINSTABLE`, `FREETEXTTABLE`), and language-specific word-breakers — adequate for moderate search loads.
- Arabic FTS in SQL Server depends on the Arabic word-breaker and stemmer provided by SQL Server's language resources. Quality must be validated before GA (see Validation).
- FTS weaknesses (fuzzy matching, typo tolerance, autocomplete, faceted search) are acceptable limitations for a 20-user internal governance tool in v1.
- The org has an existing ELK cluster, but ACMP must not use org-shared infrastructure (CON-001). Any search engine must be app-owned.
- OpenSearch (Apache-2.0) is a viable self-hosted option if FTS is outgrown; it can be added as a sidecar container with a dual-write adapter — no change to the search abstraction layer if one is provided.

## Considered Options

1. **SQL Server FTS in v1; self-hosted OpenSearch container (app-owned) if outgrown** — zero additional dependency in v1; clear upgrade path.
2. **OpenSearch from day one** — unnecessary operational complexity for ≤20 users; second datastore from launch; dual-write from day one.
3. **Org ELK cluster** — explicitly excluded by CON-001.
4. **Postgres tsvector + trigram (pg_trgm)** — not applicable; ACMP does not use PostgreSQL (ADR-0003).
5. **Azure Cognitive Search** — cloud dependency; violates on-prem constraint; data sovereignty concerns for a gov governance platform.

## Decision Outcome

Chosen option: "SQL Server FTS for v1; escalate to self-hosted OpenSearch only with measured evidence of SQL Server being insufficient", because SQL Server FTS avoids a second datastore in v1 without sacrificing acceptable search quality at this scale. If search is outgrown (evidenced by slow queries, poor Arabic recall, or user complaints at meaningful scale), the platform adds an app-owned OpenSearch container and a dual-write outbox — never the org's ELK. [Basis: §5.6 of `.context/brief-digest.md`]

### Consequences

- Good: no second datastore in v1; no dual-write complexity; one backup target; SQL Server already running for transactional data; FTS index maintenance is automatic via SQL Server.
- Bad / trade-off: Arabic FTS quality is a known risk (SQL Server Arabic word-breaker quality is [unverified] versus a tuned OpenSearch Arabic analyzer); fuzzy/typo-tolerant search is not available in v1; autocomplete requires a workaround (prefix queries or a dedicated suggestions table); if the FTS-to-OpenSearch migration is deferred too long, the dual-write retrofit may be more painful.

## Validation

- Phase 1 gate: before v1 GA, execute Arabic FTS validation test suite — representative Arabic topic titles and descriptions, verify recall and precision are acceptable. If not acceptable, escalate to `OQ-###` and plan OpenSearch addition in Phase 2 rather than deferring to Phase 3.
- English FTS validation: verify `CONTAINSTABLE` / `FREETEXTTABLE` returns expected results for mixed EN/AR content documents.
- Performance: FTS query response time under 500 ms for a database of 500 topics and 1,000 documents at development time. Document if this threshold is approached before launch.
- If OpenSearch is ever added: dual-write adapter test — write a topic, verify it appears in both SQL FTS index and OpenSearch within 5 seconds; delete a topic, verify it disappears from both.

## Links / Notes

- SQL Server FTS documentation: https://learn.microsoft.com/en-us/sql/relational-databases/search/full-text-search [unverified for latest SQL Server 2022 Arabic word-breaker quality]
- Search quality comparisons: §5.6 (airbyte, influxdata, medium references cited there; mauridb "From ElasticSearch back to SQL Server").
- If OpenSearch is added, it must run as an app-owned container in the ACMP Docker Compose stack — not a shared cluster. Image: `opensearchproject/opensearch`.
- Arabic analyzer for OpenSearch: `analysis-icu` plugin with `arabic` language filter — significantly better Arabic stemming than SQL Server's built-in word-breaker [unverified: requires head-to-head test].
- The search abstraction layer (`ISearchProvider` or similar) should be introduced at the same time as FTS in v1 so that swapping to OpenSearch does not require changing search-call sites.
- Related: ADR-0003 (single SQL Server; FTS as part of it), ADR-0013 (self-contained deployment — app-owned search only).

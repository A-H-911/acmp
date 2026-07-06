---
status: Approved
version: 1.0.0
updated: 2026-07-06
owner: Claude Code execution agent
generation: derived
---

# Traceability Matrix — ACMP

Derived from the registers — never hand-maintained; re-generate from source. Forward view: every requirement -> its governing decision(s), work item(s), test(s), and acceptance criterion. `Scope` = `MVP` (PH-1, checked by gate G-TRACE) or `Full` (PH-2/PH-3, exempt). `Coverage` = full / partial / gap. Decisions cite the module-governing ADR(s) (every module is also governed by ADR-0001); work items are `WBS-` leaves/groups ([work-breakdown](../planning/work-breakdown.md)); tests are `TEST-` suites ([test-strategy](../validation/test-strategy.md)); acceptance are `AC-` ([acceptance-criteria](../validation/acceptance-criteria.md)).

## Forward — functional requirements

| Req | Decisions | Work items | Tests | Acceptance | Scope | Coverage |
|---|---|---|---|---|---|---|
| FR-001 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | AC-001, AC-004 | MVP | full |
| FR-002 | ADR-0002, ADR-0013 | WBS-2 | TEST-002 | AC-002 | MVP | full |
| FR-003 | ADR-0002, ADR-0013 | WBS-1 | TEST-022, TEST-040 | AC-039 | MVP | full |
| FR-004 | ADR-0002, ADR-0013 | WBS-1 | TEST-034, TEST-035, TEST-040, TEST-042 | AC-040, AC-041, AC-046 | MVP | full |
| FR-005 | ADR-0002, ADR-0013 | WBS-1 | TEST-034 | AC-042 | MVP | full |
| FR-006 | ADR-0002, ADR-0013 | WBS-1 | TEST-023 | AC-049, AC-050 | MVP | full |
| FR-007 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-008 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-009 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-010 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-011 | ADR-0002, ADR-0013 | WBS-1 | TEST-031 | AC-056 | MVP | full |
| FR-012 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-013 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | AC-004 | MVP | full |
| FR-014 | ADR-0002, ADR-0013 | WBS-1 | TEST-002 | (covered by module suite) | MVP | full |
| FR-015 | ADR-0002, ADR-0013 | WBS-1 | TEST-022 | AC-047, AC-048 | MVP | full |
| FR-016 | ADR-0004, ADR-0015 | WBS-2 | TEST-001 | (covered by module suite) | MVP | full |
| FR-017 | ADR-0004, ADR-0015 | WBS-3 | TEST-001 | (covered by module suite) | MVP | full |
| FR-018 | ADR-0004, ADR-0015 | WBS-2 | TEST-001, TEST-004 | AC-002, AC-003, AC-007 | MVP | full |
| FR-019 | ADR-0004, ADR-0015 | WBS-3 | TEST-005, TEST-006 | AC-010 | MVP | full |
| FR-020 | ADR-0004, ADR-0015 | WBS-3 | TEST-002 | AC-058 | MVP | full |
| FR-021 | ADR-0004, ADR-0015 | WBS-3 | TEST-003 | AC-059 | MVP | full |
| FR-022 | ADR-0004, ADR-0015 | WBS-2 | TEST-005, TEST-021 | AC-009, AC-011 | MVP | full |
| FR-023 | ADR-0004, ADR-0015 | WBS-3 | TEST-001 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-024 | ADR-0004, ADR-0015 | WBS-2 | TEST-003, TEST-004 | AC-005, AC-006, AC-008 | MVP | full |
| FR-025 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-020, TEST-021, TEST-022 | AC-030 | MVP | full |
| FR-026 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-027 | ADR-0002, ADR-0018 | WBS-1 | TEST-023 | AC-049 | MVP | full |
| FR-028 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-029 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-019, TEST-020, TEST-021 | AC-031, AC-032 | MVP | full |
| FR-030 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-031 | ADR-0002, ADR-0018 | WBS-5 | TEST-018 | (covered by module suite) | MVP | full |
| FR-032 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-033 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-034 | ADR-0002, ADR-0018 | WBS-1 | TEST-033, TEST-035 | AC-043, AC-045, AC-046 | MVP | full |
| FR-035 | ADR-0002, ADR-0018 | WBS-5 | TEST-018 | (covered by module suite) | MVP | full |
| FR-036 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-037 | ADR-0002, ADR-0018 | WBS-5 | TEST-018 | (covered by module suite) | MVP | full |
| FR-038 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-032 | AC-057 | MVP | full |
| FR-039 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-040 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018, TEST-019 | AC-034 | MVP | full |
| FR-041 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-042 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018, TEST-019, TEST-041 | AC-035 | MVP | full |
| FR-043 | ADR-0002, ADR-0018 | WBS-1 | TEST-018 | (covered by module suite) | MVP | full |
| FR-044 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018 | AC-033 | MVP | full |
| FR-045 | ADR-0002, ADR-0018 | WBS-5.7 | TEST-018 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-046 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | MVP | full |
| FR-047 | ADR-0002 | WBS-1 | TEST-028, TEST-042 | AC-044, AC-045 | MVP | full |
| FR-048 | ADR-0002 | WBS-1 | TEST-024 | (covered by module suite) | MVP | full |
| FR-049 | ADR-0002 | WBS-1 | TEST-024 | (covered by module suite) | MVP | full |
| FR-050 | ADR-0002 | WBS-1 | TEST-024 | (covered by module suite) | MVP | full |
| FR-051 | ADR-0002 | WBS-1 | TEST-024 | (covered by module suite) | MVP | full |
| FR-052 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | MVP | full |
| FR-053 | ADR-0002 | WBS-20 | TEST-024 | (covered by module suite) | MVP | full |
| FR-054 | ADR-0002 | WBS-1 | TEST-024, TEST-025, TEST-026, TEST-027 | AC-014, AC-036, AC-037 | MVP | full |
| FR-055 | ADR-0002 | WBS-20 | TEST-024, TEST-025, TEST-026, TEST-027 | AC-038 | MVP | full |
| FR-056 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | MVP | full |
| FR-057 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-058 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-059 | ADR-0002 | WBS-20 | TEST-024 | (covered by module suite) | Full | full (post-MVP — PH3) |
| FR-060 | ADR-0002 | WBS-20 | TEST-024 | (covered by module suite) | Full | full (post-MVP — PH3) |
| FR-061 | ADR-0002 | WBS-6 | TEST-024 | (covered by module suite) | MVP | full |
| FR-062 | ADR-0009 | WBS-17.4 | TEST-007 | (covered by module suite) | MVP | full |
| FR-063 | ADR-0009 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-064 | ADR-0009 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-065 | ADR-0009 | WBS-1 | TEST-014, TEST-015, TEST-016 | AC-028 | MVP | full |
| FR-066 | ADR-0009 | WBS-1 | TEST-014, TEST-015, TEST-017 | AC-027 | MVP | full |
| FR-067 | ADR-0009 | WBS-17.4 | TEST-016 | AC-029 | MVP | full |
| FR-068 | ADR-0009 | WBS-17.4 | TEST-007 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-069 | ADR-0009 | WBS-17.4 | TEST-007 | (covered by module suite) | MVP | full |
| FR-070 | ADR-0009 | WBS-9 | TEST-011, TEST-012 | AC-021 | MVP | full |
| FR-071 | ADR-0009 | WBS-1 | TEST-013 | AC-024 | MVP | full |
| FR-072 | ADR-0009 | WBS-1 | TEST-013 | AC-022 | MVP | full |
| FR-073 | ADR-0009 | WBS-1 | TEST-007 | AC-023 | MVP | full |
| FR-074 | ADR-0009 | WBS-1 | TEST-007, TEST-015 | AC-015, AC-016 | MVP | full |
| FR-075 | ADR-0009 | WBS-9 | TEST-007, TEST-012, TEST-013 | AC-015, AC-025 | MVP | full |
| FR-076 | ADR-0009 | WBS-9 | TEST-007 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-077 | ADR-0009 | WBS-9 | TEST-011 | AC-026 | MVP | full |
| FR-078 | ADR-0009 | WBS-9 | TEST-007 | (covered by module suite) | MVP | full |
| FR-079 | ADR-0002 | WBS-10 | TEST-007 | (covered by module suite) | MVP | full |
| FR-080 | ADR-0002 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-081 | ADR-0002 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-082 | ADR-0002 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-083 | ADR-0002 | WBS-1 | TEST-030 | AC-054 | MVP | full |
| FR-084 | ADR-0002 | WBS-1 | TEST-030 | AC-055 | MVP | full |
| FR-085 | ADR-0002 | WBS-1 | TEST-007, TEST-008, TEST-009, TEST-010 | AC-012, AC-013 | MVP | full |
| FR-086 | ADR-0002 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-087 | ADR-0002 | WBS-1 | TEST-007 | (covered by module suite) | MVP | full |
| FR-088 | ADR-0002 | WBS-10 | TEST-007 | (covered by module suite) | MVP | full |
| FR-089 | ADR-0008 | WBS-11 | TEST-041 | (covered by module suite) | MVP | full |
| FR-090 | ADR-0008 | WBS-1 | TEST-041 | (covered by module suite) | MVP | full |
| FR-091 | ADR-0008 | WBS-11 | TEST-041 | (covered by module suite) | MVP | full |
| FR-092 | ADR-0008 | WBS-11 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-093 | ADR-0008 | WBS-11 | TEST-041 | (covered by module suite) | MVP | full |
| FR-094 | ADR-0008, ADR-0019 | WBS-15.6 | TEST-041 | (covered by module suite) | MVP | full |
| FR-095 | ADR-0008, ADR-0019 | WBS-15.6 | TEST-041 | (covered by module suite) | MVP | full |
| FR-096 | ADR-0008, ADR-0019 | WBS-15.6 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-097 | ADR-0008, ADR-0019 | WBS-15.6 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-098 | ADR-0008, ADR-0019 | WBS-15.6 | TEST-041 | (covered by module suite) | MVP | full |
| FR-099 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-100 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-101 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-102 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-103 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-104 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-105 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-106 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-107 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-108 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-109 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-110 | ADR-0021, ADR-0009 | WBS-17 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH3) |
| FR-111 | ADR-0007 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-112 | ADR-0007 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-113 | ADR-0007 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-114 | ADR-0007 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-115 | ADR-0007 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-116 | ADR-0002 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-117 | ADR-0002 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-118 | ADR-0002 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-119 | ADR-0002 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-120 | ADR-0002 | WBS-19 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-121 | ADR-0006 | WBS-18 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-122 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-123 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-124 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-125 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-126 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-127 | ADR-0006 | WBS-1 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-128 | ADR-0006 | WBS-18 | TEST-041 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-129 | ADR-0005 | WBS-13 | TEST-029 | AC-053 | MVP | full |
| FR-130 | ADR-0005 | WBS-20 | TEST-028, TEST-029, TEST-041 | AC-051 | MVP | full |
| FR-131 | ADR-0005 | WBS-13 | TEST-029 | AC-052 | MVP | full |
| FR-132 | ADR-0005 | WBS-13 | TEST-028 | (covered by module suite) | MVP | full |
| FR-133 | ADR-0005 | WBS-13 | TEST-028 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-134 | ADR-0005 | WBS-13 | TEST-028 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-135 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037, TEST-043 | AC-064 | MVP | full |
| FR-136 | ADR-0022, ADR-0003 | WBS-14.2 | TEST-037 | AC-065 | MVP | full |
| FR-137 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037, TEST-043 | AC-066 | MVP | full |
| FR-138 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-139 | ADR-0022, ADR-0003 | WBS-14.5 | TEST-037 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-140 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-141 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037 | (covered by module suite) | Full | full (post-MVP — PH3) |
| FR-142 | ADR-0022, ADR-0003 | WBS-20.5 | TEST-037 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-143 | ADR-0011, ADR-0020 | WBS-15 | TEST-045 | AC-060 | MVP | full |
| FR-144 | ADR-0011, ADR-0020 | WBS-15 | TEST-036 | AC-060 | MVP | full |
| FR-145 | ADR-0011, ADR-0020 | WBS-15 | TEST-045 | AC-061 | MVP | full |
| FR-146 | ADR-0011, ADR-0020 | WBS-15 | TEST-036, TEST-046 | AC-062 | MVP | full |
| FR-147 | ADR-0011, ADR-0020 | WBS-15 | TEST-036, TEST-046 | AC-063 | MVP | full |
| FR-148 | ADR-0011, ADR-0020 | WBS-15 | TEST-036 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-149 | ADR-0011, ADR-0020 | WBS-15 | TEST-036 | (covered by module suite) | Full | full (post-MVP — PH3) |
| FR-150 | ADR-0009 | WBS-16 | TEST-044 | AC-017 | MVP | full |
| FR-151 | ADR-0009 | WBS-16 | TEST-044 | AC-017 | MVP | full |
| FR-152 | ADR-0009 | WBS-16 | TEST-044 | AC-018, AC-019 | MVP | full |
| FR-153 | ADR-0009 | WBS-16 | TEST-044 | AC-020 | MVP | full |
| FR-154 | ADR-0009 | WBS-16 | TEST-044 | (covered by module suite) | Full | full (post-MVP — PH2) |
| FR-155 | ADR-0009 | WBS-16 | TEST-044 | (covered by module suite) | Full | full (post-MVP — PH2) |

## Forward — non-functional requirements

| Req | Decisions | Work items | Tests | Acceptance | Scope | Coverage |
|---|---|---|---|---|---|---|
| NFR-001 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-002 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-003 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-004 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-005 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-006 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-007 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-008 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-009 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-010 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-011 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-012 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-013 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-014 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-015 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-016 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-017 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-018 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-019 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-020 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-021 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-022 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-023 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-024 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-025 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-026 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-027 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-028 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-029 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-030 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-031 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-032 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-033 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-034 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-035 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-036 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-037 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-038 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-039 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-040 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-041 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-042 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-043 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-044 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-045 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-046 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-047 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-048 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-049 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-050 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-051 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-052 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-053 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-054 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-055 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-056 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-057 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-058 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-059 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-060 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-061 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-062 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |
| NFR-063 | ADR-0016, ADR-0012 | WBS-1 | TEST-041, TEST-045 | (NFR verified by test suite) | MVP | full |

## Backward — invariant coverage

Every non-negotiable invariant traces to its governing decision/ADR, the work that enforces it, and a test. See [invariant register](../requirements/invariant-register.md).

| Invariant | Decision/ADR | Work item | Test |
|---|---|---|---|
| INV-001 | ADR-0001, ADR-0002 | WBS-1 | TEST-041 (ArchUnit) |
| INV-002 | ADR-0001 | WBS-1 | TEST-041 (ArchUnit) |
| INV-003 | ADR-0013, ADR-0015 | WBS-1 | TEST-047 (compose health) |
| INV-004 | ADR-0004 | WBS-2 | TEST-005 (authz policy) |
| INV-005 | ADR-0009 | WBS-12 | TEST-017 (audit hash-chain) |
| INV-006 | ADR-0007 | WBS-16 | TEST-042 (AI candidate gate) |
| INV-007 | ADR-0013 | WBS-1 | TEST-048 (secret scan) |
| INV-008 | ADR-0016 | WBS-1 | TEST-041, TEST-045 (coverage) |
| INV-009 | ADR-0012 | WBS-1 | TEST-046 (i18n parity + RTL VR) |
| INV-010 | ADR-0006, ADR-0007 | WBS-14 | TEST-050 (render contract) |
| INV-011 | ADR-0001 | WBS-1 | TEST-041 (register lint) |
| INV-012 | ADR-0001 | WBS-1 | TEST-041 (ArchUnit) |
| INV-013 | ADR-0002 | WBS-1 | TEST-049 (CI gate) |
| INV-014 | ADR-0012 | WBS-1 | TEST-046 (visual regression) |

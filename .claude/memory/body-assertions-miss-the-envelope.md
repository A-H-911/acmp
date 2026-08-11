---
name: body-assertions-miss-the-envelope
description: "A test that asserts a request's BODY says nothing about whether the request is well-formed enough to be read — DEF-046 shipped a 415 through both test layers."
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 01449c66-99ef-4bad-b978-5afc8ccf49ef
  modified: 2026-08-11T19:59:12.201Z
---

`members.ts` sent its JSON body with **no `Content-Type: application/json`**. Minimal-API body
binding answers **415 Unsupported Media Type**, so the invite shipped in PR #235 could never have
succeeded — verified by posting through the real pipeline, not inferred from the pattern.

**Why nothing caught it, which is the reusable part:**

- Every backend test used `PostAsJsonAsync` / `PutAsJsonAsync` — **those helpers set the header
  themselves**, so the server-side suite structurally could not observe the omission.
- The panel test mocked the hook away, so the frontend suite never reached `fetch`.
- The api-layer test asserted the **body** (`lastBody(spy)`) and never the headers.

The defect lived exactly in the gap between two test layers, each assuming the other covered the
wire.

**Why:** an assertion on a request's *payload* is silent about its *envelope*. Method, URL, headers
and content type are all things a server rejects on before it ever looks at the body — and a
convenience helper in the test client papers over precisely the field production code forgot.

**How to apply:** when a mutation is asserted at the api layer, assert the **merged fetch headers**
too, not just the parsed body. When a backend test uses a `*AsJsonAsync` helper, remember it is
constructing part of the request the SPA must construct itself — that part is untested by
definition. Put the guard where the omission was (the client module), never in a backend test that
would pass regardless of what the client sends. Related: [[read-before-calling-it-a-defect]],
[[verify-mechanically-not-carefully]].

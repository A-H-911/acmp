# Acmp.Integration.Tests

Real SQL Server (and MinIO) through Testcontainers, so Docker must be running.

## The FTS image is pulled, and it is private

`SearchProvidersFtsTests` and `NfrPerfFixture` need SQL Server with Full-Text Search. `FtsImage` **pulls** it
at the digest pinned in [`deploy/fts-test-image.json`](../../deploy/fts-test-image.json) instead of building
`deploy/Dockerfile.sqlserver` (WBS-41.2, DEC-182, ADR-0047). The package,
`ghcr.io/a-h-911/acmp-sqlserver-fts-private`, is **private**, so a local run needs a registry login once:

```sh
gh auth refresh -h github.com -s read:packages          # or a classic PAT with read:packages
gh auth token | docker login ghcr.io -u <your GitHub user> --password-stdin
```

Your account must have read access to that package (its owner grants it in the package settings).

- **`ACMP_FTS_BUILD=1`** builds the image from the Dockerfile instead, the old path: no login needed, a few
  minutes and about 4 GB.
- **A changed `Dockerfile.sqlserver` fails red** until the pin is refreshed: dispatch
  `publish-fts-test-image.yml` on the branch and commit the pin it prints (DEC-182 f4). `node
  scripts/check-fts-pin.mjs` tells you whether the file still matches.

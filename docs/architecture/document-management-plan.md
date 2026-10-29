# Document management plan

Status: proposed. Nothing in this plan is implemented yet.

## Goal

Users can upload documents (mainly PDFs), search inside them by keyword and by
meaning, and open them in an in-app viewer that jumps to the matching page.
Everything is served by the existing .NET API, so any client (web, mobile,
third party) gets the same feature through the same auth and permissions.

## Summary of decisions

- Files live in the existing S3/MinIO bucket. Metadata, page text and vectors
  live in PostgreSQL.
- Keyword search uses PostgreSQL full-text search (`tsvector`). Meaning-based
  search uses the pgvector extension. Both results are merged in one SQL query.
- Text extraction and OCR run in a `docling-serve` container. The API calls it
  from a Hangfire job, never during the upload request.
- Embeddings (turning text into vectors) come from a local Ollama container by
  default, so document text never leaves our infrastructure.
- The browser views PDFs with PDF.js. The file is streamed through the API so
  every view passes the permission check.

## What exists today

| Area             | Finding                                                                                                                    | Relevant files                                                                                     |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Database         | PostgreSQL 18 with PostGIS (`postgis/postgis:18-3.6-alpine`). **pgvector is not in this image.**                          | `src/backend/MatricDasbhoard.AppHost/Program.cs`                                                   |
| File storage     | `IFileStorageService` over S3 (S3Mock in dev, MinIO otherwise). Upload and download take a whole `byte[]`, with no streaming or byte ranges. | `src/backend/MatricDasbhoard.Application/Features/FileStorage/IFileStorageService.cs`              |
| Background jobs  | Hangfire with PostgreSQL storage, plus a recurring job framework.                                                          | `src/backend/MatricDasbhoard.Infrastructure/Features/Jobs/`                                        |
| Permissions      | Atomic permissions discovered by reflection from `AppPermissions`.                                                         | `src/backend/MatricDasbhoard.Application/Identity/Constants/AppPermissions.cs`                     |
| Search           | Meilisearch already backs the school search box.                                                                           | `src/backend/MatricDasbhoard.Infrastructure/Features/Meilisearch/`                                 |

## Architecture

```
Client ──upload──▶ API ──store──▶ S3/MinIO
                    │
                    └─enqueue──▶ Hangfire: ProcessDocumentJob
                                   1. download file from S3
                                   2. POST to docling-serve  ──▶ page text (with OCR)
                                   3. save pages (tsvector)
                                   4. split into chunks
                                   5. POST chunks to Ollama ──▶ vectors
                                   6. save chunks (pgvector), mark Ready

Client ──search──▶ API ──▶ PostgreSQL (keyword + vector, merged) ──▶ hits with page + snippet
Client ──view────▶ API ──stream (Range)──▶ S3  ──▶ PDF.js opens page N, highlights terms
```

## Infrastructure changes

### 1. PostgreSQL with PostGIS and pgvector

Build a small custom image instead of switching images, so existing data
volumes stay compatible:

```dockerfile
# src/backend/MatricDasbhoard.AppHost/postgres/Dockerfile
FROM postgis/postgis:18-3.6-alpine
RUN apk add --no-cache --virtual .build git build-base \
 && git clone --branch v0.8.1 --depth 1 https://github.com/pgvector/pgvector.git /tmp/pgvector \
 && cd /tmp/pgvector && make with_llvm=no && make with_llvm=no install \
 && rm -rf /tmp/pgvector && apk del .build
```

- AppHost: replace `.WithImage("postgis/postgis")` with `.WithDockerfile("postgres")`.
- Pin the pgvector tag. Check the latest release before implementing.
- The EF migration runs `CREATE EXTENSION IF NOT EXISTS vector`.
- The production (Dokploy) database needs the same image. Update
  `docs/dokploy-deployment.md`.

### 2. docling-serve (text extraction and OCR)

- Container `quay.io/docling-project/docling-serve` (pin a version), internal
  only, no host port in production.
- Handles PDFs, scanned PDFs (OCR), DOCX, PPTX and images, and returns text per page.
- Behind an `IDocumentTextExtractor` interface so it can be swapped for Apache
  Tika + OCRmyPDF later.
- It needs about 2 to 4 GB of RAM. Set memory limits.

### 3. Ollama (embeddings)

- Container `ollama/ollama` with a data volume. Pull the model on first start.
- Model: `bge-m3` (1024 dimensions, multilingual, handles South African English
  and Afrikaans reasonably well), or `nomic-embed-text` (768 dimensions, smaller,
  English only).
- Behind an `IEmbeddingService` interface so a hosted provider can be added
  later if the data rules allow it.

### 4. Options classes

Following `.claude/rules/infrastructure.md`:

- `DocumentsOptions`: `MaxFileSizeBytes` (default 50 MB), `AllowedContentTypes`,
  `ChunkSizeTokens` (about 400), `ChunkOverlapTokens` (about 50).
- `DocumentExtractionOptions`: `BaseUrl`, `TimeoutSeconds`.
- `EmbeddingOptions`: `BaseUrl`, `Model`, `Dimensions`.

## Data model

All entities extend `BaseEntity`, use private setters and support soft delete.
Tables go in a new `documents` schema (a grouped feature, like `auth`).

### `Document`

| Property           | Type                  | Notes                                                    |
| ------------------ | --------------------- | -------------------------------------------------------- |
| `Title`            | string                | Defaults to the file name without extension              |
| `OriginalFileName` | string                | Cleaned up. Never used as a storage path                 |
| `ContentType`      | string                | Checked against the file's leading bytes, not the client header |
| `SizeBytes`        | long                  |                                                          |
| `Sha256`           | string                | Detects duplicates. Unique per non-deleted document      |
| `StorageKey`       | string                | `documents/{id}/original` (generated by the server)      |
| `PageCount`        | int?                  | Set after extraction                                     |
| `Status`           | `DocumentStatus` enum | `Pending`, `Processing`, `Ready`, `Failed` (`HasComment`) |
| `FailureCode`      | string?               | An `ErrorMessages` key, never a raw exception message    |
| `UploadedByUserId` | Guid                  |                                                          |
| `SchoolId`         | Guid?                 | Optional link to a school, so a school page can list its documents |
| `EmbeddingModel`   | string?               | Model used, so we can detect stale vectors after a model change |

Methods: `MarkProcessing()`, `MarkReady(pageCount, model)`, `MarkFailed(code)`, `Rename(title)`.

### `DocumentPage`

| Property       | Type     | Notes                                                                 |
| -------------- | -------- | --------------------------------------------------------------------- |
| `DocumentId`   | Guid     | FK, cascade                                                           |
| `PageNumber`   | int      | 1-based, unique with `DocumentId`                                     |
| `Text`         | string   | Extracted text                                                        |
| `SearchVector` | tsvector | Generated column: `to_tsvector('english', Text)`, GIN index           |

### `DocumentChunk`

| Property     | Type         | Notes                                     |
| ------------ | ------------ | ----------------------------------------- |
| `DocumentId` | Guid         | FK, cascade                               |
| `PageNumber` | int          | Page where the chunk starts               |
| `ChunkIndex` | int          |                                           |
| `Text`       | string       |                                           |
| `Embedding`  | vector(1024) | HNSW index, `vector_cosine_ops`           |

NuGet: `Pgvector.EntityFrameworkCore` in `Directory.Packages.props`.

Pages and chunks are child records owned by the document. Processing replaces
them in one transaction, so a re-run never leaves duplicates.

## Processing pipeline

`ProcessDocumentJob` (Hangfire, `[AutomaticRetry(Attempts = 3)]`):

1. Load the document. Skip if it is deleted or already `Ready` with the current
   embedding model.
2. `MarkProcessing()`.
3. Download from S3 and send to `IDocumentTextExtractor`. Receive text per page.
4. If no text came back, mark it `Failed` with `ErrorMessages.Documents.NoTextExtracted`.
5. Split pages into overlapping chunks, keeping the page number.
6. Embed chunks in batches (for example 32 per request) with `IEmbeddingService`.
7. In one transaction, delete old pages and chunks, insert new ones, then call `MarkReady()`.
8. On final failure, call `MarkFailed()` with a fixed error code. Log details with `ILogger` only.

A `ReindexDocumentsJob` (manual trigger) re-queues documents whose
`EmbeddingModel` differs from the configured one.

## Search

Hybrid search in one parameterized SQL query using reciprocal rank fusion
(each document chunk gets a score from its rank in both result lists):

```sql
WITH keyword AS (
  SELECT p."DocumentId", p."PageNumber",
         row_number() OVER (ORDER BY ts_rank_cd(p."SearchVector", q) DESC) AS rank
  FROM documents."DocumentPages" p, websearch_to_tsquery('english', @query) q
  WHERE p."SearchVector" @@ q
  LIMIT 50
),
semantic AS (
  SELECT c."DocumentId", c."PageNumber",
         row_number() OVER (ORDER BY c."Embedding" <=> @embedding) AS rank
  FROM documents."DocumentChunks" c
  ORDER BY c."Embedding" <=> @embedding
  LIMIT 50
)
SELECT "DocumentId", "PageNumber",
       SUM(1.0 / (60 + rank)) AS score
FROM (SELECT * FROM keyword UNION ALL SELECT * FROM semantic) hits
GROUP BY "DocumentId", "PageNumber"
ORDER BY score DESC
LIMIT @limit;
```

- Join to `Documents` to drop soft-deleted rows and apply visibility filters.
- Snippets come from `ts_headline` on the page text (keyword hits), or the best
  chunk text (semantic-only hits).
- Modes: `hybrid` (default), `keyword` (skips the embedding call and is fast),
  `semantic`.
- Cache query embeddings briefly with `HybridCache` (key in `CacheKeys`).

### Why not Meilisearch?

Meilisearch already runs here and supports hybrid search with embedders. It is a
valid alternative. We pick PostgreSQL because:

- Permission and visibility filters stay in SQL, next to the data, with no
  second copy to keep in sync.
- Deleting a document removes its search data in the same transaction.
- One less index to rebuild after restores.

Revisit this if there are more than about a million chunks, or if typo
tolerance becomes a priority.

## API

All routes are under `/api/v1/documents`, authenticated, and controllers extend
`ApiController`. Errors use `ProblemFactory`.

| Method   | Route                 | Permission           | Response                                         |
| -------- | --------------------- | -------------------- | ------------------------------------------------ |
| `POST`   | `/`                   | `documents.upload`   | `202` + `DocumentResponse` (status `Pending`), `400`, `409` duplicate, `413` |
| `GET`    | `/`                   | `documents.view`     | Paginated list: filters `status`, `schoolId`, `q` (title) |
| `GET`    | `/{id}`               | `documents.view`     | `DocumentResponse` including status              |
| `GET`    | `/{id}/file`          | `documents.view`     | Streams the file. Supports `Range`. `Content-Disposition: inline` |
| `GET`    | `/search`             | `documents.view`     | `q`, `mode`, `page`, `pageSize`. Returns hits with `documentId`, `title`, `pageNumber`, `snippet`, `score` |
| `PATCH`  | `/{id}`               | `documents.manage`   | Rename, set or clear `schoolId`                  |
| `DELETE` | `/{id}`               | `documents.manage`   | `204`. Soft delete. A cleanup job removes the S3 object later |
| `POST`   | `/{id}/reprocess`     | `documents.manage`   | `202`                                            |

- Rate limit policies for upload and search (`/add-rate-limit`).
- Response contracts frozen in `Api.Tests/Contracts/ResponseContracts.cs`.
- Clients poll `GET /{id}` for status at first. Push updates (SSE) can come later if needed.

### File storage changes (additive)

Add these to `IFileStorageService` without changing the existing methods:

- `UploadAsync(string key, Stream data, long length, string contentType, CancellationToken ct)`
- `OpenReadAsync(string key, long? rangeStart, long? rangeEnd, CancellationToken ct)`

Large PDFs then never sit fully in memory, and PDF.js can load only the pages it needs.

## Security

- **Deny by default:** three new permissions, none granted to the basic user
  role. Seed them to admin roles only.
- **Upload checks:** size limit enforced by the server (`RequestSizeLimit`
  plus a stream length check), and a file-type check against leading bytes
  (`%PDF-`, ZIP for DOCX). No HTML, SVG or other script-capable formats. The
  storage key is never built from user input.
- **Viewing:** the file is streamed through the API, never via public or
  long-lived S3 URLs. Set `X-Content-Type-Options: nosniff` and keep a strict
  CSP. PDF.js runs with `isEvalSupported: false` and scripting disabled.
- **Extraction isolation:** docling-serve and Ollama are reachable only from the
  API network, with no internet access in production, a read-only root
  filesystem where the images allow it, and all capabilities dropped.
- **Personal data:** documents may contain learner data. Local embeddings keep
  text inside our infrastructure. Do not log extracted text.
- **Audit:** record upload, delete and reprocess events through the existing
  audit feature.
- **Optional:** a ClamAV scan step before extraction, if uploads come from
  outside trusted staff.

## Frontend

- `pdfjs-dist` with a small Svelte wrapper component (`$lib/components/documents/PdfViewer.svelte`):
  virtualized pages, zoom, page jump, and term highlighting through the text layer.
- Routes:
  - `/documents`: list, status badges, upload (native `fetch` + `FormData`, as
    the frontend rules require), search box.
  - `/documents/[id]?page=N&q=terms`: viewer opens on the hit page and highlights the terms.
  - `/documents/search?q=`: results grouped by document, each hit linking to its page.
- Optional: a "Documents" section on school pages when `schoolId` is linked.
- i18n keys `documents_*` in a new `documents.json` for `en` and `cs`.
- Regenerate API types with `/gen-types`.

## Delivery phases

Each phase can be released on its own.

1. **Infrastructure:** custom Postgres image with pgvector, docling-serve and
   Ollama in AppHost, options classes, Dokploy notes. Check: `CREATE EXTENSION vector`
   succeeds, and all containers pass health checks.
2. **Store and view:** `Document` entity, migration, upload, list, get, stream
   with `Range`, delete, permissions. Frontend list, upload and PDF viewer. No search yet.
3. **Extraction and keyword search:** `ProcessDocumentJob` steps 1 to 4 and 7,
   `DocumentPage`, `/search?mode=keyword`, snippets, search UI and highlighting.
4. **Meaning-based search:** `DocumentChunk`, embeddings, hybrid query,
   `mode=hybrid` as the default, reindex job.
5. **Polish:** school linking, audit events, S3 cleanup job, optional ClamAV,
   and status updates in the UI.

## Testing

- **Unit:** chunker (overlap, page boundaries, empty pages), file-type detection,
  rank fusion scoring.
- **Component:** upload service (duplicates, size and type rejection), job
  state changes with mocked extractor and embedder, idempotent reprocessing.
- **API:** permissions (401 and 403 for each route), `Range` responses (`206`,
  `416`), ProblemDetails shapes, frozen response contracts.
- **Search integration:** the InMemory provider cannot run `tsvector` or
  pgvector. Use a Testcontainers PostgreSQL with the custom image for search
  query tests.
- **Architecture:** new Infrastructure services are `internal`.
- **Frontend:** viewer page and search results components (vitest + jsdom).

## Open questions

1. Who uploads and who views? Is `documents.view` for all signed-in staff, or
   only some roles? Do some documents need per-document visibility?
2. What kinds of documents, and roughly how many? Mostly digital PDFs, or a lot
   of scanned paper? (OCR load decides the server size for docling-serve.)
3. Must embeddings stay on our servers, or is a hosted embedding API acceptable?
4. Should documents link to schools, subjects, or something else?
5. Does the production server have enough RAM for docling-serve and Ollama
   (about 6 GB extra together), or do they need a separate worker host?

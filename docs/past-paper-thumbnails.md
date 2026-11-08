# Past paper thumbnails

The uploader renders page one of each PDF with Poppler, bounds the image to
480 pixels, and encodes it as WebP. Images are stored at
`thumbnails/<original paper object key>.webp`, outside the catalog prefix.

The public endpoint `GET /api/v1/past-papers/{id}/thumbnail` resolves the paper
through the catalog and streams its image with a one-day cache lifetime.
Missing thumbnails return 404; cards display "Preview unavailable".

## Upload and backfill

Install Poppler (`pdftoppm`), then install Python dependencies:

```sh
python3 -m venv .venv-thumbnails
.venv-thumbnails/bin/pip install boto3 Pillow
```

Use the same `FILE_STORAGE_ENDPOINT`, `FILE_STORAGE_ACCESS_KEY`,
`FILE_STORAGE_SECRET_KEY`, and `FILE_STORAGE_BUCKET` environment variables as
the existing uploader. Do not commit credentials.

Normal uploads now also generate thumbnails. To backfill images for PDFs that
are already in storage and available under `data/nsc/exam_papers`:

```sh
.venv-thumbnails/bin/python scripts/upload_nsc_papers_to_s3.py --thumbnails-only
```

Existing images are skipped. Add `--force` to regenerate them; in
`--thumbnails-only` mode the original PDFs are never re-uploaded. Storage-only
papers must first be downloaded to the matching local folder structure.
Corrupt or encrypted PDFs are reported as failures and can be retried.

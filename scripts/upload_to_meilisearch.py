"""
Upload NSC school performance CSV data to MeiliSearch.

Usage:
    python3 scripts/upload_to_meilisearch.py [--csv <path>]

Environment variables:
    MEILI_URL       MeiliSearch host URL (default: https://search.matrictrend.app)
    MEILI_MASTER_KEY   MeiliSearch API key (required)

The CSV is transformed into search-friendly documents with one document per school.
Each document uses the EMIS number as its unique ID.
"""

import csv
import json
import os
import sys
import argparse

import requests


MEILI_URL = os.environ.get("MEILI_URL", "https://search.matrictrend.app")
INDEX_NAME = "schools"
BATCH_SIZE = 500


def load_csv(path: str) -> list[dict]:
    """Load CSV and transform into MeiliSearch documents."""
    documents = []

    with open(path, newline="", encoding="utf-8") as f:
        reader = csv.DictReader(f)
        for row in reader:
            emis = row.get("emis_number", "").strip()
            if not emis:
                continue

            # Build a clean document with the EMIS as the primary key
            doc = {
                "id": emis,
                "province": row.get("province", "").strip(),
                "district_name": row.get("district_name", "").strip(),
                "emis_number": emis,
                "centre_number": row.get("centre_number", "").strip(),
                "centre_name": row.get("centre_name", "").strip(),
                "quintile": _int_or_none(row.get("quintile")),
                # Yearly performance data
                "progressed_number_2022": _int_or_none(row.get("progressed_number_2022")),
                "total_wrote_2022": _int_or_none(row.get("total_wrote_2022")),
                "total_achieved_2022": _int_or_none(row.get("total_achieved_2022")),
                "percent_achieved_2022": _float_or_none(row.get("percent_achieved_2022")),
                "progressed_number_2023": _int_or_none(row.get("progressed_number_2023")),
                "total_wrote_2023": _int_or_none(row.get("total_wrote_2023")),
                "total_achieved_2023": _int_or_none(row.get("total_achieved_2023")),
                "percent_achieved_2023": _float_or_none(row.get("percent_achieved_2023")),
                "progressed_number_2024": _int_or_none(row.get("progressed_number_2024")),
                "total_wrote_2024": _int_or_none(row.get("total_wrote_2024")),
                "total_achieved_2024": _int_or_none(row.get("total_achieved_2024")),
                "percent_achieved_2024": _float_or_none(row.get("percent_achieved_2024")),
            }
            documents.append(doc)

    return documents


def _int_or_none(val: str | None) -> int | None:
    if val is None:
        return None
    val = val.strip()
    if val == "":
        return None
    try:
        return int(val)
    except ValueError:
        return None


def _float_or_none(val: str | None) -> float | None:
    if val is None:
        return None
    val = val.strip()
    if val == "":
        return None
    try:
        return float(val)
    except ValueError:
        return None


def upload_documents(documents: list[dict], api_key: str) -> None:
    """Upload documents to MeiliSearch in batches."""
    url = f"{MEILI_URL}/indexes/{INDEX_NAME}/documents"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {api_key}",
    }

    total = len(documents)
    uploaded = 0

    task_uids = []

    for start in range(0, total, BATCH_SIZE):
        batch = documents[start : start + BATCH_SIZE]
        payload = json.dumps(batch)

        resp = requests.post(url, headers=headers, data=payload, timeout=120)
        resp.raise_for_status()
        result = resp.json()

        uploaded += len(batch)
        task_uid = result.get("taskUid")
        if task_uid is not None:
            task_uids.append(task_uid)
        print(f"  [{uploaded}/{total}] Batch uploaded — taskUid: {task_uid}")

    # Wait for all tasks to complete
    for task_uid in task_uids:
        status_url = f"{MEILI_URL}/tasks/{task_uid}"
        while True:
            status_resp = requests.get(status_url, headers=headers, timeout=30)
            status_resp.raise_for_status()
            task = status_resp.json()
            if task.get("status") == "succeeded":
                print(f"  Task {task_uid} completed successfully")
                break
            if task.get("status") == "failed":
                error = task.get("error", {}).get("message", "unknown error")
                raise RuntimeError(f"Task {task_uid} failed: {error}")
            import time
            time.sleep(0.5)


def configure_index(api_key: str) -> None:
    """Configure the MeiliSearch index settings for optimal search."""
    settings_url = f"{MEILI_URL}/indexes/{INDEX_NAME}/settings"
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {api_key}",
    }

    settings = {
        "searchableAttributes": [
            "centre_name",
            "province",
            "district_name",
            "emis_number",
            "centre_number",
        ],
        "filterableAttributes": [
            "province",
            "district_name",
            "quintile",
            "percent_achieved_2024",
            "percent_achieved_2023",
            "percent_achieved_2022",
        ],
        "sortableAttributes": [
            "centre_name",
            "percent_achieved_2024",
            "percent_achieved_2023",
            "percent_achieved_2022",
            "total_wrote_2024",
        ],
        "rankingRules": [
            "words",
            "typo",
            "proximity",
            "attribute",
            "sort",
            "exactness",
        ],
    }

    resp = requests.patch(settings_url, headers=headers, json=settings, timeout=30)
    resp.raise_for_status()
    result = resp.json()
    print(f"  Index settings configured — taskUid: {result.get('taskUid', '?')}")


def main():
    parser = argparse.ArgumentParser(description="Upload NSC school data to MeiliSearch")
    parser.add_argument(
        "--csv",
        default="2024_nsc_school_performance_report.csv",
        help="Path to the CSV file (default: 2024_nsc_school_performance_report.csv)",
    )
    args = parser.parse_args()

    api_key = os.environ.get("MEILI_MASTER_KEY")
    if not api_key:
        print("ERROR: MEILI_MASTER_KEY environment variable is required")
        sys.exit(1)

    csv_path = args.csv
    if not os.path.exists(csv_path):
        print(f"ERROR: CSV file not found: {csv_path}")
        sys.exit(1)

    print(f"Loading CSV: {csv_path}")
    documents = load_csv(csv_path)
    print(f"Loaded {len(documents)} school records")

    print(f"Configuring index '{INDEX_NAME}' on {MEILI_URL}...")
    try:
        configure_index(api_key)
    except requests.RequestException as e:
        print(f"ERROR: Could not configure index settings: {e}")
        if hasattr(e, "response") and e.response is not None:
            print(f"  Status: {e.response.status_code}")
            print(f"  Body: {e.response.text[:500]}")
        sys.exit(1)

    print(f"Uploading to MeiliSearch index '{INDEX_NAME}'...")
    try:
        upload_documents(documents, api_key)
        print("Done! All documents uploaded successfully.")
    except requests.RequestException as e:
        print(f"ERROR: Upload failed: {e}")
        if hasattr(e, "response") and e.response is not None:
            print(f"  Status: {e.response.status_code}")
            print(f"  Body: {e.response.text[:500]}")
        sys.exit(1)


if __name__ == "__main__":
    main()

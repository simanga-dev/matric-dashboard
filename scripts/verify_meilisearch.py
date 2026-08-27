"""Verify MeiliSearch data was uploaded correctly."""
import json
import os
import sys
import urllib.request
from urllib.error import HTTPError, URLError

MEILI_URL = os.environ.get("MEILI_URL", "https://search.matrictrend.app")
INDEX_NAME = "schools"
MEILI_MASTER_KEY = os.environ["MEILI_MASTER_KEY"]


def _search(url: str, data: bytes | None = None) -> dict:
    """Make a MeiliSearch request and return the JSON response."""
    headers = {"Authorization": f"Bearer {MEILI_MASTER_KEY}"}
    if data:
        headers["Content-Type"] = "application/json"
        req = urllib.request.Request(url, data=data, headers=headers, method="POST")
    else:
        req = urllib.request.Request(url, headers=headers)

    try:
        resp = urllib.request.urlopen(req, timeout=15)
        return json.loads(resp.read())
    except HTTPError as e:
        print(f"HTTP error {e.code}: {e.reason}", file=sys.stderr)
        if e.code == 401:
            print("  (Check your MEILI_MASTER_KEY)", file=sys.stderr)
    except URLError as e:
        print(f"Connection failed: {e.reason}", file=sys.stderr)
    sys.exit(1)


# Quick check: search a known school
url = f"{MEILI_URL}/indexes/{INDEX_NAME}/search"
payload = json.dumps({"q": "baleni", "limit": 2}).encode()
data = _search(url, data=payload)

print(f"Total hits: {data.get('estimatedTotalHits', '?')}")
print(f"Query time: {data.get('processingTimeMs', '?')}ms")
print()
for hit in data.get("hits", []):
    print(f"  ID: {hit.get('id')}")
    print(f"  Centre: {hit.get('centre_name')}")
    print(f"  Province: {hit.get('province')}")
    print(f"  District: {hit.get('district_name')}")
    print(f"  Quintile: {hit.get('quintile')}")
    print(f"  2024 Pass %: {hit.get('percent_achieved_2024')}")

# Stats
stats_url = f"{MEILI_URL}/indexes/{INDEX_NAME}/stats"
stats = _search(stats_url)

print("\nIndex stats:")
print(f"  Number of documents: {stats.get('numberOfDocuments', '?')}")
print(f"  Database size: {stats.get('databaseSize', '?')} bytes")

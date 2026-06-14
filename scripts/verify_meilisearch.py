"""Verify MeiliSearch data was uploaded correctly."""
import csv, json, os, sys

MEILI_KEY = os.environ["MEILI_KEY"]

# Quick check: search a known school
import urllib.request

url = "https://search.matrictrend.app/indexes/schools/search"
payload = json.dumps({"q": "baleni", "limit": 2}).encode()
req = urllib.request.Request(
    url,
    data=payload,
    headers={
        "Content-Type": "application/json",
        "Authorization": f"Bearer {MEILI_KEY}",
    },
    method="POST",
)
resp = urllib.request.urlopen(req, timeout=15)
data = json.loads(resp.read())

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
stats_url = "https://search.matrictrend.app/indexes/schools/stats"
stats_req = urllib.request.Request(
    stats_url,
    headers={"Authorization": f"Bearer {MEILI_KEY}"},
)
stats_resp = urllib.request.urlopen(stats_req, timeout=15)
stats = json.loads(stats_resp.read())
print(f"\nIndex stats:")
print(f"  Number of documents: {stats.get('numberOfDocuments', '?')}")
print(f"  Database size: {stats.get('databaseSize', '?')} bytes")

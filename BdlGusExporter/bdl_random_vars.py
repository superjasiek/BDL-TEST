import requests
import random
import csv
import xml.etree.ElementTree as ET
import time

API_BASE   = "https://bdl.stat.gov.pl/api/v1/variables"
API_KEY    = "7eb6ceb7-a994-4bf0-f27f-08ddcd5a2c67"
HEADERS    = {"X-ClientId": API_KEY}
PAGE_SIZE  = 10       # 10 rekordów na stronę
SUBJECT_ID = "P3183"  # temat do pobrania

DELAY_SEC  = 0.5      # pół sekundy przerwy między kolejnymi requestami

def fetch_subject_variables(subject_id):
    all_vars = []
    page = 1
    while True:
        params = {
            "subject-id": subject_id,
            "format":     "xml",
            "page-size":  PAGE_SIZE,
            "page":       page
        }
        resp = requests.get(API_BASE, params=params, headers=HEADERS)
        resp.raise_for_status()
        root = ET.fromstring(resp.content)
        items = root.findall(".//item")
        if not items:
            break

        print(f"Pobrano stronę {page}: {len(items)} zmiennych")
        for it in items:
            vid  = it.findtext("id")
            name = it.findtext("name")
            all_vars.append((vid, name))

        page += 1
        time.sleep(DELAY_SEC)   # <-- tutaj opóźnienie

    return all_vars

def main():
    print(f"Pobieram wszystkie zmienne dla subject-id={SUBJECT_ID}…")
    vars_list = fetch_subject_variables(SUBJECT_ID)
    print(f"→ Łącznie pobrano {len(vars_list)} zmiennych")

    if len(vars_list) < 40:
        raise RuntimeError(f"W puli jest tylko {len(vars_list)} zmiennych – nie można wylosować 40.")

    sample = random.sample(vars_list, 40)
    with open("random_zmienne.csv", "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(["variable_id", "nazwa_zmiennej"])
        for vid, name in sample:
            writer.writerow([vid, name])

    print("✔ Gotowe – zapisałem 40 losowych zmiennych do random_zmienne.csv")

if __name__ == "__main__":
    main()

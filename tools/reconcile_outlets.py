#!/usr/bin/env python3
"""Read-only reconciliation of a distributor outlet CSV against our outlet export."""

from __future__ import annotations

import argparse
import csv
import math
import re
import sys
import unicodedata
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

NATIONAL_FIELDS = {
    "OUTLET_CODE", "TRADING_NAME", "PROVINCE", "GPS_LAT", "GPS_LON",
    "CONTACT_PERSON", "CHANNEL", "LAST_UPDATED",
}
CURRENT_FIELDS = {
    "stp_number", "name", "province", "latitude", "longitude",
    "owner_name", "ap_status", "is_active",
}
OUTPUT_FIELDS = [
    "source_row", "outlet_code", "normalised_stp", "trading_name",
    "province", "classification", "matched_stp", "matched_name",
    "reason", "warnings",
]
PROVINCES = {
    "gp": "gauteng", "gauteng": "gauteng",
    "kzn": "kwazulu natal", "kwazulu natal": "kwazulu natal",
    "kwazulu-natal": "kwazulu natal", "kwa zulu natal": "kwazulu natal",
    "wc": "western cape", "w cape": "western cape", "western cape": "western cape",
    "ec": "eastern cape", "e cape": "eastern cape", "eastern cape": "eastern cape",
    "lp": "limpopo", "limpopo": "limpopo",
    "mp": "mpumalanga", "mpumalanga": "mpumalanga",
    "nw": "north west", "north-west": "north west", "north west": "north west",
    "fs": "free state", "freestate": "free state", "free state": "free state",
    "nc": "northern cape", "northern cape": "northern cape",
}

def clean(value: object) -> str:
    return str(value or "").strip()

def normalise_text(value: object) -> str:
    value = unicodedata.normalize("NFKD", clean(value))
    value = "".join(ch for ch in value if not unicodedata.combining(ch))
    value = value.casefold()
    return re.sub(r"[^a-z0-9]+", " ", value).strip()

def normalise_province(value: object) -> str:
    key = re.sub(r"\s+", " ", clean(value).casefold()).strip()
    return PROVINCES.get(key, key)

def normalise_code(value: object) -> str | None:
    raw = clean(value)
    if not raw or not raw.isdigit() or len(raw) > 6:
        return None
    return raw.zfill(6)

def parse_coordinate(value: object) -> float | None:
    raw = clean(value)
    if not raw:
        return None
    try:
        number = float(raw)
    except ValueError:
        return None
    return number if math.isfinite(number) else None

def coordinates(lat_value: object, lon_value: object):
    lat, lon = parse_coordinate(lat_value), parse_coordinate(lon_value)
    if lat is None or lon is None or (lat == 0 and lon == 0):
        return None, "GPS missing or set to 0,0"
    # South African bounds are deliberately broad; they catch obvious swaps
    # and impossible points without pretending to validate a precise address.
    if not (-35.5 <= lat <= -22.0 and 16.0 <= lon <= 33.5):
        return None, "GPS outside broad South African bounds or latitude/longitude may be swapped"
    return (lat, lon), ""

def distance_km(a, b) -> float:
    lat1, lon1 = map(math.radians, a)
    lat2, lon2 = map(math.radians, b)
    dlat, dlon = lat2 - lat1, lon2 - lon1
    h = math.sin(dlat / 2) ** 2 + math.cos(lat1) * math.cos(lat2) * math.sin(dlon / 2) ** 2
    return 6371.0 * 2 * math.asin(math.sqrt(h))

def valid_date(value: object) -> bool:
    raw = clean(value)
    for fmt in ("%Y-%m-%d", "%d/%m/%Y"):
        try:
            datetime.strptime(raw, fmt)
            return True
        except ValueError:
            pass
    return False

def read_csv(path: Path, required: set[str]) -> list[dict[str, str]]:
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        headers = set(reader.fieldnames or [])
        missing = required - headers
        if missing:
            raise ValueError(f"{path}: missing required columns: {', '.join(sorted(missing))}")
        return [dict(row) for row in reader]

def reconcile(national_rows: list[dict[str, str]], current_rows: list[dict[str, str]]):
    current_by_code = {}
    current_by_name_province = defaultdict(list)
    for row in current_rows:
        code = normalise_code(row.get("stp_number"))
        if code:
            current_by_code[code] = row
        key = (normalise_text(row.get("name")), normalise_province(row.get("province")))
        if all(key):
            current_by_name_province[key].append(row)

    source_codes = [normalise_code(r.get("OUTLET_CODE")) for r in national_rows]
    counts = Counter(code for code in source_codes if code)
    output = []

    for index, row in enumerate(national_rows, start=2):  # header is row 1
        raw_code = clean(row.get("OUTLET_CODE"))
        code = normalise_code(raw_code)
        name = clean(row.get("TRADING_NAME"))
        province = clean(row.get("PROVINCE"))
        warnings = []
        classification, matched, reason = "", None, ""

        if not raw_code:
            classification, reason = "REJECT", "Missing distributor outlet code; cannot safely identify or import this record."
        elif code is None:
            classification, reason = "REJECT", "Outlet code must contain 1–6 digits; value is not usable."
        elif counts[code] > 1:
            classification, reason = "REJECT", f"Duplicate distributor outlet code {code}; all rows with this code are rejected pending source correction."
        elif not name:
            classification, reason = "REJECT", "Missing trading name."
        elif not province:
            classification, reason = "REJECT", "Missing province."
        elif not clean(row.get("CHANNEL")):
            classification, reason = "REJECT", "Missing channel."
        elif not valid_date(row.get("LAST_UPDATED")):
            classification, reason = "REJECT", "Missing or unrecognised LAST_UPDATED date; expected YYYY-MM-DD or DD/MM/YYYY."
        else:
            source_gps, gps_warning = coordinates(row.get("GPS_LAT"), row.get("GPS_LON"))
            if gps_warning:
                warnings.append(gps_warning)

            existing = current_by_code.get(code)
            if existing:
                matched = existing
                differences = []
                if normalise_text(name) != normalise_text(existing.get("name")):
                    differences.append("trading name differs")
                if normalise_province(province) != normalise_province(existing.get("province")):
                    differences.append("province differs")
                existing_gps, _ = coordinates(existing.get("latitude"), existing.get("longitude"))
                if source_gps and existing_gps:
                    km = distance_km(source_gps, existing_gps)
                    if km > 1.0:
                        differences.append(f"GPS is {km:.1f} km from current outlet (over 1 km tolerance)")
                if gps_warning and "outside broad" in gps_warning:
                    differences.append("distributor GPS is invalid or appears swapped")
                if differences:
                    classification = "CONFLICT"
                    reason = "Same outlet code exists, but " + "; ".join(differences) + ". Review before changing master data."
                else:
                    classification = "MATCH"
                    reason = "Outlet code matches; trading name and province agree after normalisation."
                    if gps_warning:
                        reason += " GPS could not be compared."
            else:
                key = (normalise_text(name), normalise_province(province))
                candidates = current_by_name_province.get(key, [])
                # A name+province candidate is a review lead, never an automatic match.
                if candidates:
                    nearest = None
                    nearest_km = None
                    if source_gps:
                        for candidate in candidates:
                            candidate_gps, _ = coordinates(candidate.get("latitude"), candidate.get("longitude"))
                            if candidate_gps:
                                km = distance_km(source_gps, candidate_gps)
                                if nearest_km is None or km < nearest_km:
                                    nearest, nearest_km = candidate, km
                    matched = nearest or candidates[0]
                    suffix = f"; closest same-name/province outlet is {nearest_km:.1f} km away" if nearest_km is not None else ""
                    classification = "CONFLICT"
                    reason = f"Outlet code {code} is not in our master, but an existing outlet has the same normalised name and province{suffix}; possible code change or duplicate. Review manually."
                else:
                    classification = "NEW"
                    reason = "Valid unique distributor code not present in our master; no same-name/province candidate found."
                    if gps_warning:
                        reason += " GPS needs correction or confirmation."

        output.append({
            "source_row": str(index),
            "outlet_code": raw_code,
            "normalised_stp": code or "",
            "trading_name": name,
            "province": province,
            "classification": classification,
            "matched_stp": clean(matched.get("stp_number")) if matched else "",
            "matched_name": clean(matched.get("name")) if matched else "",
            "reason": reason,
            "warnings": "; ".join(warnings),
        })
    return output

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--national", type=Path, required=True, help="Distributor CSV")
    parser.add_argument("--current", type=Path, required=True, help="Current outlets CSV export")
    parser.add_argument("--output", type=Path, default=Path("outlet-reconciliation.csv"), help="Output report CSV")
    args = parser.parse_args()

    try:
        national = read_csv(args.national, NATIONAL_FIELDS)
        current = read_csv(args.current, CURRENT_FIELDS)
        results = reconcile(national, current)
        with args.output.open("w", encoding="utf-8-sig", newline="") as handle:
            writer = csv.DictWriter(handle, fieldnames=OUTPUT_FIELDS)
            writer.writeheader()
            writer.writerows(results)
    except (OSError, ValueError, csv.Error) as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 2

    counts = Counter(row["classification"] for row in results)
    print(f"Processed {len(results)} distributor records against {len(current)} current outlets.")
    for label in ("MATCH", "NEW", "CONFLICT", "REJECT"):
        print(f"{label:8} {counts[label]:4}")
    print(f"Report: {args.output.resolve()}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())

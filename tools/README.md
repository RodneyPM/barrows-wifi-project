# Outlet master reconciliation (KES-260)

A read-only Python 3 script that compares the distributor's monthly outlet CSV with an export of our current outlets. It writes a CSV report; it does **not** connect to SQL Server or modify either input.

## Run

From this folder:

```bash
python reconcile_outlets.py \
  --national ../data/national-outlet-master.csv \
  --current ../data/outlets-current.csv \
  --output outlet-reconciliation.csv
```

Python 3.10+ is recommended. There are no third-party dependencies.

## Classifications

Every distributor row receives exactly one classification and a human-readable reason:

- **MATCH** — unique distributor code matches an existing STP number; normalised trading name and province agree, and valid GPS points are within 1 km.
- **NEW** — valid, unique distributor code is not present in our master and there is no exact normalised name+province candidate.
- **CONFLICT** — code exists but name/province/GPS disagree; or code is new but the same normalised name and province already exist. These require human review.
- **REJECT** — missing/invalid code, duplicate distributor code, missing trading name/province/channel, or missing/unrecognised update date.

The report includes the source CSV row number, original and normalised code, classification, matched current outlet (when one is a review candidate), reason and warnings.

## Matching and data-quality rules

1. **Outlet code is the primary key.** Codes are treated as text (preserving leading zeroes) and left-padded to six digits when shorter. Only 1–6 digit values are accepted. This handles CSV numeric formatting such as `43812` versus `043812`, but assumes all business outlet codes are six digits.
2. **Duplicate distributor codes are rejected as a group.** No arbitrary "first row wins" choice is made. The source owner must resolve duplicates and resend the list.
3. **Names are compared conservatively.** Unicode accents are folded, case is ignored, punctuation is replaced with spaces and repeated whitespace is collapsed. Words are not removed or fuzzy-matched.
4. **Province aliases are mapped** (for example GP/Gauteng, KZN/KwaZulu-Natal, WC/Western Cape). Unknown values are compared after trimming and case-folding; they are not silently mapped to a guessed province.
5. **GPS is compared only when both points are plausible South African coordinates.** A difference over 1 km is a conflict. Blank or `0,0` GPS is treated as unavailable and reported as a warning, not proof that the outlet is wrong. Coordinates outside broad South African bounds (including likely swapped latitude/longitude) are a conflict for an existing code and a warning for a new record.
6. **A new code with an exact normalised name+province candidate is a conflict, not a match.** The script lists one candidate for investigation. If GPS is available, it chooses the nearest candidate for the report; otherwise it lists the first candidate. Names can be reused, so no automatic merge occurs.
7. **Owner/contact person, channel, AP status and active status are not match keys.** They are either not equivalent fields across the two files or may legitimately change. Channel must be present in the distributor row, but it is not compared with AP status.
8. **Dates accept only YYYY-MM-DD and DD/MM/YYYY.** The date is a required source-quality field; it is not used to determine identity.

## Trade-offs / limitations

- Code-first matching is explainable and avoids merging two different outlets that happen to share a trading name. It will not discover a genuine code change automatically.
- The 1 km GPS threshold is a review threshold, not a claim that GPS is survey-grade. Dense sites or inaccurate GPS may need manual review.
- A same-name+province candidate can be a false positive, especially for common trading names. It is deliberately labelled CONFLICT for a human to decide.
- The script does not infer swapped coordinates, repair records, choose between duplicate rows, or write changes back to the database.
- `NEW` means "not found by these rules", not approval to create an outlet. Review the report and source data before any downstream import.

## Tests

Run:

```bash
python -m unittest -v
```

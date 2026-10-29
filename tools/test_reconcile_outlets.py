import unittest
from reconcile_outlets import reconcile

CURRENT = [{
    "stp_number": "043812", "name": "Mkhize Liquor Store", "province": "Mpumalanga",
    "latitude": "-25.576693", "longitude": "30.97059", "owner_name": "Owner",
    "ap_status": "Online", "is_active": "true",
}]

def row(code="043812", name="Mkhize Liquor Store", province="MP", lat="-25.576693",
        lon="30.97059", channel="TAVERN", date="2026-10-01"):
    return {
        "OUTLET_CODE": code, "TRADING_NAME": name, "PROVINCE": province,
        "GPS_LAT": lat, "GPS_LON": lon, "CONTACT_PERSON": "Contact",
        "CHANNEL": channel, "LAST_UPDATED": date,
    }

class ReconciliationTests(unittest.TestCase):
    def classify(self, rows):
        return reconcile(rows, CURRENT)

    def test_leading_zero_code_and_alias_match(self):
        result = self.classify([row(code="43812")])[0]
        self.assertEqual(result["normalised_stp"], "043812")
        self.assertEqual(result["classification"], "MATCH")

    def test_name_difference_is_conflict(self):
        self.assertEqual(self.classify([row(name="Different Shop")])[0]["classification"], "CONFLICT")

    def test_new_unique_code_is_new(self):
        self.assertEqual(self.classify([row(code="999999", name="Brand New Shop")])[0]["classification"], "NEW")

    def test_same_name_and_province_with_new_code_is_conflict(self):
        result = self.classify([row(code="999999")])[0]
        self.assertEqual(result["classification"], "CONFLICT")
        self.assertEqual(result["matched_stp"], "043812")

    def test_duplicate_source_codes_are_both_rejected(self):
        results = self.classify([row(), row(date="2026-10-02")])
        self.assertEqual([r["classification"] for r in results], ["REJECT", "REJECT"])

    def test_missing_code_is_rejected(self):
        self.assertEqual(self.classify([row(code="")])[0]["classification"], "REJECT")

    def test_zero_gps_does_not_block_code_match(self):
        result = self.classify([row(lat="0", lon="0")])[0]
        self.assertEqual(result["classification"], "MATCH")
        self.assertIn("GPS missing", result["warnings"])

    def test_swapped_gps_is_conflict(self):
        result = self.classify([row(lat="30.97059", lon="-25.576693")])[0]
        self.assertEqual(result["classification"], "CONFLICT")

    def test_bad_date_is_rejected(self):
        self.assertEqual(self.classify([row(date="not-a-date")])[0]["classification"], "REJECT")

if __name__ == "__main__":
    unittest.main()

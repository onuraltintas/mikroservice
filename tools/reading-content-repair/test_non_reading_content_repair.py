import json
import unittest
from pathlib import Path


PACK = Path(__file__).parents[2] / "content-packs" / "non-reading-content-repair" / "v1"


class NonReadingContentRepairTests(unittest.TestCase):
    def test_question_repair_pack_has_guarded_editorial_updates(self):
        payload = json.loads((PACK / "question-bank-editorial-repair.json").read_text(encoding="utf-8"))
        updates = payload["updates"]

        self.assertEqual(len(updates), 48)
        self.assertEqual(len({item["id"] for item in updates}), 45)
        self.assertTrue(all(item["field"] in {"optionA", "optionB", "optionC", "optionD", "optionE"} for item in updates))
        self.assertTrue(all(item["original"].strip() and item["revised"].strip() for item in updates))
        self.assertTrue(all(item["original"] != item["revised"] for item in updates))

    def test_vocabulary_repair_pack_expands_definitions_and_soft_deletes_only_duplicates(self):
        payload = json.loads((PACK / "vocabulary-editorial-repair.json").read_text(encoding="utf-8"))
        definitions = payload["definitionUpdates"]
        resolutions = payload["duplicateResolutions"]
        archived_ids = [item for resolution in resolutions for item in resolution["archivedIds"]]

        self.assertEqual(len(definitions), 36)
        self.assertTrue(all(len(item["revised"].split()) >= 3 for item in definitions))
        self.assertEqual(len(resolutions), 114)
        self.assertEqual(len(archived_ids), 137)
        self.assertEqual(len(archived_ids), len(set(archived_ids)))
        self.assertTrue(all(resolution["canonicalId"] not in resolution["archivedIds"] for resolution in resolutions))

    def test_sql_is_transactional_idempotent_and_audited(self):
        apply_sql = (PACK / "apply.sql").read_text(encoding="utf-8")
        rollback_sql = (PACK / "rollback.sql").read_text(encoding="utf-8")

        self.assertTrue(apply_sql.startswith("BEGIN;"))
        self.assertTrue(apply_sql.rstrip().endswith("COMMIT;"))
        self.assertIn("content-quality-repair-v1", apply_sql)
        self.assertIn("admin_audit_records", apply_sql)
        self.assertIn("content-quality-repair-v1", rollback_sql)
        self.assertIn("deleted_by = 'content-quality-repair-v1'", rollback_sql)


if __name__ == "__main__":
    unittest.main()

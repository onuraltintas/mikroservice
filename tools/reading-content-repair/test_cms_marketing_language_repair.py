import json
import unittest
from pathlib import Path


PACK = Path(__file__).parents[2] / "content-packs" / "cms-marketing-language-repair" / "v1"
ABOUT_COMPONENT = Path(__file__).parents[2] / "clients" / "speed-reading" / "src" / "app" / "features" / "public" / "about" / "about.component.ts"


class CmsMarketingLanguageRepairTests(unittest.TestCase):
    def test_pack_replaces_outcome_promise_with_programme_flow(self):
        payload = json.loads((PACK / "updates.json").read_text(encoding="utf-8"))
        updates = payload["updates"]

        self.assertEqual(payload["scope"], "AboutPage")
        self.assertEqual([item["key"] for item in updates], ["about_hero_subtitle", "about_story_content"])
        self.assertTrue(all(item["original"].strip() and item["revised"].strip() for item in updates))
        self.assertTrue(all(item["original"] != item["revised"] for item in updates))
        self.assertIn("ölçüm", updates[0]["revised"])
        self.assertIn("düzenli", updates[1]["revised"])
        self.assertIn("kişiselleştirilmiş", updates[1]["revised"])

    def test_sql_is_guarded_transactional_and_audited(self):
        apply_sql = (PACK / "apply.sql").read_text(encoding="utf-8")
        rollback_sql = (PACK / "rollback.sql").read_text(encoding="utf-8")

        self.assertTrue(apply_sql.startswith("BEGIN;"))
        self.assertTrue(apply_sql.rstrip().endswith("COMMIT;"))
        self.assertIn('INSERT INTO speed_reading.cms_content_blocks', apply_sql)
        self.assertIn('(id, "Key", "Group"', apply_sql)
        self.assertIn('created_rows <> 2', apply_sql)
        self.assertIn('admin_audit_records', apply_sql)
        self.assertIn('cms-marketing-language-repair-v1', apply_sql)
        self.assertIn("'Create'", apply_sql)
        self.assertIn('about_story_content', rollback_sql)

    def test_application_fallback_uses_the_same_measured_language(self):
        source = ABOUT_COMPONENT.read_text(encoding="utf-8")

        self.assertIn('ölçüm, düzenli pratik ve kişisel planla sürdürün.', source)
        self.assertIn('kişiselleştirilmiş planlarla okuma hızı, anlama ve odaklanma gelişiminizi izlemenize yardımcı olur.', source)
        self.assertNotIn('okuma hızınızı artırırken', source)


if __name__ == "__main__":
    unittest.main()

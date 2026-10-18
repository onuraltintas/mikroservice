import json
from pathlib import Path
import unittest
from revise_lgs_scopes import build

ROOT = Path(__file__).resolve().parents[2]
INPUT = ROOT / 'artifacts/mebi-lgs-comparison-20261018'


class ScopeRevisionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ours = json.loads((INPUT / 'ours-live.json').read_text(encoding='utf-8-sig'))
        cls.mebi = json.loads((INPUT / 'mebi-catalog.json').read_text(encoding='utf-8'))
        cls.result = build(cls.ours, cls.mebi)

    def test_preserves_every_official_outcome_and_uses_only_existing_topics(self):
        self.assertEqual(269, len(self.result['outcomes']))
        self.assertEqual(144, len(self.result['topics']))
        self.assertEqual({o['code'] for o in self.ours['outcomes']}, {o['code'] for o in self.result['outcomes']})
        self.assertEqual({'LGS'}, {o['examCode'] for o in self.result['outcomes']})

    def test_keeps_existing_topic_names_when_mebi_uses_a_variant(self):
        original = {t["source_id"]: t for t in self.ours["topics"]}
        for topic in self.result["topics"]:
            self.assertEqual(original[topic["sourceId"]]["name"].strip(), topic["name"])

    def test_per_topic_and_per_outcome_components_have_exactly_the_same_totals(self):
        for outcome in self.result['outcomes']:
            for component in ('learningMinutes', 'practiceMinutes', 'checkMinutes'):
                self.assertEqual(outcome[component], sum(s[component] for s in outcome['shares']))
            self.assertTrue(all(s['scopeDescription'] and sum(s[k] for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')) > 0 for s in outcome['shares']))
            self.assertLessEqual(sum(outcome[k] for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')), 1440)
        for topic in self.result['topics']:
            shares = [s for o in self.result['outcomes'] for s in o['shares'] if s['topicSourceId'] == topic['sourceId']]
            for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes'):
                self.assertEqual(topic[k], sum(s[k] for s in shares))

    def test_numbered_paragraph_sections_have_distinct_scopes_and_bounded_duration(self):
        paragraphs = [t for t in self.result['topics'] if t['name'].startswith('Paragrafta Anlam')]
        self.assertEqual(4, len(paragraphs))
        self.assertEqual(4, len({tuple(t['outcomeCodes']) for t in paragraphs}))
        self.assertTrue(all(sum(t[k] for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')) == 100 for t in paragraphs))

    def test_din_matches_observed_mebi_scope_without_duplicate_extra_topic(self):
        topic = next(t for t in self.result['topics'] if t['name'] == 'Hz. Muhammed’in İnsanlara Değer Vermesi')
        self.assertEqual(['8.4.6', '8.4.7', '8.4.8'], topic['outcomeCodes'])
        self.assertEqual('lgs-2026:din:ornek-davranislar', self.result['retireUnusedTopicSourceId'])

    def test_estimates_are_reviewable_and_have_no_plan_or_account_mutations(self):
        self.assertLess(sum(o[k] for o in self.result['outcomes'] for k in ('learningMinutes', 'practiceMinutes', 'checkMinutes')), 26015)
        self.assertTrue(all(o['confidence'] == 'Low' and 'tahmin' in o['evidence'] for o in self.result['outcomes']))
        self.assertNotIn('plans', self.result)
        self.assertNotIn('users', self.result)
        self.assertEqual(self.result, build(self.ours, self.mebi))


if __name__ == '__main__':
    unittest.main()

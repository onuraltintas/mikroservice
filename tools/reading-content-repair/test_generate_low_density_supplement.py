import importlib.util
import json
import re
import unittest
from collections import Counter
from pathlib import Path


MODULE_PATH = Path(__file__).with_name("generate_low_density_supplement.py")
SPEC = importlib.util.spec_from_file_location("low_density_supplement", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class LowDensitySupplementTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.catalog = MODULE.load_catalog(MODULE.DEFAULT_CATALOG)
        cls.rows = MODULE.normalize(cls.catalog)

    def test_contains_five_questions_for_each_target_text(self):
        self.assertEqual(len(self.rows), 16)
        self.assertEqual(sum(len(row["questions"]) for row in self.rows), 80)
        for row in self.rows:
            self.assertEqual(len(row["questions"]), 5)
            self.assertEqual(
                [question["orderIndex"] for question in row["questions"]],
                list(range(4, 9)),
            )

    def test_final_answer_positions_are_balanced(self):
        for row in self.rows:
            existing = self.catalog[row["readingTextId"]]["questions"]
            answers = Counter(question["correctAnswer"] for question in existing + row["questions"])
            self.assertEqual(answers, Counter({"A": 2, "B": 2, "C": 2, "D": 2}))

    def test_questions_have_complete_keys_without_length_cues(self):
        for row in self.rows:
            ids = set()
            for question in row["questions"]:
                self.assertNotIn(question["questionId"], ids)
                ids.add(question["questionId"])
                self.assertTrue(question["questionText"].strip())
                self.assertTrue(question["explanation"].strip())
                options = [question["optionA"], question["optionB"], question["optionC"], question["optionD"]]
                self.assertEqual(len({option.casefold() for option in options}), 4)
                correct = options["ABCD".index(question["correctAnswer"])]
                self.assertTrue(correct.strip())
                lengths = [len(re.findall(r"\S+", option)) for option in options]
                self.assertFalse(lengths.count(max(lengths)) == 1 and lengths["ABCD".index(question["correctAnswer"])] == max(lengths))
                self.assertFalse(lengths.count(min(lengths)) == 1 and lengths["ABCD".index(question["correctAnswer"])] == min(lengths))
                character_lengths = [len(option) for option in options]
                self.assertFalse(character_lengths.count(max(character_lengths)) == 1 and character_lengths["ABCD".index(question["correctAnswer"])] == max(character_lengths))
                self.assertFalse(character_lengths.count(min(character_lengths)) == 1 and character_lengths["ABCD".index(question["correctAnswer"])] == min(character_lengths))

    def test_generated_sql_is_idempotent_for_both_stores(self):
        sql = MODULE.build_sql(self.rows)
        rollback = MODULE.build_rollback(self.rows)
        self.assertIn('public."ReadingQuestions"', sql)
        self.assertIn("speed_reading.reading_questions", sql)
        self.assertEqual(sql.count("::uuid"), 161)  # 80 ids + 80 text ids + one legacy actor cast
        self.assertEqual(rollback.count("::uuid"), 162)  # 160 ids + two legacy audit casts
        self.assertIn("WHERE NOT EXISTS", sql)
        self.assertIn("CREATE TEMP TABLE reading_question_supplement", sql)


if __name__ == "__main__":
    unittest.main()

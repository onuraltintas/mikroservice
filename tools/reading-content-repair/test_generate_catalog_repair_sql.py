import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from generate_catalog_repair_sql import load_questions, render


class FakeFile:
    def __init__(self, payload: str, name: str = "fixture.json"):
        self.payload = payload
        self.name = name

    def read_text(self, encoding: str = "utf-8") -> str:
        return self.payload


class FakeDirectory:
    def __init__(self, files: list[FakeFile]):
        self.files = files

    def glob(self, pattern: str) -> list[FakeFile]:
        return self.files


class CatalogRepairSqlTests(unittest.TestCase):
    def test_merges_reviewed_and_normalized_records(self):
        reviewed = {
            "readingTextId": "00000000-0000-0000-0000-000000000001",
            "questions": [{
                "questionId": "00000000-0000-0000-0000-000000000002",
                "optionA": "A", "optionB": "B", "optionC": "C", "optionD": "D",
                "correctAnswer": "A", "explanation": "Açıklama",
            }],
        }
        normalized = {
            "readingTextId": "00000000-0000-0000-0000-000000000003",
            "questionId": "00000000-0000-0000-0000-000000000004",
            "optionA": "A", "optionB": "B", "optionC": "C", "optionD": "D",
            "correctAnswer": "D", "explanation": "D açıklama",
        }
        questions = load_questions(
            FakeDirectory([FakeFile(json.dumps(reviewed, ensure_ascii=False))]),
            FakeFile(json.dumps(normalized) + "\n"),
        )
        sql = render(questions)
        self.assertEqual(len(questions), 2)
        self.assertIn("Açıklama", sql)
        self.assertIn("D açıklama", sql)
        self.assertIn("BEGIN;", sql)
        self.assertIn("COMMIT;", sql)

    def test_duplicate_question_ids_fail_closed(self):
        pack = {
            "readingTextId": "00000000-0000-0000-0000-000000000001",
            "questions": [{
                "questionId": "00000000-0000-0000-0000-000000000002",
                "optionA": "A", "optionB": "B", "optionC": "C", "optionD": "D",
                "correctAnswer": "A", "explanation": "Açıklama",
            }],
        }
        normalized = {
            "readingTextId": pack["readingTextId"],
            **pack["questions"][0],
        }
        with self.assertRaises(ValueError):
            load_questions(
                FakeDirectory([FakeFile(json.dumps(pack))]),
                FakeFile(json.dumps(normalized) + "\n"),
            )


if __name__ == "__main__":
    unittest.main()

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))

from generate_quality_backfill import correct_is_unique_longest, explanation_for, generate


class QualityBackfillTests(unittest.TestCase):
    def test_explanation_uses_question_type(self):
        self.assertIn("doğrudan verir", explanation_for({
            "type": 1,
            "correctAnswer": "A",
            "optionA": "Kısa cevap.",
        }))
        self.assertIn("doğru çıkarım", explanation_for({
            "type": 2,
            "correctAnswer": "B",
            "optionB": "Çıkarım.",
        }))
        self.assertIn("ana fikri", explanation_for({
            "type": 3,
            "correctAnswer": "C",
            "optionC": "Yorum.",
        }))

    def test_generation_only_fills_missing_explanations_by_default(self):
        rows = [{
            "textId": "text-1",
            "title": "Deneme",
            "questions": [{
                "questionId": "question-1",
                "type": 1,
                "correctAnswer": "A",
                "optionA": "Bu doğru seçenektir ve açıklaması uzundur.",
                "optionB": "Yanlış.",
                "optionC": "Yanlış.",
                "optionD": "Yanlış.",
                "explanation": None,
            }],
        }]
        updates, report = generate(rows)
        self.assertEqual(len(updates), 1)
        self.assertEqual(set(updates[0]), {"readingTextId", "title", "questionId", "explanation"})
        self.assertEqual(report["explanationsAdded"], 1)
        self.assertEqual(report["optionsCompacted"], 0)
        self.assertTrue(correct_is_unique_longest(rows[0]["questions"][0]))


if __name__ == "__main__":
    unittest.main()

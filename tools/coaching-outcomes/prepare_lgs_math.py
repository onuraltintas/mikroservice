"""Build the review-only pilot from the official PDF, without API or DB writes.

Usage: python -X utf8 tools/coaching-outcomes/prepare_lgs_math.py <official.pdf> <package.json>
Requires PyMuPDF. The source PDF is downloaded separately from the recorded MEB URL.
"""
import hashlib
import json
from pathlib import Path
import re
import sys

import pymupdf

SOURCE = "https://mufredat.meb.gov.tr/Dosyalar/201813017165445-MATEMAT%C4%B0K%20%C3%96%C4%9ERET%C4%B0M%20PROGRAMI%202018v.pdf"
SCOPE = "https://tegm.meb.gov.tr/www/2026-2027-egitim-ogretim-yili-taslak-cerceve-planlar-yayinlandi/icerik/1316"
# Official table: printed page 25 (PDF page 27). Counts are not inferred from videos.
GROUPS = [
    ("M.8.1.1", "Çarpanlar ve Katlar", 3, 10, ["ad650afa-ca7c-4716-91ce-08dd3ad55a15"]),
    ("M.8.1.2", "Üslü İfadeler", 5, 15, ["78f3c2ce-8ffb-4328-fc43-08dd54a00709"]),
    ("M.8.1.3", "Kareköklü İfadeler", 8, 25, ["6eb3d293-90df-48cc-26b4-08dd7688daf4"]),
    ("M.8.4.1", "Veri Analizi", 2, 12, ["2db1a5c6-ecf8-4c70-26b3-08dd7688daf4"]),
    ("M.8.5.1", "Basit Olayların Olma Olasılığı", 5, 12, ["3f134b87-660b-493c-26d7-08dd7688daf4"]),
    ("M.8.2.1", "Cebirsel İfadeler ve Özdeşlikler", 4, 15, ["25c4abd5-c087-4826-26d9-08dd7688daf4"]),
    ("M.8.2.2", "Doğrusal Denklemler", 6, 30, ["226d9b02-ede1-401f-7499-08de07173e11", "498052bd-05cf-4efe-bbbc-08de1c4bdb1f"]),
    ("M.8.2.3", "Eşitsizlikler", 3, 10, ["153a76d9-652b-48d2-749a-08de07173e11"]),
    ("M.8.3.1", "Üçgenler", 5, 18, ["def1071a-ac6f-4b55-749b-08de07173e11"]),
    ("M.8.3.3", "Eşlik ve Benzerlik", 2, 8, ["0698637b-2694-4c91-749c-08de07173e11"]),
    ("M.8.3.2", "Dönüşüm Geometrisi", 3, 10, ["760ce8d8-db61-4953-749d-08de07173e11"]),
    ("M.8.3.4", "Geometrik Cisimler", 6, 15, ["6585229c-e9d6-41fa-749e-08de07173e11"]),
]
ALIASES = {"M.8.1.1.1": "6106e9d7-906c-4e89-67ae-08dc0d117274", "M.8.1.1.2": "730d66e8-d13b-4bf9-67af-08dc0d117274"}


def prepare(pdf_path: Path) -> dict:
    document = pymupdf.open(pdf_path)
    if len(document) != 80:
        raise ValueError("Expected the verified 80-page 2018 curriculum PDF")
    found = {}
    for index in range(72, 78):
        lines = document[index].get_text().splitlines()
        for row, line in enumerate(lines):
            match = re.match(r"^(M\.8\.\d\.\d\.\d+)\.\s+(.+)", line)
            if not match:
                continue
            code, text = match.groups()
            # Outcome titles can wrap across lines. Explanatory paragraphs are not titles.
            cursor = row + 1
            while not text.rstrip().endswith(".") and cursor < len(lines):
                text += " " + lines[cursor].strip()
                cursor += 1
            text = re.sub(r"\s+", " ", text).strip()
            # PDF uses positioned radical glyphs; preserve the mathematical meaning in plain text.
            if code == "M.8.1.3.3":
                text = "Kareköklü bir ifadeyi a√b şeklinde yazar ve a√b şeklindeki ifadede katsayıyı kök içine alır."
            found[code] = (text, index + 1)
    if len(found) != 52:
        raise ValueError(f"Expected 52 outcomes, extracted {len(found)}")
    outcomes, mappings, groups = [], [], []
    for group, name, count, periods, subjects in GROUPS:
        codes = sorted((c for c in found if c.startswith(group + ".")), key=lambda c: int(c.rsplit(".", 1)[1]))
        if len(codes) != count:
            raise ValueError(f"Incomplete official group: {group}")
        groups.append(dict(code=group, name=name, outcomeCount=count, lessonPeriods=periods))
        # Deliberately low confidence: full topic budget, equal share, five-minute rounding.
        # 40 minutes is an explicit modelling assumption, not a claim about individual learning.
        total = max(15, int(periods * 40 / count / 5 + 0.5) * 5)
        learn = int(total * 0.4 / 5 + 0.5) * 5
        check = max(5, int(total * 0.1 / 5 + 0.5) * 5)
        evidence = (f"Taslak referans: resmi {group} konu bütçesi {periods} ders saati / {count} kazanım. "
                    "Model varsayımı: ders saati başına 40 dakika, eşit dağılım, 5 dakikaya yuvarlama; "
                    "%40 öğrenme, yaklaşık %50 alıştırma, %10 kontrol. Bireysel öğrenme süresi MEB tarafından verilmemiştir. "
                    "Mola/video süresi yoktur. Öğretmen incelemesi olmadan yayınlanamaz.")
        for code in codes:
            text, page = found[code]
            outcomes.append(dict(code=code, name=text, notes="Açıklamalar ve kapsam sınırlamaları için kaynak belgedeki sayfayı inceleyin.",
                officialTopicCode=group, sourcePage=page, displayOrder=len(outcomes) + 1,
                learningMinutes=learn, practiceMinutes=total - learn - check, checkMinutes=check, durationEvidence=evidence,
                sources=[dict(source="MEBI", sourceId=ALIASES[code])] if code in ALIASES else []))
        for subject in subjects:
            evidence = "Konu adı ve resmi kapsamdan aday eşleştirme; içerik bazında inceleme gereklidir."
            if len(subjects) > 1:
                evidence += " Doğrusal Denklemler 1/2 kapsam ayrımı doğrulanmadı; iki adayın tümünü onaylamayın."
            mappings.append(dict(topicSourceId="topic:" + subject, outcomeCodes=codes, evidence=evidence))
    return dict(code="meb-2018-lgs-matematik-2026-pilot-v1", examCode="LGS", gradeNumber=8, subjectName="Matematik",
        academicYearStart=2026, academicYearEnd=2026, sourceUrl=SOURCE,
        documentSha256=hashlib.sha256(pdf_path.read_bytes()).hexdigest(), scopeEvidenceUrl=SCOPE,
        officialTopics=groups, outcomes=outcomes, mappings=mappings)


if __name__ == "__main__":
    result = prepare(Path(sys.argv[1]))
    output = Path(sys.argv[2])
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"{len(result['outcomes'])} outcomes, {len(result['mappings'])} candidate topics; DRAFT only")

"""Prepare reviewed scope/budget changes; never write a database or old plan."""
from collections import defaultdict
import json
from pathlib import Path
import sys

COMPONENTS = ('learningMinutes', 'practiceMinutes', 'checkMinutes')


def split(total, count):
    whole, rest = divmod(total, count)
    return [whole + (i < rest) for i in range(count)]


def budget(lesson, name):
    # One initial LGS study pass, excluding breaks. These are transparent
    # editorial starting estimates, not official individual learning minutes.
    if lesson == 'Matematik':
        if name == 'Kareköklü İfadeler': return (90, 180, 20)
        if name in ('Üçgenler', 'Geometrik Cisimler'): return (75, 150, 20)
        if name.startswith('Doğrusal Denklemler') or name in ('Çarpanlar ve Katlar', 'Üslü İfadeler', 'Cebirsel İfadeler ve Özdeşlikler', 'Basit Olayların Olma Olasılığı'):
            return (60, 120, 15)
        return (45, 90, 10)
    if lesson == 'Fen Bilimleri':
        if name in ('Kalıtım', 'Basınç', 'Asitler ve Bazlar', 'Maddenin Isı ile Etkileşimi', 'Basit Makineler', 'Enerji Dönüşümleri', 'Elektrik Enerjisinin Dönüşümü'):
            return (60, 90, 15)
        if name in ('DNA ve Genetik Kod', 'Madde Döngüleri ve Çevre Sorunları', 'Elektrik Yüklü Cisimler'): return (45, 60, 10)
        if name in ('Mutasyon ve Modifikasyon', 'Adaptasyon (Çevreye Uyum)', 'Türkiye’de Kimya Endüstrisi'): return (20, 30, 5)
        return (30, 45, 10)
    if lesson == 'Türkçe':
        if name.startswith('Paragrafta Anlam'): return (30, 60, 10)
        if name.startswith('Noktalama İşaretleri'): return (20, 30, 5)
        if name.startswith('Yazım Kuralları'): return (25, 40, 5)
        if name in ('Fiilimsiler', 'Cümlenin Ögeleri'): return (45, 60, 10)
        if name == 'Sözel Mantık': return (45, 75, 10)
        if name == 'Görsel, Grafik ve Tablo Yorumlama': return (30, 60, 10)
        return (25, 45, 5)
    if lesson == 'Din Kültürü ve Ahlak Bilgisi':
        if name.startswith(('Bir Peygamber', 'Bir Sure', 'Bir Ayet')): return (15, 20, 5)
        if name in ('Kader ve Kaza İnancı', 'İnsanın İradesi ve Kader', 'Kaderle İlgili Kavramlar', 'Zekât ve Sadaka İbadeti', 'Hz. Muhammed’in İnsanlara Değer Vermesi'):
            return (30, 45, 10)
        return (20, 30, 5)
    if lesson == 'T.C. İnkılap Tarihi ve Atatürkçülük':
        if name.startswith('Birinci Dünya Savaşı') or name in ('Millî Mücadele’nin Hazırlık Dönemi', 'Atatürk İlkeleri', 'Millî Mücadele Dönemi\'nde Batı Cephesi'):
            return (35, 45, 10)
        return (20, 30, 5)
    if lesson == 'İngilizce': return (25, 35, 5)
    raise ValueError('Unexpected lesson: ' + lesson)


def scope_and_codes(lesson, name, codes):
    scope = name + ': bu başlıktaki kavramlar, LGS soruları ve kontrol çalışması.'
    if name == 'Doğrusal Denklemler 1':
        return 'Doğrusal ilişkinin temelleri, birinci dereceden denklemler, koordinatlar ve tablodan denklem kurma.', [f'M.8.2.2.{n}' for n in (1, 2, 3)]
    if name == 'Doğrusal Denklemler 2':
        return 'Doğrusal ilişkinin grafikle gösterimi, gerçek hayat problemleri ve eğim.', [f'M.8.2.2.{n}' for n in (3, 4, 5, 6)]
    if lesson == 'Türkçe':
        sections = {
            'Paragrafta Anlam 1': ('Konu, ana/yardımcı fikir, başlık, özet ve hikâye unsurları.', [12, 13, 16, 17, 18, 19, 20]),
            'Paragrafta Anlam 2': ('Anlatım biçimleri, düşünceyi geliştirme, yorum, karşılaştırma ve kurgu.', [10, 11, 21, 23, 24, 34]),
            'Paragrafta Anlam 3': ('Metne dayalı soru, çıkarım, problem ve çözüm ilişkileri.', [14, 15, 22, 25]),
            'Paragrafta Anlam 4': ('Vurgulama, medya/bilgi kaynakları, sunum karşılaştırması ve işlem basamakları.', [28, 29, 30, 31, 33, 35]),
        }
        if name in sections:
            title, numbers = sections[name]
            return title, [f'T.8.3.{n}' for n in numbers]
        if name == 'Cümlede Anlam 1': return 'Geçiş ve bağlantı ifadelerinin cümlenin anlamına katkısı.', ['T.8.3.10']
        if name == 'Cümlede Anlam 2': return 'Neden, amaç, koşul ve karşılaştırma ilişkilerinden çıkarım.', ['T.8.3.25']
        if name == 'Cümlede Anlam 3': return 'Öznel/nesnel ifadeler, duygular ve cümle yorumlama.', ['T.8.3.25']
        scope = {
            'Noktalama İşaretleri 1': 'Nokta ve virgülün kullanımlarını ayırt etme ve düzeltme.',
            'Noktalama İşaretleri 2': 'Noktalı virgül, iki nokta, soru ve ünlem kullanımlarını düzeltme.',
            'Noktalama İşaretleri 3': 'Tırnak, kesme, ayraç, kısa/uzun çizgi ve eğik çizgi kullanımlarını düzeltme.',
            'Yazım Kuralları 1': 'Büyük harf ve sayıların yazımı; yazım yanlışlarını düzeltme.',
            'Yazım Kuralları 2': 'Birleşik kelimeler, kısaltmalar ve ek/bağlaçların yazımını düzeltme.',
        }.get(name, scope)
    if name == 'Hz. Muhammed’in İnsanlara Değer Vermesi':
        return 'İnsanlara verilen değer, örnek davranışların toplumsal etkisi ve hikmetli sözlerle yönlendirme; MEBİ video kazanımlarıyla doğrulandı.', ['8.4.6', '8.4.7', '8.4.8']
    if lesson == 'İngilizce':
        if name == 'Describing Places': codes = [c for c in codes if c not in ('E8.7.SI2',)] + ['E8.7.SI2']
        if name in ('Expressing Preferences/Giving Explanations and Reasons', 'Making Comparisons') and any(c.startswith('E8.7.') for c in codes):
            codes = [c for c in codes if c not in ('E8.7.SI2', 'E8.7.W1')]
        if name == 'Expressing Likes and Dislikes' and any(c.startswith('E8.8.') for c in codes): codes = ['E8.8.L2', 'E8.8.SP1']
        if name == 'Expressing Obligation': codes = ['E8.8.L2', 'E8.8.SP1']
        if name == 'Expressing Responsibilities': codes = ['E8.8.R1', 'E8.8.W1']
        if name == 'Describing the Actions Happening Currently': codes = [c for c in codes if c not in ('E8.9.SI2', 'E8.9.W1')]
        scope = name + ': ilgili temadaki bu dil işlevini yazılı diyalog/metin ve çoktan seçmeli LGS sorularında kullanma; sözlü performans ödevi değildir.'
    return scope, sorted(set(codes))


def build(ours, mebi):
    outcomes = {x['id']: x for x in ours['outcomes']}
    mappings = defaultdict(list)
    for m in ours['mappings']:
        if m['approved']: mappings[m['topic_id']].append(outcomes[m['outcome_id']]['code'])
    parents = {x['parent_id'] for x in ours['topics'] if x['parent_id']}
    source_topics = {x['source_id'].removeprefix('topic:'): x for x in ours['topics'] if x['id'] not in parents and x['active']}
    current = {(x['lesson'], x['code']): x for x in outcomes.values()}
    topics, shares = [], defaultdict(list)
    for entry in sorted(mebi['subjects'], key=lambda x: (x['lesson'], x['id'])):
        original = source_topics[entry['id']]
        lesson, name = entry['lesson'], entry['name'].strip()
        scope, codes = scope_and_codes(lesson, name, mappings[original['id']])
        codes = sorted(set(codes))
        if not codes or any((lesson, c) not in current for c in codes): raise ValueError('Unresolved scope: ' + name)
        minutes = dict(zip(COMPONENTS, budget(lesson, name)))
        topics.append(dict(sourceId=original['source_id'], name=original['name'].strip(), lesson=lesson, scopeDescription=scope, outcomeCodes=codes, **minutes))
        parts = {k: split(minutes[k], len(codes)) for k in COMPONENTS}
        for i, code in enumerate(codes):
            shares[(lesson, code)].append(dict(topicSourceId=original['source_id'], scopeDescription=scope, **{k: parts[k][i] for k in COMPONENTS}))
    revised = []
    for key, original in sorted(current.items()):
        portions = shares[key]
        if not portions: raise ValueError('Uncovered official outcome: ' + original['code'])
        evidence = ('scope-revision-20261018-v2: LGS ilk çalışma turu için konu kapsamı ve soru yüküyle yeniden değerlendirilen düşük güvenli tahmin. '
            'Resmî yıllık ders saati bireysel dakika değildir; video süresi doğrudan kullanılmadı. '
            'Öğrenme, soru/alıştırma ve kontrol ayrı bütçelerdir; ortak kazanımın konu payları toplamı bu süreye eşittir. '
            'Numaralı bölümler için pedagojik kapsam tanımıdır; MEBİ videosunun bölüm içeriğinin birebir dökümü iddia edilmez. '
            'Eski süre sürümleri ve mevcut çalışma kayıtları korunur.')
        revised.append(dict(lesson=key[0], code=key[1], examCode='LGS', editionCode=original['edition'], confidence='Low', evidence=evidence,
            expectedPriorLearning=original['learning'], expectedPriorPractice=original['practice'], expectedPriorCheck=original['check_minutes'],
            shares=portions, **{k: sum(s[k] for s in portions) for k in COMPONENTS}))
    return dict(code='lgs-scope-revision-20261018-v2', academicYearStart=2026, retireUnusedTopicSourceId='lgs-2026:din:ornek-davranislar',
        topics=topics, outcomes=revised)


if __name__ == '__main__':
    directory = Path(sys.argv[1])
    result = build(json.loads((directory / 'ours-live.json').read_text(encoding='utf-8-sig')),
        json.loads((directory / 'mebi-catalog.json').read_text(encoding='utf-8')))
    Path(sys.argv[2]).write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({lesson: sum(o[k] for o in result['outcomes'] if o['lesson'] == lesson for k in COMPONENTS)
        for lesson in sorted({o['lesson'] for o in result['outcomes']})}, ensure_ascii=False))

"""Create the five-question supplements for the 16 low-density reading texts.

The source definitions live in this file so the content and the generated SQL
can be reviewed together.  Question ids are deterministic; rerunning the
generator therefore produces the same insert set and is safe to apply again.
"""

from __future__ import annotations

import argparse
import json
import re
import uuid
from collections import Counter
from datetime import date
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CATALOG = ROOT / ".tmp-current-reading-catalog.jsonl"
DEFAULT_OUTPUT = ROOT / "content-packs" / "reading-question-supplement" / "v1"
NAMESPACE = uuid.UUID("d57f2b3c-2d0a-4fe6-9cf5-9a5f75a1d08e")
ACTOR = "content-supplement-20260915"
LEGACY_ACTOR = "00000000-0000-0000-0000-000000000001"


def item(key: str, question: str, kind: int, correct: str, options: list[str], explanation: str) -> dict[str, Any]:
    if correct not in {"A", "B", "C", "D"} or len(options) != 4:
        raise ValueError(f"invalid answer definition: {key}")
    return {
        "questionKey": key,
        "questionText": question,
        "type": kind,
        "bloomLevel": kind,
        "difficultyLevel": None,
        "correctAnswer": correct,
        "optionA": options[0],
        "optionB": options[1],
        "optionC": options[2],
        "optionD": options[3],
        "explanation": explanation,
    }


TEXTS: list[dict[str, Any]] = [
    {
        "readingTextId": "0a55e172-f320-4319-83fb-5152091b00f5",
        "title": "Petrol Savaşlarından Yeşil Devrime: Enerji Jeopolitiği",
        "difficultyLevel": 3,
        "questions": [
            item("critical-mineral-example", "Yeşil dönüşümde kritik mineral olarak metinde hangi örnek verilmektedir?", 1, "A", ["Lityum", "Kömür", "Petrol", "Doğalgaz"], "Lityum, batarya teknolojilerinde kullanılan ve yeşil dönüşüm için stratejik görülen minerallerden biridir."),
            item("local-renewable-potential", "Güneş ve rüzgâr kaynaklarının yerel olarak üretilebilmesi hangi jeopolitik olanağı güçlendirir?", 2, "B", ["Yakıt ithalatını artırmayı", "Dış enerji bağımlılığını azaltmayı", "Petrol gelirlerini yükseltmeyi", "Maden işleme tekelini korumayı"], "Yerel yenilenebilir üretim, ülkelerin enerji ihtiyacını dış tedarikçilere daha az bağlı karşılamasına yardımcı olabilir."),
            item("technology-dependence", "Bir ülkenin güneş enerjisi kaynağına sahip olup yine de teknoloji bağımlısı kalması nasıl açıklanır?", 2, "C", ["Güneş ışığının yalnızca ithal edilebilmesiyle", "Yenilenebilir elektriğin depolanamamasıyla", "Panel, batarya ve yazılımı dışarıdan almasıyla", "Petrol kuyularının yenilenebilir santrallere dönüşmesiyle"], "Kaynak ülke içinde bulunsa bile panel, batarya ve kontrol yazılımlarının dışarıdan alınması yeni bir bağımlılık yaratabilir."),
            item("rentier-state-risk", "Rantiyer devletlerin fosil yakıt talebi azalırken karşılaşabileceği temel risk nedir?", 2, "D", ["İhracat gelirlerinin bütçeyi artık desteklememesi", "Yenilenebilir kaynakların bir anda tükenmesi", "Kritik madenlerin bütün ülkelerde bitmesi", "Enerji verimliliğinin otomatik olarak düşmesi"], "Bütçesi büyük ölçüde fosil yakıt ihracatına dayanan devletler, talep ve fiyat düştüğünde gelir kaynaklarını kaybetme riski taşır."),
            item("green-paradox", "Yeşil paradoks hangi etik sorunu görünür kılar?", 3, "A", ["Emisyonu azaltan ürünlerin maden çıkarma yükünü başka topluluklara taşıyabilmesini", "Yenilenebilir enerjinin her koşulda ücretsiz olmasını", "Geri dönüşümün hiçbir yeni kaynak gerektirmemesini", "Elektrikli araçların bütün çevresel etkileri ortadan kaldırmasını"], "Elektrikli araç veya batarya üretimi emisyonu azaltırken maden çıkarma yükünü kırılgan topluluklara taşıyabilir; bu nedenle geçişin adil olması gerekir."),
        ],
    },
    {
        "readingTextId": "10a7e06b-84a9-4df0-a3e7-051a561d818a",
        "title": "Post-Hümanizm ve İnsanın Geleceği",
        "difficultyLevel": 3,
        "questions": [
            item("cyborg-change", "Siborglaşma tartışmasının merkezindeki değişim aşağıdakilerden hangisidir?", 1, "D", ["İnsan bedeninin yalnızca biyolojik kabul edilmesi", "Teknolojinin insan davranışını hiç etkilememesi", "Biyolojinin bütün toplumsal sorunları çözmesi", "İnsan yetilerinin makine ve biyoteknolojiyle desteklenmesi"], "Siborglaşma, insan yetilerinin beyin-makine arayüzleri veya başka teknolojilerle desteklenmesi ihtimalini tartışır."),
            item("designer-baby-equity", "CRISPR ile tasarım bebekler tartışmasının başlıca toplumsal kaygısı nedir?", 2, "A", ["Erişim farklarının biyolojik sınıflar yaratabilmesi", "Her genetik müdahalenin hemen reddedilmesi", "Teknolojinin bütün hastalıkları aynı anda iyileştirmesi", "Ebeveynlerin çocuklarının eğitimini bırakması"], "Pahalı genetik geliştirmelere yalnızca bazı gruplar erişirse mevcut eşitsizlikler biyolojik bir sınıf ayrımına dönüşebilir."),
            item("life-extension-dilemma", "Radikal yaşam uzatmanın metinde işaret edilen sonuçlarından biri hangisidir?", 2, "B", ["Yaşlanmanın bütün etik sorunları bitirmesi", "Nüfus, kaynak paylaşımı ve kuşak ilişkilerinin yeniden düşünülmesi", "Ölümün toplumdaki bütün anlamlarını kaybetmesi", "Biyolojik bedenin teknolojiden tamamen kopması"], "Çok uzun yaşam, nüfusun sürdürülebilirliği ve kuşaklar arası kaynak paylaşımı gibi soruları yeniden gündeme getirir."),
            item("posthuman-transhuman", "Post-hümanizmi transhümanizmden ayıran genişletici vurgu nedir?", 3, "C", ["Yalnızca insan performansını artırmaya odaklanması", "Teknolojik gelişmeyi bütünüyle durdurmayı önermesi", "İnsan dışı varlıkların ve ekolojik ilişkilerin etik alana katılması", "Biyolojik araştırmaları yalnızca tıp ile sınırlaması"], "Post-hümanizm yalnızca insanı güçlendirmeyi değil, insan dışı varlıklarla ve ekosistemlerle ilişkimizin ahlaki çerçevesini de sorgular."),
            item("singularity-choice", "Yapay zekâ tekilliği fikri insanlık açısından hangi kararı gündeme getirir?", 3, "D", ["Bilgisayarları tümüyle kapatmayı", "Teknolojik ilerlemeyi ölçmeden hızlandırmayı", "İnsan yaratıcılığını gereksiz saymayı", "Gücü kimlerin yöneteceğini ve insanın nasıl uyum sağlayacağını"], "Tekillik senaryoları, çok güçlü sistemler ortaya çıktığında yönetişim ve insanın uyum sağlama biçiminin nasıl belirleneceğini gündeme taşır."),
        ],
    },
    {
        "readingTextId": "1a1b0ff5-d552-40b1-80a2-66d9fd9382a1",
        "title": "Sosyal Psikoloji: Grup İçinde Biz",
        "difficultyLevel": 2,
        "questions": [
            item("milgram-focus", "Milgram deneyi sosyal psikolojide öncelikle hangi davranışı incelemiştir?", 1, "D", ["Yaratıcı problem çözmeyi", "Gruplar arası dostluğu", "Kendiliğinden yardım etmeyi", "Otorite talimatına itaat etmeyi"], "Milgram'ın çalışması, katılımcıların otorite talimatı karşısında ne ölçüde itaat ettiğini incelemiştir."),
            item("bystander-effect", "Seyirci etkisi hangi durumda yardım etme olasılığının azalmasını açıklar?", 1, "A", ["Çevrede çok sayıda tanık olduğunda", "Olayı yalnızca kişi gördüğünde", "Yardım görevi açıkça paylaşıldığında", "Kişiler birbirini uzun süredir tanıdığında"], "Tanık sayısı arttığında sorumluluk dağıldığı için her birey yardım etme görevini başkasının üstleneceğini düşünebilir."),
            item("social-identity", "Sosyal kimlik kuramına göre insanlar grup üyeliğini nasıl kullanır?", 2, "B", ["Kişisel özelliklerini tümüyle yok sayarak", "Kendini ait olduğu grubun özellikleriyle tanımlayarak", "Her grubu aynı değerde ve farksız görerek", "Grup sınırlarını yalnızca biyolojiyle açıklayarak"], "Sosyal kimlik, kişinin kendisini ait olduğu grupların özellikleri ve değerleri üzerinden tanımlamasına katkı sağlar."),
            item("groupthink-cost", "Grup düşüncesinin en belirgin sakıncası aşağıdakilerden hangisidir?", 2, "C", ["Kararların daha fazla seçenek içermesi", "Azınlık görüşlerinin daha çok dinlenmesi", "Uyum baskısının eleştirel değerlendirmeyi bastırması", "Her üyenin sorumluluğu açıkça üstlenmesi"], "Grup düşüncesinde uyum sağlama baskısı, alternatifleri ve riskleri sorgulayan eleştirel sesleri susturabilir."),
            item("dissent-practice", "Bir ekip sosyal baskıyı azaltmak için hangi uygulamayı benimsemelidir?", 3, "D", ["Tek bir kişinin bütün kararları vermesini", "Muhalif görüşleri toplantı dışında bırakmasını", "İlk öneriyi tartışmadan uygulamasını", "Farklı görüşleri güvenli biçimde dile getirmeyi"], "Muhalif görüşlerin güvenli biçimde ifade edilmesi, grup baskısının görünür olmasını ve daha dengeli kararlar alınmasını sağlar."),
        ],
    },
    {
        "readingTextId": "4e1a1036-454e-4b89-87ef-30239c429e18",
        "title": "Evrenin Görünmeyen Yüzü: Karanlık Madde ve Karanlık Enerji",
        "difficultyLevel": 3,
        "questions": [
            item("dark-matter-share", "Evrenin enerji yoğunluğu içinde karanlık maddenin yaklaşık payı nedir?", 1, "D", ["Yüzde 5", "Yüzde 10", "Yüzde 68", "Yüzde 27"], "Standart kozmolojik modele göre karanlık madde yaklaşık yüzde 27'lik bir paya sahiptir; görünür madde yaklaşık yüzde 5'tir."),
            item("rotation-evidence", "Gökadaların dönme hızları karanlık madde için nasıl kanıt sağlar?", 2, "A", ["Görünen maddeye ek bir kütleçekim etkisine işaret eder", "Gökadaların hiç kütleçekimi olmadığını gösterir", "Yıldızların yalnızca ışıkla hareket ettiğini kanıtlar", "Evrenin her yönde aynı hızla çöktüğünü ortaya koyar"], "Gökadaların dış bölgelerindeki hızlar, yalnızca görünen madde hesaba katıldığında beklenenden yüksek kalır; bu ek kütleçekim karanlık maddeyle açıklanır."),
            item("dark-energy-share", "Karanlık enerjinin standart modeldeki yaklaşık payı hangisidir?", 1, "B", ["Yüzde 5", "Yüzde 68", "Yüzde 27", "Yüzde 95"], "Karanlık enerji, standart modelde evrenin enerji yoğunluğunun yaklaşık yüzde 68'ini oluşturur."),
            item("big-crunch", "Büyük Çöküş senaryosu Büyük Donma'dan hangi yönüyle ayrılır?", 2, "C", ["Yıldız oluşumunun sonsuza dek hızlanmasıyla", "Evrenin ışık hızından daha yavaş genişlemesiyle", "Genişlemenin durup evrenin yeniden büzülmesiyle", "Karanlık maddenin tamamen görünür hale gelmesiyle"], "Büyük Çöküş, genişlemenin durarak kütleçekim etkisiyle evrenin yeniden büzülmesini öngörür; Büyük Donma ise genişlemenin sürmesidir."),
            item("invisible-matter", "Karanlık maddenin doğrudan görünmemesinin temel nedeni nedir?", 3, "D", ["Hiçbir kütleye sahip olmaması", "Yalnızca Dünya'nın içinde bulunması", "Gökadalardan daha hızlı hareket etmesi", "Elektromanyetik ışıkla belirgin biçimde etkileşmemesi"], "Karanlık madde ışığı yaymadığı, soğurmadığı veya güçlü biçimde saçmadığı için doğrudan gözlenmez; etkisi kütleçekimle anlaşılır."),
        ],
    },
    {
        "readingTextId": "50f26b0f-0ace-4c42-9701-a643161acf2e",
        "title": "Liderlik Psikolojisi: Yönetmek mi, İlham Vermek mi?",
        "difficultyLevel": 2,
        "questions": [
            item("transformational-leadership", "Dönüşümcü liderliği işlemsel liderlikten ayıran temel özellik nedir?", 1, "D", ["Yalnızca görev tamamlanınca ödül vermesi", "Kararları çalışanlardan tamamen saklaması", "Performansı yalnızca para ile ölçmesi", "Ortak bir vizyon ve gelişim duygusu oluşturması"], "Dönüşümcü lider, yalnızca işi ve ödülü yönetmek yerine ortak bir vizyon kurarak insanların gelişimini destekler."),
            item("psychological-safety", "Psikolojik güvenliği yüksek bir ekipte hangi davranış beklenir?", 2, "A", ["Hataları ve kaygıları cezalandırılma korkusu olmadan paylaşmak", "Sorunları yöneticiden gizlemek", "Sadece yöneticinin konuşmasına izin vermek", "Eleştiriyi kişisel saldırı olarak kabul etmek"], "Psikolojik güvenlik, ekip üyelerinin hata, soru ve farklı görüşlerini küçük düşürülme korkusu olmadan dile getirebilmesidir."),
            item("servant-leadership", "Hizmetkâr liderlik yaklaşımının önceliği nedir?", 1, "B", ["Liderin görünürlüğünü ve statüsünü artırmak", "Ekibin ihtiyaçlarını ve gelişimini desteklemek", "Kuralları her koşulda katılaştırmak", "Bilgiyi yalnızca üst yönetimde tutmak"], "Hizmetkâr liderlik, liderin gücünü kendi statüsü için değil, ekibin ihtiyaçlarını karşılamak ve gelişimini sağlamak için kullanır."),
            item("dark-triad-risk", "Karanlık üçlü özellikleri güçlü bir yöneticide hangi risk daha olasıdır?", 2, "C", ["Şeffaf geri bildirim ve güvenin artması", "Çalışanların özerkliğinin düzenli biçimde güçlenmesi", "Manipülasyon ve korkuya dayalı toksik bir kültür", "Hataların öğrenme fırsatı olarak ele alınması"], "Narsisizm, Makyavelizm ve psikopati özellikleri manipülasyon, empati eksikliği ve tükenmişlik riskini artırabilir."),
            item("leadership-management", "Metnin ayrımına göre yönetim ve liderlik arasındaki ilişki nasıl özetlenebilir?", 3, "D", ["İkisi aynı beceridir ve birbirinin yerine geçer", "Yönetim yalnızca motivasyon, liderlik yalnızca bütçedir", "Liderlik kuralları kaldırır, yönetim insanları yok sayar", "Yönetim düzeni sürdürürken liderlik yön ve anlam verir"], "Yönetim süreç ve düzeni korur; liderlik ise insanları ortak bir yön, anlam ve değişim etrafında harekete geçirir."),
        ],
    },
    {
        "readingTextId": "623c28bb-c9cf-4ae7-8c18-e9400c35e66b",
        "title": "Sosyal Sistemlerde Kaos Teorisi: Kelebek Etkisi",
        "difficultyLevel": 3,
        "questions": [
            item("lorenz-butterfly", "Kelebek etkisi ifadesi hangi fikri özetler?", 1, "B", ["Büyük sonuçların yalnızca büyük nedenleri vardır", "Başlangıçtaki küçük farklar zamanla büyük sonuçlara dönüşebilir", "Doğadaki bütün sistemler tamamen rastlantısaldır", "Sosyal olaylar matematiksel olarak hiç incelenemez"], "Kelebek etkisi, başlangıç koşullarındaki küçük farklılıkların doğrusal olmayan süreçlerde zamanla büyük ayrışmalara yol açabilmesini anlatır."),
            item("feedback-amplify", "Sosyal bir sistemde geri besleme döngüsü küçük bir olayı nasıl büyütebilir?", 2, "A", ["Bir davranışın sonuçları yeni davranışları tetikleyerek etkiyi çoğaltabilir", "Sistemin bütün aktörleri aynı anda ortadan kalkabilir", "Bilgi sistemden tamamen silinerek değişimi durdurabilir", "Her kararın sonucu önceden sabitlenerek dalgalanma engellenebilir"], "Bir olayın sonucu yeni kararları etkiler; bu kararlar da ilk olayı güçlendirir veya zayıflatır ve zincirleme değişim yaratır."),
            item("prediction-limit", "Deterministik bir sistemde bile uzun vadeli tahmin neden zorlaşabilir?", 2, "B", ["Sistemin hiçbir kuralı bulunmadığı için", "Başlangıç ölçümündeki küçük belirsizlikler büyüyebildiği için", "Bütün değişkenler birbirinden tamamen bağımsız olduğu için", "Sonuçlar yalnızca gözlemcinin isteğine göre oluştuğu için"], "Kurallar belirli olsa bile başlangıç koşullarındaki küçük ölçüm hataları büyürse uzun vadeli sonuçlar pratikte öngörülemez hale gelebilir."),
            item("adaptive-policy", "Kaotik sosyal sistemlerde hangi politika yaklaşımı daha dayanıklıdır?", 3, "C", ["Tek bir tahmine bağlı ve değişmez plan", "Geri bildirim almayan merkezi kararlar", "Sonuçları izleyip gerektiğinde yön değiştiren uyarlanabilir plan", "Belirsizliği tamamen yok sayan hızlı uygulama"], "Uyarlanabilir politika, sonuçları izler ve yeni bilgi geldikçe adımları günceller; bu yaklaşım belirsizlikte daha dayanıklıdır."),
            item("nonlinear-difference", "Doğrusal olmayan bir sistemin doğrusal sistemden temel farkı nedir?", 3, "D", ["Hiçbir girdinin sonucu etkilememesi", "Sadece fiziksel ortamlarda görülmesi", "Bütün değişkenlerin sabit kalması", "Girdi ile sonuç arasındaki ilişkinin orantılı olmak zorunda olmaması"], "Doğrusal olmayan sistemlerde girdideki küçük bir değişiklik orantısız bir sonuç yaratabilir; etkiler basitçe toplanmayabilir."),
        ],
    },
    {
        "readingTextId": "7bf25405-29a4-4ba1-9f31-f3ad2b0f1a28",
        "title": "Marka Yönetimi: Logodan Fazlası",
        "difficultyLevel": 2,
        "questions": [
            item("identity-logo", "Marka kimliği ile logo arasındaki fark nasıl açıklanır?", 1, "C", ["Logo markanın bütün deneyimini tek başına oluşturur", "Marka kimliği yalnızca renk kodlarından oluşur", "Logo görsel işarettir; kimlik daha geniş bir anlam ve vaat bütünüdür", "Kimlik ve logo her durumda aynı kavramdır"], "Logo tanınmayı sağlayan görsel bir işarettir; marka kimliği ise değerleri, dili, kişiliği ve vaat edilen deneyimi kapsar."),
            item("promise-experience", "Bir marka vaadinin güven üretmesi için hangi koşul gereklidir?", 2, "D", ["Vaat yalnızca reklamda görünmelidir", "Her müşteri farklı ve çelişkili deneyim yaşamalıdır", "Logo sık sık değiştirilmelidir", "Temas noktalarındaki gerçek deneyim vaatle tutarlı olmalıdır"], "Müşterinin yaşadığı ürün, hizmet ve iletişim deneyimi verilen vaatle tutarlı olduğunda güven güçlenir."),
            item("crisis-golden-hour", "Kriz iletişimindeki 'altın saat' yaklaşımı hangi eylemi destekler?", 2, "B", ["Sorun tamamen unutulana kadar beklemeyi", "İlk aşamada hızlı, doğru ve sorumluluk alan açıklama yapmayı", "Yalnızca rakipleri suçlayan mesaj yayımlamayı", "Müşterilerin sorularını yanıtsız bırakmayı"], "İlk açıklama algının çerçevesini etkileyebilir; hızlı, doğrulanmış ve sorumluluk alan iletişim güveni korumaya yardımcı olur."),
            item("emotional-value", "Markanın soyut ve duygusal değer üretmesi hangi sonucu doğurabilir?", 2, "C", ["Ürünün kullanımını ve deneyimini önemsizleştirir", "Müşterinin bütün tercihlerini rastlantısal hale getirir", "Benzer ürünler arasında tercih ve fiyat kabulünü etkileyebilir", "Marka ile müşteri arasındaki güveni tamamen kaldırır"], "Güven, statü veya aidiyet gibi duygusal anlamlar, benzer işlevli ürünler arasında tercihi ve ödenmeye razı olunan fiyatı etkileyebilir."),
            item("touchpoint-consistency", "Marka temas noktalarında tutarlılık neden önemlidir?", 3, "D", ["Her kanalda farklı kimlikler yaratmak için", "Müşteri beklentilerini sürekli değiştirmek için", "Sadece logoyu daha büyük göstermek için", "Tekrarlanan deneyimlerin güvenilir bir anlam oluşturması için"], "Tutarlı dil, görsel kimlik ve hizmet deneyimi müşterinin markadan ne bekleyeceğini anlamasını kolaylaştırır ve güven oluşturur."),
        ],
    },
    {
        "readingTextId": "804bdec7-75d8-4aed-a7d9-43a7e0b3979b",
        "title": "Antroposen Çağı: İnsanlığın Jeolojik İmzası",
        "difficultyLevel": 3,
        "questions": [
            item("anthropocene-marker", "İnsan etkisinin jeolojik izlerinden biri olarak metinde hangisi öne çıkar?", 1, "D", ["Yalnızca mevsimsel yağışlar", "Okyanusların doğal gelgitleri", "Kıtasal kayaların milyon yıllık aşınması", "Plastik, beton ve nükleer izotopların birikimi"], "Plastik, beton ve nükleer denemelerden kalan izotoplar, insan faaliyetlerinin jeolojik kayıtlarda ayırt edilebilir izler bırakmasına örnektir."),
            item("great-acceleration", "Büyük Hızlanma kavramı hangi dönemdeki küresel artışları anlatır?", 1, "A", ["1950 sonrasında üretim, nüfus ve tüketimdeki hızlı artışları", "Orta Çağ'da tarımsal üretimin tek bir bölgede yükselmesini", "Sanayi öncesinde avcılığın mevsimsel değişimini", "Gelecekte nüfusun tamamen sabitlenmesini"], "Büyük Hızlanma, yaklaşık 1950 sonrasında insan nüfusu, üretim, enerji kullanımı ve tüketimde görülen keskin artışları ifade eder."),
            item("geologic-debate", "Antroposen'in resmî jeolojik çağ olarak kabulünde neden tartışma vardır?", 2, "B", ["İnsan etkisinin Dünya'da hiç ölçülememesi", "Başlangıç tarihinin ve jeolojik sınırının hangi kanıtla belirleneceği konusunda görüş ayrılığı", "İklim değişikliğinin yalnızca tek bir şehirde görülmesi", "Jeolojik çağların bilimsel olarak kullanılmaması"], "Tartışma, insan etkisinin gerçekliğinden çok, çağın başlangıç tarihinin ve küresel sınırının hangi jeolojik kanıtla tanımlanacağı üzerinedir."),
            item("good-bad-anthropocene", "İyi ve kötü Antroposen ayrımı hangi karşıtlığı vurgular?", 3, "C", ["Yalnızca doğa olaylarının insanlardan bağımsız ilerlemesini", "Bilimsel verilerin bütün etik tartışmaları gereksiz kılmasını", "İnsan etkisinin onarım veya tahribat yönünde kullanılabilmesini", "Jeolojik kayıtların insan faaliyetlerini hiç göstermemesini"], "İyi Antroposen, insanın teknolojiyi onarım ve adalet için kullanabileceğini; kötü Antroposen ise tahribatı sürdüren yaklaşımı simgeler."),
            item("just-transition", "Antroposen koşullarında sorumlu bir politika hangi bileşimi gerektirir?", 3, "D", ["Yalnızca ekonomik büyümeyi hızlandırmayı", "Bilimsel verileri karar süreçlerinden çıkarmayı", "Çevre sorunlarını yalnızca bireysel tercihlere bırakmayı", "Ekolojik onarımı bilim, yönetişim ve toplumsal adaletle birlikte ele almayı"], "İnsan etkisinin ölçeği, ekolojik onarımın bilimsel verilerle ve toplumsal adalet gözeten yönetişimle birlikte yürütülmesini gerektirir."),
        ],
    },
    {
        "readingTextId": "8b03aea9-c5c6-4a56-9cf7-a19059db14aa",
        "title": "Algı Yönetimi: Gerçeklik Nedir?",
        "difficultyLevel": 2,
        "questions": [
            item("framing-effect", "Çerçeveleme etkisi algı yönetiminde neyi gösterir?", 1, "A", ["Aynı bilginin sunuluş biçiminin değerlendirmeyi değiştirebilmesini", "Gerçeklerin hiçbir koşulda yorumlanamayacağını", "Bütün insanların aynı mesajı aynı anlamda algılamasını", "Bir mesajın yalnızca görsellerle aktarılabilmesini"], "Çerçeveleme, içerik aynı kalsa bile bilginin hangi bağlamda ve hangi kelimelerle sunulduğunun değerlendirmeyi etkileyebilmesidir."),
            item("confirmation-bias", "Doğrulama yanlılığı kişinin bilgiyi nasıl seçmesine yol açar?", 2, "B", ["Kendi inancıyla çelişen bütün kanıtları eşit incelemesine", "Önceden benimsediği görüşü destekleyen kanıtları daha kolay kabul etmesine", "Kaynakların güvenilirliğini her zaman tamamen reddetmesine", "Her görüşü aynı anda doğru saymasına"], "Doğrulama yanlılığında kişi, önceden benimsediği görüşle uyumlu bilgiyi aramaya ve çelişen kanıtı küçümsemeye daha yatkındır."),
            item("deepfake-check", "Bir deepfake iddiasını kontrol etmek için en uygun ilk adım hangisidir?", 1, "C", ["Videoyu yalnızca paylaşım sayısına göre doğru kabul etmek", "İçeriği kesip biçerek kaynağını gizlemek", "Güvenilir ve bağımsız kaynaklarda olayın izini sürmek", "Duygusal tepkiyi kanıt yerine kullanmak"], "Kaynağı, tarihi ve bağımsız doğrulamaları kontrol etmek; görüntünün tek başına sunduğu izlenimden daha güvenilir bir değerlendirme sağlar."),
            item("perception-manipulation", "Algı yönetimi hangi araçları birlikte kullanarak etkisini artırabilir?", 2, "D", ["Yalnızca teknik terimleri çoğaltarak", "Mesajı bütün bağlamından koparmadan", "Farklı görüşlere eşit alan açarak", "Tekrarlama, duygusal çağrışım ve seçilmiş bağlamı birleştirerek"], "Tekrarlanan mesajlar, güçlü duygular ve seçilmiş bağlam birlikte kullanıldığında kişinin dikkatini ve olay yorumunu yönlendirebilir."),
            item("media-literacy", "Eleştirel medya okuryazarlığı için en sağlam yaklaşım nedir?", 3, "A", ["Kaynağı, kanıtı, amacı ve alternatif açıklamaları birlikte sorgulamak", "En çok paylaşılan iddiayı otomatik olarak doğru saymak", "Yalnızca kendi görüşünü destekleyen hesapları izlemek", "Görüntü varsa yazılı açıklamayı gereksiz görmek"], "Eleştirel okuryazarlık, kaynağı ve kanıtı incelemeyi; mesajın amacını ve olası alternatif açıklamaları karşılaştırmayı gerektirir."),
        ],
    },
    {
        "readingTextId": "9213a128-3898-4e7a-a810-47d6f19f964c",
        "title": "Para Politikası: Görünmez Elin Yönü (Merkez Bankaları)",
        "difficultyLevel": 2,
        "questions": [
            item("open-market-expansion", "Merkez bankasının açık piyasa alımı yapması genellikle hangi etkiyi amaçlar?", 1, "B", ["Dolaşımdaki parayı azaltıp faizi yükseltmeyi", "Likiditeyi artırıp finansal koşulları gevşetmeyi", "Kredi talebini tamamen ortadan kaldırmayı", "Kamu bütçesini doğrudan özel şirketlere aktarmayı"], "Merkez bankası menkul kıymet aldığında sisteme likidite sağlayarak faizleri ve kredi koşullarını gevşetmeyi amaçlayabilir."),
            item("reserve-requirement", "Zorunlu karşılık oranının artırılması bankacılık sisteminde neye yol açabilir?", 2, "C", ["Bankaların sınırsız kredi vermesine", "Para politikasının etkisizleşmesine", "Kredi yaratma kapasitesinin ve likiditenin azalmasına", "Enflasyon beklentilerinin otomatik olarak yok olmasına"], "Bankalar daha büyük bir payı merkez bankasında tutmak zorunda kaldığında krediye dönüştürebilecekleri kaynak ve sistem likiditesi azalabilir."),
            item("inflation-expectations", "Enflasyon beklentileri para politikasında neden önemlidir?", 2, "D", ["Fiyatların geçmişte hiç değişmediğini gösterdiği için", "Faiz kararlarını bütün ülkelerde aynı yaptığı için", "Yalnızca borsa fiyatlarını belirlediği için", "Hane ve firmaların ücret, fiyat ve harcama kararlarını etkilediği için"], "Beklentiler yükselirse ücret, fiyat ve harcama kararları gelecekteki enflasyonu besleyebilir; bu yüzden merkez bankaları beklentileri yönetmeye çalışır."),
            item("central-bank-independence", "Merkez bankası bağımsızlığının temel amacı nasıl ifade edilir?", 3, "A", ["Kararların kısa vadeli siyasi baskılardan daha az etkilenmesini sağlamak", "Para politikasını yasama ve denetimden tamamen koparmak", "Ekonomik hedefleri yalnızca tek bir şirketin belirlemesini", "Faiz kararlarını piyasa verilerinden bağımsızlaştırmayı"], "Bağımsızlık, kararların günlük siyasi baskıdan uzak ve fiyat istikrarı gibi açık görevler doğrultusunda alınmasına katkı sağlar; hesap verebilirlik yine sürer."),
            item("recession-rate-cut", "Ekonomik durgunlukta politika faizinin düşürülmesi hangi sonucu hedefler?", 2, "B", ["Borçlanmayı pahalılaştırıp talebi bastırmayı", "Kredi koşullarını gevşetip harcama ve yatırımı desteklemeyi", "İhracat vergilerini otomatik olarak artırmayı", "Merkez bankasının bütün riskleri üstlenmesini"], "Faiz indirimi, borçlanma maliyetini düşürerek tüketim ve yatırım talebini desteklemeyi hedefleyebilir; etkisi ekonomik koşullara bağlıdır."),
        ],
    },
    {
        "readingTextId": "9ee5d169-5e73-48da-a933-bb146ca1985f",
        "title": "Sanat Terapisi: Yaratıcılıkla İyileşme",
        "difficultyLevel": 2,
        "questions": [
            item("process-product", "Sanat terapisinde süreç ile ürün arasındaki öncelik nasıl açıklanır?", 1, "C", ["Ortaya çıkan nesnenin estetik değeri her zaman birincildir", "Yalnızca profesyonel teknik başarı ölçülür", "Üretim süreci ve kişinin deneyimi, ürünün görünüşünden daha önemlidir", "Terapist eseri danışan adına tamamlar"], "Sanat terapisinde amaç sergilenebilir bir eser üretmek değil, üretim sürecinin duygu, farkındalık ve ilişki kurmayı desteklemesidir."),
            item("therapist-role", "Sanat terapistinin temel rolü aşağıdakilerden hangisidir?", 2, "D", ["Danışanın eserine tek bir doğru anlam vermek", "Sanatsal yeteneği puanlayarak tedavi kararı almak", "Her duyguyu çizimle ifade etmeyi zorunlu kılmak", "Güvenli ortam kurup süreci etik ve klinik açıdan rehberlemek"], "Terapist güvenli bir alan sağlar, süreci yapılandırır ve danışanın anlamlandırmasını kendi yorumuyla bastırmadan destekler."),
            item("no-art-skill", "Sanat terapisine katılım için neden profesyonel sanat becerisi gerekmez?", 1, "A", ["Çalışmanın hedefi estetik performans değil, ifade ve farkındalık sürecidir", "Terapist bütün çalışmayı danışan yerine yaptığı için", "Sanat eserleri hiçbir zaman değerlendirilmediği için", "Yaratıcılık yalnızca çocuklarda bulunduğu için"], "Sanat terapisi, teknik yarışma değildir; çizim, renk veya hareket aracılığıyla kişinin deneyimine erişmesini ve onu ifade etmesini amaçlar."),
            item("trauma-safe-expression", "Travma çalışmasında sanatsal ifade hangi yararı sağlayabilir?", 2, "B", ["Yaşanan olayı ayrıntılı biçimde yeniden yaşamayı zorunlu kılar", "Söze dökülmesi zor deneyimler için kontrollü ve sembolik bir kanal açabilir", "Terapistin danışan adına bütün kararları almasını sağlar", "Travmanın etkisini tek seansta tamamen ortadan kaldırır"], "Sembolik ve kontrollü üretim, kişinin söze dökmekte zorlandığı deneyimleri güvenli sınırlar içinde ele almasına yardımcı olabilir; tedavi garanti etmez."),
            item("modalities", "Sanat terapisinde hangi uygulama grubu metindeki yaklaşımı yansıtır?", 1, "C", ["Yalnızca yazılı sınav ve ezber çalışması", "Sadece tek bir resim tekniği ve değişmez yönerge", "Kolaj, resim, müzik veya heykel gibi farklı yaratıcı araçlar", "Danışanın yerine terapistin hazırladığı hazır çizimler"], "Sanat terapisi, kişinin ihtiyacına göre resim, kolaj, müzik, hareket veya heykel gibi farklı yaratıcı araçlardan yararlanabilir."),
        ],
    },
    {
        "readingTextId": "c4650166-8717-419f-82be-ee6f571c4335",
        "title": "Minimalizm: Az Çoktur Felsefesi",
        "difficultyLevel": 2,
        "questions": [
            item("minimalism-misread", "Minimalizm hakkında metnin düzelttiği yaygın yanlış anlama nedir?", 1, "D", ["Önemli seçimleri değerlerle ilişkilendirmek", "Gereksiz tüketimi sorgulamak", "Dikkati anlamlı etkinliklere ayırmak", "Bütün eşyaları ayrım yapmadan terk etmek"], "Minimalizm her şeyi bırakmak değil, sahip olunan ve yapılan şeyleri kişinin temel değerleriyle uyumlu biçimde seçmektir."),
            item("eudaimonic-choice", "Ödül yerine anlam ve gelişim sağlayan bir etkinlik hangi mutluluk yaklaşımına yakındır?", 2, "A", ["Eudaimonik mutluluğa", "Anlık haz odaklı mutluluğa", "Rastlantısal tüketime", "Kaçınma ve erteleme davranışına"], "Eudaimonik yaklaşım, anlam, amaç ve gelişimi; yalnızca anlık haz sağlayan hedonik tercihlerin ötesinde değerlendirir."),
            item("decluttering-rule", "Bir eşyayı azaltma kararında minimalizme uygun soru hangisidir?", 2, "B", ["Bu eşya en pahalı olan mı?", "Bu eşya değerlerime ve güncel ihtiyacıma gerçekten hizmet ediyor mu?", "Bu eşya başkalarında da var mı?", "Bu eşyayı saklamak için daha büyük bir ev alabilir miyim?"], "Değer ve güncel ihtiyaç sorusu, eşyayı yalnızca fiyatına veya başkalarının tercihine göre değil, kişinin yaşam amacına göre değerlendirmesine yardım eder."),
            item("consumer-attention", "Tüketim fazlasının dikkat üzerindeki etkisi nasıl açıklanır?", 2, "C", ["Seçenekler arttıkça karar vermek her zaman kolaylaşır", "Eşya sayısı arttıkça zihinsel yük kesinlikle azalır", "Fazla eşya ve seçenek, bakım ve karar yükünü artırabilir", "Tüketim davranışı dikkatle hiçbir ilişki taşımaz"], "Fazla eşya, düzenleme ve karar verme için daha çok dikkat gerektirebilir; bu da kişinin önemli işlere ayırdığı zihinsel alanı daraltabilir."),
            item("sustainable-minimalism", "Minimalizmin çevresel katkısı hangi mekanizmayla ortaya çıkabilir?", 3, "D", ["Her ürünü daha hızlı yenileyerek", "Tüketim miktarını artırıp depolama alanını büyüterek", "İhtiyaç dışı ürünleri sürekli değiştirerek", "Gereksiz tüketimi ve buna bağlı kaynak-atık akışını azaltarak"], "Daha az ve daha bilinçli tüketim, üretim için gereken kaynakları ve kullanım sonrası atığı azaltma potansiyeli taşır."),
        ],
    },
    {
        "readingTextId": "d580e67a-90f8-457e-8582-11aca491c632",
        "title": "Bilincin Zor Problemi (Qualia) ve Yapay Zeka",
        "difficultyLevel": 3,
        "questions": [
            item("hard-problem", "Chalmers'ın 'zor problem' ifadesi hangi soruya odaklanır?", 1, "A", ["Fiziksel süreçlerin neden öznel bir deneyim ürettiğine", "Beynin hangi bölgesinin en büyük olduğuna", "Bir bilgisayarın ne kadar hızlı hesap yaptığına", "Davranışların neden gözlenemediğine"], "Zor problem, sinirsel ve fiziksel süreçlerin nasıl olup da kırmızıyı görmek gibi öznel bir deneyime dönüştüğünü sorar."),
            item("philosophical-zombie", "Felsefi zombi düşünce deneyi hangi ayrımı görünür kılar?", 2, "B", ["Beden ile çevre arasındaki farkı", "Dışarıdan aynı davranışı gösterme ile içsel deneyim sahibi olma arasındaki farkı", "Zekâ ile hafıza arasındaki biyolojik farkı", "Duygu ile dil arasındaki kültürel farkı"], "Felsefi zombi, bilinçliymiş gibi davranan fakat içsel deneyimi olmayan bir varlığın mantıksal olarak düşünülebileceğini tartışır."),
            item("turing-limit", "Turing testinin bilinç tartışmasındaki temel sınırlılığı nedir?", 2, "C", ["Dilsel davranışı hiç ölçememesi", "İnsanların makineyle iletişim kurmasını engellemesi", "Başarılı davranışı içsel öznel deneyimin kesin kanıtı sayamaması", "Yalnızca biyolojik beyinleri test etmesi"], "Turing testi konuşma davranışını ölçer; bir sistemin ikna edici yanıt vermesi, gerçekten öznel deneyim yaşadığını kesin olarak göstermez."),
            item("ai-experience", "Yapay zekâda insan benzeri davranış gözlendiğinde hangi soru açık kalır?", 3, "D", ["Sistemin veri işleyip işlemediği", "Sistemin hiçbir algoritma kullanmadığı", "Sistemin insan dilini tanıyıp tanımadığı", "Bu davranışın ardında gerçek bir öznel deneyim bulunup bulunmadığı"], "Davranışsal başarı ile bilinçli deneyim aynı şey değildir; yapay sistemlerin gerçekten deneyim yaşayıp yaşamadığı çözümlenmemiştir."),
            item("qualia-first-person", "Qualia kavramı bilinç tartışmasına hangi bakış açısını ekler?", 1, "A", ["Deneyimin kişinin kendi açısından nasıl hissedildiğini", "Yalnızca dışarıdan ölçülebilen refleksleri", "Bir algoritmanın işlem süresini", "Bir makinenin fiziksel ağırlığını"], "Qualia, acı, renk veya tat gibi deneyimlerin birinci kişi açısından nasıl hissettirdiğini ifade eder."),
        ],
    },
    {
        "readingTextId": "de4d10fc-7217-46be-8216-b63f6bcc680d",
        "title": "21. Yüzyıl Girişimciliği: Fikirden Unicorn'a",
        "difficultyLevel": 2,
        "questions": [
            item("mvp-purpose", "Bir MVP'nin girişim için temel amacı nedir?", 1, "B", ["Ürünü bütün özellikleriyle ilk günden tamamlamak", "Varsayımları sınırlı kaynakla test edip kullanıcı öğrenmesi sağlamak", "Yatırımcıya yalnızca yüksek değerleme göstermek", "Pazarlama bütçesini ürün geliştirmeden önce tüketmek"], "Minimum uygulanabilir ürün, temel varsayımları gerçek kullanıcılarla test etmek ve geri bildirimle öğrenmek için kullanılır."),
            item("product-market-fit", "Ürün-pazar uyumu hangi durumu ifade eder?", 2, "C", ["Kurucunun ürünü kişisel olarak beğenmesini", "Yatırımcının fikri ilginç bulmasını", "Belirli bir kullanıcı grubunun ürünü düzenli kullanıp değerli bulmasını", "Ürünün rakiplerinin hiç olmamasını"], "Ürün-pazar uyumu, belirli kullanıcıların ürünü tekrar tekrar kullanması ve onun önemli bir ihtiyacı karşıladığını göstermesidir."),
            item("venture-capital-tradeoff", "Girişim sermayesi almanın temel karşılığı hangi seçenekte doğru verilmiştir?", 2, "D", ["Kurucunun hiçbir karar sorumluluğu taşımaması", "Geri ödeme gerektirmeyen ve koşulsuz para", "Pazar riskinin yatırımcı tarafından tamamen yok edilmesi", "Büyüme kaynağı karşılığında ortaklık ve yönetişim paylaşımı"], "Girişim sermayesi hızlı büyümeyi finanse edebilir; karşılığında kurucu ortaklık payı ve bazı yönetişim haklarını paylaşır."),
            item("pivot", "Girişimin pivot etmesi ne anlama gelir?", 3, "A", ["Öğrenilen veriye göre ürün, müşteri veya iş modelinde yön değiştirmek", "Her koşulda ilk fikre bağlı kalmak", "Yalnızca logoyu ve şirket adını değiştirmek", "Ürünü test etmeden bütün pazarlara açılmak"], "Pivot, kullanıcı geri bildirimi veya pazar verisi ilk varsayımları doğrulamadığında stratejik yönü öğrenmeye göre değiştirmektir."),
            item("network-effect", "Ağ etkisi güçlü bir dijital üründe değer nasıl artabilir?", 2, "B", ["Kullanıcı sayısı arttıkça her kullanıcı için faydanın azalmasıyla", "Kullanıcı sayısı arttıkça bağlantılar ve kullanım değeri çoğalabildiği için", "Ürün yalnızca tek bir kişiye hizmet verdiği için", "Büyüme arttıkça bütün işletme maliyetleri sıfırlandığı için"], "Ağ etkisinde daha çok kullanıcı, diğer kullanıcıların ulaşabileceği bağlantıları veya içerikleri artırarak ürünün toplam faydasını büyütebilir."),
        ],
    },
    {
        "readingTextId": "df87d7e9-8141-40c8-84a6-0e1778c68aa2",
        "title": "Küresel Ticaret Savaşları: Refah mı Güç mü?",
        "difficultyLevel": 2,
        "questions": [
            item("tariff-cost", "Ticaret savaşında gümrük tarifelerinin doğrudan sonuçlarından biri nedir?", 1, "C", ["İthal ürünlerin her ülkede ucuzlaması", "Misilleme ihtimalinin tamamen ortadan kalkması", "İthal ürünlerin fiyatını ve karşı ülkenin misilleme riskini artırması", "Tedarik zincirlerinin hiçbir değişiklik yaşamaması"], "Tarife ithal ürünün maliyetini yükseltebilir; karşı ülke de misilleme yaparsa ticaret hacmi ve fiyatlar etkilenebilir."),
            item("relative-absolute-gains", "Mutlak ve göreli kazanç ayrımı ticaret politikasında hangi farkı anlatır?", 3, "D", ["Mutlak kazanç yalnızca para birimini, göreli kazanç yalnızca vergiyi ölçer", "Mutlak kazanç üretimi; göreli kazanç tüketimi hiçbir zaman dikkate almaz", "Mutlak kazanç kısa vadeyi, göreli kazanç uzun vadeyi zorunlu olarak gösterir", "Bir tarafın toplam kazanımı ile diğerine göre konumunun ayrı değerlendirilebilmesini"], "Mutlak kazanç toplam refah veya üretimdeki artışı, göreli kazanç ise bir tarafın diğerine kıyasla avantajını ayrı ayrı değerlendirmeyi sağlar."),
            item("friend-shoring-tradeoff", "Friend-shoring yaklaşımının temel ödünleşimi nedir?", 2, "A", ["Tedarik güvenliğini artırırken maliyeti veya seçenekleri azaltabilmesi", "Tedarik zincirlerini bütün ülkelerden bağımsız hale getirmesi", "Siyasi ilişkileri ticari kararlardan tamamen ayırması", "Her ürünü en düşük maliyetli ülkeden alma zorunluluğu"], "Dost veya güvenilir ülkelerle tedarik kurmak şoklara dayanıklılığı artırabilir; ancak daha pahalı veya daha az çeşitlendirilmiş olabilir."),
            item("tariff-burden", "Tarifelerin ekonomik yükünü çoğunlukla kimler taşır?", 2, "B", ["Yalnızca tarife koyan hükümet", "İthalatçı, işletme ve tüketiciler arasında farklı biçimlerde", "Sadece yabancı üreticiler ve hiçbir yerli aktör değil", "Yalnızca merkez bankası"], "Tarifeler ithalat maliyetini artırır; bu yük daha yüksek fiyat, düşük kâr veya daha düşük talep yoluyla işletme ve tüketicilere dağılabilir."),
            item("trade-war-winner", "Ticaret savaşlarının 'kazananı' için metnin dengeli sonucu hangisidir?", 3, "C", ["Her iki tarafın da bütün kayıpları telafi etmesi", "Tarife uygulayan tarafın her durumda refah kazanması", "Tarafların çoğu zaman daha az kaybetmeye çalışması ve net refah kaybı yaşaması", "Tedarik zincirlerinin çatışmadan her zaman güçlenerek çıkması"], "Ticaret savaşı tarafların pazarlık gücünü artırabilir, ancak misilleme ve verim kaybı nedeniyle toplam refah çoğu zaman düşer."),
        ],
    },
    {
        "readingTextId": "f5b607f5-c508-493f-aa91-a2b8941e18ec",
        "title": "Mitolojinin Günümüz Dünyasına Yansımaları: Kahramanın Sonsuz Yolculuğu",
        "difficultyLevel": 2,
        "questions": [
            item("call-to-adventure", "Kahramanın yolculuğunda 'maceraya çağrı' aşaması neyi başlatır?", 1, "D", ["Kahramanın değişimi reddedip eski düzenine dönmesini", "Hikâyenin bütün çatışmalarının sona ermesini", "Kahramanın ödülü baştan elde etmesini", "Kahramanı alışılmış dünyasının dışına çıkaracak ilk daveti"], "Maceraya çağrı, kahramanı alışılmış düzeninden çıkaran ve dönüşüm gerektiren olay veya davettir."),
            item("threshold-guardian", "Eşik bekçisi ve sınavlar kahramanın yolculuğunda hangi işleve sahiptir?", 2, "A", ["Kahramanın değişim için hazırlığını ve kararlılığını sınamak", "Kahramanın bütün sorumluluklardan kaçmasını sağlamak", "Hikâyenin sonunda ödülü açıklamak", "Kahramanın geçmişini tamamen silmek"], "Eşik ve sınavlar, kahramanın bilinmeyen dünyaya geçmek için korku ve engellerle yüzleşmesini sağlar."),
            item("shadow-archetype", "Gölge arketipi mitolojik anlatılarda çoğunlukla neyi temsil eder?", 2, "B", ["Kahramanın dış görünüşünü ve sosyal statüsünü", "Kişinin bastırdığı korku, çatışma veya kabul etmekte zorlandığı yönleri", "Toplumun yalnızca hukuki kurallarını", "Hikâyedeki bütün komik karakterleri"], "Gölge, kahramanın veya toplumun kabul etmekte zorlandığı korku, arzu ve çatışmalı yönlerin simgesel karşılığı olabilir."),
            item("modern-superhero", "Modern süper kahraman anlatıları kahramanın yolculuğundan nasıl yararlanır?", 3, "C", ["Kahramanı hiçbir sınav yaşamadan doğrudan başarılı kılarak", "Mitolojik sembolleri bütün anlamlarından kopararak", "Çağdaş çatışmaları çağrı, sınav, dönüşüm ve dönüş aşamalarıyla yapılandırarak", "Hikâyeyi yalnızca tarihî tanrılarla sınırlayarak"], "Süper kahraman anlatıları, güncel teknolojik veya toplumsal çatışmaları tanıdık dönüşüm aşamaları ve arketiplerle anlatabilir."),
            item("myth-function", "Mitlerin günümüzde incelenmesinin temel katkısı nedir?", 3, "D", ["Tarihsel olayların her ayrıntısını bilimsel olarak kanıtlamak", "Farklı kültürleri tek bir kalıba zorlamak", "Bireysel deneyimi toplumsal değerlerden ayırmak", "İnsanların ortak kaygılarını, değerlerini ve dönüşüm biçimlerini anlamaya yardım etmek"], "Mitolojik analiz, farklı kültürlerde tekrarlanan sembol ve dönüşüm biçimlerini izleyerek ortak insan deneyimleriyle kültürel değerler arasındaki ilişkiyi görünür kılar."),
        ],
    },
]


def load_catalog(path: Path) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    with path.open(encoding="utf-8") as handle:
        for line in handle:
            row = json.loads(line)
            result[row["textId"]] = row
    return result


def normalize(catalog: dict[str, dict[str, Any]]) -> list[dict[str, Any]]:
    output: list[dict[str, Any]] = []
    for text in TEXTS:
        existing = catalog.get(text["readingTextId"])
        if existing is None:
            raise ValueError(f"reading text is missing from export: {text['readingTextId']}")
        if existing["title"] != text["title"]:
            raise ValueError(f"title mismatch for {text['readingTextId']}")
        if existing.get("isActive") is False:
            raise ValueError(f"target reading text is inactive: {text['readingTextId']}")
        if len(existing["questions"]) != 3:
            raise ValueError(f"target reading text is no longer low density: {text['readingTextId']}")
        existing_ids = {question["questionId"] for question in existing["questions"]}
        max_order = max((question["orderIndex"] for question in existing["questions"]), default=0)
        questions: list[dict[str, Any]] = []
        for offset, question in enumerate(text["questions"], start=1):
            question_id = str(uuid.uuid5(NAMESPACE, f"reading-question-supplement-v1:{text['readingTextId']}:{question['questionKey']}"))
            if question_id in existing_ids:
                raise ValueError(f"supplement id already exists in catalog: {question_id}")
            row = dict(question)
            row["questionId"] = question_id
            row["readingTextId"] = text["readingTextId"]
            row["orderIndex"] = max_order + offset
            row["difficultyLevel"] = text["difficultyLevel"]
            apply_option_overrides(row)
            rebalance_answer_position(row)
            questions.append(row)
        output.append({"readingTextId": text["readingTextId"], "title": text["title"], "difficultyLevel": text["difficultyLevel"], "questions": questions})
    return output


def word_count(value: str) -> int:
    return len(re.findall(r"\S+", value))


OPTION_OVERRIDES: dict[str, dict[str, str]] = {
    "local-renewable-potential": {"optionD": "Kritik maden işleme tekelini korumayı"},
    "technology-dependence": {"optionA": "Güneş ışığının yalnızca dışarıdan satın alınabilmesiyle"},
    "green-paradox": {"optionD": "Elektrikli araçların çevresel etkilerinin yalnızca egzoz salımıyla sınırlı sanılması"},
    "life-extension-dilemma": {"optionA": "Yaşlanmanın bütün etik ve toplumsal sorunları kendiliğinden bitirmesi"},
    "posthuman-transhuman": {"optionD": "Biyolojik araştırmaları yalnızca insan bedeninin tedavi amaçlarıyla sınırlı tutması"},
    "cyborg-change": {"optionA": "İnsan bedeninin ve zihninin yalnızca biyolojik kabul edilmesi"},
    "singularity-choice": {"optionB": "Teknolojik ilerlemeyi insan kararlarını tamamen dışlayacak biçimde hızlandırmayı"},
    "milgram-focus": {"optionA": "Yaratıcı problem çözme becerisini"},
    "transformational-leadership": {"optionA": "Yalnızca görev tamamlandığında çalışana maddi ödül vermeyi"},
    "social-identity": {"optionD": "Grup sınırlarını yalnızca biyolojik farklılıklarla açıklayarak"},
    "groupthink-cost": {"optionB": "Azınlık görüşlerini dikkatle dinleyip bütün seçenekleri tartışması"},
    "dissent-practice": {"optionB": "Muhalif görüşleri toplantı içinde açıkça dinlemeyi reddetmesini"},
    "rotation-evidence": {"optionC": "Yıldızların yalnızca ışıkla hareket ettiğini kesin olarak kanıtlar"},
    "invisible-matter": {"optionC": "Gökadalardan çok daha hızlı hareket ettiği için görünmemesi"},
    "psychological-safety": {"optionD": "Eleştiriyi ekip içinde kişisel ve yıkıcı bir saldırı olarak görmeyi"},
    "servant-leadership": {"optionA": "Liderin kendi görünürlüğünü ve kurumsal statüsünü sürekli artırmak"},
    "dark-triad-risk": {"optionD": "Hataları güvenli öğrenme fırsatı olarak ele almayı"},
    "leadership-management": {"optionA": "İkisi de aynı beceridir ve birbirinin yerine geçer"},
    "lorenz-butterfly": {"optionA": "Büyük sonuçların yalnızca büyük ve görünür temel nedenlerden doğması"},
    "feedback-amplify": {"optionA": "Davranışların sonuçları zamanla yeni davranışları tetikleyebilir"},
    "prediction-limit": {"optionC": "Bütün değişkenlerin birbirinden tamamen ve kesin olarak bağımsız olması"},
    "adaptive-policy": {"optionA": "Tek bir tahmine bağlı kalıp değişmez bir planı her koşulda sürdürmek"},
    "nonlinear-difference": {"optionA": "Hiçbir girdinin sistem sonucunu hiçbir koşulda ve ölçüde etkilememesi"},
    "identity-logo": {"optionA": "Logo markanın bütün müşteri deneyimini tek başına oluşturur", "optionC": "Logo görsel işarettir; kimlik anlam ve vaat bütünüdür"},
    "crisis-golden-hour": {"optionA": "Sorun kamuoyunda kendiliğinden tamamen unutulana kadar sessizce beklemeyi", "optionB": "İlk aşamada hızlı ve sorumluluk alan açıklama yapmayı"},
    "emotional-value": {"optionD": "Marka ile müşteri arasındaki güven ilişkisini tamamen ortadan kaldırır"},
    "touchpoint-consistency": {"optionA": "Her temas noktasında birbirinden farklı marka kimliği yaratmak"},
    "promise-experience": {"optionB": "Her müşteri için farklı, çelişkili ve öngörülemez bir deneyim sunmalıdır"},
    "anthropocene-marker": {"optionC": "Kıtasal kayaların milyonlarca yıl boyunca doğal aşınması"},
    "geologic-debate": {
        "optionA": "İnsan etkisinin Dünya'da hiçbir bölgede ölçülememesi",
        "optionB": "Antroposen için başlangıç tarihinin hangi kanıtla seçileceği",
        "optionC": "İklim değişikliğinin yalnızca tek ve küçük bir şehirde görülmesi",
    },
    "great-acceleration": {"optionB": "Orta Çağ'da tarımsal üretimin tek ve sınırlı bir bölgede yükselmesini"},
    "good-bad-anthropocene": {"optionB": "Bilimsel verilerin bütün ahlaki tartışmaları bütünüyle gereksiz kılmasını"},
    "just-transition": {
        "optionC": "Çevre sorunlarını yalnızca bireysel tercihlerle çözmeye çalışma yaklaşımını",
        "optionD": "Ekolojik onarımda bilim, yönetişim ve adaleti birlikte gözetmeyi",
    },
    "confirmation-bias": {"optionA": "Kendi inancıyla çelişen kanıtları eşit, tarafsız ve dikkatle incelemesini her zaman"},
    "framing-effect": {"optionC": "Bütün insanların aynı mesajı aynı anlamda ve koşulda algılamasını her zaman"},
    "perception-manipulation": {"optionC": "Tekrarlama, duygu ve seçilmiş bağlamı birlikte etkili biçimde kullanarak"},
    "media-literacy": {"optionC": "Yalnızca kendi görüşünü destekleyen hesapları ve kanıtları sürekli izlemek"},
    "inflation-expectations": {"optionD": "Ücret, fiyat ve harcama kararlarını etkilemesi"},
    "central-bank-independence": {"optionB": "Para politikasını yasama ve demokratik denetim mekanizmalarından tamamen koparmayı"},
    "recession-rate-cut": {"optionA": "Borçlanma maliyetini artırıp toplam talebi iyice bastırmayı"},
    "process-product": {"optionA": "Ortaya çıkan nesnenin estetik değerini her zaman tek ve birincil ölçüt saymayı"},
    "therapist-role": {"optionA": "Danışanın eserine tek, değişmez ve tartışmasız bir doğru anlam vermeyi"},
    "no-art-skill": {
        "optionA": "Çalışmanın hedefi estetik performans değil, ifade ve farkındalıktır",
        "optionB": "Terapistin bütün yaratıcı çalışmayı danışan yerine kendisinin yapması",
    },
    "trauma-safe-expression": {
        "optionA": "Yaşanan olayı ayrıntılı biçimde yeniden yaşamayı kişi için zorunlu tutabilir",
        "optionB": "Söze dökülmesi zor deneyimler için güvenli bir ifade kanalı açabilir",
    },
    "modalities": {"optionB": "Yalnızca tek bir resim tekniği ve değişmez bir yönerge kullanmak"},
    "minimalism-misread": {"optionA": "Önemli seçimleri kişinin temel değerleriyle ilişkilendirmek"},
    "consumer-attention": {"optionA": "Seçenekler arttıkça karar vermek her zaman ve kesinlikle kolaylaşır"},
    "sustainable-minimalism": {"optionB": "Tüketim miktarını artırıp depolama alanını sürekli olarak büyütmek"},
    "philosophical-zombie": {
        "optionA": "Beden ile çevre arasındaki temel ve ölçülebilir farkı",
        "optionB": "Dışarıdan aynı davranış ile içsel deneyim arasındaki ayrımı",
    },
    "turing-limit": {"optionB": "İnsanların makineyle doğal ve anlamlı iletişim kurmasını engellemesi"},
    "ai-experience": {"optionC": "Sistemin insan dilini her bağlamda uygun biçimde tanıyıp tanımadığı"},
    "qualia-first-person": {"optionB": "Yalnızca dışarıdan ölçülebilen bedensel otomatik refleksleri"},
    "eudaimonic-choice": {"optionA": "Eudaimonik ve anlam odaklı mutluluğa", "optionD": "Anlık haz odaklı kısa süreli mutluluğa"},
    "decluttering-rule": {"optionD": "Bu eşyayı saklamak için daha büyük ve pahalı bir ev satın alabilir miyim?"},
    "hard-problem": {"optionD": "Davranışların neden dışarıdan gözlenebilir hale gelemediğine"},
    "philosophical-zombie": {"optionA": "Beden ile çevre arasındaki temel, ölçülebilir ve biyolojik farkı açıklama biçimini bütünüyle"},
    "mvp-purpose": {"optionA": "Ürünü bütün temel özellikleriyle ilk günden eksiksiz ve kusursuz tamamlamak"},
    "product-market-fit": {"optionA": "Kurucunun ürünü kişisel olarak çok beğenip herkese ısrarla ve sürekli önermesi"},
    "venture-capital-tradeoff": {"optionC": "Pazar riskinin yatırımcı tarafından tamamen ve kalıcı biçimde yok edilmesi"},
    "pivot": {"optionB": "Daima ilk fikre bağlı kalıp öğrenilen veriye göre değişimi sürekli reddetmek"},
    "network-effect": {"optionA": "Kullanıcı sayısı arttıkça her kişi için faydanın zamanla ve sürekli azalmasıyla"},
    "tariff-cost": {"optionA": "İthal ürünlerin her pazarda ve her koşulda sürekli olarak ve hızla ucuzlaması"},
    "relative-absolute-gains": {"optionA": "Mutlak kazanç yalnızca para birimini, göreli kazanç ise yalnızca vergiyi ve fiyatları ölçer"},
    "friend-shoring-tradeoff": {"optionB": "Tedarik zincirlerini bütün ülkelerden bağımsız ve tamamen güvenli hale getirmesi"},
    "tariff-burden": {"optionC": "Yalnızca yabancı üreticiler ve hiçbir yerli ekonomik aktör değil"},
    "trade-war-winner": {
        "optionA": "Her iki tarafın da bütün ekonomik kayıpları tamamen telafi etmesi",
        "optionC": "Tarafların daha az kaybetmeye çalışması ve net refah kaybı",
    },
    "call-to-adventure": {"optionA": "Kahramanın değişimi reddedip eski düzenine kesin olarak dönmesini"},
    "threshold-guardian": {"optionB": "Kahramanın bütün sorumluluklardan ve sınavlardan kaçmasını sağlamak"},
    "shadow-archetype": {"optionA": "Kahramanın dış görünüşünü ve toplum içindeki sosyal statüsünü belirlemek"},
    "modern-superhero": {"optionA": "Kahramanı hiçbir zorlu sınav yaşamadan doğrudan başarılı figüre hızla dönüştürerek"},
    "myth-function": {"optionA": "Tarihsel olayların bütün ayrıntılarını bilimsel yöntemle kesin, eksiksiz ve tarafsız kanıtlamak"},
}


def apply_option_overrides(question: dict[str, Any]) -> None:
    for name, value in OPTION_OVERRIDES.get(question["questionKey"], {}).items():
        question[name] = value


# The existing three questions in these six texts use A/B/C, B/C/A, B/A/B,
# B/A/C, B/A/C and B/C/D respectively.  These target sequences complete each
# eight-question set with two occurrences of every answer position.
TARGET_ANSWER_KEYS = {
    "framing-effect": "D",
    "confirmation-bias": "A",
    "deepfake-check": "B",
    "perception-manipulation": "C",
    "media-literacy": "D",
    "open-market-expansion": "D",
    "reserve-requirement": "A",
    "inflation-expectations": "B",
    "central-bank-independence": "C",
    "recession-rate-cut": "D",
    "trauma-safe-expression": "C",
    "modalities": "D",
    "hard-problem": "D",
    "philosophical-zombie": "A",
    "turing-limit": "B",
    "ai-experience": "C",
    "qualia-first-person": "D",
    "mvp-purpose": "D",
    "product-market-fit": "A",
    "venture-capital-tradeoff": "B",
    "pivot": "C",
    "network-effect": "D",
    "tariff-cost": "A",
    "relative-absolute-gains": "B",
    "friend-shoring-tradeoff": "C",
    "tariff-burden": "D",
    "trade-war-winner": "A",
}


def rebalance_answer_position(question: dict[str, Any]) -> None:
    target = TARGET_ANSWER_KEYS.get(question["questionKey"])
    if target is None or target == question["correctAnswer"]:
        return
    names = ["optionA", "optionB", "optionC", "optionD"]
    current_index = "ABCD".index(question["correctAnswer"])
    target_index = "ABCD".index(target)
    current_name = names[current_index]
    target_name = names[target_index]
    question[current_name], question[target_name] = question[target_name], question[current_name]
    question["correctAnswer"] = target


def sql_literal(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def quote_uuid(value: str) -> str:
    return f"{sql_literal(value)}::uuid"


def build_sql(rows: list[dict[str, Any]]) -> str:
    values: list[str] = []
    ids: list[str] = []
    for text in rows:
        for question in text["questions"]:
            ids.append(question["questionId"])
            values.append(
                "(" + ", ".join(
                    [
                        quote_uuid(question["questionId"]),
                        quote_uuid(question["readingTextId"]),
                        sql_literal(question["questionText"]),
                        str(question["type"]),
                        str(question["bloomLevel"]),
                        str(question["difficultyLevel"]),
                        sql_literal(question["explanation"]),
                        sql_literal(question["optionA"]),
                        sql_literal(question["optionB"]),
                        sql_literal(question["optionC"]),
                        sql_literal(question["optionD"]),
                        sql_literal(question["correctAnswer"]),
                        str(question["orderIndex"]),
                    ]
                ) + ")"
            )
    value_sql = ",\n        ".join(values)
    id_sql = ", ".join(quote_uuid(value) for value in ids)
    return f"""-- Generated by generate_low_density_supplement.py on {date.today().isoformat()}.
-- Adds five questions to each of the 16 currently three-question reading texts.
-- Safe to rerun: both branches use the deterministic question ids and skip existing rows.
BEGIN;

CREATE TEMP TABLE reading_question_supplement (
    id uuid NOT NULL,
    reading_text_id uuid NOT NULL,
    question_text text NOT NULL,
    type integer NOT NULL,
    bloom_level integer NOT NULL,
    difficulty_level integer NOT NULL,
    explanation text,
    option_a text NOT NULL,
    option_b text NOT NULL,
    option_c text NOT NULL,
    option_d text NOT NULL,
    correct_answer text NOT NULL,
    order_index integer NOT NULL
) ON COMMIT DROP;

INSERT INTO reading_question_supplement
    (id, reading_text_id, question_text, type, bloom_level, difficulty_level, explanation,
     option_a, option_b, option_c, option_d, correct_answer, order_index)
VALUES
        {value_sql};

DO $$
BEGIN
    IF to_regclass('public."ReadingQuestions"') IS NOT NULL THEN
        INSERT INTO public."ReadingQuestions"
            ("Id", "ReadingTextId", "QuestionText", "Type", "BloomLevel", "DifficultyLevel",
             "Explanation", "OptionA", "OptionB", "OptionC", "OptionD", "CorrectAnswer",
             "OrderIndex", "CreatedAt", "CreatedBy", "IsDeleted")
        SELECT id, reading_text_id, question_text, type, bloom_level, difficulty_level,
               explanation, option_a, option_b, option_c, option_d, correct_answer,
               order_index, CURRENT_TIMESTAMP, {sql_literal(LEGACY_ACTOR)}::uuid, FALSE
        FROM reading_question_supplement source
        WHERE NOT EXISTS (
            SELECT 1 FROM public."ReadingQuestions" target WHERE target."Id" = source.id
        );
    END IF;

    IF to_regclass('speed_reading.reading_questions') IS NOT NULL THEN
        INSERT INTO speed_reading.reading_questions
            (id, reading_text_id, question_text, type, bloom_level, difficulty_level,
             explanation, option_a, option_b, option_c, option_d, correct_answer,
             order_index, created_at, created_by, is_deleted)
        SELECT id, reading_text_id, question_text, type, bloom_level, difficulty_level,
               explanation, option_a, option_b, option_c, option_d, correct_answer,
               order_index, CURRENT_TIMESTAMP, {sql_literal(ACTOR)}, FALSE
        FROM reading_question_supplement source
        WHERE NOT EXISTS (
            SELECT 1 FROM speed_reading.reading_questions target WHERE target.id = source.id
        );
    END IF;

    IF to_regclass('public."ReadingQuestions"') IS NULL
       AND to_regclass('speed_reading.reading_questions') IS NULL THEN
        RAISE EXCEPTION 'Neither supported reading question table exists';
    END IF;
END $$;

COMMIT;
"""


def build_rollback(rows: list[dict[str, Any]]) -> str:
    ids = [question["questionId"] for text in rows for question in text["questions"]]
    id_sql = ", ".join(quote_uuid(value) for value in ids)
    return f"""-- Generated by generate_low_density_supplement.py on {date.today().isoformat()}.
-- Soft-deletes only the 80 deterministic supplement rows created by this pack.
BEGIN;
DO $$
BEGIN
    IF to_regclass('public."ReadingQuestions"') IS NOT NULL THEN
        UPDATE public."ReadingQuestions"
        SET "IsDeleted" = TRUE,
            "DeletedAt" = CURRENT_TIMESTAMP,
            "DeletedBy" = {sql_literal(LEGACY_ACTOR)}::uuid,
            "UpdatedAt" = CURRENT_TIMESTAMP,
            "UpdatedBy" = {sql_literal(LEGACY_ACTOR)}::uuid
        WHERE "Id" IN ({id_sql});
    END IF;
    IF to_regclass('speed_reading.reading_questions') IS NOT NULL THEN
        UPDATE speed_reading.reading_questions
        SET is_deleted = TRUE,
            deleted_at = CURRENT_TIMESTAMP,
            deleted_by = {sql_literal(ACTOR)},
            updated_at = CURRENT_TIMESTAMP,
            updated_by = {sql_literal(ACTOR)}
        WHERE id IN ({id_sql});
    END IF;
END $$;
COMMIT;
"""


def audit(rows: list[dict[str, Any]], catalog: dict[str, dict[str, Any]] | None = None) -> dict[str, Any]:
    answer_counts: dict[str, dict[str, int]] = {}
    final_answer_counts: dict[str, dict[str, int]] = {}
    flags: list[dict[str, Any]] = []
    seen_keys: set[str] = set()
    for text in rows:
        answers = Counter(question["correctAnswer"] for question in text["questions"])
        answer_counts[text["readingTextId"]] = dict(sorted(answers.items()))
        if catalog is not None:
            final_answers = Counter(
                question["correctAnswer"]
                for question in catalog[text["readingTextId"]]["questions"] + text["questions"]
            )
            final_answer_counts[text["readingTextId"]] = dict(sorted(final_answers.items()))
        for question in text["questions"]:
            key = f"{text['readingTextId']}:{question['questionKey']}"
            if key in seen_keys:
                flags.append({"kind": "duplicateKey", "key": key})
            seen_keys.add(key)
            options = [question["optionA"], question["optionB"], question["optionC"], question["optionD"]]
            correct_index = "ABCD".index(question["correctAnswer"])
            for length_kind, lengths in (
                ("word", [len(re.findall(r"\S+", option)) for option in options]),
                ("character", [len(option) for option in options]),
            ):
                if lengths[correct_index] == max(lengths) and lengths.count(max(lengths)) == 1:
                    flags.append({"kind": "uniqueLongestCorrect", "lengthKind": length_kind, "textId": text["readingTextId"], "questionKey": question["questionKey"]})
                if lengths[correct_index] == min(lengths) and lengths.count(min(lengths)) == 1:
                    flags.append({"kind": "uniqueShortestCorrect", "lengthKind": length_kind, "textId": text["readingTextId"], "questionKey": question["questionKey"]})
    report = {"questionCount": sum(len(text["questions"]) for text in rows), "textCount": len(rows), "answerCounts": answer_counts, "flags": flags}
    if catalog is not None:
        report["finalQuestionCounts"] = {text["readingTextId"]: len(catalog[text["readingTextId"]]["questions"]) + len(text["questions"]) for text in rows}
        report["finalAnswerCounts"] = final_answer_counts
    return report


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", type=Path, default=DEFAULT_CATALOG)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    catalog = load_catalog(args.catalog)
    rows = normalize(catalog)
    report = audit(rows, catalog)
    if report["flags"]:
        raise SystemExit(json.dumps(report, ensure_ascii=False, indent=2))
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / "low-density-supplement.json").write_text(json.dumps({"version": "v1", "generatedAt": date.today().isoformat(), "texts": rows}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (args.output / "low-density-supplement.sql").write_text(build_sql(rows), encoding="utf-8")
    (args.output / "low-density-supplement.rollback.sql").write_text(build_rollback(rows), encoding="utf-8")
    (args.output / "low-density-supplement.audit.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()

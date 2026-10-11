import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { pathToFileURL } from 'node:url';

const file = name => new URL(name, import.meta.url);
const read = name => JSON.parse(readFileSync(file(name), 'utf8'));
const words = text => text.trim().split(/\s+/u).length;
const uuid = key => {
  const hash = createHash('sha256').update(`skimming-v1:${key}`).digest('hex');
  return `${hash.slice(0,8)}-${hash.slice(8,12)}-5${hash.slice(13,16)}-8${hash.slice(17,20)}-${hash.slice(20,32)}`;
};
// Pilot product settings, not validated age norms or full-reading WPM targets.
const profiles = {
  child: { font: 24, bands: [[200,280,10,75],[280,360,12,90],[360,450,15,105]], themes: ['garden','map','library'] },
  teen: { font: 22, bands: [[300,400,12,90],[400,520,15,105],[520,650,18,120]], themes: ['route','exhibition','bridge'] },
  'young-adult': { font: 20, bands: [[450,600,15,105],[600,750,18,120],[750,900,20,135]], themes: ['archive','repair','library'] },
  adult: { font: 20, bands: [[500,650,15,120],[650,850,20,135],[850,1050,25,150]], themes: ['archive','repair','library'] }
};
// Each selection ends with a theme-specific conclusion, not filler or a fallback runtime text.
const themes = {
  garden: {
    source: '../../grouping/v1/texts/garden.txt', title: 'Bir bahçede birlikte öğrenmek',
    topic: 'Bir bitki köşesini gözlem ve görev paylaşımıyla geliştirmek',
    idea: 'Ortak gözlem, kayıt ve görev paylaşımı bahçede öğrenmeyi destekler',
    conclusion: 'Bu çalışmada önemli olan yalnız bitkilerin büyümesi değildi. Çocuklar koşulları gözleyerek, notlarını karşılaştırarak ve görevleri paylaşarak karar vermeyi denedi. Bahçe, bir işi hemen bitirmekten çok birlikte öğrenmenin ve gerektiğinde düzenlemeyi değiştirmenin örneği oldu.',
    purpose: 'Bahçe çalışması üzerinden birlikte gözlem yapmanın önemini anlatmak',
    wrongTopics: ['Bir bitki yarışmasının kazananını belirlemek','Her bitkiye aynı miktarda su vermeyi savunmak','Bahçedeki tüm canlıları uzaklaştırma yöntemini öğretmek'],
    wrongIdeas: ['Bitkilerin gelişmesini yalnız sulama miktarı belirler','İşlerin hızlı bitmesi için bütün görevler tek kişiye bırakılmalıdır','Bir gözlem her koşul için kesin sonuç vermeye yeter'],
    wrongPurposes: ['Bir tohum markasının satın alınmasını sağlamak','Bütün bitki türlerinin özelliklerini eksiksiz listelemek','Öğrencileri yetiştirdikleri bitkilere göre sıralamak'],
    wrongTitles: ['En Hızlı Büyüyen Tohum Yarışı','Bahçeyi Bir Günde Tamamlamak','Her Bitkiye Tek Bir Kural']
  },
  map: {
    source: '../../grouping/v1/texts/map.txt', title: 'İhtiyaca göre gelişen harita',
    topic: 'Bir haritayı kullanım deneyimleriyle anlaşılır hale getirmek',
    idea: 'Harita, kullanıcı ihtiyaçlarına göre denenip güncellendiğinde yararlı olur',
    conclusion: 'Haritanın değeri bütün sokakları kusursuz çizmesinden gelmiyordu. Kullanıcıların neye ihtiyaç duyduğunu anlamak, karışan işaretleri denemek ve değişen bilgileri güncellemek onu yararlı kılıyordu. Deniz için çizim, bitmiş bir resimden çok birlikte geliştirilebilen bir yol gösterici oldu.',
    purpose: 'Haritanın kullanıcıların ihtiyaçlarına göre geliştirilebileceğini göstermek',
    wrongTopics: ['Bir koşu yarışında en hızlı kişiyi seçmek','Mahalledeki bütün binaların yaşını hesaplamak','Parkın yerini başka bir mahalleye taşımak'],
    wrongIdeas: ['Bir çizimi yalnız hazırlayan kişinin anlaması yeterlidir','Bir harita ilk çizildiğinde bir daha değiştirilmemelidir','En kısa yol her kullanıcı için daima en uygun yoldur'],
    wrongPurposes: ['Her yürüyüşün süresinin aynı olduğunu kanıtlamak','Bütün kullanıcıların aynı yolu seçmesini zorunlu tutmak','Mahalledeki tüm yolların teknik ölçülerini vermek'],
    wrongTitles: ['Değişmeyen Kusursuz Çizim','Parktaki Hız Yarışı','Bütün Yollar Aynıdır']
  },
  library: {
    source: '../../free-reading/v1/texts/library-long.txt', title: 'Merakı destekleyen kütüphane',
    topic: 'Kütüphanede bilgiye ulaşmayı ve öğrenmeyi birlikte geliştirmek',
    idea: 'Kütüphane, merak ve bilgiye erişimi ortak çalışmayla destekler',
    conclusion: 'Kütüphanenin katkısı yalnız bir kitabı ödünç vermekle sınırlı değildi. Okuyucuların sorularını dinlemek, bilgiyi farklı kaynaklarla kontrol etmek ve yönlendirmeleri denemek öğrenmeyi destekliyordu. Duru için bu yer, hazır cevapların deposundan çok merakı ve ortak çalışmayı geliştiren bir alan oldu.',
    purpose: 'Kütüphanenin merakı ve bilgiye erişimi destekleyen rolünü anlatmak',
    wrongTopics: ['Kitap kapaklarını yalnız renklerine göre sıralamak','Bütün okuyuculara aynı kitabı zorunlu seçtirmek','En çok sayfa okuyan kişiyi ödüllendirmek'],
    wrongIdeas: ['Kitap seçiminde bütün okuyucuların ihtiyacı aynıdır','Bir kaynağa bakmak bütün soruları kesin olarak çözer','Kütüphanede yalnız ödünç alınan kitap sayısı önemlidir'],
    wrongPurposes: ['Bir okuma hızının herkese uygun olduğunu kanıtlamak','Kitapları yalnız kalınlıklarına göre değerlendirmek','Bütün ziyaretçileri aynı görüşte birleştirmek'],
    wrongTitles: ['Herkese Tek Kitap','Sayfa Sayısına Göre Yarış','Yalnız Kapaklara Bakmak']
  },
  route: {
    source: '../../guided-reading/v1/texts/teen-5-route.txt', title: 'Gözlemden ortak karara',
    topic: 'Okul girişindeki kalabalığı gözlemlerle incelemek',
    idea: 'Sorunu koşullarıyla gözlemek, ortak çözüm kararlarını destekler',
    conclusion: 'Grubun çalışması bir çözümü peşinen doğru ilan etmeye değil, sorunu koşullarıyla anlamaya dayanıyordu. Farklı günlerin kayıtlarını karşılaştırmak ve eksikleri açıkça belirtmek ortak kararları daha anlaşılır kıldı. Gözlem, kişileri sıralamak için değil uygulanabilir bir düzenleme tartışmak için kullanıldı.',
    purpose: 'Bir soruna çözüm ararken gözlem ve koşulların birlikte değerlendirilmesini göstermek',
    wrongTopics: ['Öğrencileri giriş sırasına göre başarıyla sıralamak','Bir servis şirketinin reklamını hazırlamak','Okulun bütün ders saatlerini değiştirmek'],
    wrongIdeas: ['İlk kişisel izlenim bütün kararlar için yeterlidir','Farklı çıkan gözlemler açıklanmadan silinmelidir','Eksik değerler ölçülmüş gibi tahminle doldurulmalıdır'],
    wrongPurposes: ['Tek bir kapı genişliğinin her sorunu çözdüğünü kanıtlamak','Öğrencilerin kimliklerini ve hareketlerini izlemek','En kalabalık günü bütün günlerin değişmez örneği saymak'],
    wrongTitles: ['İlk Tahmin Her Zaman Doğrudur','Giriş Sırasına Göre Başarı','Her Gün Aynı Kalabalık']
  },
  exhibition: {
    source: '../../guided-reading/v1/texts/teen-5-exhibition.txt', title: 'Nesnelerin hikâyelerini araştırmak',
    topic: 'Gündelik nesneler hakkında dayanağı açık sergi anlatıları hazırlamak',
    idea: 'Sergi anlatısında bilgi, yorum ve belirsizlik açıkça ayrılmalıdır',
    conclusion: 'Serginin değeri her nesne için kesin bir tarih bulmakta değil, anlatının nasıl oluştuğunu açıkça göstermekteydi. Hatıraları, yorumları ve doğrulanabilen bilgileri ayırmak ziyaretçinin anlatıyı daha dikkatli değerlendirmesini sağlıyordu. Bir nesnenin hikâyesi, kanıt ve belirsizlik birlikte görülebildiğinde daha anlaşılır hale geldi.',
    purpose: 'Bir nesnenin hikâyesini aktarırken bilgi ve belirsizliği ayırmanın önemini göstermek',
    wrongTopics: ['Radyoların bütün teknik parçalarını onarmak','Bir tamir kutusunun satış fiyatını hesaplamak','En eski eşyayı getiren kişiyi seçmek'],
    wrongIdeas: ['Her hatıra kesin tarih ve eksiksiz kanıt sayılmalıdır','Tek bir aile eşyası bütün bir mesleği temsil etmeye yeter','Belirsiz bilgiler kesinmiş gibi yazılmalıdır'],
    wrongPurposes: ['Nesnelerin yalnız ekonomik değerini karşılaştırmak','Görünüş benzerliğinin kesin kanıt olduğunu savunmak','Katılımcıları eşyalarının yaşına göre sıralamak'],
    wrongTitles: ['En Pahalı Eşya Yarışı','Her Hatıra Kesin Kanıttır','Yalnız Görünüş Yeter']
  },
  bridge: {
    source: '../../guided-reading/v1/texts/child-5-bridge.txt', title: 'Köprü maketinde karşılaştırmalı denemeler',
    topic: 'Köprü maketlerini kayıtlı ve karşılaştırılabilir denemelerle geliştirmek',
    idea: 'Tasarım kararları, koşulları açık ve karşılaştırılabilir denemelerle değerlendirilmelidir',
    conclusion: 'Maket çalışmasının amacı yalnız en çok ağırlığı taşıyan köprüyü seçmek değildi. Malzeme miktarını ve yükün konduğu yeri kaydetmek, tasarım değişikliklerini karşılaştırmayı mümkün kılıyordu. Gruplar bir sonucun nedenini açıklayabilmek için görünüşten çok deneme koşullarına ve kayıtlarına bakmaları gerektiğini gördü.',
    purpose: 'Tasarım denemelerinde koşulları ve kayıtları karşılaştırmanın önemini anlatmak',
    wrongTopics: ['Bir köprü resminin satış fiyatını belirlemek','En hızlı yürüyen kişiyi ödüllendirmek','Bütün köprülerin aynı biçimde yapılmasını zorunlu tutmak'],
    wrongIdeas: ['Tek bir yük denemesi her tasarım için kesin sonuç verir','Maketin güzel görünmesi taşıma gücünü belirlemeye yeterlidir','Malzeme miktarı ve yük konumu denemeyi etkilemez'],
    wrongPurposes: ['Bir maketin gerçek köprüler için kesin güvenlik kanıtı olduğunu göstermek','Deneme koşullarını kaydetmeyi gereksiz göstermek','Maketleri yalnız görünüşlerine göre sıralamak'],
    wrongTitles: ['Yalnız Görünüşe Göre Karar','Tek Kişinin Köprüsü','En Hızlı Geçiş Yarışı']
  },
  archive: {
    source: '../../comprehension/v1/texts/community-archive.txt', title: 'Sınırları açık bir ortak arşiv',
    topic: 'Mahalle belgelerini kapsamı ve paylaşım yetkisi açık bir arşivde düzenlemek',
    idea: 'Arşiv kayıtları kaynakları, belirsizlikleri ve paylaşım sınırlarıyla sunulmalıdır',
    conclusion: 'Arşivin değeri her boşluğu kesin bilgiyle doldurmakta değildi. Belgelerin kaynağını, belirsizliğini ve paylaşım sınırlarını görünür tutmak okuyucunun kaydı dikkatle kullanmasına yardım ediyordu. Ortak arşiv, eksiksiz tarih iddiasından çok gerekçeleri izlenebilen ve yeni katkılarla geliştirilebilen bir çalışma olarak ele alındı.',
    purpose: 'Bir arşiv oluştururken kaynak, kapsam ve paylaşım sınırlarını açıklamanın önemini göstermek',
    wrongTopics: ['Kentin bütün tarihini eksiksiz ve kesin biçimde yazmak','Fotoğrafları yalnız estetik değerlerine göre sıralamak','Eldeki bütün dosyaları izin incelemeden yayımlamak'],
    wrongIdeas: ['Bir dosyanın elde bulunması onu yayımlama yetkisini otomatik verir','Belirsiz kayıtlar kesin bilgiyle tamamlanmış gibi sunulmalıdır','Arşiv bütün toplulukları eşit temsil etmek zorundadır ve bunu zaten başarır'],
    wrongPurposes: ['En eski belgenin her zaman en doğru olduğunu kanıtlamak','Paylaşım yetkisini kayıt sayısına göre belirlemek','Bütün kişisel anlatıları aynı kesinlikte sunmak'],
    wrongTitles: ['Kentin Eksiksiz Tarihi','Her Dosya Herkese Açıktır','Belirsizlikleri Silmek']
  },
  repair: {
    source: '../../comprehension/v1/texts/repair-workshop.txt', title: 'Tamir atölyesinde gerekçeli kararlar',
    topic: 'Tamir atölyesinde güvenlik ve kayıtlarla ortak kararlar vermek',
    idea: 'Atölye kararları, işlem sayısıyla birlikte güvenlik ve gerekçelerle değerlendirilmelidir',
    conclusion: 'Atölyenin başarısı yalnız tamamlanan eşya sayısıyla anlatılamıyordu. Güvenlik sınırlarını korumak, gözlemle nedeni ayırmak ve bekleyen işleri gerekçeleriyle izlemek de önemliydi. Kayıtlar, farklı işlerin anlamını tek bir puanda gizlemek yerine ortak kararların nasıl verildiğini görünür kılan bir araç oldu.',
    purpose: 'Tamir kararlarını güvenlik, kayıt ve kapsamıyla birlikte değerlendirmeyi göstermek',
    wrongTopics: ['Her elektrikli cihazı herkesin hemen onarmasını sağlamak','Gönüllüleri yalnız tamamladıkları eşya sayısıyla sıralamak','Bütün eşyaları incelemeden yenileriyle değiştirmek'],
    wrongIdeas: ['En çok eşya tamamlanan gün bütün açılardan en başarılı gündür','Uzman bulunmasa da her cihazı çalıştırarak denemek gerekir','Sahibin anlattığı belirti kontrol edilmeden kesin neden sayılmalıdır'],
    wrongPurposes: ['Onarılan eşya sayısını ölçülmemiş çevresel tasarrufla eşitlemek','Her işi aynı gün bitirme sözü vermek','Kayıt tutmak yerine yalnız gönüllülerin belleğine güvenmek'],
    wrongTitles: ['Her Eşya Aynı Gün Biter','Sadece Sayılar Önemlidir','İncelemeden Kesin Sonuç']
  }
};

export function buildSkimmingPack() {
  const previous = existsSync(file('settings.json')) ? read('settings.json') : [];
  const settings = [], content = [];
  for (const [age, profile] of Object.entries(profiles)) {
    const catalog = read(`../../${age}-exercises/v1/catalog.json`);
    for (const [index, [min, max, minimumSeconds, maximumSeconds]] of profile.bands.entries()) {
      const level = index + 3;
      const row = catalog.find(item => item.type === 'Skimming' && item.difficultyLevel === level);
      if (!row) throw new Error(`Missing Skimming ${age}/${level}`);
      settings.push({ id: row.id, ageGroupId: row.targetAgeGroupId, age, level,
        previousConfiguration: previous.find(item => item.id === row.id)?.previousConfiguration ?? row.configuration,
        configuration: { engineType: 'skimming', difficultyLevel: level, engineConfig: {
          mode: 'skimming', readingPurpose: 'evaluation',
          content: { source: 'reading_text', minWordCount: min, maxWordCount: max, questionCount: 4 },
          timing: { minReadingTimeMs: minimumSeconds * 1000, maxReadingTimeMs: maximumSeconds * 1000 },
          visuals: { fontSize: `${profile.font}px` }
        } } });
      for (const key of profile.themes) {
        const theme = themes[key];
        const original = readFileSync(file(theme.source), 'utf8').trim().replace(/\r\n/g, '\n');
        const paragraphs = original.split(/\n\s*\n/u);
        const candidates = paragraphs.map((_, end) => paragraphs.slice(0,end+1).join('\n\n') + '\n\n' + theme.conclusion)
          .filter(text => words(text) >= min && words(text) <= max);
        candidates.sort((a,b) => Math.abs(words(a)-(min+max)/2) - Math.abs(words(b)-(min+max)/2));
        if (!candidates.length) throw new Error(`No whole-paragraph selection ${age}/${level}/${key}`);
        const definitions = [
          ['Metnin genel konusu aşağıdakilerden hangisidir?', theme.topic, theme.wrongTopics],
          ['Metnin ana düşüncesini en iyi hangi ifade özetler?', theme.idea, theme.wrongIdeas],
          ['Yazar bu metinle öncelikle neyi anlatmayı amaçlamaktadır?', theme.purpose, theme.wrongPurposes],
          ['Metnin bütününe en uygun başlık hangisidir?', theme.title, theme.wrongTitles]
        ];
        const answerOffset = createHash('sha256').update(`${age}:${level}:${key}`).digest()[0] % 4;
        const questions = definitions.map(([questionText, answer, distractors], position) => {
          const answerPosition = (position + answerOffset) % 4;
          const options = [...distractors]; options.splice(answerPosition,0,answer);
          return { id: uuid(`${age}:${level}:${key}:question:${position}`), questionText,
            type: 1, bloomLevel: position === 0 ? 2 : 4, orderIndex: position,
            optionA: options[0], optionB: options[1], optionC: options[2], optionD: options[3],
            correctAnswer: ['A','B','C','D'][answerPosition], evidence: theme.conclusion,
            explanation: 'Metnin örnekleri ve sonuç paragrafı birlikte ele alındığında doğru seçenek genel anlamı kapsar; diğer seçenekler kapsamı daraltır veya metinle çelişir.' };
        });
        content.push({ id: uuid(`${age}:${level}:${key}:text`), exerciseId: row.id, ageGroupId: row.targetAgeGroupId,
          age, level, title: `${theme.title} — Seviye ${level}`, content: candidates[0], wordCount: words(candidates[0]),
          source: theme.source, sourceSha256: createHash('sha256').update(original).digest('hex'), questions });
      }
    }
  }
  return { settings, content };
}

if (process.argv[1] && pathToFileURL(process.argv[1]).href === import.meta.url) {
  const { settings, content } = buildSkimmingPack();
  const json = value => JSON.stringify(value, null, 2);
  writeFileSync(file('settings.json'), json(settings) + '\n');
  writeFileSync(file('content.json'), json(content) + '\n');
  for (const name of ['apply', 'rollback'])
    writeFileSync(file(`${name}.sql`), readFileSync(file(`${name}.template.sql`), 'utf8')
      .replace('{{SETTINGS}}', json(settings)).replace('{{CONTENT}}', json(content)));
  console.log(`Generated ${settings.length} settings, ${content.length} texts and ${content.length * 4} questions.`);
}

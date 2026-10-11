import { existsSync, readFileSync, writeFileSync } from 'node:fs';

const ages = [
  { name: 'child', suffix: 1, seconds: [40,45,50,55,60], contexts: [
    'Deniz, okulun bahçesindeki bilim şenliğinde deney standını inceliyor.',
    'Ece, arkadaşlarıyla parkta doğa gözlemi yapıyor.',
    'Mert, ailesiyle mutfakta piknik için yiyecek hazırlıyor.',
    'Ada, sınıf gezisinde oyuncak müzesini ziyaret ediyor.',
    'Can, ailesiyle tren istasyonunda geziye hazırlanıyor.',
    'Elif, okulun resim kulübünde sergi hazırlıyor.' ] },
  { name: 'teen', suffix: 2, seconds: [30,35,40,45,50], contexts: [
    'Deniz, robotik kulübünün bilim fuarında prototipleri düzenliyor.',
    'Ece, çevre kulübüyle parkta gözlem istasyonu kuruyor.',
    'Mert, okulun yemek atölyesinde malzemeleri hazırlıyor.',
    'Ada, tarih kulübüyle müzedeki sergi düzenini inceliyor.',
    'Can, okul takımıyla istasyonda turnuva yolculuğuna hazırlanıyor.',
    'Elif, tasarım kulübünün yıl sonu sergisini düzenliyor.' ] },
  { name: 'young-adult', suffix: 4, seconds: [25,30,35,40,45], contexts: [
    'Deniz, üniversitenin araştırma laboratuvarında deney düzeneğini kontrol ediyor.',
    'Ece, kampüsün ekoloji sahasında örnekleme noktalarını hazırlıyor.',
    'Mert, beslenme atölyesinde uygulama malzemelerini düzenliyor.',
    'Ada, müze stajında geçici serginin yerleşimini inceliyor.',
    'Can, öğrenci topluluğuyla tren istasyonunda saha gezisini organize ediyor.',
    'Elif, kampüsün tasarım stüdyosunda proje sunumunu hazırlıyor.' ] },
  { name: 'adult', suffix: 3, seconds: [25,30,35,40,45], contexts: [
    'Deniz, üretim atölyesinde kalite kontrol düzeneğini hazırlıyor.',
    'Ece, belediyenin park düzenleme projesinde çalışma alanını inceliyor.',
    'Mert, işletmenin mutfağında servis hazırlıklarını planlıyor.',
    'Ada, kültür merkezindeki serginin açılış hazırlıklarını kontrol ediyor.',
    'Can, tren istasyonunda çalışma ekibinin yolculuğunu organize ediyor.',
    'Elif, tasarım ofisinde müşteriye sunulacak örnekleri düzenliyor.' ] }
];
// Authored scenes; no external content or images. Each row is a distinct fact layer.
const themes = [
  { name: 'deney', layers: [
    ['mavi kap','sarı cetvel','kırmızı kutu','yeşil kart','beyaz fiş','üç pil','kabı açıyor','cetveli alıyor','kutuyu kapatıyor'],
    ['cam tüp','mor işaret','metal halka','pembe kart','küçük kapak','iki sensör','tüpü yerleştiriyor','halkayı ölçüyor','kartı çeviriyor'],
    ['ölçüm kabı','şeffaf şişe','koyu şerit','gri tepsi','siyah düğme','dört bağlantı','şişeyi dolduruyor','kabı tartıyor','şeridi bağlıyor'],
    ['deney panosu','turuncu kablo','beyaz sayaç','yeşil anahtar','kırmızı mandal','beş etiket','kabloyu bağlıyor','sayacı okuyor','anahtarı çeviriyor'],
    ['kontrol ekranı','mavi sensör','sarı düğme','mor dosya','beyaz not','altı ölçüm','sensörü kontrol ediyor','notu yazıyor','ekranı kapatıyor'] ] },
  { name: 'park', layers: [
    ['taş işaret','çiçek saksısı','kuş yuvası','su kabı','tahta levha','iki ağaç','levhayı okuyor','suyu dolduruyor','yuvayı gözlüyor'],
    ['gözlem noktası','kırmızı çalı','yeşil bank','küçük fener','sarı çanta','üç fide','fideyi ekiyor','bankı siliyor','feneri kaldırıyor'],
    ['yol ayrımı','meşe ağacı','ahşap köprü','mavi tabela','beyaz taş','dört yaprak','tabelayı inceliyor','köprüye ilerliyor','taşı işaretliyor'],
    ['örnekleme alanı','toprak kutusu','ölçüm çubuğu','siyah kap','turuncu bayrak','beş örnek','çubuğu yerleştiriyor','örneği topluyor','bayrağı dikiyor'],
    ['harita panosu','kırmızı rota','sarı ölçer','yeşil klasör','mavi kalem','altı durak','rotayı işaretliyor','ölçümü yazıyor','klasörü kapatıyor'] ] },
  { name: 'mutfak', layers: [
    ['beyaz tabak','mavi bardak','yeşil kase','kırmızı peçete','sarı kaşık','üç elma','elmayı yıkıyor','kaseye koyuyor','bardağı dolduruyor'],
    ['kesme tahtası','cam kavanoz','metal süzgeç','pembe bez','küçük kap','iki armut','kavanozu açıyor','armudu dilimliyor','süzgeci kaldırıyor'],
    ['servis tepsisi','siyah kepçe','beyaz sürahi','mor örtü','kırmızı tutacak','dört dilim','örtüyü seriyor','sürahiyi dolduruyor','tepsiyi taşıyor'],
    ['hazırlık listesi','sarı tartı','mavi zamanlayıcı','yeşil kutu','turuncu not','beş paket','paketi tartıyor','süreyi ayarlıyor','notu ekliyor'],
    ['servis çizelgesi','beyaz etiket','kırmızı kapak','gri sepet','mavi dosya','altı porsiyon','etiketi yazıyor','kapağı kapatıyor','sepeti kaldırıyor'] ] },
  { name: 'muze', layers: [
    ['sergi vitrini','oyuncak tren','tahta gemi','kırmızı bilet','mavi broşür','iki kukla','bileti gösteriyor','treni inceliyor','broşürü açıyor'],
    ['bilgi panosu','sarı afiş','eski saat','beyaz kart','yeşil dosya','üç fotoğraf','afişi okuyor','saati inceliyor','kartı alıyor'],
    ['eser kaidesi','bronz heykel','cam kase','mor katalog','siyah levha','dört çizim','kataloğu açıyor','heykeli gözlüyor','levhayı okuyor'],
    ['sergi krokisi','mavi ok','kırmızı etiket','sarı klasör','beyaz maket','beş numara','krokiyi inceliyor','etiketi takıyor','maketi düzeltiyor'],
    ['koruma planı','yeşil sensör','gri perde','turuncu liste','mavi kutu','altı kayıt','sensörü okuyor','perdeyi ayarlıyor','listeyi tamamlıyor'] ] },
  { name: 'istasyon', layers: [
    ['bilet gişesi','kırmızı bavul','mavi bank','sarı çanta','beyaz tabela','üç vagon','bileti alıyor','bavulu kaldırıyor','bankta bekliyor'],
    ['yolcu panosu','yeşil saat','siyah dolap','mor kart','küçük sepet','iki hat','saati kontrol ediyor','kartı çıkarıyor','sepete uzanıyor'],
    ['peron işareti','turuncu bariyer','beyaz levha','mavi valiz','kırmızı fener','dört sıra','levhayı okuyor','valizi taşıyor','bariyerde duruyor'],
    ['aktarma krokisi','sarı ok','gri kapı','yeşil dosya','beyaz not','beş durak','rotayı inceliyor','kapıya ilerliyor','notu güncelliyor'],
    ['yolculuk çizelgesi','mavi etiket','kırmızı sayaç','mor çanta','gri klasör','altı bilet','biletleri sayıyor','etiketi takıyor','klasörü kapatıyor'] ] },
  { name: 'tasarim', layers: [
    ['resim sehpası','mavi boya','sarı fırça','beyaz kağıt','kırmızı bez','iki çizgi','boyayı açıyor','fırçayı alıyor','kağıda çiziyor'],
    ['çalışma panosu','yeşil karton','mor kalem','siyah bant','küçük makas','üç şekil','kartonu kesiyor','şekli çiziyor','bandı yapıştırıyor'],
    ['sunum maketi','gri blok','şeffaf panel','turuncu not','beyaz örnek','dört parça','bloğu yerleştiriyor','paneli ekliyor','notu yazıyor'],
    ['renk çizelgesi','kırmızı numune','mavi ölçer','sarı kart','yeşil kutu','beş ton','numuneyi seçiyor','ölçümü alıyor','kartı ekliyor'],
    ['proje şeması','mor bağlantı','siyah işaret','gri klasör','beyaz etiket','altı bölüm','bağlantıyı çiziyor','etiketi yazıyor','şemayı tamamlıyor'] ] }
];
const path = relative => new URL(relative, import.meta.url);
const load = relative => JSON.parse(readFileSync(path(relative), 'utf8'));
const baselinePath = path('./baseline.json');
const baseline = existsSync(baselinePath) ? load('./baseline.json') : ages.flatMap(age => load(`../../${age.name}-exercises/v1/scenes.json`));
const scenes = [], outputs = [];
const uuid = (prefix, number) => `${prefix}-0000-5000-8000-${String(number).padStart(12,'0')}`;
for (const age of ages) {
  const catalog = load(`../../${age.name}-exercises/v1/catalog.json`);
  const ageScenes = [];
  for (let level = 1; level <= 5; level++) {
    const exercise = catalog.find(row => row.type === 'Visualization' && row.difficultyLevel === level);
    if (!exercise) throw new Error(`Missing exercise ${age.name}/${level}`);
    for (const [themeIndex, theme] of themes.entries()) {
      const [center,left,right,front,extra,count,first,second,last] = theme.layers[level-1];
      const detail = `“${center}” için ${count} ayrılmış.`;
      const spatial = `“${center}” ortada. Solunda ${left}, sağında ${right} bulunuyor. Soldaki nesnenin önünde ${front} var.`
        + (level >= 4 ? ` Öndeki nesnenin yanında ${extra} duruyor.` : '');
      const sequence = `Görevli önce ${first}, ardından ${second}, en son ${last}.`;
      const context = age.contexts[themeIndex];
      const description = [context,
        ...theme.layers.slice(0,level-1).map(row => `“${row[0]}” için ${row[5]} ayrılmış.`),
        detail, spatial, sequence,
        ...(age.name === 'child' ? [] : ['Yerleşimi ve işlem sırasını zihninizde birlikte canlandırın.'])].join(' ');
      const spatialQuestion = level === 1 ? 'Ortadaki nesnenin solunda ne vardı?' : level >= 4
        ? 'Ortadaki nesnenin solundaki nesnenin önünde duran nesnenin yanında ne vardı?'
        : 'Ortadaki nesnenin solundaki nesnenin önünde ne vardı?';
      const detailQuestion = level >= 3 ? `Diğer nesneler için ayrılanlardan farklı olarak “${center}” için ne ayrılmıştı?`
        : `“${center}” için ne ayrılmıştı?`;
      const sequenceQuestion = level === 1 ? 'Görevli ilk işlemden hemen sonra ne yaptı?'
        : level === 2 ? 'Görevlinin son işlemi hangisiydi?'
        : level === 3 ? 'Görevli son işlemden hemen önce ne yaptı?'
        : level === 4 ? 'İlk ve son işlem arasında yapılan işlem hangisiydi?'
        : 'Görevlinin işlemlerini doğru sırayla veren seçenek hangisi?';
      const [amount, ...unit] = count.split(' ');
      const countOptions = [count, ...['bir','iki','üç','dört','beş','altı','yedi']
        .filter(value => value !== amount).slice(Math.max(0, level-2), Math.max(0, level-2)+3)
        .map(value => `${value} ${unit.join(' ')}`)];
      const definition = [
        { questionText: detailQuestion, options: countOptions, evidence: detail, questionType: 'detail' },
        { questionText: spatialQuestion, options: [level === 1 ? left : level >= 4 ? extra : front,
          center, right, level === 1 ? front : left], evidence: spatial, questionType: 'spatial' },
        { questionText: sequenceQuestion, options: level === 5 ? [
          `${first} → ${second} → ${last}`, `${last} → ${second} → ${first}`,
          `${second} → ${first} → ${last}`, `${first} → ${last} → ${second}` ]
          : [level === 2 ? last : second, level === 2 ? second : first,
            level === 2 ? first : last, 'hiçbir işlem yapmadı'], evidence: sequence, questionType: 'sequence' }
      ];
      const number = age.suffix*1000+level*10+themeIndex;
      const scene = { id: uuid('daf10000',number), exerciseId: exercise.id, theme: theme.name,
        description, duration: age.seconds[level-1], displayOrder: themeIndex, difficultyLevel: level,
        targetAgeGroupId: exercise.targetAgeGroupId, mode: 'assessment', questions: definition.map((q,index) => {
          const rotation = (themeIndex+level+index)%4;
          const options = [...q.options.slice(4-rotation), ...q.options.slice(0,4-rotation)];
          return { ...q, id: uuid('daf20000',number*10+index), options,
            correctAnswer: String.fromCharCode(65+rotation), demandLevel: level, displayOrder: index };
        }) };
      ageScenes.push(scene); scenes.push(scene);
    }
  }
  outputs.push([path(`../../${age.name}-exercises/v1/scenes.json`),JSON.stringify(ageScenes,null,2)+'\n']);
}
outputs.push([baselinePath,JSON.stringify(baseline,null,2)+'\n'], [path('./scenes.json'),JSON.stringify(scenes,null,2)+'\n']);
const template = readFileSync(path('./apply.template.sql'),'utf8');
outputs.push([path('./apply.sql'),template.replace('{{SCENES}}',JSON.stringify(scenes)).replace('{{BASELINE}}',JSON.stringify(baseline))]);
if (process.argv.includes('--check')) {
  for (const [file,content] of outputs) {
    if (!existsSync(file) || readFileSync(file,'utf8') !== content) throw new Error(`Stale generated output: ${file.pathname}`);
  }
} else {
  for (const [file,content] of outputs) writeFileSync(file,content);
}

// Ortak havuz metinleri için soru yazım kuralları (ölçme-değerlendirme), 26.11.2026.
// Kaynak: proje notu hizli-okuma-metin-havuzu-plani.md ("Soru yazım kuralları"). Her soru paketi bu denetimden geçer.

export const QUESTIONS_PER_TEXT = 8;
export const LENGTH_RATIO = 1.3;      // en uzun şık / en kısa şık
export const SHORT_OPTION_SLACK = 6;  // kısa şıklarda izin verilen harf farkı
const BANNED_OPTION = /\b(hepsi|hiçbiri|yukarıdakiler|a ve b|b ve c)\b/iu;
const LETTER_PREFIX = /^[A-D][).:]\s/u;
const NEGATIVE_STEM = /(değildir|söylenemez|ulaşılamaz|yanlıştır|olamaz)\s*\??$/iu;
const TEMPLATE = /(doğru cevap|şıkkıdır|bilgisini doğrudan verir|Yanıt, metindeki ilgili bilgi|metin bağlamında)/iu;
const ABSOLUTE = /\b(her zaman|asla|hiçbir zaman|kesinlikle|tamamen|hep)\b/iu;

export const words = text => (text.toLocaleLowerCase('tr').match(/[\p{L}\p{N}]+/gu) ?? []);
const stem = word => word.slice(0, 5);

export function optionLengthProblem(options) {
  const lengths = options.map(option => option.length);
  const min = Math.min(...lengths), max = Math.max(...lengths);
  if (max - min > SHORT_OPTION_SLACK && max / min > LENGTH_RATIO) return `şık uzunlukları ${lengths.join('/')} (oran ${(max / min).toFixed(2)})`;
  const correct = lengths[0];
  if (correct === max && lengths.filter(length => length === max).length === 1) return 'doğru şık tek başına en uzun';
  if (correct === min && lengths.filter(length => length === min).length === 1) return 'doğru şık tek başına en kısa';
  return null;
}

// Bir sorunun kuralları. question: { p, type, bloom, text, correct, wrong: [3], explanation }
export function questionProblems(question, paragraphCount) {
  const problems = [];
  const options = [question.correct, ...question.wrong];
  if (!Number.isInteger(question.p) || question.p < 1 || question.p > paragraphCount) problems.push('paragraf numarası geçersiz');
  if (![1, 2, 3].includes(question.type)) problems.push('tür 1–3 olmalı');
  if (!(question.bloom >= 1 && question.bloom <= 6)) problems.push('Bloom 1–6 olmalı');
  if (!question.text?.trim().endsWith('?')) problems.push('kök soru işaretiyle bitmeli');
  if (NEGATIVE_STEM.test(question.text)) problems.push('olumsuz kök kullanılmaz');
  if (question.wrong?.length !== 3) problems.push('üç çeldirici olmalı');
  if (new Set(options.map(option => option.trim().toLocaleLowerCase('tr'))).size !== 4) problems.push('şıklar farklı olmalı');
  for (const option of options) {
    if (!option?.trim() || option.length > 500) problems.push('şık boş ya da çok uzun');
    if (BANNED_OPTION.test(option)) problems.push(`yasak şık: ${option}`);
    if (LETTER_PREFIX.test(option)) problems.push('şıkta harf öneki');
  }
  const ends = options.map(option => /[.!?]$/u.test(option.trim()));
  if (new Set(ends).size > 1) problems.push('şıkların noktalaması aynı olmalı');
  const lengthProblem = optionLengthProblem(options);
  if (lengthProblem) problems.push(lengthProblem);
  if (ABSOLUTE.test(question.correct) && !question.wrong.some(option => ABSOLUTE.test(option))) problems.push('kesin sözcük yalnız doğru şıkta');
  if (question.wrong.some(option => ABSOLUTE.test(option)) && !ABSOLUTE.test(question.correct)
    && question.wrong.filter(option => ABSOLUTE.test(option)).length === 3) problems.push('kesin sözcük yalnız çeldiricilerde');
  // Kökle kelime örtüşmesi: doğru şık kökün kelimelerini çeldiricilerden belirgin fazla tekrar etmemeli.
  const stemWords = new Set(words(question.text).filter(word => word.length > 3).map(stem));
  const overlap = option => words(option).filter(word => word.length > 3 && stemWords.has(stem(word))).length;
  const correctOverlap = overlap(question.correct), wrongOverlap = Math.max(...question.wrong.map(overlap));
  if (correctOverlap >= 2 && correctOverlap > wrongOverlap + 1) problems.push('doğru şık kökle belirgin fazla kelime paylaşıyor');
  if (/^\d/u.test(question.correct) && options.every(option => /^\d/u.test(option))) {
    // sayı şıkları küçükten büyüğe sıralanır; sıralama yayımlanırken yapılır, burada yalnız sayı olduklarını doğrula
  }
  const explanation = question.explanation ?? '';
  if (explanation.trim().length < 20 || explanation.length > 300) problems.push('açıklama 20–300 harf olmalı');
  if (TEMPLATE.test(explanation) || /\b[A-D] şıkk/u.test(explanation)) problems.push('açıklamada kalıp ya da şık harfi');
  return problems;
}

// Bir metnin 8 sorusu birlikte.
export function textProblems(questions, paragraphCount, { young = false } = {}) {
  const problems = [];
  if (questions.length !== QUESTIONS_PER_TEXT) problems.push(`${questions.length} soru (8 olmalı)`);
  const count = type => questions.filter(question => question.type === type).length;
  if (count(1) < 3) problems.push('en az 3 gerçek anlam sorusu');
  if (count(2) < 3) problems.push('en az 3 çıkarım sorusu');
  if (count(3) < 1) problems.push('en az 1 değerlendirme sorusu');
  if (new Set(questions.map(question => question.bloom)).size < 2) problems.push('en az 2 farklı Bloom düzeyi');
  if (!questions.some(question => question.bloom >= (young ? 3 : 4))) problems.push(`en az 1 soru Bloom ${young ? 3 : 4}+`);
  const paragraphs = new Set(questions.map(question => question.p));
  if (!paragraphs.has(1)) problems.push('ilk paragraftan soru yok');
  if (!paragraphs.has(paragraphCount)) problems.push('son paragraftan soru yok');
  if (new Set(questions.map(question => question.text.trim())).size !== questions.length) problems.push('yinelenen soru');
  // Bir sorunun doğru cevabı başka bir sorunun kökünde geçmemeli.
  for (const question of questions) for (const other of questions) {
    if (question === other) continue;
    const answer = words(question.correct).filter(word => word.length > 4);
    if (answer.length >= 3 && answer.every(word => words(other.text).includes(word))) problems.push(`cevap başka sorunun kökünde: ${question.text}`);
  }
  return problems;
}

// Cevap harfleri: her metinde A, B, C, D ikişer kez; sıra metin kimliğinden belirlenir (tekrarlanabilir).
export function letterPlan(seed) {
  let state = [...seed].reduce((hash, char) => (hash * 31 + char.charCodeAt(0)) >>> 0, 2166136261);
  const next = () => { state = (state * 1664525 + 1013904223) >>> 0; return state; };
  const letters = ['A', 'A', 'B', 'B', 'C', 'C', 'D', 'D'];
  for (let index = letters.length - 1; index > 0; index--) { const swap = next() % (index + 1); [letters[index], letters[swap]] = [letters[swap], letters[index]]; }
  return letters;
}

// Doğru şıkkı plandaki harfe yerleştirir; çeldiricilerin kendi sırası korunur. Sayısal şıklar küçükten büyüğe sıralanır.
export function arrange(question, letter) {
  let options = [...question.wrong];
  const numeric = [question.correct, ...question.wrong].every(option => /^\d/u.test(option.trim()));
  if (numeric) {
    options = [question.correct, ...question.wrong].sort((a, b) => parseFloat(a.replace(',', '.')) - parseFloat(b.replace(',', '.')));
    return { options, answer: 'ABCD'[options.indexOf(question.correct)] };
  }
  options.splice('ABCD'.indexOf(letter), 0, question.correct);
  return { options, answer: letter };
}

// Paket düzeyinde "okumadan tahmin" ölçümü. questions: [{ options: [4], answer: index, text: metin }]
// Uzunluk gözle algılandığı gibi karşılaştırılır: fark en çok 3 harf ya da %10 ise iki şık eşit sayılır.
// longest: hep gözle en uzun görüneni seçenin beklenen başarısı; shortest: en kısayı seçenin;
// overlap: metinle en çok sözcük paylaşan şıkkı seçenin. Şans düzeyi %25.
export function surfaceCues(questions) {
  const st = word => word.slice(0, 5);
  let longest = 0, shortest = 0, overlap = 0;
  for (const { options, answer, text } of questions) {
    const lengths = options.map(option => option.length), tolerance = length => Math.max(3, length * 0.1);
    const pick = test => { const chosen = [0, 1, 2, 3].filter(test); return chosen.includes(answer) ? 1 / chosen.length : 0; };
    longest += pick(i => !lengths.some(other => other > lengths[i] + tolerance(lengths[i])));
    shortest += pick(i => !lengths.some(other => other < lengths[i] - tolerance(lengths[i])));
    const pool = new Set(words(text).map(st));
    const score = options.map(option => { const list = words(option).map(st); return list.filter(word => pool.has(word)).length / Math.max(1, list.length); });
    overlap += pick(i => score[i] === Math.max(...score));
  }
  const n = questions.length;
  return { longest: longest / n, shortest: shortest / n, overlap: overlap / n };
}
export const MAX_SURFACE_CUE = { longest: 0.35, shortest: 0.35, overlap: 0.35 };

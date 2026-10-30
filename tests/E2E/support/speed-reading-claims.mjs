// Overstated speed-reading claims (decision 30.10.2026). Exercises are described by what they practise; these phrases
// promise effects the research does not support for ordinary readers.
const patterns = [
  /göz kas\p{L}*/giu,
  /(periferik|çevresel)\s+(görüş|görme)\s+alan\p{L}*/giu,
  /(görüş|görme)\s+alan\p{L}*\s+genişlet\p{L}*/giu,
  /beynin\s+algılama\s+kapasitesi\p{L}*/giu
];

export function overstatedClaims(text) {
  const found = [];
  for (const pattern of patterns) for (const match of text.matchAll(pattern)) found.push(match[0]);
  return [...new Set(found)];
}

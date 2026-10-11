export interface GroupingBoundary { endIndex: number; hasNewline: boolean; }

const abbreviations = new Set(['dr.', 'prof.', 'doç.', 'av.', 'sn.', 'vb.', 'vd.', 'vs.', 'örn.', 'bkz.']);

// Kept in parity with Domain/GroupingTextRules: word grouping, not semantic parsing.
export function buildGroupingBoundaries(text: string, chunkSize: number): GroupingBoundary[] {
  if (!text.trim() || !Number.isInteger(chunkSize) || chunkSize < 1 || chunkSize > 10) {
    throw new Error('Grouping requires real text and a valid group size.');
  }
  // .NET whitespace is Unicode separators, ASCII whitespace and NEL; not BOM.
  const tokens = [...text.matchAll(/[^\p{Z}\t-\r\u0085]+/gu)];
  const groups: GroupingBoundary[] = [];
  let count = 0;
  tokens.forEach((token, index) => {
    count++;
    const nextOffset = tokens[index + 1]?.index ?? text.length;
    const hasNewline = text.slice(token.index + token[0].length, nextOffset).includes('\n');
    const word = token[0].replace(/["'”’)\]}]+$/u, '');
    const sentenceEnd = /[!?…]$/u.test(word) || (word.endsWith('.')
      && !abbreviations.has(word.toLowerCase()) && !/^[0-9]+\.$/u.test(word)
      && !/^[A-Za-zÇĞİÖŞÜçğıöşü]\.$/u.test(word));
    if (count === chunkSize || sentenceEnd || hasNewline || index === tokens.length - 1) {
      groups.push({ endIndex: index + 1, hasNewline });
      count = 0;
    }
  });
  return groups;
}

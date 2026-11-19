/**
 * Long site text (blog posts, pages, legal texts) as blocks: paragraphs are separated by an empty line, a line starting
 * with "## " is a heading, lines starting with "- " form a list. Everything is rendered as text by the templates, never
 * as HTML, so nothing an editor writes can run in a visitor's browser.
 */
export type TextBlock = { kind: 'heading'; text: string } | { kind: 'paragraph'; text: string } | { kind: 'list'; items: string[] };

export function textBlocks(body: string | null | undefined): TextBlock[] {
  const blocks: TextBlock[] = [];
  for (const chunk of (body ?? '').replace(/\r\n?/g, '\n').split(/\n\s*\n/)) {
    const lines = chunk.split('\n').map(line => line.trim()).filter(Boolean);
    if (!lines.length) continue;
    let paragraph: string[] = [];
    let list: string[] = [];
    const flush = () => {
      if (paragraph.length) blocks.push({ kind: 'paragraph', text: paragraph.join(' ') });
      if (list.length) blocks.push({ kind: 'list', items: list });
      paragraph = [];
      list = [];
    };
    for (const line of lines) {
      if (line.startsWith('## ')) { flush(); blocks.push({ kind: 'heading', text: line.slice(3).trim() }); }
      else if (line.startsWith('- ')) { if (paragraph.length) { blocks.push({ kind: 'paragraph', text: paragraph.join(' ') }); paragraph = []; } list.push(line.slice(2).trim()); }
      else { if (list.length) { blocks.push({ kind: 'list', items: list }); list = []; } paragraph.push(line); }
    }
    flush();
  }
  return blocks;
}

/** A link the site may follow itself (inside maxkampus.com) or must open as an outside address. */
export function isInternalLink(url: string | null | undefined): boolean {
  return !!url && url.startsWith('/') && !url.startsWith('//');
}

export function trDate(value: string | null | undefined): string {
  if (!value) return '';
  const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(value) ? value : value + 'Z');
  return Number.isNaN(date.getTime()) ? '' : new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Europe/Istanbul' }).format(date);
}

export function trMoney(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat('tr-TR', { style: 'currency', currency, maximumFractionDigits: amount % 1 === 0 ? 0 : 2 }).format(amount);
  } catch {
    return `${amount} ${currency}`;
  }
}

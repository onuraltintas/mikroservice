export type CoachingCmsContentSegment =
  | { type: 'text'; value: string }
  | { type: 'heading'; value: string }
  | { type: 'list'; items: string[] }
  | { type: 'image'; alt: string; src: string };

const coachingCmsImagePattern = /^!\[([^\]\r\n]{0,300})\]\((\/api\/coaching\/cms\/media\/[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12})\)$/i;
const coachingCmsHeadingPattern = /^##\s+(.+)$/;
const coachingCmsListPattern = /^-\s+(.+)$/;

export function parseCoachingCmsContent(content: string): CoachingCmsContentSegment[] {
  const segments: CoachingCmsContentSegment[] = [];
  let paragraph: string[] = [];
  let list: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length) segments.push({ type: 'text', value: paragraph.join('\n') });
    paragraph = [];
  };
  const flushList = () => {
    if (list.length) segments.push({ type: 'list', items: list });
    list = [];
  };

  for (const line of content.split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed) {
      flushParagraph();
      flushList();
      continue;
    }

    const heading = coachingCmsHeadingPattern.exec(trimmed);
    if (heading) {
      flushParagraph();
      flushList();
      segments.push({ type: 'heading', value: heading[1].trim() });
      continue;
    }

    const bullet = coachingCmsListPattern.exec(trimmed);
    if (bullet) {
      flushParagraph();
      list.push(bullet[1].trim());
      continue;
    }

    const image = coachingCmsImagePattern.exec(trimmed);
    if (image) {
      flushParagraph();
      flushList();
      segments.push({ type: 'image', alt: image[1].trim(), src: image[2] });
      continue;
    }

    flushList();
    paragraph.push(line);
  }

  flushParagraph();
  flushList();
  return segments;
}

import { describe, expect, it } from 'vitest';
import { parseCoachingCmsContent } from './coaching-cms-content';

describe('parseCoachingCmsContent', () => {
  it('renders safe editorial structure and same-service image embeds without interpreting HTML', () => {
    const segments = parseCoachingCmsContent([
      '## Birlikte planla',
      '',
      'Hedefini kısa ve anlaşılır bir cümleyle yaz.',
      '',
      '- Bu hafta yapılacakları belirle',
      '- Sonunda ilerlemeyi gözden geçir',
      '',
      '![Öğrenci çalışma planı](/api/coaching/cms/media/123e4567-e89b-12d3-a456-426614174000)',
      '<script>alert(1)</script>',
      '![Harici](https://example.com/image.png)'
    ].join('\n'));

    expect(segments).toEqual([
      { type: 'heading', value: 'Birlikte planla' },
      { type: 'text', value: 'Hedefini kısa ve anlaşılır bir cümleyle yaz.' },
      { type: 'list', items: ['Bu hafta yapılacakları belirle', 'Sonunda ilerlemeyi gözden geçir'] },
      { type: 'image', alt: 'Öğrenci çalışma planı', src: '/api/coaching/cms/media/123e4567-e89b-12d3-a456-426614174000' },
      { type: 'text', value: '<script>alert(1)</script>\n![Harici](https://example.com/image.png)' }
    ]);
  });
});

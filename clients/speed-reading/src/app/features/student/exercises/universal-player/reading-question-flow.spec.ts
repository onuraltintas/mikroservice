import { shouldShowReadingQuestions } from './reading-question-flow';

describe('reading question flow', () => {
  it('does not show comprehension questions after tachistoscope flashes', () => {
    expect(shouldShowReadingQuestions('text_stream', 'flash', 3)).toBeFalse();
    expect(shouldShowReadingQuestions('text_stream', 'tachistoscope', 3)).toBeFalse();
  });

  it('preserves RSVP and reading comprehension questions', () => {
    expect(shouldShowReadingQuestions('text_stream', 'rsvp', 3)).toBeTrue();
    expect(shouldShowReadingQuestions('text_stream', undefined, 3, 'RSVP')).toBeTrue();
    expect(shouldShowReadingQuestions('text_stream', 'rsvp', 3, 'Tachistoscope')).toBeFalse();
    expect(shouldShowReadingQuestions('reading_comprehension', '', 3)).toBeTrue();
    expect(shouldShowReadingQuestions('text_stream', 'flash', 0)).toBeFalse();
  });
});

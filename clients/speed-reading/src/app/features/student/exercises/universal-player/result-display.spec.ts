import {
  awaitsServerResult,
  examCorrectAnswer,
  focusSummary,
  resultPerformanceLevel,
  scanningResultMessage,
  serverErrorCount
} from './result-display';

// H21 (result audit 30.10.2026): the result screen shows what the server measured, not numbers the browser guessed.
describe('result display', () => {
  it('rates paced exercises by comprehension only, never by the display pace', () => {
    expect(resultPerformanceLevel({ engineType: 'rsvp', rawWpm: null, comprehension: 80 })).toBe('Çok iyi anlama');
    expect(resultPerformanceLevel({ engineType: 'text_fade', rawWpm: null, comprehension: 40 })).toBe('Pratik gerekli');
    expect(resultPerformanceLevel({ engineType: 'rsvp', rawWpm: null, comprehension: null })).toBe('Tamamlandı');
  });

  it('rates measured reading by the server WPM and comprehension', () => {
    expect(resultPerformanceLevel({ engineType: 'free_reading', rawWpm: 320, comprehension: 85 })).toBe('Mükemmel!');
    expect(resultPerformanceLevel({ engineType: 'free_reading', rawWpm: 320, comprehension: 40 })).toBe('Pratik Gerekli');
    expect(resultPerformanceLevel({ engineType: 'reading_comprehension', rawWpm: 300, comprehension: 90 })).toBe('Anlama değerlendirmesi');
  });

  it('waits for the server before showing scores of hidden-feedback and server-scored exercises', () => {
    for (const engineType of ['reading_comprehension', 'exam_simulation', 'grid_interaction', 'focus'])
      expect(awaitsServerResult({ engineType, isAssessment: false, saveStatus: 'saving' })).withContext(engineType).toBeTrue();
    expect(awaitsServerResult({ engineType: 'rsvp', isAssessment: true, saveStatus: 'failed' })).toBeTrue();
    expect(awaitsServerResult({ engineType: 'rsvp', isAssessment: false, saveStatus: 'saving' })).toBeFalse();
    expect(awaitsServerResult({ engineType: 'reading_comprehension', isAssessment: false, saveStatus: 'saved' })).toBeFalse();
    expect(awaitsServerResult({ engineType: 'grid_interaction', isAssessment: false, saveStatus: 'preview' })).toBeFalse();
  });

  it('takes the error count from the server result', () => {
    expect(serverErrorCount({ incorrectCount: 2, measurementStatus: 'Measured' }, 5)).toBe(2);
    expect(serverErrorCount({ incorrectCount: 0, measurementStatus: 'NotMeasured' }, 5)).toBe(5);
  });

  it('shows the exam answer key from the server answer', () => {
    expect(examCorrectAnswer({ correctAnswer: 'B' }, '')).toBe('B');
    expect(examCorrectAnswer({}, 'C')).toBe('C');
    expect(examCorrectAnswer({}, '')).toBe('—');
  });

  it('reads focus hits, misses and false alarms from the server summary', () => {
    expect(focusSummary({ focusSummary: { hits: 7, misses: 2, falseAlarms: 1 } }))
      .toEqual({ hits: 7, misses: 2, falseAlarms: 1 });
    expect(focusSummary({})).toBeNull();
  });

  it('describes a scanning time-out as a measured attempt', () => {
    expect(scanningResultMessage({ timedOut: true, found: 2, total: 3 }))
      .toBe('Süre doldu. 3 hedeften 2 tanesini buldun; sonuç bulduğun hedeflere göre puanlandı.');
    expect(scanningResultMessage({ timedOut: false, found: 3, total: 3 })).toBe('Tüm hedefleri buldun.');
  });
});

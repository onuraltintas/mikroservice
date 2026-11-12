import { fakeAsync, tick } from '@angular/core/testing';
import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { TextStreamEngine } from './text-stream.engine';

describe('Tachistoscope runtime', () => {
  it('keeps the selected digit count at both duration bounds only in previews', fakeAsync(() => {
    const { engine } = create({ previewOnly: true, content: { type: 'number', length: 4, count: 500 },
      timing: { durationMs: 400 }, adaptive: { enabled: true, minLength: 3, maxLength: 5, minDurationMs: 400, maxDurationMs: 600 } });
    engine.start();
    const answerBlocks = (correct: boolean, blocks: number) => {
      for (let round = 0; round < blocks * 5; round++) {
        const stimulus = engine.getCurrentStimulus();
        expect(stimulus.length).toBe(4);
        tick(engine.getCurrentDuration());
        engine.handleInput({ answer: correct ? stimulus : 'wrong' }); tick(600);
      }
    };
    answerBlocks(true, 25);
    expect(engine.getCurrentDuration()).toBe(75);
    expect(engine.getCurrentStimulus().length).toBe(4);
    answerBlocks(false, 30);
    expect(engine.getCurrentDuration()).toBe(600);
    expect(engine.getCurrentStimulus().length).toBe(4);
    engine.reset(); engine.start();
    expect(engine.getCurrentStimulus().length).toBe(4);
    expect(engine.getCurrentDuration()).toBe(400);
    engine.destroy();
  }));
  it('preserves the server-owned student duration when a preview flag is also present', () => {
    const { engine } = create({ previewOnly: true, serverAuthoritative: true,
      content: { type: 'number', length: 3 }, timing: { durationMs: 75 },
      adaptive: { enabled: true, minLength: 3, maxLength: 5, minDurationMs: 400, maxDurationMs: 2000 } });
    expect(engine.getCurrentDuration()).toBe(400);
    engine.destroy();
  });
  for (const [correctCount, duration] of [[0, 575], [1, 575], [2, 500], [3, 425]]) {
    it(`adapts a three-answer preview block with ${correctCount} correct answers to ${duration} ms`, fakeAsync(() => {
      const { engine } = create({ content: { type: 'number', length: 3, count: 6 },
        timing: { durationMs: 500 }, adaptive: { enabled: true, minLength: 3, maxLength: 5 } });
      engine.start();
      for (let i = 0; i < 3; i++) {
        expect(engine.getCurrentDuration()).toBe(500);
        tick(engine.getCurrentDuration());
        engine.handleInput({ answer: i < correctCount ? engine.getCurrentStimulus() : 'wrong' }); tick(600);
      }
      expect(engine.getCurrentDuration()).toBe(duration);
      expect(engine.getCurrentStimulus().length).toBe(3);
      engine.destroy();
    }));
  }

  for (const [minimum, maximum] of [[3, 5], [3, 6], [4, 6], [4, 7]]) {
    it(`changes preview length only at duration bounds and stays within ${minimum}-${maximum}`, fakeAsync(() => {
      const { engine } = create({ content: { type: 'number', length: maximum - 1, count: 100 },
        timing: { durationMs: 500 }, adaptive: { enabled: true, minLength: minimum, maxLength: maximum, minDurationMs: 400, maxDurationMs: 600 } });
      engine.start();
      const answerBlock = (correct: boolean) => {
        for (let i = 0; i < 3; i++) {
          tick(engine.getCurrentDuration());
          engine.handleInput({ answer: correct ? engine.getCurrentStimulus() : 'wrong' }); tick(600);
        }
      };
      answerBlock(true);
      expect(engine.getCurrentDuration()).toBe(425);
      answerBlock(true);
      expect(engine.getCurrentDuration()).toBe(400);
      answerBlock(true);
      expect(engine.getCurrentStimulus().length).toBe(maximum);
      expect(engine.getCurrentDuration()).toBe(500);
      for (let block = 0; block < 2; block++) answerBlock(true);
      expect(engine.getCurrentDuration()).toBe(400);
      answerBlock(true);
      expect(engine.getCurrentStimulus().length).toBe(maximum);
      expect(engine.getCurrentDuration()).toBe(400);
      answerBlock(false);
      expect(engine.getCurrentDuration()).toBe(460);
      for (let block = 0; block < 2; block++) answerBlock(false);
      expect(engine.getCurrentDuration()).toBe(600);
      answerBlock(false);
      expect(engine.getCurrentStimulus().length).toBe(maximum - 1);
      expect(engine.getCurrentDuration()).toBe(500);
      engine.reset(); engine.start();
      expect(engine.getCurrentStimulus().length).toBe(maximum - 1);
      expect(engine.getCurrentDuration()).toBe(500);
      engine.destroy();
    }));
  }
  it('keeps duration on a 2/3 block and ignores an incomplete final block', fakeAsync(() => {
    const { engine } = create({ content: { type: 'number', length: 3, count: 5 },
      timing: { durationMs: 500 }, adaptive: { enabled: true, minLength: 3, maxLength: 5 } });
    engine.start();
    for (const correct of [true, false, true]) {
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: correct ? engine.getCurrentStimulus() : 'wrong' }); tick(600);
    }
    expect(engine.getCurrentDuration()).toBe(500);
    for (let i = 0; i < 2; i++) {
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: engine.getCurrentStimulus() }); tick(600);
    }
    expect(engine.getCurrentDuration()).toBe(500);
    engine.destroy();
  }));
  it('pairs preview phrases randomly instead of with their list neighbour', () => {
    const words: string[] = (TextStreamEngine as any).WORD_POOL;
    const neighbours = new Set(words.map((word, index) => word + ' ' + words[(index + 1) % words.length]));
    let adjacent = 0;
    let total = 0;
    for (let run = 0; run < 5; run++) {
      const { engine } = create({ content: { type: 'phrase', count: 3 }, adaptive: { enabled: false } });
      const pool: string[] = (engine as any).contentPool;
      expect(pool.length).toBeGreaterThan(20);
      for (const phrase of pool) {
        const [first, second] = phrase.split(' ');
        expect(words).toContain(first);
        expect(words).toContain(second);
        expect(first).not.toBe(second);
        total++;
        if (neighbours.has(phrase)) adjacent++;
      }
      engine.destroy();
    }
    expect(adjacent / total).toBeLessThan(0.1);
  });
  it('builds local preview phrases near the configured length', () => {
    const { engine } = create({ content: { type: 'phrase', length: 10, count: 3 }, adaptive: { enabled: true, minLength: 8, maxLength: 12 } });
    engine.start();
    expect(engine.getCurrentStimulus().split(' ').length).toBe(2);
    expect(Math.abs(engine.getCurrentStimulus().length - 10)).toBeLessThanOrEqual(1);
    expect(engine.getContentType()).toBe('phrase');
    engine.destroy();
  });
  for (const [stimulus, answer, correct] of [['kâğıt', 'kağıt', true], ['kâğıt', 'KAĞIT', true], ['hâlâ', 'hala', true],
    ['millî', 'milli', true], ['mahkûm', 'mahkum', true], ['Îlan', 'ilan', true], ['kağıt', 'kâğıt', true],
    ['kağıt', 'kagıt', false], ['ılık', 'ilik', false]] as const) {
    it(`treats "${answer}" as ${correct ? 'correct' : 'wrong'} for "${stimulus}" in a preview`, fakeAsync(() => {
      const { engine } = create({ content: { type: 'word', source: 'custom', items: [stimulus], count: 1 } });
      engine.start();
      expect(engine.getCurrentStimulus()).toBe(stimulus);
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer });
      expect(engine.getLastTrialResult()?.isCorrect).toBe(correct);
      tick(600);
      engine.destroy();
    }));
  }
  it('previews with everyday words only: none excluded, no acronyms, lengths 3–10 covered', () => {
    const excluded = ['Bej', 'Endişeli', 'Korkmuş', 'Kızgın', 'Mat', 'Çirkin', 'Abonelik', 'Adapte', 'Akor', 'Akım', 'Alegori',
      'Analog', 'Antivirüs', 'Arp', 'Banner', 'Beğeni', 'Blog', 'Bluetooth', 'Captcha', 'Diatonik', 'Dize', 'Domain', 'Dram',
      'Drone', 'Emoji', 'Epik', 'GIF', 'Global', 'Hashtag', 'Hikaye', 'Influencer', 'Kafiye', 'Kaos', 'Kaygı', 'Kriz', 'Laptop',
      'Link', 'Lirik', 'Lokal', 'Miraz', 'Mısra', 'Nazım', 'Nesir', 'Not', 'Ping', 'Podcast', 'Prestige', 'Redif', 'Reels',
      'Router', 'Sansür', 'Savaş', 'Scooter', 'Ses', 'Siber', 'Spam', 'Takipçi', 'Teşbih', 'Ton', 'Tweet', 'URL', 'USB',
      'Uyak', 'Viral', 'VPN', 'WiFi', 'Wiki', 'Özçekim'].map(word => word.toLocaleLowerCase('tr-TR'));
    // Abstract or technical terms the old preview list held; difficulty comes from time and length, not vocabulary.
    const unfamiliar = ['konsantrasyon', 'motivasyon', 'verimlilik', 'strateji', 'refleks', 'sinir', 'algı', 'kavram', 'paragraf', 'süreç'];
    const words: string[] = (TextStreamEngine as any).WORD_POOL;
    expect(excluded.length).toBe(68);
    for (const word of words) {
      expect(excluded).not.toContain(word.toLocaleLowerCase('tr-TR'));
      expect(unfamiliar).not.toContain(word);
      expect(word).toMatch(/^[a-zçğıöşü]+$/);
    }
    for (let length = 3; length <= 10; length++) expect(words.some(word => word.length === length)).withContext(`length ${length}`).toBeTrue();
  });
  it('reports the server content type of a word session', () => {
    const { engine } = create({ serverAuthoritative: true, content: { type: 'number' },
      tachistoscope: { count: 3, round: 0, contentType: 'word', displayDurationMs: 480 } });
    expect(engine.getContentType()).toBe('word');
    engine.destroy();
  });
  it('prefers unused equally sized preview words', () => {
    const { engine } = create({ Words: ['bir', 'iki', 'ses'], adaptive: { enabled: false } });
    const runtime = engine as any;
    runtime.currentStimulus = 'iki';
    runtime.trials = [{ stimulus: 'bir' }, { stimulus: 'iki' }];
    spyOn(Math, 'random').and.returnValue(0);
    expect(runtime.selectPreviewStimulus()).toBe('ses');
    engine.destroy();
  });
  function create(config: Record<string, unknown>) {
    const actions: any[] = [];
    let result: EngineResult | undefined;
    const engine = new TextStreamEngine();
    const callbacks: EngineCallbacks = {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined,
      onAction: action => actions.push(action), onError: () => undefined,
      onComplete: value => result = value
    };
    engine.initialize({ mode: 'flash', visuals: { showFixation: false },
      adaptive: { enabled: false }, ...config }, callbacks);
    return { engine, actions, result: () => result };
  }

  it('uses the configured gap and count without replaying a paused stimulus', fakeAsync(() => {
    const { engine } = create({ Words: ['bir', 'iki', 'üç', 'dört'],
      timing: { durationMs: 100, intervalMs: 75 }, content: { count: 2 } });
    expect(engine.state.totalSteps).toBe(2);
    engine.start();
    tick(100);
    engine.handleInput({ answer: engine.getCurrentStimulus() });
    tick(599);
    expect(engine.isShowingContent()).toBeFalse();
    tick(1);
    expect(engine.isShowingContent()).toBeTrue();
    engine.pause();
    expect(engine.isShowingContent()).toBeFalse();
    engine.handleInput({ answer: engine.getCurrentStimulus() });
    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));

  it('preserves the RSVP automatic playback score', fakeAsync(() => {
    const { engine, result } = create({ mode: 'rsvp', Words: ['bir'], DisplayDurationMs: 50 });
    engine.start(); tick(550);
    expect(result()?.accuracy).toBe(100);
    expect(result()?.score).toBe(100);
    engine.destroy();
  }));

  it('does not submit an answer while paused waiting for input', fakeAsync(() => {
    const { engine } = create({ Words: ['bir'], DisplayDurationMs: 50 });
    engine.start(); tick(50); engine.pause();
    engine.handleInput({ answer: 'bir' });
    expect(engine.state.currentStep).toBe(0);
    engine.destroy();
  }));

  it('generates letters rather than words for a letter exercise', () => {
    const { engine } = create({ content: { type: 'letter', count: 2 }, difficultyLevel: 1 });
    engine.start();
    expect(engine.getCurrentStimulus()).toMatch(/^[ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ]$/);
    engine.destroy();
  });

  it('waits for three answers and adjusts duration without also increasing length', fakeAsync(() => {
    const { engine } = create({ Words: ['bir', 'iki', 'masa', 'kapı', 'kalem'],
      DisplayDurationMs: 500, content: { count: 6 }, adaptive: { enabled: true } });
    engine.start();
    for (let round = 0; round < 2; round++) {
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: engine.getCurrentStimulus() });
      tick(600);
    }
    expect(engine.getCurrentDuration()).toBe(500);
    expect(engine.getCurrentStimulus().length).toBe(3);
    tick(engine.getCurrentDuration());
    engine.handleInput({ answer: engine.getCurrentStimulus() });
    tick(600);
    expect(engine.getCurrentDuration()).toBe(425);
    expect(engine.getCurrentStimulus().length).toBe(3);
    engine.destroy();
  }));

  it('uses the same legacy number length as the server, not two-times-level', () => {
    const { engine } = create({ content: { type: 'number', count: 2 }, difficultyLevel: 5 });
    engine.start();
    expect(engine.getCurrentStimulus().length).toBe(7);
    engine.destroy();
  });

  it('uses the explicit starting length and keeps assessment adaptation disabled', fakeAsync(() => {
    const { engine } = create({ difficultyLevel: 5, isAssessmentMode: true,
      timing: { durationMs: 500 }, content: { type: 'number', length: 4, count: 6 },
      adaptive: { enabled: true, minLength: 3, maxLength: 5 } });
    engine.start();
    for (let round = 0; round < 5; round++) {
      expect(engine.getCurrentStimulus().length).toBe(4);
      tick(engine.getCurrentDuration());
      engine.handleInput({ answer: engine.getCurrentStimulus() }); tick(600);
    }
    expect(engine.getCurrentDuration()).toBe(500);
    engine.destroy();
  }));

  it('restores verified counters when an authoritative session is resumed', fakeAsync(() => {
    const { engine, actions, result } = create({ serverAuthoritative: true,
      tachistoscope: { count: 3, round: 2, correctCount: 1, incorrectCount: 1, displayDurationMs: 50 } });
    engine.start();
    expect(engine.state.currentStep).toBe(2);
    expect(engine.getCorrectCount()).toBe(1);
    expect(engine.state.errors).toBe(1);
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 2, stimulus: 'bir', displayDurationMs: 50 } });
    tick(50); engine.handleInput({ answer: 'bir' });
    engine.reconcileServerResponse(actions[1], { isValid: true, isCorrect: true,
      feedbackData: { round: 3 } });
    expect(engine.getCorrectCount()).toBe(2);
    tick(600);
    expect(result()?.accuracy).toBe(67);
    expect(result()?.errors).toBe(1);
    engine.destroy();
  }));

  it('uses explicitly configured custom numbers without generating replacements', () => {
    const { engine } = create({ content: { type: 'number', source: 'custom', items: ['2468'], count: 1 } });
    engine.start();
    expect(engine.getCurrentStimulus()).toBe('2468');
    engine.destroy();
  });

  it('waits for server validation and never reports a reading WPM', fakeAsync(() => {
    const { engine, actions, result } = create({ serverAuthoritative: true,
      tachistoscope: { count: 1, round: 0, displayDurationMs: 50 } });
    engine.start();
    expect(actions[0]?.action).toBe('tachistoscope_present');
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 0, stimulus: 'İZ', displayDurationMs: 50, targetLength: 2 } });
    tick(50);
    engine.handleInput({ answer: 'iz' });
    expect(engine.state.currentStep).toBe(0);
    const answer = actions[1];
    expect(answer.action).toBe('tachistoscope_answer');
    engine.reconcileServerResponse(answer, { isValid: true, isCorrect: true, isCompleted: true,
      feedbackData: { round: 1, displayDurationMs: 50, targetLength: 2,
        trial: { stimulus: 'İZ', userAnswer: 'iz', isCorrect: true, responseTimeMs: 100, displayDurationMs: 50 } } });
    tick(600);
    expect(result()?.accuracy).toBe(100);
    expect(result()?.details.wpm).toBeNull();
    engine.destroy();
  }));

  it('does not reconstruct withheld assessment correctness', fakeAsync(() => {
    const { engine, actions } = create({ serverAuthoritative: true, isAssessmentMode: true,
      tachistoscope: { count: 1, round: 0, displayDurationMs: 50 } });
    engine.start();
    engine.reconcileServerResponse(actions[0], { isValid: true,
      feedbackData: { round: 0, stimulus: 'bir', displayDurationMs: 50 } });
    tick(50); engine.handleInput({ answer: 'bir' });
    engine.reconcileServerResponse(actions[1], { isValid: true, isCorrect: null,
      feedbackData: { round: 1 } });
    expect(engine.getLastTrialResult()).toBeNull();
    expect(engine.state.currentStep).toBe(1);
    engine.destroy();
  }));
});

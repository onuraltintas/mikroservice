import { fakeAsync, tick } from '@angular/core/testing';
import { VocabularyBuilderEngine } from './vocabulary-builder.engine';

describe('VocabularyBuilderEngine server validation contract', () => {
  it('ignores delayed acknowledgements after destroy and cannot resume a stopped timer', fakeAsync(() => {
    const engine = createQuizEngine([], []); (engine as any).serverAuthoritative = true; engine.timeLimitPerWord = 2;
    engine.start(); engine.submitQuizAnswer(engine.getQuizOptions()[0].letter); engine.destroy();
    engine.applyServerResponse({ isValid: true, isCorrect: true }); expect(engine.getCorrectCount()).toBe(0);
    engine.reset(); engine.start(); engine.pause(); engine.stop(); engine.resume(); tick(3000);
    expect(engine.isAwaitingPersistence()).toBeFalse(); expect(engine.state.isRunning).toBeFalse(); engine.destroy();
  }));
  it('defers acknowledged learning advancement until resume so next response time is active', fakeAsync(() => {
    const actions: any[] = []; const engine = new VocabularyBuilderEngine();
    engine.initialize({ serverAuthoritative: true, mode: 'learning', words: [{ id: 'a', word: 'a', definition: 'b' }, { id: 'b', word: 'c', definition: 'd' }] } as any, createCallbacks(actions, []));
    engine.start(); tick(200); engine.markAsKnown(); engine.pause(); tick(500);
    engine.applyServerResponse({ isValid: true, isCorrect: true, feedbackData: { box: 2 } });
    expect(engine.getCurrentWord()?.id).toBe('a'); tick(500); engine.resume(); tick(200); engine.markAsKnown();
    expect(actions[1].responseTime).toBe(200); engine.destroy();
  }));
  it('rejects missing or duplicate words and safely exposes empty results', () => {
    for (const words of [[], [{ id: 'a', word: '', definition: 'b' }], [{ id: 'a', word: 'x', definition: 'y' }, { id: 'a', word: 'z', definition: 't' }]]) {
      const errors: string[] = []; const engine = new VocabularyBuilderEngine();
      engine.initialize({ mode: 'learning', words } as any, createCallbacks([], errors)); engine.pause(); engine.resume(); engine.start();
      expect(errors.length).toBe(1); expect(engine.state.isRunning).toBeFalse(); expect(engine.getQuizQuestion()).toBe(words[0]?.word ?? ''); engine.destroy();
    }
  });
  it('excludes pause time from quiz response duration and blocks input while paused', fakeAsync(() => {
    const actions: any[] = []; const engine = createQuizEngine(actions, []); engine.start();
    tick(300); engine.pause(); engine.pause(); tick(1000); engine.submitQuizAnswer('A'); expect(actions.length).toBe(0);
    engine.resume(); engine.resume(); tick(200); engine.submitQuizAnswer(engine.getCorrectAnswer());
    expect(actions[0].responseTime).toBe(500); engine.destroy();
  }));
  it('shows learning progress, tracks unknown words, and ignores late feedback after stop', () => {
    const engine = new VocabularyBuilderEngine(); const id = crypto.randomUUID();
    engine.initialize({ mode: 'review', previewOnly: true, words: [{ id, word: 'a', definition: 'b' }, { id: crypto.randomUUID(), word: 'c', definition: 'd' }] } as any, createCallbacks([], []));
    engine.start(); engine.showDefinition(); expect(engine.isShowingDefinition()).toBeTrue();
    engine.markAsUnknown(); expect(engine.getIncorrectCount()).toBe(1); expect(engine.getWordBox(id)).toBe(1);
    expect(engine.getProgress()).toEqual({ current: 2, total: 2 });
    engine.markAsKnown(); expect(engine.getCorrectCount()).toBe(1); expect(engine.getResult().accuracy).toBe(50);
    expect(engine.getCurrentWord()).toBeNull(); engine.reset(); expect(engine.state.currentStep).toBe(0);
    engine.start(); engine.stop(); engine.applyServerResponse({ isValid: true, isCorrect: true });
    expect(engine.getCorrectCount()).toBe(0); engine.destroy();
  });
  it('generates real word options in definition-to-word mode and records a correct preview answer', () => {
    const engine = createQuizEngine([], []); (engine as any).quizType = 'definition_to_word';
    engine.start(); expect(engine.getQuizQuestion()).toBe('Öğrenme isteği');
    expect(engine.getQuizOptions().some(option => option.text.startsWith('Option '))).toBeFalse();
    engine.submitQuizAnswer(engine.getCorrectAnswer()); expect(engine.getLastAnswerCorrect()).toBeTrue();
    expect(engine.getCorrectCount()).toBe(1); engine.nextQuizQuestion(); engine.destroy();
  });
  it('does not score a malformed server acknowledgement', () => {
    const engine = createQuizEngine([], []); (engine as any).serverAuthoritative = true;
    engine.start(); engine.submitQuizAnswer(engine.getQuizOptions()[0].letter);
    engine.applyServerResponse({ isValid: true }); expect(engine.state.currentStep).toBe(0);
    expect(engine.isAwaitingPersistence()).toBeFalse(); engine.destroy();
  });
  it('does not restart timeout while a rejected pending answer is paused', fakeAsync(() => {
    const actions: any[] = []; const engine = createQuizEngine(actions, []);
    (engine as any).serverAuthoritative = true; engine.timeLimitPerWord = 2;
    engine.start(); engine.submitQuizAnswer(engine.getQuizOptions()[0].letter); engine.pause();
    engine.applyServerResponse({ isValid: false }); tick(5000);
    expect(actions.length).toBe(1); expect(engine.state.currentStep).toBe(0); engine.destroy();
  }));
  it('preserves the exact remaining deadline and excludes paused response time', fakeAsync(() => {
    const actions: any[] = []; const engine = createQuizEngine(actions, []);
    (engine as any).timeLimitPerWord = 10;
    engine.start(); tick(2500); engine.pause(); tick(5000); engine.resume();
    expect(engine.wordTimeRemaining).toBe(8);
    tick(7500); expect(actions[0]?.action).toBe('timeout');
    engine.destroy();
  }));
  it('blocks learning input before start and during pause', () => {
    const actions: any[] = []; const engine = new VocabularyBuilderEngine();
    engine.initialize({ mode: 'learning', words: [{ id: crypto.randomUUID(), word: 'a', definition: 'b' }] } as any, createCallbacks(actions, []));
    engine.markAsKnown(); expect(actions.length).toBe(0);
    engine.start(); engine.pause(); engine.markAsUnknown(); expect(actions.length).toBe(0); engine.destroy();
  });
  it('waits for learning persistence, permits retry, and uses the server box', () => {
    const actions: any[] = []; const engine = new VocabularyBuilderEngine(); const id = crypto.randomUUID();
    engine.initialize({ serverAuthoritative: true, mode: 'learning', words: [{ id, word: 'a', definition: 'b', box: 4 }] } as any, createCallbacks(actions, []));
    expect(engine.getWordBox(id)).toBe(4);
    engine.start(); engine.markAsKnown(); engine.markAsKnown();
    expect(actions.length).toBe(1); expect(engine.state.isCompleted).toBeFalse();
    engine.applyServerResponse({ isValid: false }); expect(engine.state.currentStep).toBe(0);
    engine.markAsKnown(); engine.applyServerResponse({ isValid: true, isCorrect: true, feedbackData: { box: 5 } });
    expect(engine.getWordBox(id)).toBe(5); expect(engine.state.isCompleted).toBeTrue(); engine.destroy();
  });
  it('clears the prior session when initialized again', () => {
    const engine = createQuizEngine([], []); engine.start(); engine.submitQuizAnswer(engine.getQuizOptions()[0].letter);
    engine.initialize({ mode: 'learning', words: [{ id: crypto.randomUUID(), word: 'yeni', definition: 'anlam' }] } as any, createCallbacks([], []));
    expect(engine.getResult().completedSteps).toBe(0); expect(engine.isShowingFeedback()).toBeFalse(); engine.destroy();
  });
  it('rejects ambiguous quiz content rather than generating placeholder options', () => {
    const errors: string[] = []; const engine = new VocabularyBuilderEngine();
    engine.initialize({ mode: 'quiz', words: [{ id: crypto.randomUUID(), word: 'a', definition: 'b' }] } as any, createCallbacks([], errors));
    engine.start(); expect(engine.state.isRunning).toBeFalse(); expect(errors.length).toBe(1); engine.destroy();
  });
  it('reads words and settings from nested engine configuration', () => {
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      engineConfig: {
        mode: 'quiz',
        quizType: 'definition_to_word',
        timeLimitPerWord: 12,
        words: [{ id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği' }]
      }
    } as any, createCallbacks([], []));

    expect(engine.getMode()).toBe('quiz');
    expect(engine.getCurrentWord()?.word).toBe('merak');
    expect(engine.timeLimitPerWord).toBe(12);
    expect(engine.state.totalSteps).toBe(1);
  });

  it('starts and completes only once and stops running on completion', () => {
    let starts = 0;
    let completions = 0;
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      mode: 'learning',
      words: [{ id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği' }]
    } as any, {
      ...createCallbacks([], []),
      onStart: () => starts++,
      onComplete: () => completions++
    });

    engine.start();
    engine.start();
    engine.markAsKnown();
    (engine as any).completeExercise();

    expect(starts).toBe(1);
    expect(completions).toBe(1);
    expect(engine.state.isRunning).toBeFalse();
  });

  it('does not skip a quiz word before feedback is shown', () => {
    const engine = createQuizEngine([], []);
    engine.start();
    const firstWord = engine.getCurrentWord()?.id;

    engine.nextQuizQuestion();

    expect(engine.getCurrentWord()?.id).toBe(firstWord);
    expect(engine.state.currentStep).toBe(0);
    engine.destroy();
  });

  it('waits for the authoritative server result before scoring a quiz answer', () => {
    const actions: any[] = [];
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      serverAuthoritative: true,
      mode: 'quiz',
      quizType: 'word_to_definition',
      words: [
        { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği' },
        { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma' }
      ]
    } as any, createCallbacks(actions, []));
    engine.start();

    engine.submitQuizAnswer(engine.getQuizOptions()[0].letter);
    expect(engine.state.currentStep).toBe(0);
    expect(engine.state.score).toBe(0);
    expect(engine.getCorrectAnswer()).toBe('');
    const pendingWord = engine.getCurrentWord()?.id;
    engine.nextQuizQuestion();
    expect(engine.getCurrentWord()?.id).toBe(pendingWord);

    engine.applyServerResponse({ isValid: true, isCorrect: true });
    expect(engine.state.currentStep).toBe(1);
    expect(engine.state.score).toBe(1);
    expect(engine.getLastAnswerCorrect()).toBeTrue();
    expect(engine.getCorrectAnswer()).not.toBe('');
    engine.destroy();
  });

  it('releases a rejected authoritative quiz answer for retry', () => {
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      serverAuthoritative: true,
      mode: 'quiz',
      words: [
        { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği' },
        { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma' }
      ]
    } as any, createCallbacks([], []));
    engine.start();
    engine.submitQuizAnswer(engine.getQuizOptions()[0].letter);

    engine.applyServerResponse({ isValid: false });

    expect(engine.isShowingFeedback()).toBeFalse();
    expect(engine.state.currentStep).toBe(0);
    engine.destroy();
  });

  it('waits for the authoritative server result before recording a timeout', () => {
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      serverAuthoritative: true,
      mode: 'quiz',
      timeLimitPerWord: 10,
      words: [
        { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği' },
        { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma' }
      ]
    } as any, createCallbacks([], []));
    engine.start();

    (engine as any).handleTimeout();
    expect(engine.state.currentStep).toBe(0);
    expect(engine.state.errors).toBe(0);

    engine.applyServerResponse({ isValid: true, isCorrect: false });
    expect(engine.state.currentStep).toBe(1);
    expect(engine.state.errors).toBe(1);
    engine.destroy();
  });

  it('sends the selected option text without exposing a client correctness key', () => {
    const actions: any[] = [];
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      mode: 'quiz',
      quizType: 'word_to_definition',
      words: [
        { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği', category: 'Genel', difficultyLevel: 1 },
        { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma', category: 'Genel', difficultyLevel: 1 },
        { id: crypto.randomUUID(), word: 'sabır', definition: 'Bekleme gücü', category: 'Genel', difficultyLevel: 1 },
        { id: crypto.randomUUID(), word: 'sevinç', definition: 'Mutluluk duygusu', category: 'Genel', difficultyLevel: 1 }
      ],
      totalSteps: 4
    } as any, {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: () => undefined,
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: action => actions.push(action)
    });
    engine.start();
    const selected = engine.getQuizOptions()[0];

    engine.submitQuizAnswer(selected.letter);

    expect(actions).toHaveSize(1);
    expect(actions[0].customData.selectedAnswer).toBe(selected.text);
    expect(actions[0].customData.correctAnswer).toBeUndefined();
    engine.destroy();
  });

  it('does not mutate state for an invalid option', () => {
    const actions: any[] = [];
    const errors: string[] = [];
    const engine = createQuizEngine(actions, errors);
    engine.start();

    engine.submitQuizAnswer('Z');

    expect(actions).toHaveSize(0);
    expect(engine.state.currentStep).toBe(0);
    expect(errors).toEqual(['Geçerli bir seçenek seçilmelidir.']);
    engine.destroy();
  });

  it('does not expose the correctness key on timeout', () => {
    const actions: any[] = [];
    const engine = createQuizEngine(actions, []);
    engine.start();

    (engine as any).handleTimeout();

    expect(actions).toHaveSize(1);
    expect(actions[0].action).toBe('timeout');
    expect(actions[0].customData.correctAnswer).toBeUndefined();
    engine.destroy();
  });

  it('uses the server-owned question direction for the current word', () => {
    const actions: any[] = [];
    const engine = new VocabularyBuilderEngine();
    engine.initialize({
      mode: 'quiz',
      quizType: 'word_to_definition',
      words: [
        { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği', category: 'Genel', difficultyLevel: 1, questionType: 'definition' },
        { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma', category: 'Genel', difficultyLevel: 1 }
      ],
      totalSteps: 2
    } as any, createCallbacks(actions, []));
    engine.start();

    engine.submitQuizAnswer(engine.getQuizOptions()[0].letter);

    expect(actions[0].customData.questionType).toBe('definition');
    engine.destroy();
  });
});

function createQuizEngine(actions: any[], errors: string[]): VocabularyBuilderEngine {
  const engine = new VocabularyBuilderEngine();
  engine.initialize({
    mode: 'quiz',
    quizType: 'word_to_definition',
    words: [
      { id: crypto.randomUUID(), word: 'merak', definition: 'Öğrenme isteği', category: 'Genel', difficultyLevel: 1 },
      { id: crypto.randomUUID(), word: 'özen', definition: 'Dikkatli çalışma', category: 'Genel', difficultyLevel: 1 },
      { id: crypto.randomUUID(), word: 'sabır', definition: 'Bekleme gücü', category: 'Genel', difficultyLevel: 1 },
      { id: crypto.randomUUID(), word: 'sevinç', definition: 'Mutluluk duygusu', category: 'Genel', difficultyLevel: 1 }
    ],
    totalSteps: 4
  } as any, createCallbacks(actions, errors));
  return engine;
}

function createCallbacks(actions: any[], errors: string[]): any {
  return {
    onStart: () => undefined,
    onPause: () => undefined,
    onResume: () => undefined,
    onComplete: () => undefined,
    onError: (error: string) => errors.push(error),
    onStateChange: () => undefined,
    onStepComplete: () => undefined,
    onAction: (action: any) => actions.push(action)
  };
}

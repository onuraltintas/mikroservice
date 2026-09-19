import { VocabularyBuilderEngine } from './vocabulary-builder.engine';

describe('VocabularyBuilderEngine server validation contract', () => {
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

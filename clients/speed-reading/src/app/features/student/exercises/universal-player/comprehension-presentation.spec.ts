import { ExercisePlayerComponent } from './exercise-player.component';

describe('comprehension presentation', () => {
  it('keeps all three valid question categories in configured comprehension previews', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.parsedConfig = { engineType: 'reading_comprehension', engineConfig: { content: { questionCount: 2 } } };
    const questions = [1, 2, 3, 4].map(type => ({ type, bloomLevel: 3, correctAnswer: 'A',
      optionA: 'A', optionB: 'B', optionC: 'C', optionD: 'D' }));
    const config = player.createPreviewReadingConfig({ content: 'Gerçek okuma metni.', questions });
    expect(config.questions.map((question: any) => question.type)).toEqual([1, 2, 3]);
  });

  it('permits text review only in learning, never in assessment', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension' };
    player.parsedConfig = { engineConfig: { comprehensionMode: 'learning' } };
    expect(player.isComprehensionLearning()).toBeTrue();
    player.isAssessmentMode = true;
    expect(player.isComprehensionLearning()).toBeFalse();
    player.isAssessmentMode = false;
    player.parsedConfig.engineConfig.comprehensionMode = 'evaluation';
    expect(player.isComprehensionLearning()).toBeFalse();
  });

  it('defers correctness in evaluation and keeps immediate feedback in learning', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension' };
    player.parsedConfig = { engineConfig: { comprehensionMode: 'evaluation' } };
    expect(player.defersComprehensionFeedback()).toBeTrue();
    player.parsedConfig.engineConfig.comprehensionMode = 'learning';
    expect(player.defersComprehensionFeedback()).toBeFalse();
  });

  it('does not install a default question deadline for comprehension', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension' };
    player.comprehensionQuestions = [{ questionId: 'q', targetTimeSeconds: 60 }];
    player.currentQuestionIndex = 0;
    player.cdr = { detectChanges: () => {} };
    await player.startQuestionTimer();
    const timer = player.questionTimerInterval;
    player.stopQuestionTimer();
    expect(timer).toBeFalsy();
    expect(player.questionTimeRemaining).toBe(0);
  });

  it('applies the comprehension word band to previews', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.exercise = { exerciseTypeName: 'Comprehension' };
    player.parsedConfig = { engineType: 'reading_comprehension', engineConfig: { content: { minWordCount: 180, maxWordCount: 250 } } };
    expect(player.previewTextMatchesLength(179)).toBeFalse();
    expect(player.previewTextMatchesLength(251)).toBeFalse();
    expect(player.previewTextMatchesLength(200)).toBeTrue();
  });

  it('does not reward speed in comprehension performance labels', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension' };
    expect(player.getPerformanceLevel(150, 80)).toEqual(player.getPerformanceLevel(500, 80));
  });

  it('does not display whole-text speed before reading is finished', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension', getWordCount: () => 100 };
    player.engineState = { timeElapsed: 1000 };
    expect(player.getCurrentReadingWpm()).toBe(0);
  });

  it('exposes configured line height and the same completion gate as the engine', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension', getLineHeight: () => 2.2, canComplete: () => false };
    player.engineState = { isRunning: true, isPaused: false };
    expect(player.getComprehensionLineHeight()).toBe(2.2);
    expect(player.canCompleteReading()).toBeFalse();
  });
});

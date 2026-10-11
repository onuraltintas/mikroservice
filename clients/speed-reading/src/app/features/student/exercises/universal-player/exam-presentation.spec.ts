import { ExercisePlayerComponent } from './exercise-player.component';
import { getCustomPreviewControls } from './custom-preview-settings';
import { ExamSimulationEngine } from './engines/exam-simulation.engine';

describe('exam presentation', () => {
  it('keeps answer correctness hidden during the exam', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation' };
    player.isAssessmentMode = false;
    expect(player.defersComprehensionFeedback()).toBeTrue();
  });
  it('starts reading before questions in the new exam protocol', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation', start: jasmine.createSpy('start') };
    player.backendSessionConfig = { examProtocolVersion: 1 };
    player.comprehensionQuestions = [{}]; player.clickedCells = new Set(); player.correctCells = new Set(); player.wrongCells = new Set();
    player.questionAnswers = []; player.engineState = {}; player.cdr = { detectChanges: () => undefined };
    player.isPreviewSession = () => false;
    player.startQuestionTimer = jasmine.createSpy('question');
    player.startReadingTracking = (callback: () => void) => callback();
    await player.startExercise();
    expect(player.engine.start).toHaveBeenCalled();
    expect(player.startQuestionTimer).not.toHaveBeenCalled();
  });
  it('enforces minimum reading time on the button and has a bounded reading clock', () => {
    const engine = new ExamSimulationEngine() as any;
    engine.initialize({ content: { text: 'Gerçek sınav metni.' }, timing: { minReadingTimeMs: 10000, maxReadingTimeMs: 30000 } },
      { onStateChange: () => undefined, onComplete: () => undefined, onStart: () => undefined });
    engine.state.isRunning = true; engine.state.timeElapsed = 9999;
    expect(engine.canComplete()).toBeFalse();
    engine.state.timeElapsed = 10000;
    expect(engine.canComplete()).toBeTrue();
    expect(engine.getMaximumMs()).toBe(30000);
  });
  it('restores exam answers after refreshing the page', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.isAssessmentMode = false; player.backendSessionConfig = { engineType: 'exam_simulation' }; player.questionAnswers = []; player.currentQuestionIndex = 0;
    player.comprehensionQuestions = [{ questionId: 'one' }, { questionId: 'two' }];
    player.restoreAssessmentQuestionProgress({ answers: [{ questionId: 'one', answer: 'A', isCorrect: true, timeSpentSeconds: 3 }] });
    expect(player.questionAnswers.length).toBe(1);
    expect(player.currentQuestionIndex).toBe(1);
  });
  it('does not install a timer after a pending question start was cancelled', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation' }; player.currentQuestionIndex = 0;
    player.comprehensionQuestions = [{ questionId: 'question' }]; player.isPreviewSession = () => false;
    player.cdr = { detectChanges: () => undefined };
    let approve!: () => void;
    player.enqueueAction = (_: any, apply: any) => new Promise<void>(resolve => approve = () => { apply({ isValid: true }); resolve(); });
    const pending = player.startQuestionTimer();
    player.stopQuestionTimer(); approve(); await pending;
    const interval = player.questionTimerInterval;
    if (interval) clearInterval(interval);
    expect(interval).toBeFalsy();
  });
  it('exposes exam duration and line spacing controls', () => {
    const controls = getCustomPreviewControls({ engineType: 'exam_simulation', engineConfig: { timing: { questionTimeSeconds: 30 } } });
    expect(controls.map(control => control.key)).toContain('questionTimeSeconds');
    expect(controls.map(control => control.key)).toContain('lineHeightPercent');
  });
  it('uses question activity duration for exam results', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation' }; player.engineState = { timeElapsed: 0 };
    player.questionAnswers = [{ isCorrect: true, timeSpent: 4, targetTime: 30 }];
    player.comprehensionQuestions = [{}]; player.readingWpm = 0;
    player.stopQuestionTimer = () => undefined; player.saveResult = () => undefined;
    player.cdr = { detectChanges: () => undefined };
    player.finishQuestionPhase();
    expect(player.result.totalTime).toBe(4000);
  });
  it('shows the authoritative reading body beside a question without its own paragraph', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation', getText: () => 'Sunucunun sınav paragrafı.' };
    player.comprehensionQuestions = [{ questionText: 'Ana fikir nedir?' }];
    player.currentQuestionIndex = 0;
    expect(player.getQuestionContent()).toBe('Sunucunun sınav paragrafı.');
  });

  it('does not label combined exam activity as reading speed', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation', getWordCount: () => 100 };
    player.engineState = { timeElapsed: 1000 };
    expect(player.getCurrentReadingWpm()).toBe(0);
  });

  it('starts the question timer only after server approval', async () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'exam_simulation' };
    player.comprehensionQuestions = [{ questionId: 'question' }]; player.currentQuestionIndex = 0;
    player.backendSessionConfig = { examQuestionTimeSeconds: 30 };
    player.isPreviewSession = () => false;
    player.stopQuestionTimer = () => undefined; player.cdr = { detectChanges: () => undefined };
    player.enqueueAction = jasmine.createSpy().and.callFake(async (_: any, apply: any) => apply({ isValid: true }));
    await player.startQuestionTimer();
    expect(player.enqueueAction).toHaveBeenCalled();
    expect(player.questionTimeRemaining).toBe(30);
    clearInterval(player.questionTimerInterval);
  });
});

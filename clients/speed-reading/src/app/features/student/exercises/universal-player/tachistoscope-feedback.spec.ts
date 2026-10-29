import { ExercisePlayerComponent } from './exercise-player.component';

describe('Tachistoscope feedback presentation', () => {
  it('hides feedback during fixation, stimulus and answer entry', () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.tachistoscopeFeedback = { isCorrect: true, correctAnswer: 'bir' };
    player.isShowingFixation = () => true;
    player.isShowingStimulus = () => false;
    player.isWaitingForAnswer = () => false;
    expect(player.isShowingTachistoscopeFeedback()).toBeFalse();
    player.isShowingFixation = () => false;
    expect(player.isShowingTachistoscopeFeedback()).toBeTrue();
    player.isShowingStimulus = () => true;
    expect(player.isShowingTachistoscopeFeedback()).toBeFalse();
    player.isShowingStimulus = () => false;
    player.isWaitingForAnswer = () => true;
    expect(player.isShowingTachistoscopeFeedback()).toBeFalse();
  });

  // Reported 29.10.2026: after a wrong answer, the next correct answer first showed the old "Yanlış" and only then
  // "Doğru", because the previous round's feedback stayed visible while the server checked the new answer.
  it('does not show the previous round while the new answer is being checked', () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    let awaitingServer = false;
    const handleInput = jasmine.createSpy('handleInput').and.callFake(() => { awaitingServer = true; });
    player.engine = { engineType: 'text_stream', handleInput, isAwaitingServer: () => awaitingServer };
    player.cdr = { detectChanges: () => undefined };
    player.isAssessmentMode = false;
    player.isShowingFixation = () => false;
    player.isShowingStimulus = () => false;
    player.isWaitingForAnswer = () => false;
    player.tachistoscopeFeedback = { isCorrect: false, correctAnswer: 'masa' };
    player.tachistoscopeAnswer = 'kalem';

    player.submitTachistoscopeAnswer();

    expect(handleInput).toHaveBeenCalledWith({ answer: 'kalem' });
    expect(player.isShowingTachistoscopeFeedback()).toBeFalse();
  });

  it('does not bring the last feedback back while the next stimulus is being fetched', () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    player.engine = { engineType: 'text_stream', isAwaitingServer: () => true };
    player.isAssessmentMode = false;
    player.isShowingFixation = () => false;
    player.isShowingStimulus = () => false;
    player.isWaitingForAnswer = () => false;
    player.tachistoscopeFeedback = { isCorrect: false, correctAnswer: 'masa' };

    expect(player.isShowingTachistoscopeFeedback()).toBeFalse();
  });
});

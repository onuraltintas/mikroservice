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
});

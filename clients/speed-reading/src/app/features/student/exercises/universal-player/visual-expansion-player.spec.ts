import { ExercisePlayerComponent } from './exercise-player.component';

describe('visual expansion answer UI', () => {
  function player(stimuli: string[]) {
    const value = Object.create(ExercisePlayerComponent.prototype) as any;
    value.expansionAnswers = [];
    value.engine = { engineType: 'visual_expansion', getLastShownStimuli: () => stimuli,
      getExpectedAnswerCount: () => stimuli.length, getPattern: () => 'radial',
      isWaitingForInput: true, state: { isPaused: false }, handleInput: jasmine.createSpy('input') };
    value.cdr = { detectChanges: () => undefined };
    return value;
  }
  it('accepts four complete words in the displayed radial order', () => {
    const value = player(['GÜN', 'SU', 'YOL', 'EV']);
    expect(value.getExpansionAnswerSlots()).toEqual([0, 1, 2, 3]);
    expect(value.getExpansionAnswerMaxLength(0)).toBe(3);
    expect(value.getExpansionAnswerLabel(2)).toBe('Alt sol');
    value.expansionAnswers = ['GÜN', 'SU', 'YOL', 'EV'];
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).toHaveBeenCalledWith({ answers: ['GÜN', 'SU', 'YOL', 'EV'] });
  });
  it('does not submit missing answers or accept input while paused', () => {
    const value = player(['A', 'B']);
    value.expansionAnswers = ['A'];
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).not.toHaveBeenCalled();
    value.expansionAnswers = ['A', 'B'];
    value.engine.state.isPaused = true;
    value.submitExpansionAnswer();
    expect(value.engine.handleInput).not.toHaveBeenCalled();
  });
});

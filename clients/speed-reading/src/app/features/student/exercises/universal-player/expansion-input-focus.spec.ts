import { ExercisePlayerComponent } from './exercise-player.component';

describe('Visual expansion answer focus', () => {
  function setup(max = 1) {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    const area = document.createElement('div');
    area.innerHTML = '<input class="char-input"><input class="char-input">';
    document.body.appendChild(area);
    player.visualExpansionArea = { nativeElement: area };
    player.engineState = { isPaused: false };
    player.expansionAnswers = [];
    player.getExpansionAnswerSlots = () => [0, 1];
    player.getExpansionAnswerMaxLength = () => max;
    player.cdr = { detectChanges: () => undefined };
    const inputs = area.querySelectorAll('input');
    const type = (index: number, value: string, isComposing = false) => {
      inputs[index].value = value;
      player.onExpansionAnswerInput(index, { target: inputs[index], isComposing });
    };
    return { player, area, inputs, type };
  }

  it('moves to the next box after a complete answer', () => {
    const { player, area, inputs, type } = setup();
    inputs[0].focus();
    type(0, 'A');
    expect(document.activeElement).toBe(inputs[1]);
    expect(player.expansionAnswers).toEqual(['A', '']);
    area.remove();
  });

  // Android keyboards compose every letter; the composition must not keep the student in a full box.
  it('moves on while the keyboard is still composing the letter', () => {
    const { player, area, inputs, type } = setup();
    inputs[0].focus();
    type(0, 'A', true);
    expect(document.activeElement).toBe(inputs[1]);
    expect(player.expansionAnswers).toEqual(['A', '']);
    area.remove();
  });

  it('carries a second letter typed into a full box into the next box so the answer can be sent', () => {
    const { player, area, inputs, type } = setup();
    inputs[0].focus();
    type(0, 'AB', true);
    expect(inputs[0].value).toBe('A');
    expect(inputs[1].value).toBe('B');
    expect(player.expansionAnswers).toEqual(['A', 'B']);
    area.remove();
  });

  it('does not advance an incomplete two-letter answer', () => {
    const { area, inputs, type } = setup(2);
    inputs[0].focus();
    type(0, 'A');
    expect(document.activeElement).toBe(inputs[0]);
    area.remove();
  });

  it('goes back to the previous box when deleting in an empty one', () => {
    const { player, area, inputs } = setup();
    inputs[1].focus();
    const event = { target: inputs[1], preventDefault: jasmine.createSpy('preventDefault') };
    player.onExpansionAnswerBackspace(1, event);
    expect(document.activeElement).toBe(inputs[0]);
    expect(event.preventDefault).toHaveBeenCalled();
    area.remove();
  });
});

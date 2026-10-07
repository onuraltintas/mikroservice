import { ExercisePlayerComponent } from './exercise-player.component';

describe('Visual expansion answer focus', () => {
  function setup() {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    const area = document.createElement('div');
    area.innerHTML = '<input class="char-input"><input class="char-input">';
    document.body.appendChild(area);
    player.visualExpansionArea = { nativeElement: area };
    player.engineState = { isPaused: false };
    player.getExpansionAnswerMaxLength = () => 1;
    return { player, area, inputs: area.querySelectorAll('input') };
  }
  it('moves to the next box after a complete answer', () => {
    const { player, area, inputs } = setup();
    inputs[0].focus();
    player.onExpansionAnswerInput(0, { target: { value: 'A' }, isComposing: false });
    expect(document.activeElement).toBe(inputs[1]);
    area.remove();
  });
  it('does not advance an incomplete two-letter answer or composition', () => {
    const { player, area, inputs } = setup();
    player.getExpansionAnswerMaxLength = () => 2;
    inputs[0].focus();
    player.onExpansionAnswerInput(0, { target: { value: 'A' }, isComposing: false });
    expect(document.activeElement).toBe(inputs[0]);
    player.onExpansionAnswerInput(0, { target: { value: 'AB' }, isComposing: true });
    expect(document.activeElement).toBe(inputs[0]);
    area.remove();
  });
});

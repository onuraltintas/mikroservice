import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { GridInteractionEngine } from './grid-interaction.engine';

describe('GridInteractionEngine', () => {
  it('uses the server layout when the template also has grid dimensions', () => {
    const engine = new GridInteractionEngine();
    const layout = [
      [4, 3],
      [2, 1]
    ];
    engine.initialize({
      gridSize: 2,
      sequenceType: 'numeric',
      grid: { rows: 2, cols: 2 },
      serverGrid: layout
    } as any, {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: () => undefined, onError: () => undefined,
      onStateChange: () => undefined, onStepComplete: () => undefined,
      onAction: () => undefined
    });

    expect(engine.getGrid()).toEqual([4, 3, 2, 1]);
  });

  it('queues the final 6x6 click before completing the exercise', () => {
    const events: string[] = [];
    let result: EngineResult | undefined;
    const engine = new GridInteractionEngine();
    const callbacks: EngineCallbacks = {
      onStart: () => undefined,
      onPause: () => undefined,
      onResume: () => undefined,
      onComplete: value => { result = value; events.push('complete'); },
      onError: () => undefined,
      onStateChange: () => undefined,
      onStepComplete: () => undefined,
      onAction: action => events.push(`${action.action}:${action.number}`)
    };
    engine.initialize({ gridSize: 6, sequenceType: 'numeric' }, callbacks);
    engine.start();
    for (let number = 1; number <= 36; number++) {
      engine.handleInput({ cellIndex: engine.getGrid().indexOf(number), value: number });
    }

    expect(events.slice(-2)).toEqual(['grid_click:36', 'complete']);
    expect(result?.accuracy).toBe(100);
  });
});

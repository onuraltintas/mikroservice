import { EngineCallbacks, EngineResult } from './base-engine.interface';
import { GridInteractionEngine } from './grid-interaction.engine';

describe('GridInteractionEngine', () => {
  function create(config: any) {
    const complete = jasmine.createSpy('complete');
    const error = jasmine.createSpy('error');
    const engine = new GridInteractionEngine();
    engine.initialize(config, {
      onStart: () => undefined, onPause: () => undefined, onResume: () => undefined,
      onComplete: complete, onError: error, onStateChange: () => undefined,
      onStepComplete: () => undefined, onAction: () => undefined
    });
    return { engine, complete, error };
  }

  it('finishes an incomplete table when its time limit expires', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(0));
    try {
      const { engine, complete, error } = create({ gridSize: 3, timeLimit: 1 });
      engine.start();
      jasmine.clock().tick(1000);
      expect(complete).not.toHaveBeenCalled();
      expect(error).toHaveBeenCalledTimes(1);
      expect(engine.state.isRunning).toBeFalse();
      engine.destroy();
    } finally { jasmine.clock().uninstall(); }
  });

  it('retains search time before a pause and measures the exact final click', () => {
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(0));
    try {
      const { engine, complete } = create({ gridSize: 3 });
      engine.start();
      jasmine.clock().tick(250);
      engine.pause();
      jasmine.clock().tick(2000);
      engine.resume();
      jasmine.clock().tick(125);
      for (let value = 1; value <= 9; value++) {
        engine.handleInput({ cellIndex: engine.getGrid().indexOf(value), value });
      }
      const result = complete.calls.mostRecent().args[0];
      expect(result.totalTime).toBe(375);
      expect(result.details.clickHistory[0].responseTime).toBe(375);
      engine.destroy();
    } finally { jasmine.clock().uninstall(); }
  });

  it('does not accept a number submitted for another cell', () => {
    const { engine } = create({ gridSize: 3 });
    engine.start();
    engine.handleInput({ cellIndex: engine.getGrid().indexOf(2), value: 1 });
    expect(engine.state.currentStep).toBe(0);
    engine.destroy();
  });

  it('rejects duplicate server values and generates a complete numeric table', () => {
    const { engine } = create({ gridSize: 3, serverGrid: Array(9).fill(1) });
    expect([...engine.getGrid()].sort((a, b) => Number(a) - Number(b))).toEqual([1,2,3,4,5,6,7,8,9]);
  });
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

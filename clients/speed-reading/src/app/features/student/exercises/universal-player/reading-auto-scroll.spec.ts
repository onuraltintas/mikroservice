import { ExercisePlayerComponent } from './exercise-player.component';
import { EngineFactory } from './engines/engine-factory';

describe('reading player three-line auto-scroll', () => {
  const modes = [
    ['word_highlight', 'wordHighlightContainer', 'handleWordHighlightScroll', 'chunk-group highlight', 'shouldScrollWord'],
    ['text_fade', 'textFadeContainer', 'handleTextFadeScroll', 'fading-word-item active', 'shouldScrollFade'],
    ['regression_reduction', 'regressionContainer', 'handleRegressionScroll', 'regression-word active', 'shouldScrollRegression'],
    ['subvocalization_reduction', 'subvocDisplayArea', 'handleSubvocScroll', 'subvoc-word active', 'shouldScrollSubvoc']
  ];
  let container: HTMLDivElement;
  let active: HTMLSpanElement;
  let scroll: jasmine.Spy;
  let activeTop: number;
  let containerHeight: number;
  let contentHeight: number;
  let viewportDescriptor: PropertyDescriptor | undefined;

  beforeEach(() => {
    viewportDescriptor = Object.getOwnPropertyDescriptor(window, 'innerHeight');
    Object.defineProperty(window, 'innerHeight', { configurable: true, value: 700 });
    spyOn(window, 'matchMedia').and.returnValue({ matches: false } as MediaQueryList);
    container = document.createElement('div');
    active = document.createElement('span');
    active.style.lineHeight = '20px';
    container.appendChild(active);
    document.body.appendChild(container);
    activeTop = 320;
    containerHeight = 400;
    contentHeight = 1600;
    Object.defineProperties(container, {
      clientHeight: { get: () => containerHeight },
      scrollHeight: { get: () => contentHeight }
    });
    spyOn(container, 'getBoundingClientRect').and.callFake(() => new DOMRect(0, 100, 500, containerHeight));
    spyOn(active, 'getBoundingClientRect').and.callFake(() => new DOMRect(0, 100 + activeTop - container.scrollTop, 60, 20));
    scroll = spyOn(container, 'scrollTo');
  });

  afterEach(() => {
    container.remove();
    if (viewportDescriptor) Object.defineProperty(window, 'innerHeight', viewportDescriptor);
  });

  function playerFor(mode: string[]): any {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: mode[0], shouldAutoScroll: () => true };
    player.engineState = { isRunning: true, isPaused: false };
    player[mode[1]] = { nativeElement: container };
    player.getSubvocDisplayMode = () => 'highlight';
    active.className = mode[3];
    return player;
  }

  for (const mode of modes) {
    it(`${mode[0]} scrolls after a timer render without waiting for another host view check`, () => {
      const player = playerFor(mode);
      const order: string[] = [];
      let callbacks: any;
      const engine = { ...player.engine, initialize: (_config: unknown, value: unknown) => callbacks = value };
      spyOn(EngineFactory, 'create').and.returnValue(engine as any);
      player.parsedConfig = { engineType: mode[0] };
      player.sessionId = 'preview-mode';
      player.cdr = { detectChanges: () => order.push('render') };
      scroll.and.callFake(() => order.push('scroll'));
      player.initializeEngine();
      callbacks.onStateChange({ isRunning: true, isPaused: false, currentStep: 1 });
      expect(order).toEqual(['render', 'scroll']);
    });

    it(`${mode[0]} waits until three lines remain, then scrolls a block`, () => {
      const player = playerFor(mode);
      activeTop = 300;
      player[mode[2]]();
      expect(scroll).not.toHaveBeenCalled();
      activeTop = 320;
      player[mode[2]]();
      expect(scroll).toHaveBeenCalledOnceWith({ top: 300, behavior: 'smooth' });
    });

    it(`${mode[0]} does not restart the same smooth scroll on every timer tick`, () => {
      const player = playerFor(mode);
      player[mode[2]]();
      player[mode[2]]();
      player[mode[2]]();
      expect(scroll).toHaveBeenCalledTimes(1);
    });

    it(`${mode[0]} repeats only when another three-line boundary is reached`, () => {
      const player = playerFor(mode);
      player[mode[2]]();
      container.scrollTop = 300;
      activeTop = 340;
      player[mode[2]]();
      expect(scroll).toHaveBeenCalledTimes(1);
      activeTop = 620;
      player[mode[2]]();
      expect(scroll).toHaveBeenCalledWith({ top: 600, behavior: 'smooth' });
      expect(scroll).toHaveBeenCalledTimes(2);
    });

    it(`${mode[0]} uses the actual viewport when the panel extends below the screen`, () => {
      const player = playerFor(mode);
      Object.defineProperty(window, 'innerHeight', { configurable: true, value: 400 });
      containerHeight = 500;
      activeTop = 220;
      player[mode[2]]();
      expect(scroll).toHaveBeenCalledOnceWith({ top: 200, behavior: 'smooth' });
    });

    it(`${mode[0]} leaves short text and paused reading still`, () => {
      const player = playerFor(mode);
      contentHeight = containerHeight;
      player[mode[2]]();
      expect(scroll).not.toHaveBeenCalled();
      contentHeight = 1600;
      player.engineState.isPaused = true;
      player[mode[4]] = true;
      player.ngAfterViewChecked();
      expect(scroll).not.toHaveBeenCalled();
    });
  }

  it('respects an explicitly disabled pacer and non-highlight subvocalization', () => {
    const wordPlayer = playerFor(modes[0]);
    wordPlayer.engine.shouldAutoScroll = () => false;
    wordPlayer.handleWordHighlightScroll();
    const subvocPlayer = playerFor(modes[3]);
    subvocPlayer.getSubvocDisplayMode = () => 'rsvp';
    subvocPlayer.handleSubvocScroll();
    expect(scroll).not.toHaveBeenCalled();
  });
});

import { AttentionGame, ATTENTION_PRESETS } from './attention-game';

describe('Hedefi Yakala prototype', () => {
  const preset = { label: 'Test', shapeCount: 6, rounds: 2, responseMs: 4000 };
  const game = () => new AttentionGame(preset, () => 0);

  it('has one target, the configured board size and a fresh target position', () => {
    const g = game(); g.startSession(0);
    expect(g.shapes.length).toBe(6);
    expect(g.shapes.filter(shape => shape === 'star').length).toBe(1);
    const first = g.shapes.indexOf('star');
    g.choose(first, 300); g.tick(1000);
    expect(g.shapes.indexOf('star')).not.toBe(first);
  });

  it('keeps the three untimed tutorial rounds out of real results', () => {
    const g = game(); g.startTutorial(0);
    g.tick(100000);
    expect(g.status).toBe('playing');
    g.choose(g.shapes.indexOf('circle'), 100001);
    expect(g.status).toBe('playing');
    expect(g.message).toContain('Üstteki sembolü');
    for (let index = 0; index < 3; index++) {
      g.choose(g.shapes.indexOf('star'), 101000 + index * 1000);
      g.tick(101700 + index * 1000);
    }
    expect(g.status).toBe('ready');
    expect(g.hits + g.wrong + g.missed).toBe(0);
    g.startSession(105000);
    expect(g.round).toBe(1);
    expect(g.hits).toBe(0);
  });

  it('counts a correct answer once and averages only correct response times', () => {
    const g = game(); g.startSession(0);
    g.choose(g.shapes.indexOf('star'), 500);
    g.choose(g.shapes.indexOf('star'), 501);
    expect(g.hits).toBe(1);
    expect(g.meanResponseMs).toBe(500);
    g.tick(1200); g.choose(g.shapes.indexOf('circle'), 1800); g.tick(2500);
    expect(g.wrong).toBe(1);
    expect(g.status).toBe('complete');
    expect(g.accuracy).toBe(50);
    expect(g.meanResponseMs).toBe(500);
  });

  it('counts a timeout once, including a click at the exact deadline', () => {
    const g = game(); g.startSession(0);
    g.choose(g.shapes.indexOf('star'), 4000); g.tick(4001);
    expect(g.hits).toBe(0); expect(g.missed).toBe(1);
    expect(g.status).toBe('feedback');
    g.tick(4700); g.tick(8700); g.tick(9400);
    expect(g.status).toBe('complete'); expect(g.missed).toBe(2);
    expect(g.accuracy).toBe(0); expect(g.meanResponseMs).toBeNull();
  });

  it('rejects invalid positions and clicks outside active rounds', () => {
    const g = game(); g.choose(0, 0); g.startSession(0);
    for (const index of [-1, 6, 0.5, NaN]) g.choose(index, 1);
    expect(g.hits + g.wrong).toBe(0);
    g.pause(500); g.choose(g.shapes.indexOf('star'), 501);
    expect(g.hits).toBe(0);
  });

  it('freezes remaining time and excludes paused time from response speed', () => {
    const g = game(); g.startSession(0); g.pause(1000);
    expect(g.remainingMs(20000)).toBe(3000);
    g.tick(20000); expect(g.missed).toBe(0);
    g.resume(21000); g.choose(g.shapes.indexOf('star'), 21500);
    expect(g.meanResponseMs).toBe(1500);
  });

  it('freezes feedback transitions during a pause', () => {
    const g = game(); g.startSession(0); g.choose(g.shapes.indexOf('star'), 100);
    g.pause(200); g.tick(10000); expect(g.round).toBe(1);
    g.resume(10200); g.tick(10700); expect(g.round).toBe(1);
    g.tick(10800); expect(g.round).toBe(2);
  });

  it('does not let delayed callbacks skip multiple unseen boards', () => {
    const g = game(); g.startSession(0); g.tick(20000); g.tick(21000);
    expect(g.missed).toBe(1); expect(g.round).toBe(2);
    expect(g.remainingMs(21000)).toBe(4000);
  });

  it('restart clears earlier counters and latency samples', () => {
    const g = game(); g.startSession(0); g.choose(g.shapes.indexOf('star'), 100);
    g.startSession(200); expect(g.hits).toBe(0); expect(g.meanResponseMs).toBeNull();
  });

  it('completes exactly the chosen round count and cannot score afterwards', () => {
    const g = game(); g.startSession(0);
    g.choose(g.shapes.indexOf('star'), 100); g.tick(800);
    g.choose(g.shapes.indexOf('star'), 900); g.tick(1600);
    g.choose(g.shapes.indexOf('star'), 1700); g.tick(20000);
    expect(g.status).toBe('complete'); expect(g.round).toBe(2);
    expect(g.hits).toBe(2); expect(g.accuracy).toBe(100);
  });

  it('validates settings and exposes explicit pilot presets', () => {
    for (const settings of [{ ...preset, rounds: 0 }, { ...preset, shapeCount: 1 },
      { ...preset, responseMs: 0 }, { ...preset, shapeCount: 6.5 }]) {
      expect(() => new AttentionGame(settings)).toThrowError();
    }
    expect(ATTENTION_PRESETS.length).toBe(20);
  });

  const approved = [
    [[6, 6000, 24], [8, 5500, 28], [10, 5000, 32], [12, 4500, 36], [16, 4000, 40]],
    [[8, 5500, 28], [10, 5000, 32], [12, 4500, 36], [16, 4000, 40], [20, 3500, 44]],
    [[10, 5000, 32], [12, 4500, 36], [16, 4000, 40], [20, 3500, 44], [20, 3000, 48]],
    [[10, 5000, 32], [12, 4500, 36], [16, 4000, 40], [20, 3500, 44], [20, 3000, 48]]
  ];
  for (const [group, levels] of approved.entries()) {
    for (const [level, [shapeCount, responseMs, rounds]] of levels.entries()) {
      it(`applies the approved group ${group + 1} level ${level + 1} through completion`, () => {
        const settings = ATTENTION_PRESETS[group * 5 + level];
        expect(settings).toEqual(jasmine.objectContaining({ shapeCount, responseMs, rounds }));
        if (!settings) return;
        const g = new AttentionGame(settings, () => 0);
        g.startSession(0);
        expect(g.shapes.length).toBe(shapeCount);
        expect(g.remainingMs(0)).toBe(responseMs);
        let now = 0;
        for (let round = 1; round <= rounds; round++) {
          expect(g.round).toBe(round);
          expect(g.status).toBe('playing');
          g.choose(g.shapes.indexOf('star'), now + 100);
          now += 800; g.tick(now);
        }
        expect(g.status).toBe('complete');
        expect(g.hits).toBe(rounds);
      });
    }
  }
});

import { shuffled, shuffleInPlace } from './shuffle';

describe('shuffle', () => {
  it('places every item in every position about equally often', () => {
    const counts = [0, 0, 0, 0];
    const runs = 20000;
    for (let run = 0; run < runs; run++) counts[shuffled(['correct', 'b', 'c', 'd']).indexOf('correct')]++;
    for (const count of counts) expect(Math.abs(count / runs - 0.25)).toBeLessThan(0.02);
  });

  it('keeps the items and does not change the input of shuffled()', () => {
    const input = [1, 2, 3, 4, 5];
    const output = shuffled(input);
    expect(input).toEqual([1, 2, 3, 4, 5]);
    expect([...output].sort()).toEqual([1, 2, 3, 4, 5]);
    expect(shuffleInPlace([] as number[])).toEqual([]);
  });
});

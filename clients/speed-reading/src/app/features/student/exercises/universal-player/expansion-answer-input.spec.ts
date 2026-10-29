import { distributeExpansionAnswer } from './expansion-answer-input';

// Reported 29.10.2026 (mobile): Android keyboards deliver every letter as a composition, so the answer never moved to
// the next box; a second letter typed into the full box was then cut by the box limit and the answer could not be sent.
describe('distributeExpansionAnswer', () => {
  it('keeps a complete letter and moves on to the next box', () => {
    expect(distributeExpansionAnswer(['', ''], 0, 'k', [1, 1])).toEqual({ values: ['k', ''], focusIndex: 1 });
  });

  it('carries letters typed past a full box into the following boxes instead of dropping them', () => {
    expect(distributeExpansionAnswer(['', ''], 0, 'km', [1, 1])).toEqual({ values: ['k', 'm'], focusIndex: null });
    expect(distributeExpansionAnswer(['', '', '', ''], 0, 'kmt', [1, 1, 1, 1])).toEqual({ values: ['k', 'm', 't', ''], focusIndex: 3 });
  });

  it('waits for a whole word and carries the rest of a longer entry', () => {
    expect(distributeExpansionAnswer(['', ''], 0, 'mas', [4, 4])).toEqual({ values: ['mas', ''], focusIndex: null });
    expect(distributeExpansionAnswer(['', ''], 0, 'masak', [4, 4])).toEqual({ values: ['masa', 'k'], focusIndex: 1 });
  });

  it('ignores spaces from the keyboard and drops letters beyond the last box', () => {
    expect(distributeExpansionAnswer(['', ''], 0, 'k ', [1, 1])).toEqual({ values: ['k', ''], focusIndex: 1 });
    expect(distributeExpansionAnswer(['k', ''], 1, 'mx', [1, 1])).toEqual({ values: ['k', 'm'], focusIndex: null });
  });

  it('fills holes so every box has a value slot', () => {
    expect(distributeExpansionAnswer([], 1, '', [1, 1])).toEqual({ values: ['', ''], focusIndex: null });
  });
});

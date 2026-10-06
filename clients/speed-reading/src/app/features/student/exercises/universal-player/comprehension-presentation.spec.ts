import { ExercisePlayerComponent } from './exercise-player.component';

describe('comprehension presentation', () => {
  it('does not display whole-text speed before reading is finished', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension', getWordCount: () => 100 };
    player.engineState = { timeElapsed: 1000 };
    expect(player.getCurrentReadingWpm()).toBe(0);
  });

  it('exposes configured line height and the same completion gate as the engine', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'reading_comprehension', getLineHeight: () => 2.2, canComplete: () => false };
    player.engineState = { isRunning: true, isPaused: false };
    expect(player.getComprehensionLineHeight()).toBe(2.2);
    expect(player.canCompleteReading()).toBeFalse();
  });
});

import { ExercisePlayerComponent } from './exercise-player.component';
import { getCustomPreviewControls } from './custom-preview-settings';

describe('free reading player', () => {
  it('routes completion, font size and scrolling to the free reading engine', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'free_reading', getFontSize: () => 'large', completeReading: jasmine.createSpy(), handleInput: jasmine.createSpy() };
    player.completeReading();
    expect(player.engine.completeReading).toHaveBeenCalledTimes(1);
    expect(player.getComprehensionFontSize()).toBe('large');
    player.onReadingScroll({ target: { scrollTop: 50, scrollHeight: 200, clientHeight: 100 } });
    expect(player.engine.handleInput).toHaveBeenCalledWith({ scrollProgress: 50 });
  });
  it('offers duration and line spacing controls for free reading', () => {
    const keys = getCustomPreviewControls({ engineType: 'free_reading' }).map(control => control.key);
    expect(keys).toContain('minReadingTimeSec');
    expect(keys).toContain('maxReadingTimeSec');
    expect(keys).toContain('lineHeightPercent');
  });
});

import { ExercisePlayerComponent } from './exercise-player.component';
import { getCustomPreviewControls } from './custom-preview-settings';

describe('free reading player', () => {
  it('checks preview length and suppresses source questions in practice', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType: 'free_reading' };
    player.parsedConfig = { engineType:'free_reading', engineConfig:{ readingPurpose:'practice', content:{ minWordCount:150,maxWordCount:200 } } };
    expect(player.previewTextMatchesLength(149)).toBeFalse();
    expect(player.previewTextMatchesLength(175)).toBeTrue();
    expect(player.previewTextMatchesLength(201)).toBeFalse();
    expect(player.createPreviewReadingConfig({ content:'Gerçek metin.', questions:[{ id:'q' }] }).questions).toEqual([]);
  });
  it('preserves self-reported tempo without claiming measured performance', () => {
    const player = Object.create(ExercisePlayerComponent.prototype) as any;
    player.engine = { engineType:'free_reading' }; player.questionAnswers=[];
    const result=player.normalizeEngineResultForDisplay({ score:0,accuracy:0,details:{ wpm:200,wordCount:100 } });
    expect(result.details.wpm).toBe(200);
    expect(result.details.measurementStatus).toBe('NotMeasured');
    expect(result.details.comprehensionScore).toBeNull();
  });
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

import { ExercisePlayerComponent } from './exercise-player.component';
import { applyCustomPreviewSettings, getCustomPreviewControls } from './custom-preview-settings';
import { ErrorAnalysisEngine } from './engines/error-analysis.engine';

describe('prepared Error Analysis preview', () => {
  const configuration = () => ({ engineType:'error_analysis',engineConfig:{content:{source:'reading_text',minWordCount:2,maxWordCount:3},
    errorCount:1,timeLimit:240,errorsByTextId:{text:[{wordIndex:0,originalWord:'yalnız',errorWord:'yanlız',errorType:'transposition',explanation:'Doğru: yalnız'}]}} });
  const player = () => { const p=Object.create(ExercisePlayerComponent.prototype) as any;
    p.parsedConfig=configuration(); p.exercise={difficultyLevel:4,targetAgeGroupId:'child'}; return p; };
  it('loads real text without comprehension questions and obeys the length band', () => {
    const p=player(); expect(p.previewRequiresReadingText()).toBeTrue(); expect(p.previewRequiresQuestions()).toBeFalse();
    expect(p.previewTextMatchesLength(1)).toBeFalse(); expect(p.previewTextMatchesLength(4)).toBeFalse();
    expect(p.previewTextMatchesLength(2)).toBeTrue();
  });
  it('prepares the same annotated tokens for preview without changing the source', () => {
    const p=player(), source={id:'text',content:'yalnız bugün.',questions:[{id:'q'}]};
    const config=p.createPreviewReadingConfig(source);
    expect(config.words).toEqual([{index:0,text:'yanlız'},{index:1,text:'bugün.'}]);
    expect(config.errors).toEqual(p.parsedConfig.engineConfig.errorsByTextId.text);
    expect(config.questions).toEqual([]); expect(source.content).toBe('yalnız bugün.');
    const engine=new ErrorAnalysisEngine(); const complete=jasmine.createSpy();
    engine.initialize({...p.parsedConfig,...config} as any,{onComplete:complete} as any); engine.start();
    engine.handleInput({type:'select_word',wordIndex:0}); expect(complete).toHaveBeenCalled(); engine.destroy();
  });
  it('rejects missing annotations, changed originals, count mismatch and invalid indices', () => {
    for(const kind of ['missing','original','count','index']) {
      const p=player(); if(kind==='missing') p.parsedConfig.engineConfig.errorsByTextId={};
      if(kind==='original') p.parsedConfig.engineConfig.errorsByTextId.text[0].originalWord='başka';
      if(kind==='count') p.parsedConfig.engineConfig.errorCount=2;
      if(kind==='index') p.parsedConfig.engineConfig.errorsByTextId.text[0].wordIndex=99;
      expect(()=>p.createPreviewReadingConfig({id:'text',content:'yalnız bugün.'})).toThrow();
    }
  });
  it('requires the exact age and prepared text ID rather than a shared fallback', () => {
    const p=player();
    expect(p.previewTextMatchesAge({targetAgeGroupConfigurationId:'child'})).toBeTrue();
    expect(p.previewTextMatchesAge({})).toBeFalse();
    expect(p.previewTextMatchesAge({targetAgeGroupConfigurationId:'adult'})).toBeFalse();
    expect(p.previewTextMatchesScanningTasks({id:'text'})).toBeTrue();
    expect(p.previewTextMatchesScanningTasks({id:'unprepared'})).toBeFalse();
  });
  it('reduces prepared targets without modifying the original and rejects duplicate/empty targets', () => {
    const config=configuration();
    config.engineConfig.errorCount=2;
    config.engineConfig.errorsByTextId.text.push({wordIndex:1,originalWord:'bugün.',errorWord:'bügun.',errorType:'transposition',explanation:'Doğru: bugün.'});
    const custom=applyCustomPreviewSettings(config,{errorCount:1},{preview:true,roles:['Admin']});
    expect(custom.engineConfig.errorsByTextId.text.length).toBe(1);
    expect(config.engineConfig.errorsByTextId.text.length).toBe(2);
    for (const errors of [[],[config.engineConfig.errorsByTextId.text[0],config.engineConfig.errorsByTextId.text[0]]]) {
      const p=player(); p.parsedConfig.engineConfig.errorCount=errors.length;
      p.parsedConfig.engineConfig.errorsByTextId.text=errors;
      expect(()=>p.createPreviewReadingConfig({id:'text',content:'yalnız bugün.'})).toThrow();
    }
  });
  it('exposes bounded time and target count controls without rewriting the catalogue', () => {
    const config=configuration(); const keys=getCustomPreviewControls(config).map(control=>control.key);
    expect(keys).toContain('timeLimitSeconds'); expect(keys).toContain('errorCount');
    const custom=applyCustomPreviewSettings(config,{timeLimitSeconds:90,errorCount:1},{preview:true,roles:['Teacher']});
    expect(custom.engineConfig.timeLimit).toBe(90); expect(config.engineConfig.timeLimit).toBe(240);
    for(const values of [{timeLimitSeconds:0},{timeLimitSeconds:3601},{errorCount:0},{errorCount:2}])
      expect(()=>applyCustomPreviewSettings(config,values,{preview:true,roles:['Admin']})).toThrow();
    expect(applyCustomPreviewSettings(config,{timeLimitSeconds:90},{preview:false,roles:['Teacher']})).toBe(config);
  });
  it('uses the requested custom time even when legacy timer aliases exist', () => {
    const config:any={engineType:'error_analysis',timeLimitSeconds:240,engineConfig:{timing:{timeLimitSec:300},
      words:[{index:0,text:'yanlız'}],errors:[{wordIndex:0,originalWord:'yalnız',errorWord:'yanlız'}]}};
    const custom=applyCustomPreviewSettings(config,{timeLimitSeconds:90},{preview:true,roles:['Admin']});
    const engine=new ErrorAnalysisEngine(); engine.initialize(custom as any,{} as any);
    expect((engine as any).timeLimitMs).toBe(90000); engine.destroy();
  });
});

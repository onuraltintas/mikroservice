import { of, Subject } from 'rxjs';
import { ExercisePlayerComponent } from './exercise-player.component';
import { CustomPreviewDialogComponent } from './custom-preview-dialog.component';
import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';

describe('bounded RSVP preview', () => {
  const config = () => ({ engineType:'text_stream',engineConfig:{mode:'rsvp',readingPurpose:'evaluation',
    timing:{durationMs:500,intervalMs:0},content:{source:'reading_text',minWordCount:2,maxWordCount:4,questionCount:5}} });
  const question = (index:number) => ({id:`q${index}`,type:1,bloomLevel:1,correctAnswer:'A',optionA:'A',optionB:'B',optionC:'C',optionD:'D'});
  const player = () => { const p=Object.create(ExercisePlayerComponent.prototype) as any;
    p.parsedConfig=config(); p.exercise={id:'exercise',difficultyLevel:4,exerciseTypeName:'RSVP'};
    p.destroy$=new Subject();p.initializePreviewEngine=jasmine.createSpy();p.failPreviewReadingText=jasmine.createSpy(); return p; };
  it('requires the configured text band in preview',()=>{
    const p=player();expect(p.previewTextMatchesLength(1)).toBeFalse();expect(p.previewTextMatchesLength(5)).toBeFalse();
    expect(p.previewTextMatchesLength(3)).toBeTrue();
  });
  it('passes the loaded text words to the RSVP engine instead of only custom content items',()=>{
    const p=player();const preview=p.createPreviewReadingConfig({id:'text',content:'bir iki üç',wordCount:3,
      questions:Array.from({length:5},(_,i)=>question(i))});
    expect(preview.words).toEqual(['bir','iki','üç']);
  });
  for (const count of [4,5,6]) it(`requires exactly five valid preview questions from a pool of ${count}`,()=>{
    const p=player();p.exerciseService={getReadingText:()=>of({id:'text',content:'bir iki üç',wordCount:3,difficultyLevel:4,
      questions:Array.from({length:count},(_,i)=>question(i))})};
    p.loadPreviewReadingText('text');
    if(count<5) expect(p.failPreviewReadingText).toHaveBeenCalled();
    else { expect(p.initializePreviewEngine).toHaveBeenCalled();expect(p.comprehensionQuestions.length).toBe(5); }
  });
  it('does not count invalid question options as a fifth question',()=>{
    const p=player();const questions=Array.from({length:5},(_,i)=>question(i));questions[4].optionD='';
    p.exerciseService={getReadingText:()=>of({id:'text',content:'bir iki üç',wordCount:3,difficultyLevel:4,questions})};
    p.loadPreviewReadingText('text');expect(p.failPreviewReadingText).toHaveBeenCalled();
  });
  it('shows tempo and a recalculated duration range in the custom preview dialog',()=>{
    TestBed.configureTestingModule({imports:[CustomPreviewDialogComponent],providers:[
      {provide:MAT_DIALOG_DATA,useValue:config()}, {provide:MatDialogRef,useValue:{close:()=>undefined}}]});
    const fixture=TestBed.createComponent(CustomPreviewDialogComponent);fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('120 kelime/dk');
    expect(fixture.nativeElement.textContent).toContain('1,3–2,3 saniye');
    fixture.componentInstance.values['displayDurationMs']=1000;fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('60 kelime/dk');
    expect(fixture.nativeElement.textContent).toContain('2,3–4,3 saniye');
    expect(fixture.nativeElement.textContent).toContain('yalnız başlangıçta');
  });
});

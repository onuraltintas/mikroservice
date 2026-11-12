import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../../core/services/auth.service';
import { ExerciseProgramService } from '../../../../core/services/exercise-program.service';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseSessionService } from '../../../../core/services/exercise-session.service';
import { LearningPathService } from '../../../../core/services/learning-path.service';
import { StudentProgramService } from '../../../../core/services/student-program.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { ReviewService } from '../../../../services/review.service';
import { ExercisePlayerComponent } from './exercise-player.component';
import { ScanFindEngine } from './engines/scan-find.engine';
import { EngineCallbacks } from './engines/base-engine.interface';

const noop: EngineCallbacks = {
  onStart: () => undefined, onPause: () => undefined, onResume: () => undefined, onComplete: () => undefined,
  onError: () => undefined, onStateChange: () => undefined, onStepComplete: () => undefined, onAction: () => undefined
};

describe('scan and find instructions', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ExercisePlayerComponent],
      providers: [AuthService, ExerciseProgramService, ExerciseService, ExerciseSessionService,
        LearningPathService, StudentProgramService, ToasterService, ReviewService, Router, ActivatedRoute]
        .map(provide => ({ provide, useValue: {} }))
    });
    spyOn(ExercisePlayerComponent.prototype, 'ngOnInit');
  });

  function render(config: Record<string, unknown>, running: boolean) {
    const engine = new ScanFindEngine();
    engine.initialize(config, noop);
    const fixture = TestBed.createComponent(ExercisePlayerComponent);
    const player = fixture.componentInstance;
    player.isLoading = false;
    player.exercise = { title: 'Sözcüğü Bul', description: '', difficultyLevel: 1 } as any;
    player.engine = engine;
    player.engineState = { ...engine.state, isRunning: running };
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const words = { scanningRounds: [{ textContent: 'okul kitap deniz', targets: ['okul', 'kitap'] }], timeLimitSeconds: 60 };

  it('tells the student what to do above the text, with the targets as chips', () => {
    const element = render(words, true);
    const panel = element.querySelector('.scan-targets-panel')!;
    const heading = panel.querySelector('h2')!;
    expect(heading.textContent).toContain('Aşağıdaki metinde şu kelimeleri bul ve üzerine dokun:');
    const chips = Array.from(panel.querySelectorAll('li.target-word')).map(chip => chip.textContent!.trim());
    expect(chips).toEqual(['okul', 'kitap']);
    expect(panel.querySelector('.scan-instruction-hint')!.textContent).toContain('Süre bitmeden hepsini bul.');
    // The instruction comes before the text it describes.
    const text = element.querySelector('.scan-text-area')!;
    expect(panel.compareDocumentPosition(text) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it('asks for the answers when the targets are questions and shows the round', () => {
    const element = render({ scanningRounds: [
      { textContent: 'Derya 24 okul', targets: ['derya'], targetLabels: { derya: 'Etkinliği kim hazırladı?' } },
      { textContent: 'deniz bulut', targets: ['deniz'] }
    ] }, true);
    const panel = element.querySelector('.scan-targets-panel')!;
    expect(panel.querySelector('h2')!.textContent).toContain('Aşağıdaki metinde soruların cevabını bul ve cevaba dokun:');
    expect(panel.querySelector('li.target-word')!.textContent).toContain('Etkinliği kim hazırladı?');
    expect(panel.querySelector('.scan-instruction-hint')!.textContent).toContain('Tur 1 / 2');
  });

  it('asks for one target when any of them is enough', () => {
    const element = render({ serverAuthoritative: true, scanningFindAny: true, timeLimitSeconds: 90,
      scanningRounds: [{ textContent: 'okul kitap', targets: ['okul', 'kitap'] }] }, true);
    const panel = element.querySelector('.scan-targets-panel')!;
    expect(panel.querySelector('h2')!.textContent).toContain('Aşağıdaki metinde bu kelimelerden birini bul ve üzerine dokun:');
    expect(panel.querySelector('.scan-instruction-hint')!.textContent).toContain('Süre bitmeden birini bul.');
  });

  it('explains the task on the start card in at most two short sentences', () => {
    const element = render(words, false);
    const text = element.querySelector('.scan-find-ready')!.textContent!.trim();
    expect(text).toContain('kelimeleri bul ve üzerine dokun.');
    expect(text).toContain('Toplam 2 hedef, süre 1 dakika; yanlış dokunuş puanı düşürür.');
    expect(text.split(/[.!?](\s|$)/).filter(part => part.trim().length > 0).length).toBeLessThanOrEqual(2);
  });

  it('names any time limit the server sent and leaves the time out when none was sent', () => {
    const long = render({ ...words, timeLimitSeconds: 3600 }, false).querySelector('.scan-find-ready')!.textContent!;
    expect(long).toContain('süre 60 dakika');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [ExercisePlayerComponent],
      providers: [AuthService, ExerciseProgramService, ExerciseService, ExerciseSessionService,
        LearningPathService, StudentProgramService, ToasterService, ReviewService, Router, ActivatedRoute]
        .map(provide => ({ provide, useValue: {} }))
    });
    const element = render({ scanningRounds: words.scanningRounds }, true);
    expect(element.querySelector('.scan-instruction-hint')!.textContent).not.toContain('Süre');
    expect(element.querySelector('.scan-instruction-hint')!.textContent).toContain('Yanlış dokunuş puanı düşürür.');
  });

  it('asks for one answer when the targets are questions and any one is enough', () => {
    const element = render({ serverAuthoritative: true, scanningFindAny: true, timeLimitSeconds: 45, scanningRounds: [
      { textContent: 'Derya 24 okul', targets: ['derya', '24'], targetLabels: { derya: 'Kim?', '24': 'Kaç kişi?' } }
    ] }, true);
    const panel = element.querySelector('.scan-targets-panel')!;
    expect(panel.querySelector('h2')!.textContent).toContain('Aşağıdaki metinde sorulardan birinin cevabını bul ve cevaba dokun:');
    expect(panel.querySelector('.scan-instruction-hint')!.textContent).toContain('Süre bitmeden birinin cevabını bul.');
  });

  it('puts the instruction heading under a page heading with the exercise name', () => {
    const element = render(words, true);
    const container = element.querySelector('.scan-find-container')!;
    const headings = Array.from(container.querySelectorAll('h1, h2')).map(item => item.tagName);
    expect(headings).toEqual(['H1', 'H2']);
    const title = container.querySelector('h1')!;
    expect(title.classList).toContain('sr-only');
    expect(title.textContent).toContain('Sözcüğü Bul');
  });

  it('names the time limit in minutes and seconds', () => {
    const player: any = Object.create(ExercisePlayerComponent.prototype);
    const cases: Array<[number, string]> = [
      [45, '45 saniye'], [60, '1 dakika'], [90, '1 dakika 30 saniye'], [110, '1 dakika 50 saniye'],
      [120, '2 dakika'], [150, '2 dakika 30 saniye'], [3600, '60 dakika']
    ];
    expect(cases.map(([seconds]) => player.formatScanLimit(seconds))).toEqual(cases.map(([, text]) => text));
  });

  it('uses the same wording for the limit on the start card', () => {
    const text = render({ ...words, timeLimitSeconds: 110 }, false).querySelector('.scan-find-ready')!.textContent!;
    expect(text).toContain('süre 1 dakika 50 saniye;');
  });
});


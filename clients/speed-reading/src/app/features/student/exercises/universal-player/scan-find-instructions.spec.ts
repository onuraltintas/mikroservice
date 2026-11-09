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
    const element = render({ serverAuthoritative: true, scanningFindAny: true,
      scanningRounds: [{ textContent: 'okul kitap', targets: ['okul', 'kitap'] }] }, true);
    const panel = element.querySelector('.scan-targets-panel')!;
    expect(panel.querySelector('h2')!.textContent).toContain('Aşağıdaki metinde bu kelimelerden birini bul ve üzerine dokun:');
    expect(panel.querySelector('.scan-instruction-hint')!.textContent).toContain('Süre bitmeden birini bul.');
  });

  it('explains the task on the start card before the search begins', () => {
    const element = render(words, false);
    const text = element.querySelector('.scan-find-ready')!.textContent!;
    expect(text).toContain('kelimeleri bul ve üzerine dokun');
    expect(text).toContain('Toplam 2 hedef var, süre 60 saniye.');
    expect(text).toContain('Yanlış dokunuş puanı düşürür.');
  });
});

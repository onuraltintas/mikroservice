import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingExamSessionsEditorComponent, examSessionErrors } from './coaching-exam-sessions-editor';
import { examDisplayName, groupExamsForSelect, scoreTypeLabel, sourceLabel } from './coaching-exam-groups';
import { CoachingOutcomeApprovalsComponent } from './coaching-outcome-approvals';
import { CoachingOutcomeSourceEditorComponent, validSourceUrl } from './coaching-outcome-source-editor';
import { CoachingScoreModelsComponent } from './coaching-score-models';
import { CoachingTargetProgramsComponent } from './coaching-target-programs';
import { CoachingYksScopeEditorComponent } from './coaching-yks-scope-editor';

const api = (path: string) => (req: { url: string }) => req.url.endsWith(path);
const flushAll = () => new Promise(resolve => setTimeout(resolve, 0));

function setup(confirm = true) {
  const toaster = { confirm: vi.fn(async () => confirm), success: vi.fn(), error: vi.fn() };
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ToasterService, useValue: toaster },
      { provide: AuthService, useValue: { hasPermission: () => true, userProfile: () => ({ roles: ['SystemAdmin'] }) } },
      { provide: CoachingCatalogService, useValue: { exams: () => of([{ code: 'TYT', name: 'TYT' }, { code: 'OABT_MAT', name: 'Matematik (ÖABT)' }, { code: 'YDS', name: 'YDS' }]) } }]
  });
  return { http: TestBed.inject(HttpTestingController), toaster };
}

describe('KM7 labels and exam groups', () => {
  it('groups the ÖABT fields and never shows raw codes or YDS · YDS', () => {
    const groups = groupExamsForSelect([{ code: 'TYT', name: 'TYT' }, { code: 'OABT_MAT', name: 'Matematik' }, { code: 'OABT_BIO', name: 'Biyoloji' }]);
    expect(groups.map(x => x.label)).toEqual(['Sınavlar', 'ÖABT']);
    expect(groups[1].exams.map(x => x.code)).toEqual(['OABT_BIO', 'OABT_MAT']);
    expect(examDisplayName({ code: 'YDS', name: 'YDS' })).toBe('YDS');
    expect(scoreTypeLabel('SOZ')).toBe('Sözel (SÖZ)');
    expect(sourceLabel('coaching-catalog-km3')).toContain('KM3');
  });
});

describe('CoachingOutcomeApprovalsComponent', () => {
  it('filters the queue, selects every matching link and decides in batches with one reason after a confirmation', async () => {
    const { http, toaster } = setup();
    const fixture = TestBed.createComponent(CoachingOutcomeApprovalsComponent); fixture.detectChanges();
    http.expectOne(api('/outcomes/mappings/sources')).flush({ data: ['coaching-catalog-km2'] });
    http.expectOne(r => r.url.endsWith('/curriculum-management/children') && r.params.get('kind') === 'lessons').flush({ data: { items: [{ id: 'l1', name: 'Tarih (AYT)', examCode: 'AYT' }] } });
    const row = { topicId: 't1', outcomeId: 'o1', topicPath: 'Tarih (AYT) › Ünite › Konu', lessonName: 'Tarih (AYT)', examCode: 'AYT', outcomeCode: 'T.1', outcomeText: 'Kazanım', evidence: 'Kanıt', source: 'coaching-catalog-km2', status: 'pending', reviewedAt: null };
    http.expectOne(r => r.url.endsWith('/outcomes/mappings') && r.params.get('status') === 'pending' && r.params.get('page') === '1').flush({ data: { items: [row], totalCount: 2 } });
    await flushAll(); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Tarih (AYT) › Ünite › Konu'); expect(text).toContain('Onay bekliyor');
    expect(fixture.componentInstance.examGroups().map(g => g.label)).toEqual(['Sınavlar', 'ÖABT']);

    const all = fixture.componentInstance.selectAllMatching();
    http.expectOne(r => r.url.endsWith('/outcomes/mappings') && r.params.get('pageSize') === '500')
      .flush({ data: { items: [row, { ...row, topicId: 't2' }], totalCount: 2 } });
    await all;
    expect(fixture.componentInstance.selectedCount()).toBe(2);
    expect(fixture.componentInstance.canDecide()).toBe(false);
    fixture.componentInstance.reason = 'ÖSYM kılavuzuyla karşılaştırıldı';
    const decided = fixture.componentInstance.decide('approve');
    await flushAll();
    expect(toaster.confirm).toHaveBeenCalledWith(expect.stringContaining('2 bağlantı onaylanacak'), expect.anything());
    const post = http.expectOne(api('/outcomes/mappings/bulk'));
    expect(post.request.body).toEqual({ items: [{ topicId: 't1', outcomeId: 'o1' }, { topicId: 't2', outcomeId: 'o1' }], decision: 'approve', reason: 'ÖSYM kılavuzuyla karşılaştırıldı' });
    post.flush({ data: { changed: 1, skipped: [{ topicId: 't2', outcomeId: 'o1', reason: 'Bağlantı zaten onaylı.' }] } });
    await flushAll();
    http.expectOne(r => r.url.endsWith('/outcomes/mappings') && r.params.get('page') === '1').flush({ data: { items: [], totalCount: 0 } });
    await decided; fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('1 bağlantı onaylandı');
    expect(fixture.nativeElement.textContent).toContain('Bağlantı zaten onaylı. (1)');
    expect(fixture.componentInstance.selectedCount()).toBe(0);
  });

  it('does nothing when the confirmation is cancelled', async () => {
    const { http, toaster } = setup(false);
    const fixture = TestBed.createComponent(CoachingOutcomeApprovalsComponent); fixture.detectChanges();
    fixture.componentInstance.loading.set(false);
    fixture.componentInstance.selected.set(new Map([['t:o', { topicId: 't', outcomeId: 'o' }]])); fixture.componentInstance.reason = 'Yeterli gerekçe';
    await fixture.componentInstance.decide('reject');
    expect(toaster.confirm).toHaveBeenCalled();
    http.expectNone(api('/outcomes/mappings/bulk'));
  });
});

describe('CoachingYksScopeEditorComponent', () => {
  it('loads the topic scopes, limits score types to the session and confirms before removing a scope', async () => {
    const { http, toaster } = setup();
    const fixture = TestBed.createComponent(CoachingYksScopeEditorComponent);
    fixture.componentRef.setInput('topicId', 'topic-1'); fixture.detectChanges();
    http.expectOne(api('/catalog/yks-test-codes')).flush({ data: [{ code: 'TYT.MAT', examCode: 'TYT', label: 'TYT Temel Matematik' }, { code: 'AYT.MAT', examCode: 'AYT', label: 'AYT Matematik' }] });
    http.expectOne(api('/catalog/topics/topic-1/yks-scopes')).flush({ data: [{ scoreType: 'SAY', testCode: 'AYT.MAT', examCode: 'AYT', foreignLanguage: null, excludedOutcomeIds: [] }] });
    await flushAll();
    http.expectOne(r => r.url.endsWith('/curriculum-management/children') && r.params.get('parentId') === 'topic-1').flush({ data: { items: [] } });
    await flushAll(); fixture.detectChanges();
    const editor = fixture.componentInstance;
    expect(editor.scoreTypes('AYT')).toEqual(['SAY', 'EA', 'SOZ']);
    expect(fixture.nativeElement.textContent).toContain('Sayısal (SAY)');
    const saved = vi.fn(); editor.saved.subscribe(saved);
    editor.remove(0); editor.add(); editor.reason = 'TYT kapsamına taşındı';
    const done = editor.save(); await flushAll();
    expect(toaster.confirm).toHaveBeenCalledWith(expect.stringContaining('1 YKS kapsamı kaldırılacak'), expect.anything());
    const put = http.expectOne(api('/catalog/topics/topic-1/yks-scopes'));
    expect(put.request.body.scopes).toEqual([{ examCode: 'TYT', scoreType: 'TYT', testCode: 'TYT.MAT', foreignLanguage: null, excludedOutcomeIds: [] }]);
    put.flush({ data: put.request.body.scopes }); await done;
    expect(saved).toHaveBeenCalled();
  });
});

describe('CoachingOutcomeSourceEditorComponent', () => {
  it('accepts only https source URLs and saves the source ids', async () => {
    expect(validSourceUrl('https://mufredat.meb.gov.tr/a.pdf')).toBe(true);
    expect(validSourceUrl('http://meb.gov.tr')).toBe(false);
    expect(validSourceUrl('')).toBe(true);
    const { http } = setup();
    const fixture = TestBed.createComponent(CoachingOutcomeSourceEditorComponent);
    fixture.componentRef.setInput('outcomeId', 'o-1'); fixture.detectChanges();
    http.expectOne(api('/outcomes/o-1/source')).flush({ data: { outcomeId: 'o-1', sourceKind: 'Official', sourceUrl: null, sourceIds: [{ source: 'meb-2024', sourceId: 'M.1' }] } });
    await flushAll(); fixture.detectChanges();
    const editor = fixture.componentInstance;
    editor.url = 'http://güvensiz'; editor.reason = 'Resmî adres eklendi';
    expect(editor.canSave()).toBe(false);
    editor.url = 'https://mufredat.meb.gov.tr/a.pdf';
    const done = editor.save();
    const put = http.expectOne(api('/outcomes/o-1/source'));
    expect(put.request.body).toEqual({ sourceUrl: 'https://mufredat.meb.gov.tr/a.pdf', sourceIds: [{ source: 'meb-2024', sourceId: 'M.1' }], reason: 'Resmî adres eklendi' });
    put.flush({ data: { outcomeId: 'o-1', sourceKind: 'Official', sourceUrl: 'https://mufredat.meb.gov.tr/a.pdf', sourceIds: [] } }); await done;
    expect(editor.message()).toBe('Kaynak kaydedildi.');
  });
});

describe('CoachingExamSessionsEditorComponent', () => {
  it('validates dates client-side and confirms before removing a saved session', async () => {
    expect(examSessionErrors([{ id: null, label: '', date: '', applicationStart: '2027-02-01', applicationEnd: '' }]).flat())
      .toEqual(['Oturum adını girin.', 'Sınav tarihini girin.', 'Başvuru başlangıcını ve bitişini birlikte girin ya da ikisini de boş bırakın.']);
    expect(examSessionErrors([{ id: null, label: '2027/1', date: '2027-03-01', applicationStart: '2027-02-01', applicationEnd: '2027-03-05' }]).flat())
      .toEqual(['Başvuru bitişi sınav tarihinden önce olmalıdır.']);
    const { toaster } = setup(false);
    const fixture = TestBed.createComponent(CoachingExamSessionsEditorComponent);
    fixture.componentRef.setInput('sessions', [{ id: 's1', label: '2027/1', date: '2027-03-28', applicationStart: '', applicationEnd: '' }]);
    const validity: boolean[] = []; fixture.componentInstance.validChange.subscribe(v => validity.push(v));
    fixture.detectChanges();
    await fixture.componentInstance.remove(0);
    expect(toaster.confirm).toHaveBeenCalled(); expect(fixture.componentInstance.sessions().length).toBe(1);
    fixture.componentInstance.add(); fixture.detectChanges();
    expect(validity.at(-1)).toBe(false);
    await fixture.componentInstance.remove(1);
    expect(fixture.componentInstance.sessions().length).toBe(1);
  });
});

describe('CoachingTargetProgramsComponent', () => {
  it('searches programs, edits with the fingerprint and reason, and refuses an inverted score range', async () => {
    const { http } = setup();
    const fixture = TestBed.createComponent(CoachingTargetProgramsComponent); fixture.detectChanges();
    const program = { id: 'p1', universityName: 'İSTANBUL ÜNİVERSİTESİ', name: 'Tıp', programCode: '101', facultyName: 'TIP', programType: 'Bachelor', scoreType: 'SAY', city: 'İSTANBUL',
      quota: 300, placed: 300, minimumScore: 480, maximumScore: 500, rank: 1500, year: 2025, catalogYear: 2026, isActive: true, source: 'mebi-yks', sourceUrl: null, updatedAt: null };
    http.expectOne(api('/catalog/university-programs')).flush({ data: { items: [program], totalCount: 1, pageNumber: 1, pageSize: 25 } });
    await flushAll(); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Lisans · Sayısal (SAY)');
    const component = fixture.componentInstance;
    const open = component.openEdit(program);
    http.expectOne(api('/catalog/university-programs/p1')).flush({ data: { fingerprint: 'F', data: program, goalCount: 0 } }); await open;
    component.form.minimumScore = 510; expect(component.formError()).toContain('Taban puan');
    component.form.minimumScore = 470; component.reason = 'Taban puan düzeltildi';
    const saving = component.save(); await flushAll();
    const put = http.expectOne(api('/catalog/university-programs/p1'));
    expect(put.request.body).toEqual(expect.objectContaining({ fingerprint: 'F', minimumScore: 470, reason: 'Taban puan düzeltildi' }));
    put.flush({ data: { fingerprint: 'G', data: { ...program, minimumScore: 470 }, goalCount: 0 } }); await flushAll();
    http.expectOne(api('/catalog/university-programs')).flush({ data: { items: [], totalCount: 0, pageNumber: 1, pageSize: 25 } }); await saving;
    expect(component.message()).toBe('Program kaydedildi.');
  });
});

describe('CoachingScoreModelsComponent', () => {
  it('previews again when the year changes, asks before overwriting and sends the reason', async () => {
    const { http, toaster } = setup();
    const fixture = TestBed.createComponent(CoachingScoreModelsComponent); fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/score-models')).flush([]); await flushAll();
    http.expectOne(r => r.url.endsWith('/score-models/history')).flush([]);
    const component = fixture.componentInstance;
    const file = new File(['tur;puan_turu'], 'model.csv', { type: 'text/csv' });
    const choosing = component.choose({ target: { files: [file] } } as unknown as Event);
    let preview = http.match(api('/score-models/preview'));
    for (let i = 0; i < 50 && !preview.length; i++) { await flushAll(); preview = http.match(api('/score-models/preview')); }
    preview[0].flush({ fingerprint: 'A', year: component.year, models: [{ scoreType: 'LGS', name: 'LGS', baseScore: 200, coefficients: 6, rankRows: 2, replaces: true }], errors: [] });
    await choosing;
    component.year = 2024; const changed = component.yearChanged(); await flushAll();
    const again = http.expectOne(api('/score-models/preview'));
    expect(again.request.body.year).toBe(2024);
    again.flush({ fingerprint: 'B', year: 2024, models: [{ scoreType: 'LGS', name: 'LGS', baseScore: 200, coefficients: 6, rankRows: 2, replaces: true }], errors: [] }); await changed;
    component.applyReason = '2024 sonuçları yüklendi';
    const applying = component.apply(); await flushAll();
    expect(toaster.confirm).toHaveBeenCalledWith(expect.stringContaining('LGS verisi değiştirilecek'), expect.anything());
    const apply = http.expectOne(api('/score-models/apply'));
    expect(apply.request.body).toEqual(expect.objectContaining({ year: 2024, fingerprint: 'B', reason: '2024 sonuçları yüklendi' }));
    apply.flush({ applied: 1 }); await flushAll();
    http.expectOne(r => r.url.endsWith('/score-models')).flush([]); await flushAll();
    http.expectOne(r => r.url.endsWith('/score-models/history')).flush([]); await applying;
    expect(component.success()).toContain('2024 yılı için 1 puan türünün');
  });
});

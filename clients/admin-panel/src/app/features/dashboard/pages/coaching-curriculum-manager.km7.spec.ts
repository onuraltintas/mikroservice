import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingCurriculumManagerComponent } from './coaching-curriculum-manager.component';

// KM7 (01.11.2026): Müfredat Yönetimi is the single curriculum editor (filters, Turkish search, inline minutes/weight,
// bulk status, link approval, quality view, YKS scope and outcome source editors, topic shares, Turkish labels).
describe('Müfredat Yönetimi (KM7)', () => {
  const page = (items: unknown[], totalCount = items.length) => ({ data: { items, totalCount, pageNumber: 1, pageSize: 25 } });
  const exams = [{ id: 'e-ayt', code: 'AYT', name: 'Alan Yeterlilik Testleri', isActive: true }, { id: 'e-ags', code: 'AGS', name: 'Akademi Giriş Sınavı', isActive: true },
    { id: 'e-o1', code: 'OABT_TURKCE', name: 'ÖABT · Türkçe Öğretmenliği', isActive: false }, { id: 'e-o2', code: 'OABT_MAT', name: 'ÖABT · İlköğretim Matematik', isActive: false }];
  const row = (id: string, extra: object = {}) => ({ kind: 'topics', id, name: 'Türev ' + id, parentId: 'unit', parentKind: 'units', isActive: true, path: 'Matematik (AYT) / Türev / Türev ' + id,
    fingerprint: 'fp-' + id, version: 'v-' + id, canDelete: false, blockers: [], estimates: [], mappings: [], estimatedMinutes: 60, yieldWeight: 3, outcomeCount: 2, pendingLinkCount: 1, ...extra });
  const confirm = vi.fn(async (_message: string, _options?: unknown) => true);
  function setup() {
    confirm.mockClear();
    TestBed.configureTestingModule({ imports: [CoachingCurriculumManagerComponent], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([]),
      { provide: ToasterService, useValue: { confirm, success: vi.fn(), error: vi.fn(), info: vi.fn() } }] });
    const fixture = TestBed.createComponent(CoachingCurriculumManagerComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/curriculum-management/children')).flush(page([]));
    http.expectOne(r => r.url.endsWith('/coaching/catalog/exams')).flush({ data: exams });
    const pending = http.expectOne(r => r.url.endsWith('/outcomes/mappings') && r.params.get('status') === 'pending');
    pending.flush({ data: { items: [], totalCount: 206 } });
    fixture.detectChanges();
    return { fixture, http, c: fixture.componentInstance, el: fixture.nativeElement as HTMLElement };
  }
  const text = (el: HTMLElement) => el.textContent!.replace(/\s+/g, ' ');
  async function topicsTab(env: ReturnType<typeof setup>, rows: unknown[]) {
    env.c.chooseTab('topics');
    env.http.expectOne(r => r.url.endsWith('/children') && r.params.get('level') === 'root').flush(page(rows));
    env.http.match(r => r.url.endsWith('/children')).forEach((r: TestRequest) => r.flush(page([])));
    env.fixture.detectChanges(); await env.fixture.whenStable(); env.fixture.detectChanges();
  }

  it('is not a nested main landmark and links to the approval queue with the pending count', () => {
    const { el } = setup();
    expect(el.querySelector('main')).toBeNull();
    const link = el.querySelector('a[href="/dashboard/coaching/outcome-approvals"]');
    expect(link?.textContent).toContain('Onay kuyruğu (206 bekleyen)');
  });

  it('filters by exam (ÖABT fields grouped), lesson and state and sends the search to the API unchanged', async () => {
    const env = setup(); const { c, http, el, fixture } = env;
    await topicsTab(env, []);
    const group = el.querySelector('#curriculum-exam-filter optgroup') as HTMLOptGroupElement;
    expect(group.label).toBe('ÖABT'); expect(group.querySelectorAll('option').length).toBe(2);
    expect(text(el)).not.toContain('OABT_TURKCE');
    c.filters.examCode = 'AYT'; c.examFilterChanged();
    http.expectOne(r => r.url.endsWith('/children') && r.params.get('kind') === 'lessons' && r.params.get('examCode') === 'AYT')
      .flush(page([{ ...row('l'), kind: 'lessons', id: 'lesson-mat', name: 'Matematik (AYT)', examCode: 'AYT' }]));
    http.match(r => r.url.endsWith('/children')).forEach(r => r.flush(page([])));
    c.filters.lessonId = 'lesson-mat'; c.filters.active = 'false'; c.rootSearch = 'türev'; c.loadRoot();
    const list = http.expectOne(r => r.url.endsWith('/children') && r.params.get('kind') === 'topics');
    expect(list.request.params.get('search')).toBe('türev'); expect(list.request.params.get('examCode')).toBe('AYT');
    expect(list.request.params.get('lessonId')).toBe('lesson-mat'); expect(list.request.params.get('active')).toBe('false');
    list.flush(page([row('a')])); fixture.detectChanges(); http.verify();
  });

  it('shows minutes, weight, state, outcome and pending counts on each row', async () => {
    const env = setup(); await topicsTab(env, [row('a', { estimatedMinutes: 140, yieldWeight: 5, isActive: false, outcomeCount: 4, pendingLinkCount: 2 })]);
    const r = env.el.querySelector('.record-row') as HTMLElement;
    expect((r.querySelector('input[name="rowMinutes"]') as HTMLInputElement).value).toBe('140');
    expect(text(r)).toContain('Pasif'); expect(text(r)).toContain('4 kazanım'); expect(text(r)).toContain('2 bekleyen bağlantı');
    expect(text(r)).toContain('Çok sık');
  });

  it('saves inline minutes and weight of several rows with one reason in one bulk request', async () => {
    const env = setup(); const { c, http } = env; await topicsTab(env, [row('a'), row('b'), row('c')]);
    const [a, b] = c.listing().rows;
    c.editMinutes(a, 140); c.editWeight(a, 5); c.editWeight(b, 4);
    expect(c.inlineCount()).toBe(2);
    c.inlineReason = 'ÖSYM 2026 AYT dağılımına göre güncellendi'; await c.saveInline();
    expect(confirm).toHaveBeenCalled();
    const req = http.expectOne(r => r.method === 'PATCH' && r.url.endsWith('/curriculum-management/topics/bulk'));
    expect(req.request.body).toEqual({ reason: 'ÖSYM 2026 AYT dağılımına göre güncellendi', items: [
      { id: 'a', version: 'v-a', estimatedMinutes: 140, yieldWeight: 5 }, { id: 'b', version: 'v-b', estimatedMinutes: 60, yieldWeight: 4 }] });
    req.flush({ data: { changed: 1, conflicts: [{ id: 'b' }] } });
    http.expectOne(r => r.url.endsWith('/children')).flush(page([row('a', { estimatedMinutes: 140 }), row('b')]));
    expect(c.message()).toContain('1 konu güncellendi'); expect(c.error()).toContain('1 konu başka bir işlemde değişmiş');
    http.verify();
  });

  it('activates the selected rows with their descendants after one confirmation and lists skipped rows in Turkish', async () => {
    const env = setup(); const { c, http, fixture, el } = env; await topicsTab(env, [row('a', { isActive: false }), row('b', { isActive: false })]);
    c.toggleRow(c.listing().rows[0]); c.toggleRow(c.listing().rows[1]); fixture.detectChanges();
    expect(text(el)).toContain('2 kayıt seçildi');
    c.includeDescendants = true; c.bulkReason = 'KM3 içeriği ekip tarafından incelendi'; await c.bulkSetActive(true);
    expect(confirm.mock.calls[0][0]).toContain('2 konu ve alt kayıtları etkinleştirilsin mi');
    const req = http.expectOne(r => r.url.endsWith('/curriculum-management/bulk/status'));
    expect(req.request.body).toEqual({ kind: 'topics', ids: ['a', 'b'], active: true, includeDescendants: true, reason: 'KM3 içeriği ekip tarafından incelendi' });
    req.flush({ data: { changed: 1, skipped: [{ id: 'b', kind: 'topics', reason: 'Önce üst kaydı etkinleştirin.' }] } });
    http.expectOne(r => r.url.endsWith('/children')).flush(page([row('a'), row('b', { isActive: false })])); fixture.detectChanges();
    expect(text(el)).toContain('1 kayıt güncellendi'); expect(text(el)).toContain('Önce üst kaydı etkinleştirin.');
    expect(c.selectedIds().size).toBe(0); http.verify();
  });

  it('does not send a bulk action when the confirmation is declined or the reason is short', async () => {
    const env = setup(); const { c, http } = env; await topicsTab(env, [row('a')]);
    c.toggleRow(c.listing().rows[0]); c.bulkReason = 'kısa'; await c.bulkSetActive(false); expect(confirm).not.toHaveBeenCalled();
    confirm.mockResolvedValueOnce(false); c.bulkReason = 'Konu birleştirilecek'; await c.bulkSetActive(false);
    http.expectNone(r => r.url.endsWith('/bulk/status')); http.verify();
  });

  it('approves all links of a topic from the topic form', async () => {
    const { c, http, fixture } = setup(); c.select(row('a') as never); http.expectOne(r => r.url.endsWith('/topics/a')).flush({ data: row('a') });
    http.match(r => r.url.includes('yks')).forEach(r => r.flush({ data: [] }));
    c.form.reason = 'AYT Tarih bağlantıları incelendi'; await c.approveTopicLinks();
    const outcomes = http.expectOne(r => r.url.endsWith('/children') && r.params.get('kind') === 'outcomes' && r.params.get('parentId') === 'a');
    outcomes.flush(page([{ kind: 'outcomes', id: 'o1' }, { kind: 'outcomes', id: 'o2' }]));
    const bulk = http.expectOne(r => r.url.endsWith('/outcomes/mappings/bulk'));
    expect(bulk.request.body).toEqual({ items: [{ topicId: 'a', outcomeId: 'o1' }, { topicId: 'a', outcomeId: 'o2' }], decision: 'approve', reason: 'AYT Tarih bağlantıları incelendi' });
    bulk.flush({ data: { changed: 2, skipped: [] } });
    http.match(r => r.url.endsWith('/topics/a') || r.url.endsWith('/children') || r.url.endsWith('/outcomes/mappings')).forEach(r => r.flush(r.request.url.endsWith('/topics/a') ? { data: row('a') } : page([])));
    fixture.detectChanges(); expect(c.message()).toContain('2 bağlantı onaylandı');
  });

  it('approves, rejects and removes outcome links and shows their Turkish states', async () => {
    const { c, http, fixture, el } = setup();
    const outcome = { ...row('o'), kind: 'outcomes', id: 'o', code: 'PREP.TAR.91', sourceKind: 'ExamSkill', mappings: [
      { topicId: 't1', path: 'Tarih (TYT) › Osmanlı', isApproved: false, status: 'pending', evidence: 'KM2' },
      { topicId: 't2', path: 'Tarih (AYT) › Osmanlı', isApproved: false, status: 'pending', evidence: 'KM2' },
      { topicId: 't3', path: 'Tarih (AYT) › Eski', isApproved: false, status: 'rejected', evidence: 'eski' }],
      estimates: [{ id: 'e1', number: 1, learningMinutes: 10, practiceMinutes: 5, checkMinutes: 5, confidence: 'Medium', evidence: 'x', isPublished: true, method: 'ReviewedStudy' }] };
    c.select(outcome as never); http.expectOne(r => r.url.endsWith('/outcomes/o')).flush({ data: outcome });
    http.match(r => r.url.endsWith('/outcomes/o/topics')).forEach(r => r.flush({ data: [] })); fixture.detectChanges();
    expect(text(el)).toContain('Reddedildi'); expect(text(el)).not.toContain('Medium'); expect(text(el)).not.toContain('ReviewedStudy');
    expect(text(el)).not.toContain('ExamSkill');
    c.form.reason = 'KM2 önerileri incelendi'; await c.reviewAllPending();
    const bulk = http.expectOne(r => r.url.endsWith('/outcomes/mappings/bulk'));
    expect(bulk.request.body.items).toEqual([{ topicId: 't1', outcomeId: 'o' }, { topicId: 't2', outcomeId: 'o' }]); expect(bulk.request.body.decision).toBe('approve');
    bulk.flush({ data: { changed: 2, skipped: [] } });
    http.match(() => true).forEach(r => r.flush(r.request.url.endsWith('/outcomes/o') ? { data: outcome } : r.request.url.endsWith('/topics') ? { data: [] } : page([])));
    http.match(() => true).forEach(r => r.flush(page([])));
    c.form.reason = 'KM2 önerileri incelendi'; await c.reviewMapping(outcome.mappings[0] as never, 'reject');
    expect(confirm).toHaveBeenCalled();
    const reject = http.expectOne(r => r.url.endsWith('/outcomes/mappings/bulk')); expect(reject.request.body.decision).toBe('reject');
    reject.flush({ data: { changed: 1, skipped: [] } });
    http.match(() => true).forEach(r => r.flush(r.request.url.endsWith('/outcomes/o') ? { data: outcome } : r.request.url.endsWith('/topics') ? { data: [] } : page([])));
    http.match(() => true).forEach(r => r.flush(page([])));
    c.form.reason = 'KM2 önerileri incelendi'; await c.removeMapping(outcome.mappings[2] as never);
    const del = http.expectOne(r => r.method === 'DELETE' && r.url.endsWith('/outcomes/mappings/t3/o'));
    expect(del.request.body).toEqual({ reason: 'KM2 önerileri incelendi' });
    del.flush({ message: 'Bu bağlantı yayınlanmış bir süre payında kullanılıyor.' }, { status: 409, statusText: 'Conflict' });
    expect(c.error()).toContain('süre payında kullanılıyor');
  });

  it('asks for topic shares when the outcome has several approved topics and sends them with the new estimate', () => {
    const { c, http, fixture, el } = setup();
    const outcome = { ...row('o'), kind: 'outcomes', id: 'o', code: 'TAR.1', mappings: [
      { topicId: 't1', path: 'Tarih (TYT) › Osmanlı', isApproved: true, status: 'approved', evidence: 'x' },
      { topicId: 't2', path: 'Tarih (AYT) › Osmanlı', isApproved: true, status: 'approved', evidence: 'x' }], estimates: [] };
    c.select(outcome as never); http.expectOne(r => r.url.endsWith('/outcomes/o')).flush({ data: outcome });
    http.expectOne(r => r.url.endsWith('/outcomes/o/topics')).flush({ data: [{ topicId: 't1', topicPath: 'Tarih (TYT) › Osmanlı' }, { topicId: 't2', topicPath: 'Tarih (AYT) › Osmanlı' }] });
    fixture.detectChanges();
    expect(text(el)).toContain('Konu payları'); expect(c.shares.length).toBe(2);
    c.learningMinutes = 21; c.practiceMinutes = 10; c.checkMinutes = 5; c.splitShares();
    expect(c.shares.map(s => s.learningMinutes)).toEqual([11, 10]); expect(c.sharesValid()).toBe(true);
    c.shares[0].learningMinutes = 1; expect(c.sharesValid()).toBe(false);
    c.form.reason = 'AYT payı girildi'; c.durationEvidence = 'Öğretmen gözlemi'; c.saveDuration();
    http.expectNone(r => r.url.endsWith('/estimates')); expect(c.error()).toContain('Konu payları');
    c.splitShares(); c.saveDuration();
    const req = http.expectOne(r => r.url.endsWith('/outcomes/o/estimates'));
    expect(req.request.body.topicShares).toEqual([
      { topicId: 't1', learningMinutes: 11, practiceMinutes: 5, checkMinutes: 3, scopeDescription: 'Tarih (TYT) › Osmanlı' },
      { topicId: 't2', learningMinutes: 10, practiceMinutes: 5, checkMinutes: 2, scopeDescription: 'Tarih (AYT) › Osmanlı' }]);
  });

  it('embeds the YKS scope editor on topics and the source editor on outcomes', () => {
    const { c, http, fixture, el } = setup();
    c.select(row('a') as never); http.expectOne(r => r.url.endsWith('/topics/a')).flush({ data: row('a') }); fixture.detectChanges();
    expect(el.querySelector('app-yks-scope-editor')).not.toBeNull(); expect(el.querySelector('app-outcome-source-editor')).toBeNull();
    const outcome = { ...row('o'), kind: 'outcomes', id: 'o', code: 'X', mappings: [], estimates: [] };
    c.select(outcome as never); http.expectOne(r => r.url.endsWith('/outcomes/o')).flush({ data: outcome });
    http.match(r => r.url.endsWith('/outcomes/o/topics')).forEach(r => r.flush({ data: [] })); fixture.detectChanges();
    expect(el.querySelector('app-outcome-source-editor')).not.toBeNull();
  });

  it('shows the quality view by lesson and opens a listed topic', () => {
    const { c, http, fixture, el } = setup();
    c.chooseTab('quality');
    const req = http.expectOne(r => r.url.endsWith('/curriculum-management/quality'));
    req.flush({ data: { counts: { noMinutes: 3954, noOutcomes: 2878, noYksScope: 10, inactiveParent: 0, pendingLinks: 206 },
      byLesson: [{ lessonId: 'l1', lessonName: 'Tarih (AYT)', examCode: 'AYT', counts: { noMinutes: 0, noOutcomes: 3, noYksScope: 0, inactiveParent: 0, pendingLinks: 206 } }],
      items: [{ topicId: 't9', topicPath: 'Tarih (AYT) › Osmanlı', issue: 'pendingLinks' }], totalCount: 1 } });
    fixture.detectChanges();
    expect(text(el)).toContain('Süresi yok'); expect(text(el)).toContain('3.954'); expect(text(el)).toContain('Tarih (AYT)'); expect(text(el)).not.toContain('pendingLinks');
    c.filterQualityLesson('l1');
    expect(http.expectOne(r => r.url.endsWith('/quality')).request.params.get('lessonId')).toBe('l1');
    c.openQualityItem({ topicId: 't9', topicPath: 'Tarih (AYT) › Osmanlı', issue: 'pendingLinks' });
    http.expectOne(r => r.url.endsWith('/topics/t9')).flush({ data: row('t9') }); expect(c.selected()?.id).toBe('t9');
  });

  it('switches to the quality view when "eksikleri göster" is chosen in the filter bar', async () => {
    const env = setup(); await topicsTab(env, []);
    env.c.filters.issue = 'noMinutes'; env.c.issueFilterChanged();
    expect(env.c.tab).toBe('quality');
    expect(env.http.expectOne(r => r.url.endsWith('/quality')).request.params.get('issue')).toBe('noMinutes');
  });

  it('reports an in-place move and a copy-on-use move differently', async () => {
    const { c, http } = setup(); c.select(row('a') as never); http.expectOne(r => r.url.endsWith('/topics/a')).flush({ data: row('a') });
    c.destinationId = 'unit-2'; c.moveConfirmed = true; c.form.reason = 'Doğru üniteye taşındı'; await c.move();
    http.expectOne(r => r.url.endsWith('/topics/a/move')).flush({ data: row('a') });
    http.match(() => true).forEach(r => r.flush(page([])));
    expect(c.message()).toContain('aynı kayıt yeni konuma taşındı');
    c.destinationId = 'unit-3'; c.moveConfirmed = true; c.form.reason = 'Doğru üniteye taşındı'; await c.move();
    http.expectOne(r => r.url.endsWith('/topics/a/move')).flush({ data: row('copy') });
    http.match(() => true).forEach(r => r.flush(page([])));
    expect(c.message()).toContain('kullanımda olduğu için');
  });

  it('lists the outcomes of a topic on demand and removes a link from the topic side', async () => {
    const { c, http } = setup(); c.select(row('a') as never); http.expectOne(r => r.url.endsWith('/topics/a')).flush({ data: row('a') });
    c.loadTopicOutcomes();
    http.expectOne(r => r.url.endsWith('/children') && r.params.get('kind') === 'outcomes' && r.params.get('parentId') === 'a')
      .flush(page([{ kind: 'outcomes', id: 'o1', name: 'Türev kuralları', code: 'MAT.12.3.1', isActive: true }]));
    expect(c.topicOutcomes().map(o => o.id)).toEqual(['o1']);
    c.form.reason = 'Yanlış bağlantı'; await c.removeTopicLink(c.topicOutcomes()[0]);
    expect(confirm).toHaveBeenCalled();
    http.expectOne(r => r.method === 'DELETE' && r.url.endsWith('/outcomes/mappings/a/o1')).flush({ data: { deleted: true } });
    http.expectOne(r => r.url.endsWith('/topics/a')).flush({ data: row('a') });
    expect(c.message()).toContain('Bağlantı kaldırıldı');
  });
  it('filters the exam list by state so passive exams can be selected together', () => {
    const { c, http, fixture } = setup();
    c.filters.active = 'false'; c.applyFilters();
    http.expectOne(r => r.url.endsWith('/children') && r.params.get('kind') === 'exams' && r.params.get('active') === 'false')
      .flush(page([{ ...row('x'), kind: 'exams', id: 'e1', isActive: false }, { ...row('y'), kind: 'exams', id: 'e2', isActive: true }]));
    fixture.detectChanges(); expect(c.listing().rows.map(r => r.id)).toEqual(['e1']);
    c.selectPage(); expect([...c.selectedIds()]).toEqual(['e1']);
  });
});

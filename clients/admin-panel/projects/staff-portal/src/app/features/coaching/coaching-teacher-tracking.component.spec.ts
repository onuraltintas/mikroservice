import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingTeacherTrackingComponent } from './coaching-teacher-tracking.component';
import { CoachingTeacherTrackingService, TrackingItem, TrackingPage } from './coaching-teacher-tracking.service';

const item: TrackingItem = { kind: 'study', recordId: 'work-8', studentId: 'student-3', studentName: 'Ayşe Öğrenci', title: 'Matematik tekrarı', planId: 'revision-4', date: '2026-10-19', startMinute: 1080, minutes: 30, timeZoneId: 'Europe/Istanbul' };
const page: TrackingPage = { items: [item], totalCount: 60, pageNumber: 1, pageSize: 25, overdueStudyCount: 40, overdueAssignmentCount: 15, awaitingReviewCount: 5, awaitingReflectionCount: 0, overdueDecisionCount: 0 };

describe('CoachingTeacherTrackingComponent', () => {
  it('keeps separate rows for multiple missing weeks of the same student and opens the chosen week',async()=>{
    const{c,fixture}=await setup();const rows=['2026-09-28','2026-10-05'].map(weekStart=>({...item,kind:'reflection' as const,recordId:item.studentId,weekStart,title:'Haftalık değerlendirme bekleniyor'}));
    c.data.set({...page,items:rows,awaitingReflectionCount:2});fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('başlangıç tarihinden önceki haftalar');
    await c.open(rows[1]);expect(c.selected()?.weekStart).toBe('2026-10-05');
  });
  it('offers the weekly review queue, count and opens the exact submitted week',async()=>{
    const{c,fixture,api}=await setup();const row={...item,kind:'weekly-review' as const,weekStart:'2026-09-28',title:'Haftalık değerlendirme inceleme bekliyor'};
    c.data.set({...page,awaitingWeeklyReviewCount:7,items:[row]});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('İnceleme bekleyen haftalık değerlendirmeler');
    expect(fixture.nativeElement.textContent).toContain('Haftalık inceleme bekleyen');
    c.filters.mode='weekly-review';c.applyFilters();expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({mode:'weekly-review'}),1);
    await c.open(row);expect(c.selected()?.weekStart).toBe('2026-09-28');
  });
  it('jumps beyond page one thousand using applied filters and retains global totals',async()=>{
    const{c,api,fixture}=await setup();c.filters.mode='weekly-review';c.applyFilters();
    const large={...page,totalCount:30001,awaitingWeeklyReviewCount:30001};c.data.set(large);
    api.get.mockReturnValueOnce(of({...large,pageNumber:1001}));c.pageInput=1001;c.goToPage();fixture.detectChanges();
    expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({mode:'weekly-review'}),1001);
    expect(c.pageInput).toBe(1001);expect(c.totalPages(c.data()!)).toBe(1201);expect(c.data()?.awaitingWeeklyReviewCount).toBe(30001);
    expect(fixture.nativeElement.textContent).toContain('1001 / 1201');
  });
  it('keeps the current rows for invalid jumps and follows the clamped server page after review',async()=>{
    const{c,api}=await setup();const calls=api.get.mock.calls.length;
    for(const invalid of [0,4,1.5,NaN]){c.pageInput=invalid;c.goToPage();expect(c.pageError()).toContain('sayfa');expect(c.data()?.items).toEqual(page.items);}
    expect(api.get).toHaveBeenCalledTimes(calls);expect(c.error()).toBe('');
    c.data.set({...page,pageNumber:3});await c.open({...item,kind:'weekly-review',weekStart:'2026-10-12'});
    api.get.mockReturnValueOnce(of({...page,totalCount:50,pageNumber:2}));await c.back();
    expect(api.get).toHaveBeenLastCalledWith(expect.anything(),3);expect(c.pageInput).toBe(2);expect(c.pageError()).toBe('');
  });
  it('shows weekly counts, filters and exact decision scope and refreshes after return',async()=>{
    const{c,fixture,api}=await setup();c.data.set({...page,awaitingReflectionCount:3,overdueDecisionCount:2,items:[{...item,kind:'decision',weekStart:'2026-10-12'}]});fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Geciken karar');expect(fixture.nativeElement.textContent).toContain('Kararı aç');
    c.filters.mode='reflection';c.applyFilters();expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({mode:'reflection'}),1);
    expect((await c.openWeekly({studentId:'student',weekStart:'2026-10-12',decisionId:'decision'}))).toBe(true);expect(c.selected()).toMatchObject({kind:'decision',weekStart:'2026-10-12',recordId:'decision'});
    const children=c as unknown as {weekly?:{canLeavePage():boolean}};children.weekly={canLeavePage:()=>false};(await c.back());expect(c.selected()).not.toBeNull();
    children.weekly={canLeavePage:()=>true};(await c.back());expect(c.selected()).toBeNull();expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({mode:'reflection'}),1);
  });
  async function setup() {
    const api = { get: vi.fn(() => of(page)) };
    await TestBed.configureTestingModule({ imports: [CoachingTeacherTrackingComponent], providers: [provideHttpClient(), { provide: CoachingTeacherTrackingService, useValue: api }] }).compileComponents();
    const fixture = TestBed.createComponent(CoachingTeacherTrackingComponent); fixture.detectChanges();
    return { fixture, c: fixture.componentInstance, api };
  }
  it('shows global filtered counts, student, timezone and record action', async () => {
    const { fixture } = await setup(); const text = fixture.nativeElement.textContent;
    expect(text).toContain('Takip merkezi'); expect(text).toContain('40'); expect(text).toContain('15'); expect(text).toContain('5');
    expect(text).toContain('Ayşe Öğrenci'); expect(text).toContain('Europe/Istanbul'); expect(text).toContain('18:00');
    fixture.nativeElement.querySelector('[aria-label="Ayşe Öğrenci · Matematik tekrarı kaydını aç"]').click();
    await vi.waitFor(()=>expect(fixture.componentInstance.selected()).toEqual(item));
  });
  it('applies date, search and kind filters and keeps applied filters when paging', async () => {
    const { c, api } = await setup();
    c.filters = { fromDate: '2026-10-01', toDate: '2026-10-20', mode: 'review', search: 'Ayşe' }; c.applyFilters();
    expect(api.get).toHaveBeenLastCalledWith(c.filters, 1);
    c.filters.search = 'henüz uygulanmadı'; (await c.load(2));
    expect(api.get).toHaveBeenLastCalledWith(expect.objectContaining({ mode: 'review', search: 'Ayşe' }), 2);
  });
  it('blocks invalid periods and removes old counts instead of displaying stale results', async () => {
    const { c, api } = await setup();
    c.filters.fromDate = '2026-11-01'; c.filters.toDate = '2026-10-20'; c.applyFilters();
    expect(api.get).toHaveBeenCalledTimes(1); expect(c.data()).toBeNull(); expect(c.error()).toContain('tarih');
  });
  it('cancels previous requests and ignores late data after a new filter', async () => {
    const { c, api } = await setup(); const first = new Subject<TrackingPage>(); const second = new Subject<TrackingPage>();
    api.get.mockReturnValueOnce(first).mockReturnValueOnce(second); (await c.load()); c.filters.mode = 'review'; c.applyFilters();
    expect(c.data()).toBeNull(); first.next(page); expect(c.data()).toBeNull();
    second.next({ ...page, items: [], totalCount: 0 }); second.complete(); expect(c.data()?.totalCount).toBe(0); expect(c.loading()).toBe(false);
  });
  it('clears results on authorization failure and allows retry', async () => {
    const { c, api, fixture } = await setup();
    api.get.mockReturnValueOnce(throwError(() => ({ error: { message: 'Erişim kaldırıldı' } }))); (await c.refresh()); fixture.detectChanges();
    expect(c.data()).toBeNull(); expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('Erişim kaldırıldı');
    c.applyFilters(); expect(c.data()?.totalCount).toBe(60);
  });
  it('returns to the same filtered page and refreshes after a detail operation', async () => {
    const { c, api } = await setup(); c.data.set({ ...page, pageNumber: 2 }); (await c.open(item)); (await c.back());
    expect(c.selected()).toBeNull(); expect(api.get).toHaveBeenLastCalledWith(expect.anything(), 2);
  });
  it('keeps the detail while a plan edit or grade is pending', async () => {
    const { c, api } = await setup(); (await c.open(item));
    const children = c as unknown as { plan?: { canLeavePage(): boolean }; assignment?: { canLeavePage(): boolean } };
    children.plan = { canLeavePage: () => false }; (await c.back()); expect(c.selected()).toEqual(item); expect(api.get).toHaveBeenCalledTimes(1);
    children.plan = undefined; children.assignment = { canLeavePage: () => false }; (await c.back()); expect(c.selected()).toEqual(item);
  });
  it('labels submitted work separately and shows an honest empty state', async () => {
    const { c, fixture } = await setup();
    c.data.set({ ...page, items: [{ ...item, kind: 'review', planId: null }], awaitingReviewCount: 1 }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Teslimi değerlendir');
    c.data.set({ ...page, items: [], totalCount: 0 }); fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('çalışmadığı anlamına gelmez');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

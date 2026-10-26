import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentWeeklyHomeComponent } from './student-weekly-home.component';
import { CoachingWeeklyReviewService } from '../../../core/services/coaching-weekly-review.service';
const sample={weekStart:'2026-10-12',currentWeekStart:'2026-10-19',needsReflection:true,openDecisions:8,overdueDecisions:2,decisions:[{id:'decision',weekStart:'2026-10-05',title:'Paragraf tekrarı',dueOn:'2026-10-13',isOverdue:true}]};
describe('StudentWeeklyHomeComponent',()=>{
  async function setup(){const api={home:vi.fn(()=>of(sample))};TestBed.configureTestingModule({imports:[StudentWeeklyHomeComponent],providers:[provideRouter([]),{provide:CoachingWeeklyReviewService,useValue:api}]});await TestBed.compileComponents();const f=TestBed.createComponent(StudentWeeklyHomeComponent);f.detectChanges(); await f.whenStable();return{f,c:f.componentInstance,api};}
  it('shows the full missing-week count and sends the student to the oldest missing week',async()=>{
    const{c,f}=await setup();c.data.set({...sample,weekStart:'2026-09-28',missingReflectionCount:3});f.detectChanges();
    expect(f.nativeElement.textContent).toContain('3 haftanın değerlendirmesi eksik');
    expect((f.nativeElement.querySelector('a') as HTMLAnchorElement).getAttribute('href')).toContain('week=2026-09-28');
  });
  it('links a missing review and an older decision to their exact weeks and keeps global counts',async()=>{
    const{f}=await setup();const links=Array.from(f.nativeElement.querySelectorAll('a')) as HTMLAnchorElement[];
    expect(links[0].getAttribute('href')).toContain('week=2026-10-12');expect(links[1].getAttribute('href')).toContain('week=2026-10-05&decision=decision');
    expect(links[2].getAttribute('href')).toContain('week=2026-10-19');expect(f.nativeElement.textContent).toContain('8 açık karar');expect(f.nativeElement.textContent).toContain('Tarihi geçti');
  });
  it('clears stale data on error and refreshes the empty state after completed decisions',async()=>{
    const{f,c,api}=await setup();api.home.mockReturnValueOnce(throwError(()=>({status:403})));await c.load();f.detectChanges(); await f.whenStable();expect(c.data()).toBeNull();expect(f.nativeElement.querySelector('[role="alert"]')).toBeTruthy();
    api.home.mockReturnValueOnce(of({...sample,needsReflection:false,openDecisions:0,overdueDecisions:0,decisions:[]}));await c.load();f.detectChanges(); await f.whenStable();expect(f.nativeElement.textContent).toContain('Açık kararın bulunmuyor.');
  });
  it('does not issue duplicate reads during loading',async()=>{const{c,api}=await setup();const pending=new Subject<any>();api.home.mockReturnValueOnce(pending);await c.load();await c.load();expect(api.home).toHaveBeenCalledTimes(2);pending.next(sample);pending.complete();expect(c.loading()).toBe(false);});
  it('keeps the reflection link without repeating decisions on the unified home', async()=>{
    const { f } = await setup(); f.componentRef.setInput('showDecisions', false); f.detectChanges();
    expect(f.nativeElement.textContent).toContain('Haftalık değerlendirmen');
    expect(f.nativeElement.textContent).toContain('Haftanı değerlendir');
    expect(f.nativeElement.querySelectorAll('article')).toHaveLength(0);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });

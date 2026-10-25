import { of, throwError } from 'rxjs';
import { TestBed } from '@angular/core/testing';
import { StaffNotificationsService } from './staff-notifications.service';
import { StaffNotificationCenterComponent } from './staff-notification-center.component';

describe('StaffNotificationCenterComponent', () => {
  it('emits the weekly destination without treating it as an assignment',()=>{
    const student='473bd2d6-e3db-47c4-be56-eeedec0022ed';
    const{fixture,component}=createComponent({getNotifications:vi.fn(()=>of([{id:'weekly',title:'Hafta',message:'Yeni',isRead:false,type:'WeeklyReflectionSaved',relatedEntityId:`weekly:${student}:2026-10-12`}]))});
    fixture.componentRef.setInput('canOpenCoachingAssignments',true);fixture.detectChanges();const emit=vi.spyOn(component.openCoachingWeekly,'emit');const assignment=vi.spyOn(component.openCoachingAssignment,'emit');
    const button=Array.from(fixture.nativeElement.querySelectorAll('button')).find((button:any)=>button.textContent.includes('Haftalık değerlendirmeyi aç'))as HTMLButtonElement;button.click();
    expect(emit).toHaveBeenCalledWith(expect.objectContaining({studentId:student,weekStart:'2026-10-12',decisionId:null}));expect(assignment).not.toHaveBeenCalled();
  });
  it('emits the exact submitted assignment only for the Coaching teacher workspace',()=>{
    const id='c260e5c4-facd-42cd-af3a-69aa0b9cc67f';
    const {component,fixture}=createComponent({getNotifications:vi.fn(()=>of([
      {id:'submitted',title:'Teslim',message:'Ödev hazır',isRead:false,type:'AssignmentSubmitted',relatedEntityId:id},
      {id:'legacy',title:'Eski teslim',message:'Kimlik yok',isRead:false,type:'AssignmentSubmitted'}
    ]))});
    const emit=vi.spyOn(component.openCoachingAssignment,'emit');
    expect(fixture.nativeElement.textContent).not.toContain('Teslimleri incele');
    fixture.componentRef.setInput('canOpenCoachingAssignments',true);fixture.detectChanges();
    const button=Array.from(fixture.nativeElement.querySelectorAll('button')).find((button:any)=>button.textContent.includes('Teslimleri incele')) as HTMLButtonElement;
    button.click();expect(emit).toHaveBeenCalledExactlyOnceWith(id);
    expect(component.target(component.notifications()[1])).toBeNull();
    fixture.componentRef.setInput('product','speed-reading');fixture.detectChanges();
    expect(component.target(component.notifications()[0])).toBeNull();
  });
  const coachingNotification = { id: 'c-1', title: 'Seans', message: 'Yarın', isRead: false };
  const speedReadingNotification = { id: 's-1', title: 'Ödev', message: 'Yeni ödev', isRead: false };

  function createComponent(service: Partial<StaffNotificationsService>) {
    TestBed.configureTestingModule({
      imports: [StaffNotificationCenterComponent],
      providers: [{ provide: StaffNotificationsService, useValue: service }]
    });
    const fixture = TestBed.createComponent(StaffNotificationCenterComponent);
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance };
  }

  it('loads and filters Coaching notifications and supports read/delete actions', () => {
    const service = {
      getNotifications: vi.fn().mockReturnValue(of([coachingNotification])),
      markAsRead: vi.fn().mockReturnValue(of(null)),
      deleteNotification: vi.fn().mockReturnValue(of(null))
    };
    const { component } = createComponent(service);

    expect(service.getNotifications).toHaveBeenCalledWith('coaching');
    expect(component.unreadCount).toBe(1);
    component.filter.set('read');
    expect(component.visibleNotifications).toHaveLength(0);
    component.filter.set('all');
    component.markAsRead(coachingNotification);
    expect(component.notifications()[0].isRead).toBe(true);
    expect(service.markAsRead).toHaveBeenCalledWith('coaching', 'c-1');
    component.confirmDelete('c-1');
    component.deleteNotification('c-1');
    expect(component.notifications()).toHaveLength(0);
    expect(service.deleteNotification).toHaveBeenCalledWith('coaching', 'c-1');
  });

  it('loads Speed Reading-only notification preferences and saves them', () => {
    const preferences = [{ id: 'p-1', notificationType: 1, enableInApp: true, enableEmail: false, enablePush: false }];
    const service = {
      getNotifications: vi.fn().mockReturnValue(of([speedReadingNotification])),
      getSpeedReadingPreferences: vi.fn().mockReturnValue(of(preferences)),
      saveSpeedReadingPreferences: vi.fn().mockReturnValue(of(null))
    };
    const { fixture, component } = createComponent(service);
    fixture.componentRef.setInput('product', 'speed-reading');
    fixture.detectChanges();

    component.togglePreferences();
    expect(service.getSpeedReadingPreferences).toHaveBeenCalledOnce();
    expect(component.preferencesVisible()).toBe(true);
    component.setAllInApp(false);
    component.setAllEmail(true);
    expect(component.preferences()[0].enableInApp).toBe(false);
    expect(component.preferences()[0].enableEmail).toBe(true);
    component.savePreferences();
    expect(service.saveSpeedReadingPreferences).toHaveBeenCalledWith(component.preferences());
    expect(component.successMessage()).toBe('Bildirim tercihleri kaydedildi.');
  });

  it('does not expose Speed Reading preferences in Coaching', () => {
    const service = {
      getNotifications: vi.fn().mockReturnValue(of([])),
      getSpeedReadingPreferences: vi.fn().mockReturnValue(of([]))
    };
    const { component } = createComponent(service);

    component.togglePreferences();

    expect(service.getSpeedReadingPreferences).not.toHaveBeenCalled();
    expect(component.preferencesVisible()).toBe(false);
  });

  it('shows a friendly message when notifications fail to load', () => {
    const { component } = createComponent({ getNotifications: () => throwError(() => new Error('unavailable')) });

    expect(component.errorMessage()).toBe('Bildirimler yüklenemedi. Lütfen yeniden deneyin.');
  });
});

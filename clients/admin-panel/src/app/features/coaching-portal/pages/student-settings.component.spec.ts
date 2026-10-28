import { CoachingParentService } from '../parent/coaching-parent.service';
import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CoachingTimeZoneService } from '../../../core/services/coaching-time-zone.service';
import { StudentSettingsComponent } from './student-settings.component';

describe('StudentSettingsComponent', () => {
  async function setup(zone: string | null = 'Europe/Berlin') {
    const api = { get: vi.fn(() => of({timeZoneId:zone,availabilityVersion:3,draftVersion:4})), save: vi.fn(() => of({timeZoneId:'Europe/London',availabilityVersion:4,draftVersion:5})) };
    TestBed.configureTestingModule({imports:[StudentSettingsComponent],providers:[{provide:CoachingTimeZoneService,useValue:api},{provide:CoachingParentService,useValue:{getLinks:()=>of([])}}]});
    const fixture=TestBed.createComponent(StudentSettingsComponent); fixture.detectChanges(); await fixture.whenStable();
    return {fixture,component:fixture.componentInstance,api};
  }
  it('keeps the saved zone instead of overwriting it with the device zone',async()=>{
    const {component,api}=await setup(); expect(component.zone).toBe('Europe/Berlin'); expect(api.save).not.toHaveBeenCalled();
  });
  it('suggests the device zone for a new account without silently saving',async()=>{
    const {component,api}=await setup(null); expect(component.zone).toBe(component.detected); expect(api.save).not.toHaveBeenCalled();
  });
  it('saves with both versions and preserves input when a concurrent update is rejected',async()=>{
    const {component,api}=await setup(); component.zone='Europe/London';component.dirty=true;
    api.save.mockReturnValueOnce(throwError(()=>({status:409,error:{message:'Yeniden yükle'}})));
    component.save(); expect(component.zone).toBe('Europe/London');expect(component.dirty).toBe(true);
    expect(api.save).toHaveBeenCalledWith({timeZoneId:'Europe/London',availabilityVersion:3,draftVersion:4});
    component.save();expect(component.dirty).toBe(false);
  });
});

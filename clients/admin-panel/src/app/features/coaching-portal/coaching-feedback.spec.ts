import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { coachingFeedback } from './coaching-feedback';
import { ToasterService } from '../../core/services/toaster.service';

@Component({standalone:true,template:''})
class FeedbackHost {
  readonly success = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  private readonly feedback = coachingFeedback(this.success, this.error);
}

describe('Coaching feedback', () => {
  it('announces outcomes centrally without repeating messages on unrelated renders', async () => {
    const toaster = {success:vi.fn(),error:vi.fn()};
    TestBed.configureTestingModule({imports:[FeedbackHost],providers:[{provide:ToasterService,useValue:toaster}]});
    const fixture=TestBed.createComponent(FeedbackHost);fixture.detectChanges();
    expect(toaster.success).not.toHaveBeenCalled();expect(toaster.error).not.toHaveBeenCalled();
    fixture.componentInstance.success.set('Kaydedildi.');fixture.detectChanges();fixture.detectChanges();
    await vi.waitFor(()=>expect(toaster.success).toHaveBeenCalledExactlyOnceWith('Kaydedildi.'));
    fixture.componentInstance.error.set('Bağlantıyı kontrol edin.');fixture.detectChanges();
    await vi.waitFor(()=>expect(toaster.error).toHaveBeenCalledExactlyOnceWith('Bağlantıyı kontrol edin.'));
    fixture.componentInstance.success.set(null);fixture.detectChanges();fixture.componentInstance.success.set('Kaydedildi.');fixture.detectChanges();
    await vi.waitFor(()=>expect(toaster.success).toHaveBeenCalledTimes(2));
    fixture.destroy();
  });
});

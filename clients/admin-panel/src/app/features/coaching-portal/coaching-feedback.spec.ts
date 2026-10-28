import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { coachingFeedback } from './coaching-feedback';
import { ToasterService } from '../../core/services/toaster.service';

@Component({standalone:true,template:`@if (success()) { <p role="status">{{ success() }}</p> } @if (error()) { <p role="alert">{{ error() }}</p> }`})
class FeedbackHost {
  readonly success = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly other = signal(0);
  private readonly feedback = coachingFeedback(this.success, this.error);
}

describe('Coaching feedback', () => {
  it('brings each new message into view once without a pop-up', async () => {
    const toaster = {success:vi.fn(),error:vi.fn()}; const scroll = vi.fn(); Element.prototype.scrollIntoView = scroll;
    TestBed.configureTestingModule({imports:[FeedbackHost],providers:[{provide:ToasterService,useValue:toaster}]});
    const fixture=TestBed.createComponent(FeedbackHost);fixture.detectChanges();
    expect(scroll).not.toHaveBeenCalled();
    fixture.componentInstance.success.set('Kaydedildi.');fixture.detectChanges();await fixture.whenStable();
    await vi.waitFor(()=>expect(scroll).toHaveBeenCalledTimes(1));
    expect((scroll.mock.contexts[0] as HTMLElement).getAttribute('role')).toBe('status');
    fixture.componentInstance.other.set(1);fixture.detectChanges();await fixture.whenStable();
    expect(scroll).toHaveBeenCalledTimes(1);
    fixture.componentInstance.error.set('Bağlantını kontrol et.');fixture.detectChanges();await fixture.whenStable();
    await vi.waitFor(()=>expect(scroll).toHaveBeenCalledTimes(2));
    expect((scroll.mock.contexts[1] as HTMLElement).getAttribute('role')).toBe('alert');
    expect(toaster.success).not.toHaveBeenCalled();expect(toaster.error).not.toHaveBeenCalled();
    fixture.destroy();
  });
});

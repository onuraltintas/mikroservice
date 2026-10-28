import { afterNextRender, DestroyRef, effect, ElementRef, inject, Injector, Signal } from '@angular/core';
import type { ToasterService } from '../../core/services/toaster.service';

function feedbackLoader() {
  const injector=inject(Injector), destroy=inject(DestroyRef);
  return async () => {
    const {ToasterService}=await import('../../core/services/toaster.service');
    return destroy.destroyed ? null : injector.get(ToasterService);
  };
}

/** Shared Material feedback loads on demand, including in the lean staff application. */
export function coachingToaster(): Pick<ToasterService,'confirm'|'success'|'error'> {
  const load=feedbackLoader();
  return {
    confirm: async (message,options) => (await load())?.confirm(message,options) ?? false,
    success: (message) => { void load().then(toaster=>toaster?.success(message)); },
    error: (message) => { void load().then(toaster=>toaster?.error(message)); }
  };
}

/**
 * Roadmap O1: a message is shown once, in the page next to what it is about (role="alert" / role="status").
 * On a phone that place can be off screen, so the newest message is brought into view instead of being
 * repeated as a pop-up.
 */
export function coachingFeedback(success?: Signal<string | null>, error?: Signal<string | null>): void {
  const host=inject(ElementRef) as ElementRef<HTMLElement>, injector=inject(Injector);
  for(const [source,role] of [[success,'status'],[error,'alert']] as const) if(source) effect(()=>{
    const message=source();
    if(!message) return;
    afterNextRender(()=>{
      const nodes=[...host.nativeElement.querySelectorAll<HTMLElement>(`[role="${role}"]`)];
      const node=nodes.find(item=>item.textContent?.includes(message))??nodes[0];
      node?.scrollIntoView?.({block:'nearest',behavior:'smooth'});
    },{injector});
  });
}

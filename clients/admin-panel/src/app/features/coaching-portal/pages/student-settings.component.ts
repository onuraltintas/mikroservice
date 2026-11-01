import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingTimeZone, CoachingTimeZoneService, detectedTimeZone } from '../../../core/services/coaching-time-zone.service';
import { coachingFeedback, coachingToaster } from '../coaching-feedback';

const zoneNames:Record<string,string>={'Europe/Istanbul':'İstanbul (Türkiye)'};
/** A readable label with the current offset, e.g. "İstanbul (Türkiye) · GMT+3". */
export function timeZoneOptions(ids:string[]):{id:string;label:string}[]{
  const now=new Date();
  return ids.map(id=>{
    let offset='';
    try{offset=new Intl.DateTimeFormat('en-US',{timeZone:id,timeZoneName:'shortOffset'}).formatToParts(now).find(part=>part.type==='timeZoneName')?.value??'';}catch{offset='';}
    const name=zoneNames[id]??id.split('/').map(part=>part.replace(/_/g,' ')).join(' / ');
    return {id,label:offset?`${name} · ${offset}`:name};
  });
}

/** T5 (31.10.2026): the time zone, shown under Profil ve ayarlar › Çalışma tercihlerim. */
@Component({selector:'app-student-settings',standalone:true,imports:[FormsModule],
  template:`<section class="cs-account-section time-zone" aria-labelledby="time-zone-title">
    <h2 id="time-zone-title">Saat dilimi</h2>
    <p>Planın ve hatırlatmaların bu saate göre gelir. Cihazın: <strong>{{detected}}</strong></p>
    @if(error()){<p role="alert" class="cs-work-alert is-danger">{{error()}}<button type="button" (click)="reload()" [disabled]="loading() || saving()">Yeniden yükle</button></p>}
    @if(loading()){<p role="status">Yükleniyor…</p>}
    <form (ngSubmit)="save()">
      <fieldset [disabled]="loading() || saving() || !ready()">
        <label for="time-zone" class="sr-only">Saat dilimi</label>
        <select id="time-zone" name="timeZone" [(ngModel)]="zone" (ngModelChange)="dirty=true" required>
          <optgroup label="Önerilen">@for(item of suggestedZones();track item.id){<option [value]="item.id">{{item.label}}</option>}</optgroup>
          <optgroup label="Tüm saat dilimleri">@for(item of allZones;track item.id){<option [value]="item.id">{{item.label}}</option>}</optgroup>
        </select>
        <div class="actions"><button type="button" class="cs-button-secondary" (click)="useDevice()">Cihazımınkini kullan</button>
        <button type="submit" class="cs-button-primary" [disabled]="!zone.trim() || zone===saved?.timeZoneId">{{saving()?'Kaydediliyor…':'Kaydet'}}</button></div>
      </fieldset>
    </form>
    <p class="muted">Değiştirirsen mevcut planındaki saatler ve bitirdiğin çalışmalar aynen kalır.</p>
  </section>`,
  styles:[`:host{display:block}fieldset{display:grid;gap:var(--coaching-space-3);margin:0;padding:0;border:0}select{width:100%}.actions{display:flex;flex-wrap:wrap;gap:var(--coaching-space-2)}h2{margin:0 0 var(--coaching-space-1)}p{margin:0 0 var(--coaching-space-3)}.muted{margin:var(--coaching-space-3) 0 0}`]})
export class StudentSettingsComponent implements OnInit {
  private readonly api=inject(CoachingTimeZoneService); private readonly destroy=inject(DestroyRef); private readonly toaster=coachingToaster();
  readonly loading=signal(false); readonly saving=signal(false); readonly ready=signal(false); readonly error=signal(''); readonly message=signal('');
  readonly detected=detectedTimeZone();
  // Roadmap O1: the zone is picked from a list (no free typing); Istanbul, this device and the saved zone come first.
  readonly allZones=timeZoneOptions(((Intl as unknown as {supportedValuesOf?:(key:string)=>string[]}).supportedValuesOf?.('timeZone') ?? ['Europe/Istanbul','Europe/London','Europe/Berlin','America/New_York','Asia/Dubai']));
  private readonly savedZone=signal<string|null>(null);
  readonly suggestedZones=computed(()=>timeZoneOptions([...new Set(['Europe/Istanbul',this.detected,this.savedZone()].filter((x):x is string=>!!x))]));
  zone=''; dirty=false; saved:CoachingTimeZone|null=null;
  ngOnInit(){this.load();}
  private load(){this.loading.set(true);this.error.set('');this.api.get().pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:value=>{this.saved=value;this.savedZone.set(value.timeZoneId);this.zone=value.timeZoneId??this.detected;this.ready.set(true);this.dirty=false;},error:()=>{this.ready.set(false);this.error.set('Saat dilimin yüklenemedi.');}});}
  useDevice(){this.zone=this.detected;this.dirty=this.zone!==this.saved?.timeZoneId;}
  async reload(){if(this.saving()||this.loading()||!await this.canLeavePage())return;this.load();}
  save(){if(!this.ready()||this.saving()||this.loading()||!this.saved||!this.zone.trim())return;this.saving.set(true);this.error.set('');this.message.set('');this.api.save({...this.saved,timeZoneId:this.zone.trim()}).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false))).subscribe({next:value=>{this.saved=value;this.savedZone.set(value.timeZoneId);this.zone=value.timeZoneId!;this.dirty=false;this.message.set('Saat dilimin kaydedildi.');},error:e=>this.error.set(e?.error?.message??'Kaydedilemedi. Seçimin duruyor; yeniden deneyebilirsin.')});}
  async canLeavePage(){return !this.saving()&&(!this.dirty||await this.toaster.confirm('Saat dilimindeki değişikliği kaydetmeden çıkılsın mı?'));}
  private readonly feedback=coachingFeedback(this.message,this.error);
}

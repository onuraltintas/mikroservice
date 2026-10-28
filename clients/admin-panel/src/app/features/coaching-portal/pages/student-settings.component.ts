import { Component, DestroyRef, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingTimeZone, CoachingTimeZoneService, detectedTimeZone } from '../../../core/services/coaching-time-zone.service';
import { coachingFeedback, coachingToaster } from '../coaching-feedback';
import { StudentParentLinksComponent } from './student-parent-links.component';
import { StudentTeachersComponent } from './student-teachers.component';
import { StudentDataExportComponent } from './student-data-export.component';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { CoachingCalendarSubscriptionApi, CoachingCalendarSubscriptionComponent } from '../../../../../shared/coaching-calendar-subscription.component';
import { NotificationPreferencesApi, NotificationPreferencesComponent } from '../../../../../shared/notification-preferences.component';

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

@Component({selector:'app-student-settings',standalone:true,imports:[FormsModule, StudentParentLinksComponent, StudentTeachersComponent, StudentDataExportComponent, CoachingCalendarSubscriptionComponent, NotificationPreferencesComponent],styleUrl:'./student-study-plans.component.scss',
  template:`<section class="study-plans" aria-labelledby="settings-title">
    <header class="page-heading"><div><p class="eyebrow">Çalışma tercihlerim</p><h1 id="settings-title">Ayarlar</h1></div></header>
    @if(error()){<p role="alert" class="notice error">{{error()}}</p>}
    @if(message()){<p role="status" class="notice success">{{message()}}</p>}
    @if(loading()){<p role="status">Ayarlar yükleniyor…</p>}
    <form class="panel" (ngSubmit)="save()"><h2>Tarih ve saat</h2>
      <p>Cihazından algılanan saat dilimi: <strong>{{detected}}</strong>. Konum izni veya IP sorgusu kullanılmaz.</p>
      <fieldset [disabled]="loading() || saving() || !ready()">
        <label>Saat dilimi<select name="timeZone" [(ngModel)]="zone" (ngModelChange)="dirty=true" required>
          <optgroup label="Önerilen">@for(item of suggestedZones();track item.id){<option [value]="item.id">{{item.label}}</option>}</optgroup>
          <optgroup label="Tüm saat dilimleri">@for(item of allZones;track item.id){<option [value]="item.id">{{item.label}}</option>}</optgroup>
        </select></label>
        <div class="actions"><button type="button" class="secondary" (click)="useDevice()">Cihazımdan algıla</button>
        <button type="submit" [disabled]="!zone.trim() || zone===saved?.timeZoneId">{{saving()?'Kaydediliyor…':'Ayarları kaydet'}}</button></div>
      </fieldset>
      <p class="muted">Bu seçim hesabına kaydedilir; cihazın değiştiğinde kendiliğinden değiştirilmez. Müsaitlik ve haftalık taslak aynı ayarı kullanır. Mevcut tarihli planların saatleri ve tamamlanan çalışmalar korunur.</p>
    </form><button type="button" class="secondary" (click)="reload()" [disabled]="loading() || saving()">Ayarları yeniden yükle</button>
    <app-student-teachers />
    <edu-coaching-calendar-subscription [api]="calendar" role="student" />
    <edu-notification-preferences [api]="notifications" />
    <app-student-parent-links />
    <app-student-data-export />
  </section>`})
export class StudentSettingsComponent implements OnInit {
  // Roadmap O6: personal calendar subscription link for the student's sessions.
  readonly calendar=new CoachingCalendarSubscriptionApi(inject(HttpClient),environment.apiUrl);
  // Roadmap O8: which notifications also come by e-mail.
  readonly notifications=new NotificationPreferencesApi(inject(HttpClient),environment.apiUrl);
  private readonly api=inject(CoachingTimeZoneService); private readonly destroy=inject(DestroyRef); private readonly toaster=coachingToaster();
  readonly loading=signal(false); readonly saving=signal(false); readonly ready=signal(false); readonly error=signal(''); readonly message=signal('');
  readonly detected=detectedTimeZone();
  // Roadmap O1: the zone is picked from a list (no free typing); Istanbul, this device and the saved zone come first.
  readonly allZones=timeZoneOptions(((Intl as unknown as {supportedValuesOf?:(key:string)=>string[]}).supportedValuesOf?.('timeZone') ?? ['Europe/Istanbul','Europe/London','Europe/Berlin','America/New_York','Asia/Dubai']));
  private readonly savedZone=signal<string|null>(null);
  readonly suggestedZones=computed(()=>timeZoneOptions([...new Set(['Europe/Istanbul',this.detected,this.savedZone()].filter((x):x is string=>!!x))]));
  zone=''; dirty=false; saved:CoachingTimeZone|null=null;
  ngOnInit(){this.load();}
  private load(){this.loading.set(true);this.error.set('');this.api.get().pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:value=>{this.saved=value;this.savedZone.set(value.timeZoneId);this.zone=value.timeZoneId??this.detected;this.ready.set(true);this.dirty=false;},error:()=>{this.ready.set(false);this.error.set('Ayarlar yüklenemedi. Yeniden deneyebilirsin.');}});}
  useDevice(){this.zone=this.detected;this.dirty=this.zone!==this.saved?.timeZoneId;}
  async reload(){if(this.saving()||this.loading()||!await this.canLeavePage())return;this.load();}
  save(){if(!this.ready()||this.saving()||this.loading()||!this.saved||!this.zone.trim())return;this.saving.set(true);this.error.set('');this.message.set('');this.api.save({...this.saved,timeZoneId:this.zone.trim()}).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false))).subscribe({next:value=>{this.saved=value;this.savedZone.set(value.timeZoneId);this.zone=value.timeZoneId!;this.dirty=false;this.message.set('Saat ayarın kaydedildi. Mevcut çalışma planın değişmedi.');},error:e=>this.error.set(e?.error?.message??'Ayar kaydedilemedi. Değişikliklerin korunuyor; güncel ayarları yeniden yükleyebilirsin.')});}
  async canLeavePage(){return !this.saving()&&(!this.dirty||await this.toaster.confirm('Kaydedilmemiş saat ayarından vazgeçilsin mi?'));}
  private readonly feedback=coachingFeedback(this.message,this.error);
}

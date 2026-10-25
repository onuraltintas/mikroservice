import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { finalize } from 'rxjs';
import { CoachingTimeZone, CoachingTimeZoneService, detectedTimeZone } from '../../../core/services/coaching-time-zone.service';
import { coachingFeedback, coachingToaster } from '../coaching-feedback';

@Component({selector:'app-student-settings',standalone:true,imports:[FormsModule],styleUrl:'./student-study-plans.component.scss',
  template:`<section class="study-plans" aria-labelledby="settings-title">
    <header class="page-heading"><div><p class="eyebrow">Çalışma tercihlerim</p><h1 id="settings-title">Ayarlar</h1></div></header>
    @if(error()){<p role="alert" class="notice error">{{error()}}</p>}
    @if(message()){<p role="status" class="notice success">{{message()}}</p>}
    @if(loading()){<p role="status">Ayarlar yükleniyor…</p>}
    <form class="panel" (ngSubmit)="save()"><h2>Tarih ve saat</h2>
      <p>Cihazından algılanan saat dilimi: <strong>{{detected}}</strong>. Konum izni veya IP sorgusu kullanılmaz.</p>
      <fieldset [disabled]="loading() || saving() || !ready()">
        <label>Saat dilimi<input name="timeZone" [(ngModel)]="zone" (ngModelChange)="dirty=true" list="time-zones" maxlength="100" required></label>
        <datalist id="time-zones">@for(item of zones;track item){<option [value]="item"></option>}</datalist>
        <div class="actions"><button type="button" class="secondary" (click)="useDevice()">Cihazımdan algıla</button>
        <button type="submit" [disabled]="!zone.trim() || zone===saved?.timeZoneId">{{saving()?'Kaydediliyor…':'Ayarları kaydet'}}</button></div>
      </fieldset>
      <p class="muted">Bu seçim hesabına kaydedilir; cihazın değiştiğinde kendiliğinden değiştirilmez. Müsaitlik ve haftalık taslak aynı ayarı kullanır. Mevcut tarihli planların saatleri ve tamamlanan çalışmalar korunur.</p>
    </form><button type="button" class="secondary" (click)="reload()" [disabled]="loading() || saving()">Ayarları yeniden yükle</button>
  </section>`})
export class StudentSettingsComponent implements OnInit {
  private readonly api=inject(CoachingTimeZoneService); private readonly destroy=inject(DestroyRef); private readonly toaster=coachingToaster();
  readonly loading=signal(false); readonly saving=signal(false); readonly ready=signal(false); readonly error=signal(''); readonly message=signal('');
  readonly detected=detectedTimeZone();
  readonly zones=((Intl as unknown as {supportedValuesOf?:(key:string)=>string[]}).supportedValuesOf?.('timeZone') ?? ['Europe/Istanbul','Europe/London','Europe/Berlin','America/New_York','Asia/Dubai']);
  zone=''; dirty=false; saved:CoachingTimeZone|null=null;
  ngOnInit(){this.load();}
  private load(){this.loading.set(true);this.error.set('');this.api.get().pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.loading.set(false))).subscribe({next:value=>{this.saved=value;this.zone=value.timeZoneId??this.detected;this.ready.set(true);this.dirty=false;},error:()=>{this.ready.set(false);this.error.set('Ayarlar yüklenemedi. Yeniden deneyebilirsin.');}});}
  useDevice(){this.zone=this.detected;this.dirty=this.zone!==this.saved?.timeZoneId;}
  async reload(){if(this.saving()||this.loading()||!await this.canLeavePage())return;this.load();}
  save(){if(!this.ready()||this.saving()||this.loading()||!this.saved||!this.zone.trim())return;this.saving.set(true);this.error.set('');this.message.set('');this.api.save({...this.saved,timeZoneId:this.zone.trim()}).pipe(takeUntilDestroyed(this.destroy),finalize(()=>this.saving.set(false))).subscribe({next:value=>{this.saved=value;this.zone=value.timeZoneId!;this.dirty=false;this.message.set('Saat ayarın kaydedildi. Mevcut çalışma planın değişmedi.');},error:e=>this.error.set(e?.error?.message??'Ayar kaydedilemedi. Değişikliklerin korunuyor; güncel ayarları yeniden yükleyebilirsin.')});}
  async canLeavePage(){return !this.saving()&&(!this.dirty||await this.toaster.confirm('Kaydedilmemiş saat ayarından vazgeçilsin mi?'));}
  private readonly feedback=coachingFeedback(this.message,this.error);
}

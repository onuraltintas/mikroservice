import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { StudentDataExportComponent } from '../pages/student-data-export.component';
import { CoachingParentService, ParentWeeklyEmail } from './coaching-parent.service';

/**
 * V4 (01.11.2026): Bildirim tercihleri for a parent — the weekly summary e-mail on or off (notification category
 * ParentSummary). The in-app summary still lands on Bildirimler.
 */
@Component({
  selector: 'app-parent-notification-settings',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="parent-settings-title">
    <header>
      <h1 id="parent-settings-title">Bildirim tercihleri</h1>
      <p>Haftalık özeti e-postayla da almak isteyip istemediğini seç.</p>
    </header>
    <section aria-labelledby="parent-weekly-title">
      <h2 id="parent-weekly-title">Haftalık özet</h2>
      <p>Her hafta tek bir e-posta: Koçluk ve Hızlı okuma bölümleriyle. Yalnızca çocuğunun kullandığı ve seninle paylaştığı bölümler yer alır; paylaşılan bir bölüm yoksa e-posta gönderilmez.</p>
      @if (error()) { <p class="cp-alert" role="alert">{{ error() }}</p> }
      @if (saved()) { <p class="cp-success" role="status">{{ saved() }}</p> }
      @if (choice(); as current) {
        <label class="cp-switch">
          <input type="checkbox" role="switch" [checked]="current.email" [disabled]="saving()" (change)="toggle($event)" />
          <span class="cp-switch-text"><strong>Haftalık özet e-postası</strong><small>{{ current.email ? 'Açık: özet e-postayla da gelir.' : 'Kapalı: özet yalnızca Bildirimler sayfasında görünür.' }}</small></span>
        </label>
      } @else if (loading()) { <p class="cp-muted" role="status">Tercihin yükleniyor…</p> }
      @else if (!error()) { <p class="cp-muted">Bu tercih şu an değiştirilemiyor.</p> }
    </section>
  </div>`
})
export class ParentNotificationSettingsComponent implements OnInit {
  private readonly service = inject(CoachingParentService);
  readonly choice = signal<ParentWeeklyEmail | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly saved = signal<string | null>(null);

  ngOnInit(): void {
    this.service.getWeeklyEmail().subscribe({
      next: choice => { this.choice.set(choice); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Bildirim tercihin yüklenemedi. Sayfayı yenileyip yeniden dene.'); }
    });
  }

  toggle(event: Event) {
    const current = this.choice(); if (!current) return;
    const next = { ...current, email: (event.target as HTMLInputElement).checked };
    this.saving.set(true); this.error.set(null); this.saved.set(null);
    this.service.setWeeklyEmail(next).subscribe({
      next: result => { this.choice.set(result ?? next); this.saving.set(false); this.saved.set('Tercihin kaydedildi.'); },
      error: () => { this.saving.set(false); this.choice.set({ ...current }); this.error.set('Tercihin kaydedilemedi. Yeniden dene.'); }
    });
  }
}

/** V4: Kişisel verilerim for a parent — the Coaching data they created themselves (KVKK). */
@Component({
  selector: 'app-parent-personal-data',
  standalone: true,
  imports: [StudentDataExportComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="cs-account" aria-labelledby="parent-data-title">
    <header>
      <h1 id="parent-data-title">Kişisel verilerim</h1>
      <p>Veli hesabınla oluşturduğun bilgilerin bir kopyasını istediğin zaman indirebilirsin.</p>
    </header>
    <section aria-label="Verilerimi indir"><app-student-data-export audience="parent" /></section>
  </div>`
})
export class ParentPersonalDataComponent {}

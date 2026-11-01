import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { DarkModeService, ThemeMode } from '../../../core/services/dark-mode.service';

/** T2 (31.10.2026): the colour theme lives in Profil ve ayarlar › Görünüm (light, dark or the device setting). */
@Component({
  selector: 'app-student-theme-setting',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<section class="theme" aria-labelledby="theme-title">
    <h1 id="theme-title">Görünüm</h1>
    <p>Paneli sana en rahat gelen renklerle kullan.</p>
    <fieldset>
      <legend>Tema</legend>
      @for (option of options; track option.value) {
        <label [class.is-selected]="theme.mode() === option.value">
          <input type="radio" name="theme" [value]="option.value" [checked]="theme.mode() === option.value" (change)="theme.setMode(option.value)">
          <span><strong>{{ option.label }}</strong><small>{{ option.hint }}</small></span>
        </label>
      }
    </fieldset>
  </section>`,
  styles: [`.theme{max-width:640px}
    h1{margin:0 0 var(--coaching-space-1);font-size:var(--coaching-text-xl);font-weight:800;color:var(--ui-text)}
    p{margin:0 0 var(--coaching-space-5);color:var(--ui-text-muted);font-size:var(--coaching-text-body)}
    fieldset{display:grid;gap:var(--coaching-space-3);margin:0;padding:0;border:0}
    legend{margin-bottom:var(--coaching-space-3);font-weight:700;color:var(--ui-text)}
    label{display:flex;align-items:center;gap:var(--coaching-space-3);min-height:56px;padding:var(--coaching-space-3) var(--coaching-space-4);border:1px solid var(--ui-border);border-radius:var(--coaching-radius-card);background:var(--ui-surface);color:var(--ui-text);cursor:pointer}
    label.is-selected{border-color:var(--ui-brand);box-shadow:inset 0 0 0 1px var(--ui-brand)}
    input{width:20px;height:20px;accent-color:var(--ui-brand)}
    span{display:grid}
    small{color:var(--ui-text-muted);font-size:var(--coaching-text-sm)}`]
})
export class StudentThemeSettingComponent {
  readonly theme = inject(DarkModeService);
  readonly options: { value: ThemeMode; label: string; hint: string }[] = [
    { value: 'light', label: 'Açık', hint: 'Gündüz ve aydınlık ortamlar için' },
    { value: 'dark', label: 'Koyu', hint: 'Akşam çalışırken gözünü yormaz' },
    { value: 'system', label: 'Sistemi izle', hint: 'Cihazının ayarına göre kendiliğinden değişir' }
  ];
}

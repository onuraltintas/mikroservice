import { ChangeDetectionStrategy, Component, ElementRef, computed, input, signal, viewChild } from '@angular/core';
import { ScoreEstimate, scoreGroup } from '../../../core/services/coaching-score-estimate.service';
import { copyText } from '../copy-text';

let nextId = 0;

/** Which official sites fit a student: ÖSYM exams with YÖK Atlas (YKS), ÖSYM exams without it (MEB-AGS, YDS, YÖKDİL), LGS (MEB). */
export type OfficialGroup = 'YKS' | 'OSYM' | 'LGS';
export interface OfficialLink { name: string; purpose: string; url: string }

const OSYM: OfficialLink[] = [
  { name: 'ÖSYM sınav takvimi', purpose: 'Başvuru, sınav ve sonuç tarihleri', url: 'https://www.osym.gov.tr/TR,8797/takvim.html' },
  { name: 'ÖSYM Aday İşlemleri', purpose: 'Başvuru, sınava giriş belgesi ve tercih', url: 'https://ais.osym.gov.tr' },
  { name: 'ÖSYM Sonuç Açıklama', purpose: 'Sınav sonuçları', url: 'https://sonuc.osym.gov.tr' }
];
const YOK_ATLAS: OfficialLink = { name: 'YÖK Atlas', purpose: 'Programların taban puanı, başarı sırası ve kontenjanı', url: 'https://yokatlas.yok.gov.tr' };
const MEBI: OfficialLink = { name: 'MEBİ', purpose: 'MEB’in ücretsiz sınava hazırlık platformu', url: 'https://mebi.eba.gov.tr' };
const LGS: OfficialLink[] = [
  { name: 'Millî Eğitim Bakanlığı', purpose: 'LGS kılavuzu, duyurular ve sonuçlar', url: 'https://www.meb.gov.tr' },
  { name: 'MEB Ölçme, Değerlendirme ve Sınav Hizmetleri', purpose: 'LGS örnek soruları', url: 'https://odsgm.meb.gov.tr' },
  { name: 'e-Okul', purpose: 'LGS başvurusu ve tercih', url: 'https://eokul.meb.gov.tr' }
];

/** KocApp uyumu (22.11.2026): the official sites for the groups, without duplicates, ÖSYM first. */
export function officialLinks(groups: ReadonlySet<OfficialGroup>): OfficialLink[] {
  const links: OfficialLink[] = [];
  if (groups.has('YKS') || groups.has('OSYM')) links.push(...OSYM);
  if (groups.has('YKS')) links.push(YOK_ATLAS, MEBI);
  if (groups.has('LGS')) links.push(...LGS, ...(groups.has('YKS') ? [] : [MEBI]));
  return links;
}

function groupOf(code: string | null | undefined): OfficialGroup | null {
  if (!code) return null;
  if (code === 'LGS') return 'LGS';
  if (code === 'MEB-AGS' || code === 'YDS' || code === 'YÖKDİL' || code === 'YOKDIL' || code === 'YDS_EN' || code.startsWith('YOKDIL_')
    || code === 'P1' || code.startsWith('P2') || code === 'P3' || code === 'AGS' || code.startsWith('OABT')) return 'OSYM';
  return 'YKS';
}

/** The exam groups a score estimate speaks about: its estimates, its goal and its target. */
export function officialGroups(estimate: ScoreEstimate | null): Set<OfficialGroup> {
  const groups = new Set<OfficialGroup>();
  if (!estimate) return groups;
  for (const item of [...estimate.estimates, ...estimate.notComputed ?? []]) {
    const group = scoreGroup(item); groups.add(group === 'MEB-AGS' ? 'OSYM' : group);
  }
  if ((estimate.languageScores ?? []).length || estimate.targetExam) groups.add('OSYM');
  for (const code of [estimate.goal?.group, estimate.goal?.scoreType, estimate.target?.scoreType]) {
    const group = groupOf(code); if (group) groups.add(group);
  }
  if (estimate.target?.programCode) groups.add('YKS');
  return groups;
}

/**
 * KocApp uyumu (22.11.2026): shortcuts to the official exam and programme sites a coach otherwise keeps in a bookmark list, and
 * the target programme's ÖSYM code. YÖK Atlas has no stable page per programme (its old programme pages refuse direct visits),
 * so the code is shown to copy into YÖK Atlas's "Program Kodu" field.
 */
@Component({
  selector: 'app-official-links',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (code() || links().length) {
      <section class="official" data-testid="official-links" [attr.aria-labelledby]="titleId">
        <p class="official-title" [id]="titleId" role="heading" [attr.aria-level]="level()">Resmi kaynaklar</p>
        @if (code(); as value) {
          <div class="official-code" data-testid="official-program-code">
            <p>YÖK Atlas program kodu <b #codeText>{{ value }}</b></p>
            <button type="button" (click)="copy(value)">Kodu kopyala</button>
            <small>{{ codeHint() }}</small>
            <span class="official-status" role="status">{{ copied() }}</span>
          </div>
        }
        @if (links().length) {
          <ul>
            @for (link of links(); track link.url) {
              <li><a [href]="link.url" target="_blank" rel="noopener noreferrer"><b>{{ link.name }}</b><span class="sr-only"> (yeni sekmede açılır)</span></a><small>{{ link.purpose }}</small></li>
            }
          </ul>
        }
      </section>
    }`,
  styles: [`
    .official { display: grid; gap: 10px; padding: 14px 16px; border: 1px solid var(--ui-border); border-radius: 10px; background: var(--ui-surface); color: var(--ui-text); }
    .official-title { margin: 0; font-size: 16px; font-weight: 800; }
    .official-code { display: grid; grid-template-columns: 1fr auto; gap: 4px 12px; align-items: center; padding-bottom: 10px; border-bottom: 1px solid var(--ui-border); }
    .official-code p { margin: 0; }
    .official-code b { font-variant-numeric: tabular-nums; letter-spacing: .02em; }
    .official-code small, .official-status { grid-column: 1 / -1; }
    .official-status:empty { display: none; }
    button { min-height: 44px; padding: 0 14px; border: 1px solid var(--ui-border-strong); border-radius: 8px; background: var(--ui-surface); color: var(--ui-text); font: inherit; font-weight: 700; cursor: pointer; }
    ul { display: grid; gap: 2px; margin: 0; padding: 0; list-style: none; }
    li { display: grid; gap: 2px; padding: 8px 0; }
    li + li { border-top: 1px solid var(--ui-border); }
    a { display: inline-flex; align-items: center; min-height: 32px; color: var(--ui-brand-text); text-underline-offset: 3px; }
    small { font-size: 14px; color: var(--ui-text-muted); }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
  `]
})
export class OfficialLinksComponent {
  readonly estimate = input<ScoreEstimate | null>(null);
  /** 'staff' speaks with "siz"; the student and the parent with "sen". */
  readonly audience = input<'student' | 'parent' | 'staff'>('student');
  readonly level = input(2);
  readonly links = computed(() => officialLinks(officialGroups(this.estimate())));
  readonly code = computed(() => this.estimate()?.target?.programCode || null);
  // One sentence for everybody: the kod, written into YÖK Atlas's field, opens the programme (no "sen" or "siz" needed).
  readonly codeHint = computed(() => 'Kod YÖK Atlas’ta “Program Kodu” alanına yazılınca bu programın taban puanı, başarı sırası ve kontenjanı görünür.');
  readonly copied = signal('');
  readonly titleId = `official-title-${++nextId}`;
  private readonly codeText = viewChild<ElementRef<HTMLElement>>('codeText');

  async copy(value: string) {
    const result = await copyText(value, this.codeText()?.nativeElement);
    this.copied.set(result === 'copied' ? 'Kod kopyalandı.' : 'Kod seçildi; kopyalamak için Ctrl+C ya da uzun basma kullanılabilir.');
  }
}

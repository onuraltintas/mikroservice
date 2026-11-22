import { TestBed } from '@angular/core/testing';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { OfficialLinksComponent, officialGroups, officialLinks } from './official-links.component';

const base: ScoreEstimate = { estimates: [], target: null, notice: null };
const target = (scoreType: string, programCode: string | null = null) =>
  ({ targetName: 'Ankara Üniversitesi · Hukuk', scoreType, targetScore: 450, targetRank: null, targetPercentile: null,
    scoreGap: null, netGap: null, rankGap: null, reached: false, programCode });

describe('official links (KocApp uyumu)', () => {
  it('picks ÖSYM, YÖK Atlas and MEBİ for YKS and MEB sites for LGS', () => {
    const yks = officialLinks(new Set(['YKS'])).map(x => x.name);
    expect(yks).toEqual(['ÖSYM sınav takvimi', 'ÖSYM Aday İşlemleri', 'ÖSYM Sonuç Açıklama', 'YÖK Atlas', 'MEBİ']);
    const lgs = officialLinks(new Set(['LGS'])).map(x => x.name);
    expect(lgs).toContain('e-Okul'); expect(lgs).toContain('MEBİ'); expect(lgs).not.toContain('YÖK Atlas');
    expect(lgs.some(x => x.startsWith('ÖSYM'))).toBe(false);
    const ags = officialLinks(new Set(['OSYM'])).map(x => x.name);
    expect(ags).toEqual(['ÖSYM sınav takvimi', 'ÖSYM Aday İşlemleri', 'ÖSYM Sonuç Açıklama']);
    expect(officialLinks(new Set())).toEqual([]);
  });

  it('reads the groups from the estimate, the goal and the target', () => {
    expect([...officialGroups(null)]).toEqual([]);
    expect([...officialGroups({ ...base, target: target('LGS') })]).toEqual(['LGS']);
    expect([...officialGroups({ ...base, target: target('P2_MAT') })]).toEqual(['OSYM']);
    expect([...officialGroups({ ...base, target: target('YDS_EN') })]).toEqual(['OSYM']);
    expect([...officialGroups({ ...base, target: target('EA', '101110001') })]).toEqual(['YKS']);
    const estimates = [{ scoreType: 'TYT', group: 'YKS' }] as ScoreEstimate['estimates'];
    expect([...officialGroups({ ...base, estimates })]).toEqual(['YKS']);
  });

  it('shows the programme code with a copy button and opens every site in a new tab', async () => {
    TestBed.configureTestingModule({ imports: [OfficialLinksComponent] });
    const fixture = TestBed.createComponent(OfficialLinksComponent);
    fixture.componentRef.setInput('estimate', { ...base, target: target('EA', '101110001') });
    fixture.componentRef.setInput('audience', 'staff');
    fixture.detectChanges();
    const element: HTMLElement = fixture.nativeElement;
    const code = element.querySelector('[data-testid="official-program-code"]')!;
    expect(code.textContent).toContain('101110001');
    expect(code.textContent).toContain('alanına yazılınca');
    const links = Array.from(element.querySelectorAll('a'));
    expect(links.length).toBe(5);
    expect(links.every(a => a.target === '_blank' && a.rel.includes('noopener'))).toBe(true);
    expect(links.every(a => a.textContent!.includes('yeni sekmede açılır'))).toBe(true);
    const heading = element.querySelector('[role="heading"]')!;
    expect(heading.getAttribute('aria-level')).toBe('2');

    Object.defineProperty(navigator, 'clipboard', { value: { writeText: () => Promise.resolve() }, configurable: true });
    (element.querySelector('button') as HTMLButtonElement).click();
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('[role="status"]')!.textContent).toContain('Kod kopyalandı.');
  });

  it('shows nothing without an exam or a target', () => {
    TestBed.configureTestingModule({ imports: [OfficialLinksComponent] });
    const fixture = TestBed.createComponent(OfficialLinksComponent);
    fixture.componentRef.setInput('estimate', base);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="official-links"]')).toBeNull();
  });
});

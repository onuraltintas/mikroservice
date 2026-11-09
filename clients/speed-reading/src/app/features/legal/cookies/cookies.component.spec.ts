import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of } from 'rxjs';
import { PlatformLegalPagesService } from '../../../core/services/platform-legal-pages.service';
import { CookiesComponent } from './cookies.component';

describe('CookiesComponent', () => {
  let fixture: ComponentFixture<CookiesComponent>;
  let originalCanonical: string | null;

  function canonicalHref(): string | null {
    return document.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;
  }

  beforeEach(async () => {
    let link = document.querySelector('link[rel="canonical"]');
    if (!link) {
      link = document.createElement('link');
      link.setAttribute('rel', 'canonical');
      document.head.appendChild(link);
    }
    originalCanonical = link.getAttribute('href');
    // index.html ships the home page as the default canonical.
    link.setAttribute('href', `${window.location.origin}/`);

    await TestBed.configureTestingModule({
      imports: [CookiesComponent],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        {
          provide: PlatformLegalPagesService,
          useValue: {
            getPage: jasmine.createSpy('getPage').and.returnValue(of({
              slug: 'cookies', title: 'Çerez Politikası', content: '<p>Çerez metni</p>',
              isPublished: true, version: 1, createdAt: '2026-01-01T00:00:00Z'
            }))
          }
        }
      ]
    }).compileComponents();
    fixture = TestBed.createComponent(CookiesComponent);
    fixture.detectChanges();
  });

  afterEach(() => {
    const link = document.querySelector('link[rel="canonical"]');
    if (originalCanonical === null) {
      link?.remove();
    } else {
      link?.setAttribute('href', originalCanonical);
    }
  });

  it('points the canonical link at its own /legal/cookies URL, not the home page', () => {
    expect(canonicalHref()).toBe(`${window.location.origin}/legal/cookies`);
    expect(canonicalHref()).not.toBe(`${window.location.origin}/`);
  });

  it('loads the public cookie policy from the shared legal-pages slug', () => {
    const legalPages = TestBed.inject(PlatformLegalPagesService) as unknown as { getPage: jasmine.Spy };
    expect(legalPages.getPage).toHaveBeenCalledOnceWith('cookies');
    expect(fixture.nativeElement.textContent).toContain('Çerez metni');
  });
});

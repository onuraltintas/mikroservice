import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { HeroSectionComponent } from './hero-section';

@Component({
  standalone: true,
  imports: [HeroSectionComponent],
  template: `
    <div class="public-cms-home">
      <app-hero-section></app-hero-section>
    </div>
  `
})
class PublicCmsDesignHostComponent {}

describe('Public CMS design system', () => {
  it('uses the rounded, inset hero surface shared with the Coaching CMS', async () => {
    await TestBed.configureTestingModule({
      imports: [PublicCmsDesignHostComponent],
      providers: [provideRouter([])]
    }).compileComponents();

    const fixture = TestBed.createComponent(PublicCmsDesignHostComponent);
    fixture.detectChanges();

    const heroSurface = fixture.nativeElement.querySelector('.hero-section .container') as HTMLElement;
    const heroStyle = getComputedStyle(heroSurface);
    const contentGrid = fixture.nativeElement.querySelector('.hero-section .content-grid') as HTMLElement;
    const expectedColumns = window.matchMedia('(max-width: 64rem)').matches ? 1 : 2;

    expect(parseFloat(heroStyle.borderRadius)).toBeGreaterThan(20);
    expect(heroStyle.backgroundImage).toContain('linear-gradient');
    expect(getComputedStyle(contentGrid).gridTemplateColumns.trim().split(/\s+/)).toHaveSize(expectedColumns);
  });
});

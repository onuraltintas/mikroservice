import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, FormArray, Validators } from '@angular/forms';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatOptionModule } from '@angular/material/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { FormsModule } from '@angular/forms';
import { BaseComponent } from '../../../../core/components/base.component';
import { CmsService } from '../../../../core/services/cms.service';
import { ContentBlock } from '../../../../core/models/cms.models';
import { finalize } from 'rxjs/operators';

@Component({
  selector: 'app-home-page-editor',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatOptionModule
  ],
  templateUrl: './home-page-editor.html',
  styleUrl: './home-page-editor.scss'
})
export class HomePageEditorComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private cmsService = inject(CmsService);

  homePageForm!: FormGroup;

  iconOptions = [
    { value: 'speed', label: 'Hız' },
    { value: 'psychology', label: 'Psikoloji' },
    { value: 'analytics', label: 'Analitik' },
    { value: 'school', label: 'Okul' },
    { value: 'emoji_events', label: 'Ödül' },
    { value: 'favorite', label: 'Kalp' },
    { value: 'star', label: 'Yıldız' },
    { value: 'trending_up', label: 'Artış' }
  ];

  statIconOptions = [
    { value: 'people', label: 'İnsanlar' },
    { value: 'menu_book', label: 'Kitap' },
    { value: 'trending_up', label: 'Artış' },
    { value: 'star', label: 'Yıldız' },
    { value: 'emoji_events', label: 'Ödül' },
    { value: 'school', label: 'Okul' },
    { value: 'timer', label: 'Zamanlayıcı' },
    { value: 'speed', label: 'Hız' }
  ];

  statFormatOptions = [
    { value: 'number', label: 'Sayı (1234)' },
    { value: 'number_plus', label: 'Sayı+ (1,234+)' },
    { value: 'millions', label: 'Milyon (1.2M+)' },
    { value: 'percent', label: 'Yüzde (%95)' }
  ];

  faqCategoryOptions = ['Genel', 'Teknik', 'Fiyatlandırma', 'Sertifika', 'Destek', 'Diğer'];

  constructor() {
    super();
    this.loading.set(true);
    this.initForm();
  }

  ngOnInit() {
    this.loadContent();
  }

  private initForm() {
    this.homePageForm = this.fb.group({
      hero: this.fb.group({
        title: ['', Validators.required],
        subtitle: ['', Validators.required]
      }),
      features: this.fb.group({
        list: this.fb.array([])
      }),
      pricing: this.fb.group({
        title: ['', Validators.required],
        subtitle: ['', Validators.required]
      }),
      stats: this.fb.array([]),
      faqs: this.fb.array([]),
      cta: this.fb.group({
        title: [''],
        subtitle: [''],
        buttonText: [''],
        smallText: ['']
      }),
      blog: this.fb.group({
        sectionTitle: [''],
        sectionSubtitle: ['']
      }),
      newsletter: this.fb.group({
        title: [''],
        description: [''],
        benefits: ['']
      })
    });
  }

  // Features
  get featuresList(): FormArray { return this.homePageForm.get('features.list') as FormArray; }

  createFeature(icon = 'speed', title = '', description = ''): FormGroup {
    return this.fb.group({
      icon: [icon, Validators.required],
      title: [title, Validators.required],
      description: [description, Validators.required]
    });
  }

  addFeature() { this.featuresList.push(this.createFeature()); }
  removeFeature(i: number) { this.featuresList.removeAt(i); }

  // Stats
  get statsList(): FormArray { return this.homePageForm.get('stats') as FormArray; }

  createStat(data: any = {}): FormGroup {
    return this.fb.group({
      icon: [data.icon || 'people', Validators.required],
      label: [data.label || '', Validators.required],
      target: [data.target || 0, [Validators.required, Validators.min(0)]],
      format: [data.format || 'number', Validators.required]
    });
  }

  addStat() { this.statsList.push(this.createStat()); }
  removeStat(i: number) { this.statsList.removeAt(i); }

  // FAQ
  get faqList(): FormArray { return this.homePageForm.get('faqs') as FormArray; }

  createFaq(data: any = {}): FormGroup {
    return this.fb.group({
      question: [data.question || '', Validators.required],
      answer: [data.answer || '', Validators.required],
      category: [data.category || 'Genel', Validators.required]
    });
  }

  addFaq() { this.faqList.push(this.createFaq()); }
  removeFaq(i: number) { this.faqList.removeAt(i); }

  private loadContent() {
    this.loading.set(true);
    this.cmsService.getLandingContentForAdmin().subscribe({
      next: (blocks: ContentBlock[]) => {
        const map: Record<string, string> = {};
        blocks.forEach(b => map[b.key] = b.value);

        // Hero
        this.homePageForm.get('hero')?.patchValue({
          title: map['hero_title'] || '',
          subtitle: map['hero_subtitle'] || ''
        });

        // Features
        if (map['features_list']) {
          try {
            const features = JSON.parse(map['features_list']);
            this.featuresList.clear();
            features.forEach((f: any) => this.featuresList.push(this.createFeature(f.icon, f.title, f.description)));
          } catch { console.warn('Failed to parse features_list'); }
        }

        // Pricing (sadece başlık/alt başlık)
        this.homePageForm.get('pricing')?.patchValue({
          title: map['pricing_title'] || '',
          subtitle: map['pricing_subtitle'] || ''
        });

        // Stats
        if (map['stats_data']) {
          try {
            const stats = JSON.parse(map['stats_data']);
            this.statsList.clear();
            stats.forEach((s: any) => this.statsList.push(this.createStat(s)));
          } catch { console.warn('Failed to parse stats_data'); }
        }

        // FAQs
        if (map['faq_list']) {
          try {
            const faqs = JSON.parse(map['faq_list']);
            this.faqList.clear();
            faqs.forEach((f: any) => this.faqList.push(this.createFaq(f)));
          } catch { console.warn('Failed to parse faq_list'); }
        }

        // CTA
        this.homePageForm.get('cta')?.patchValue({
          title: map['cta_title'] || '',
          subtitle: map['cta_subtitle'] || '',
          buttonText: map['cta_button_text'] || '',
          smallText: map['cta_small_text'] || ''
        });

        // Blog Section
        this.homePageForm.get('blog')?.patchValue({
          sectionTitle: map['blog_section_title'] || '',
          sectionSubtitle: map['blog_section_subtitle'] || ''
        });

        // Newsletter
        let benefitsText = '';
        if (map['newsletter_benefits']) {
          try {
            const arr = JSON.parse(map['newsletter_benefits']);
            if (Array.isArray(arr)) benefitsText = arr.join('\n');
          } catch { /* ignore */ }
        }
        this.homePageForm.get('newsletter')?.patchValue({
          title: map['newsletter_title'] || '',
          description: map['newsletter_description'] || '',
          benefits: benefitsText
        });

        this.loading.set(false);
      },
      error: (err: any) => {
        this.handleError(err, 'İçerik yüklenirken hata oluştu');
        this.loading.set(false);
      }
    });
  }

  onSave() {
    const heroGroup = this.homePageForm.get('hero');
    if (heroGroup?.invalid) {
      this.toaster.warning('Lütfen Hero section alanlarını doldurun', 3000);
      heroGroup.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    const v = this.homePageForm.value;

    const blocks: Record<string, string> = {
      hero_title: v.hero.title,
      hero_subtitle: v.hero.subtitle,
      features_list: JSON.stringify(v.features.list),
      pricing_title: v.pricing.title,
      pricing_subtitle: v.pricing.subtitle,
      stats_data: JSON.stringify(v.stats),
      faq_list: JSON.stringify(v.faqs),
      cta_title: v.cta.title,
      cta_subtitle: v.cta.subtitle,
      cta_button_text: v.cta.buttonText,
      cta_small_text: v.cta.smallText,
      blog_section_title: v.blog.sectionTitle,
      blog_section_subtitle: v.blog.sectionSubtitle,
      newsletter_title: v.newsletter.title,
      newsletter_description: v.newsletter.description,
      newsletter_benefits: JSON.stringify(
        (v.newsletter.benefits || '').split('\n').map((b: string) => b.trim()).filter((b: string) => b)
      )
    };

    this.cmsService.updateLandingContent(blocks)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: () => this.toaster.success('Ana sayfa güncellendi!', 3000),
        error: (err: any) => this.handleError(err, 'Kaydetme hatası')
      });
  }

  onCancel() {
    this.homePageForm.reset();
    this.featuresList.clear();
    this.statsList.clear();
    this.faqList.clear();
    this.loadContent();
  }

  trackByFn(index: number): number { return index; }
}

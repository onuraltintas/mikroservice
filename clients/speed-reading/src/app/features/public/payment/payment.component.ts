import { ChangeDetectorRef, Component, ElementRef, OnInit, ViewChild, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { PublicCmsService, PageDto } from '../../../core/services/public-cms.service';
import { SeoService } from '../../../core/services/seo.service';
import { SubscriptionService, SubscriptionPlan } from '../../../core/services/subscription.service';
import { PaymentService } from '../../../core/services/payment.service';
import { NavbarComponent } from '../../../shared/components/navbar/navbar';
import { FooterComponent } from '../../../shared/components/footer/footer';

@Component({
    selector: 'app-payment',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        RouterModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatProgressSpinnerModule,
        NavbarComponent,
        FooterComponent,
    ],
    templateUrl: './payment.component.html',
    styleUrl: './payment.component.scss'
})
export class PaymentComponent implements OnInit {
    private cmsService          = inject(PublicCmsService);
    private seoService          = inject(SeoService);
    private subscriptionService = inject(SubscriptionService);
    private paymentService      = inject(PaymentService);
    private sanitizer           = inject(DomSanitizer);
    private changeDetector      = inject(ChangeDetectorRef);

    @ViewChild('checkoutHost') private checkoutHost?: ElementRef<HTMLElement>;

    plans:        SubscriptionPlan[] = [];
    pageContent:  PageDto | null     = null;
    selectedPlan: SubscriptionPlan | null = null;

    // Checkout form content — Iyzico'dan gelen HTML (iframe yaklaşımı)
    checkoutFormContent: string | null = null;

    loading        = signal(true);
    plansLoading   = signal(true);
    checkoutLoading = signal(false);
    checkoutError  = signal<string | null>(null);
    // Ödeme devre dışıysa true — 503 geldiğinde set edilir
    paymentDisabled = signal(false);

    buyerDetails = {
        phoneNumber: '',
        identityNumber: '',
        billingAddress: '',
        city: '',
        zipCode: ''
    };

    readonly billingPeriodLabel: Record<string, string> = {
        Monthly:  '/ay',
        Quarterly: '/3 ay',
        Annual:   '/yıl',
        Lifetime: 'tek seferlik'
    };

    ngOnInit() {
        this.loadPage();
        this.loadPlans();
    }

    private loadPage() {
        this.cmsService.getPage('odeme').subscribe({
            next: (page) => {
                if (page.isPublished) {
                    this.pageContent = page;
                    this.seoService.updateTags({
                        title:       page.seoSettings?.metaTitle || 'Abonelik Planları',
                        description: page.seoSettings?.metaDescription || 'Size uygun abonelik planını seçin',
                        keywords:    page.seoSettings?.metaKeywords || undefined,
                        url:         `${window.location.origin}/odeme`
                    });
                    this.seoService.setNoIndex(page.seoSettings?.noIndex || false);
                }
                this.loading.set(false);
            },
            error: () => this.loading.set(false)
        });
    }

    private loadPlans() {
        this.subscriptionService.getPublicPlans().subscribe({
            next:  (plans) => { this.plans = plans; this.plansLoading.set(false); },
            error: ()      => this.plansLoading.set(false)
        });
    }

    selectPlan(plan: SubscriptionPlan) {
        this.selectedPlan       = plan;
        this.checkoutFormContent = null;
        this.checkoutError.set(null);
    }

    checkout() {
        if (!this.selectedPlan) return;

        if (Object.values(this.buyerDetails).some(value => !value.trim())) {
            this.checkoutError.set('Ödeme için telefon, T.C. kimlik ve fatura adresi bilgileri gereklidir.');
            return;
        }

        this.checkoutLoading.set(true);
        this.checkoutError.set(null);

        this.paymentService.initializePayment(this.selectedPlan.id, this.buyerDetails).subscribe({
            next: (res) => {
                this.checkoutLoading.set(false);

                // Tercih 1: redirect URL varsa kullanıcıyı yönlendir
                if (res.paymentPageUrl) {
                    window.location.href = res.paymentPageUrl;
                    return;
                }

                // Tercih 2: checkout form HTML varsa embed et
                if (res.checkoutFormContent) {
                    this.checkoutFormContent = res.checkoutFormContent;
                    this.changeDetector.detectChanges();
                    this.mountCheckoutForm(res.checkoutFormContent);
                    return;
                }

                this.checkoutError.set('Ödeme formu alınamadı. Lütfen tekrar deneyin.');
            },
            error: (err) => {
                this.checkoutLoading.set(false);
                if (err?.status === 503) {
                    this.paymentDisabled.set(true);
                } else {
                    const msg = err?.error?.message || 'Ödeme başlatılamadı. Lütfen daha sonra tekrar deneyin.';
                    this.checkoutError.set(msg);
                }
            }
        });
    }

    cancelCheckout() {
        this.clearCheckoutForm();
        this.checkoutFormContent = null;
        this.checkoutError.set(null);
        this.selectedPlan = null;
    }

    isPopular(plan: SubscriptionPlan): boolean {
        return (plan.includedProductSlugs?.length ?? 0) > 0;
    }

    formatPrice(plan: SubscriptionPlan): string {
        if (plan.price === 0) return 'Ücretsiz';
        return `${plan.price.toLocaleString('tr-TR')} TL`;
    }

    getPeriodLabel(plan: SubscriptionPlan): string {
        return this.billingPeriodLabel[plan.billingPeriod] ?? '';
    }

    get safeContent(): SafeHtml {
        return this.sanitizer.bypassSecurityTrustHtml(this.pageContent?.content ?? '');
    }

    private mountCheckoutForm(content: string) {
        const host = this.checkoutHost?.nativeElement;
        if (!host) return;

        host.replaceChildren();
        const template = document.createElement('template');
        template.innerHTML = content;

        for (const node of Array.from(template.content.childNodes)) {
            if (node instanceof HTMLScriptElement) {
                const script = document.createElement('script');
                for (const attribute of Array.from(node.attributes)) {
                    script.setAttribute(attribute.name, attribute.value);
                }
                script.text = node.textContent ?? '';
                host.appendChild(script);
            } else {
                host.appendChild(node.cloneNode(true));
            }
        }
    }

    private clearCheckoutForm() {
        this.checkoutHost?.nativeElement.replaceChildren();
    }
}

import { Directive, ElementRef, OnInit, OnDestroy, inject } from '@angular/core';

@Directive({
    selector: '[appScrollAnimation]',
    standalone: true
})
export class ScrollAnimationDirective implements OnInit, OnDestroy {
    private el = inject(ElementRef);
    private observer: IntersectionObserver | null = null;

    ngOnInit(): void {
        this.setupIntersectionObserver();
    }

    ngOnDestroy(): void {
        if (this.observer) {
            this.observer.disconnect();
        }
    }

    private setupIntersectionObserver(): void {
        const options: IntersectionObserverInit = {
            root: null,
            rootMargin: '0px',
            threshold: 0.1
        };

        this.observer = new IntersectionObserver((entries) => {
            entries.forEach(entry => {
                if (entry.isIntersecting) {
                    entry.target.classList.add('animate-in');
                }
            });
        }, options);

        this.el.nativeElement.classList.add('scroll-animate');
        this.observer.observe(this.el.nativeElement);
    }
}

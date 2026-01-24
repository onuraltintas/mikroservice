import { Component, computed, inject, signal } from '@angular/core';
import { MatSidenavModule } from '@angular/material/sidenav';
import { RouterOutlet } from '@angular/router';
import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs/operators';
import { SidenavComponent } from '../components/sidenav/sidenav';
import { ToolbarComponent } from '../components/toolbar/toolbar';

@Component({
    selector: 'app-main-layout',
    standalone: true,
    imports: [MatSidenavModule, RouterOutlet, SidenavComponent, ToolbarComponent],
    templateUrl: './main-layout.html',
    styleUrl: './main-layout.scss'
})
export class MainLayoutComponent {

    private breakpointObserver = inject(BreakpointObserver);

    // Responsive Signal: True if screen is Handset (Mobile)
    isHandset = toSignal(
        this.breakpointObserver.observe(Breakpoints.Handset)
            .pipe(map(result => result.matches)),
        { initialValue: false }
    );

    // Sidenav state
    // On desktop, default is open. On mobile, default is closed.
    // We use a separate signal for manual toggle, and compute the actual state based on screen size
    private _manualToggle = signal<boolean | null>(null);

    isSidenavOpen = computed(() => {
        const manual = this._manualToggle();
        const isMobile = this.isHandset();

        // If manually toggled, respect user choice
        if (manual !== null) return manual;

        // Otherwise default based on screen size: Closed on mobile, Open on desktop
        return !isMobile;
    });

    sidenavMode = computed(() => this.isHandset() ? 'over' : 'side');

    toggleSidenav() {
        // Current logical state
        const currentState = this.isSidenavOpen();
        this._manualToggle.set(!currentState);
    }
}

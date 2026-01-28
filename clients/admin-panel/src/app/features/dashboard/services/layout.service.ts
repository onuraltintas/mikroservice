import { Injectable, signal } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class LayoutService {
    // State for Desktop Sidebar (Collapsed vs Expanded)
    isSidebarCollapsed = signal<boolean>(false);

    // State for Mobile Sidebar (Open vs Closed)
    isMobileSidebarOpen = signal<boolean>(false);

    toggleSidebar() {
        this.isSidebarCollapsed.update(v => !v);
    }

    toggleMobileSidebar() {
        this.isMobileSidebarOpen.update(v => !v);
    }

    closeMobileSidebar() {
        this.isMobileSidebarOpen.set(false);
    }
}

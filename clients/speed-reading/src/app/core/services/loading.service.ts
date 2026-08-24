import { Injectable, signal, computed } from '@angular/core';

/**
 * Service for managing global loading state
 * Uses Angular signals for reactive state management
 */
@Injectable({
    providedIn: 'root'
})
export class LoadingService {
    // Track number of active requests
    private activeRequests = signal<number>(0);

    // Computed signal - true if any requests are active
    public isLoading = computed(() => this.activeRequests() > 0);

    /**
     * Increment active request counter
     */
    show(): void {
        this.activeRequests.update(count => count + 1);
    }

    /**
     * Decrement active request counter
     */
    hide(): void {
        this.activeRequests.update(count => Math.max(0, count - 1));
    }

    /**
     * Reset all active requests
     */
    reset(): void {
        this.activeRequests.set(0);
    }

    /**
     * Get current loading state (for non-signal contexts)
     */
    get loading(): boolean {
        return this.isLoading();
    }
}

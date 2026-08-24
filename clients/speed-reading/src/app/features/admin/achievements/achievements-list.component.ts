import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { takeUntil, finalize } from 'rxjs/operators';
import { AdminAchievementService, Achievement, AchievementListResponse, AchievementStats } from '../../../core/services/admin-achievement.service';
import { AchievementDialogComponent } from './achievement-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
    selector: 'app-achievements-list',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatTableModule,
        MatButtonModule,
        MatIconModule,
        MatCardModule,
        MatChipsModule,
        MatDialogModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatPaginatorModule,
        MatTooltipModule,
        MatProgressSpinnerModule,
        MatSlideToggleModule,
        MatSortModule
    ],
    templateUrl: './achievements-list.component.html',
    styleUrls: ['./achievements-list.component.scss']
})
export class AchievementsListComponent extends BaseComponent implements OnInit {
    private achievementService = inject(AdminAchievementService);
    private dialog = inject(MatDialog);

    achievements: Achievement[] = [];
    displayedColumns = ['iconEmoji', 'name', 'category', 'tier', 'xpReward', 'isActive', 'unlockedByUsersCount', 'actions'];
    categories: string[] = [];
    tiers: string[] = [];

    // Stats
    stats: AchievementStats = {
        totalCount: 0,
        bronzeCount: 0,
        silverCount: 0,
        goldCount: 0,
        diamondCount: 0,
        activeCount: 0,
        inactiveCount: 0,
        categoryCounts: {}
    };

    // Pagination
    pageNumber = 1;
    pageSize = 20;
    totalCount = 0;

    // Sorting
    sortBy = 'SortOrder';
    sortDescending = false;

    filters = {
        search: '',
        category: '',
        tier: '',
        isActive: undefined as boolean | undefined
    };

    ngOnInit() {
        this.loadCategories();
        this.loadTiers();
        this.loadStats();
        this.loadAchievements();
    }

    loadStats() {
        this.achievementService.getStats()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (stats) => this.stats = stats,
                error: (error) => console.error('Error loading stats:', error)
            });
    }

    loadCategories() {
        this.achievementService.getCategories()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (categories) => this.categories = categories,
                error: (error) => console.error('Error loading categories:', error)
            });
    }

    loadTiers() {
        this.achievementService.getTiers()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (tiers) => this.tiers = tiers,
                error: (error) => console.error('Error loading tiers:', error)
            });
    }

    loadAchievements() {
        this.loading.set(true);
        this.achievementService.getAchievements(
            this.pageNumber,
            this.pageSize,
            this.filters.search || undefined,
            this.filters.category || undefined,
            this.filters.tier || undefined,
            this.filters.isActive,
            this.sortBy,
            this.sortDescending
        )
            .pipe(
                takeUntil(this.destroy$),
                finalize(() => this.loading.set(false))
            )
            .subscribe({
                next: (result: AchievementListResponse) => {
                    this.achievements = result.items;
                    this.totalCount = result.totalCount;
                },
                error: (error) => {
                    this.handleError(error, 'Başarımlar yüklenirken hata oluştu');
                }
            });
    }

    onFilterChange() {
        this.pageNumber = 1;
        this.loadAchievements();
    }

    onPageChange(event: PageEvent) {
        this.pageNumber = event.pageIndex + 1;
        this.pageSize = event.pageSize;
        this.loadAchievements();
    }

    onSortChange(sort: Sort) {
        this.sortBy = sort.active || 'SortOrder';
        this.sortDescending = sort.direction === 'desc';
        this.loadAchievements();
    }

    clearFilters() {
        this.filters = {
            search: '',
            category: '',
            tier: '',
            isActive: undefined
        };
        this.pageNumber = 1;
        this.loadAchievements();
    }

    hasActiveFilters(): boolean {
        return !!(
            this.filters.search ||
            this.filters.category ||
            this.filters.tier ||
            this.filters.isActive !== undefined
        );
    }

    openAchievementDialog(achievement?: Achievement) {
        const dialogRef = this.dialog.open(AchievementDialogComponent, {
            width: '800px',
            maxHeight: '95vh',
            data: { achievement, categories: this.categories, tiers: this.tiers }
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result) {
                this.loadAchievements();
            }
        });
    }

    async deleteAchievement(achievement: Achievement) {
        const confirmed = await this.confirm(
            `"${achievement.name}" başarımını silmek istediğinizden emin misiniz?\n\n` +
            `⚠️ UYARI: Bu işlem geri alınamaz!\n` +
            `Bu başarımı kazanmış ${achievement.unlockedByUsersCount} kullanıcının kaydı da silinecektir.`
        );

        if (confirmed) {
            this.achievementService.deleteAchievement(achievement.id)
                .pipe(takeUntil(this.destroy$))
                .subscribe({
                    next: () => {
                        this.handleSuccess('Başarım kalıcı olarak silindi');
                        this.loadAchievements();
                    },
                    error: (error) => {
                        this.handleError(error, 'Başarım silinirken hata oluştu');
                    }
                });
        }
    }

    getTierColor(tier: string): string {
        switch (tier) {
            case 'Bronze': return '#cd7f32';
            case 'Silver': return '#c0c0c0';
            case 'Gold': return '#ffd700';
            case 'Diamond': return '#b9f2ff';
            default: return '#9e9e9e';
        }
    }

    getCategoryIcon(category: string): string {
        switch (category) {
            case 'Streak': return 'local_fire_department';
            case 'Reading': return 'menu_book';
            case 'Progress': return 'trending_up';
            case 'Exercise': return 'fitness_center';
            case 'RSVP': return 'speed';
            case 'Quiz': return 'quiz';
            case 'Vocabulary': return 'translate';
            case 'Level': return 'star';
            case 'FirstTime': return 'celebration';
            case 'Special': return 'auto_awesome';
            default: return 'emoji_events';
        }
    }

    getTierCount(tier: string): number {
        return this.achievements.filter(a => a.tier === tier).length;
    }
}

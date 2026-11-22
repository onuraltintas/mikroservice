import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { RouterModule } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { IdentityService } from '../../../../core/services/identity.service';
import { AuthService, hasRole } from '../../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../../core/auth/permissions';
import { BillingAdminService, BillingAdminSubscription, BillingOverview } from '../../../../core/services/billing-admin.service';
import { CoachingAdminOverview, CoachingAdminService } from '../../../../core/services/coaching-admin.service';
import { AdminPlatformUsageAnalytics, SpeedReadingAdminService } from '../../../../core/services/speed-reading-admin.service';
import { SupportAdminService } from '../../../../core/services/support-admin.service';
import { CourseManagementAdminOverview, CourseManagementAdminService } from '../../../course-management/course-management-admin.service';
import { AdminBarsComponent, AdminMeterComponent, AdminPart, AdminPoint, AdminRingComponent, AdminStatComponent, AdminTone, AdminTrendComponent } from '../../../../shared/components/admin-charts';

interface UserSummary { totalUsers: number; activeUsers: number; inactiveUsers: number; }
interface WaitingItem { label: string; value: number | null; tone: AdminTone; route: string; hint: string; }

/**
 * Yönetim paneli ana sayfası (canlı tasarım, 21.11.2026): bekleyen işler önce, sonra her ürünün kısa özeti ve grafikleri.
 * Her bölüm yalnız yöneticinin yetkisi varsa yüklenir ve görünür; bir bölüm yüklenemezse yalnız o bölüm bunu söyler.
 */
@Component({
  selector: 'app-dashboard-home',
  standalone: true,
  imports: [RouterModule, AdminStatComponent, AdminBarsComponent, AdminTrendComponent, AdminRingComponent, AdminMeterComponent],
  templateUrl: './dashboard-home.html',
  styleUrl: './dashboard-home.scss',
})
export class DashboardHomeComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  private readonly auth = inject(AuthService);
  private readonly billing = inject(BillingAdminService);
  private readonly coaching = inject(CoachingAdminService);
  private readonly speedReading = inject(SpeedReadingAdminService);
  private readonly support = inject(SupportAdminService);
  private readonly maxkampus = inject(CourseManagementAdminService);
  private readonly platformId = inject(PLATFORM_ID);

  readonly permissions = ADMIN_PERMISSIONS;
  can = (permission: string) => this.auth.hasPermission(permission);
  readonly isSystemAdmin = computed(() => hasRole(this.auth.userProfile(), 'SystemAdmin'));
  readonly firstName = computed(() => this.auth.userProfile()?.firstName ?? '');

  readonly loading = signal(true);
  readonly users = signal<UserSummary | null>(null);
  readonly roleCount = signal<number | null>(null);
  readonly permissionCount = signal<number | null>(null);
  readonly billingOverview = signal<BillingOverview | null>(null);
  readonly openSupport = signal<number | null>(null);
  readonly coachingOverview = signal<CoachingAdminOverview | null>(null);
  readonly usage = signal<AdminPlatformUsageAnalytics | null>(null);
  readonly maxkampusOverview = signal<CourseManagementAdminOverview | null>(null);
  readonly failed = signal<string[]>([]);

  readonly greeting = computed(() => {
    const hour = new Date().getHours();
    const part = hour < 6 ? 'İyi geceler' : hour < 12 ? 'Günaydın' : hour < 18 ? 'İyi günler' : 'İyi akşamlar';
    return this.firstName() ? `${part}, ${this.firstName()}` : part;
  });
  readonly today = new Intl.DateTimeFormat('tr-TR', { weekday: 'long', day: 'numeric', month: 'long' }).format(new Date());

  /** Bekleyen işler: yalnız yöneticinin görebildiği kutular. */
  readonly waiting = computed<WaitingItem[]>(() => {
    const items: WaitingItem[] = [];
    const b = this.billingOverview();
    if (this.isSystemAdmin()) {
      items.push({ label: 'Onay bekleyen ödeme', value: b?.waitingOrders ?? null, tone: 'honey', route: '/dashboard/billing/orders', hint: 'Siparişler' });
      items.push({ label: 'Bildirilen havale', value: b?.declaredOrders ?? null, tone: 'orange', route: '/dashboard/billing/orders', hint: 'Eşleştir ve onayla' });
      items.push({ label: 'Eşleşmeyen hesap hareketi', value: b?.unmatchedTransactions ?? null, tone: 'danger', route: '/dashboard/billing/statements', hint: 'Hesap hareketleri' });
      items.push({ label: 'Cayma talebi', value: b?.withdrawalRequests ?? null, tone: 'violet', route: '/dashboard/billing/withdrawals', hint: 'Yanıt bekliyor' });
    }
    if (this.can(ADMIN_PERMISSIONS.supportView)) {
      items.push({ label: 'Açık destek talebi', value: this.openSupport(), tone: 'info', route: '/dashboard/notifications/support', hint: 'Destek kutusu' });
    }
    return items;
  });
  readonly waitingTotal = computed(() => this.waiting().reduce((sum, item) => sum + (item.value ?? 0), 0));

  readonly userParts = computed<AdminPart[]>(() => {
    const u = this.users();
    return u ? [{ label: 'Etkin', value: u.activeUsers, tone: 'success' }, { label: 'Pasif', value: u.inactiveUsers, tone: 'honey' }] : [];
  });

  readonly activeTrend = computed<AdminPoint[]>(() => this.series(this.usage()?.dailyActiveUsers, 30));
  readonly activityBars = computed<AdminPoint[]>(() => this.series(this.usage()?.activityVolume, 14));

  readonly subscriptionBars = computed<AdminPoint[]>(() => {
    const b = this.billingOverview();
    return b ? [{ label: 'Geçen ay', value: b.approvedLastMonth }, { label: 'Bu ay', value: b.approvedThisMonth }] : [];
  });

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) void this.load();
    else this.loading.set(false);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set([]);
    const failed: string[] = [];
    const run = async <T>(allowed: boolean, name: string, call: () => Promise<T>, set: (value: T) => void) => {
      if (!allowed) return;
      try { set(await call()); } catch { failed.push(name); }
    };
    const monthAgo = new Date(Date.now() - 30 * 86400000).toISOString();
    await Promise.all([
      run(this.can(ADMIN_PERMISSIONS.usersView), 'Kullanıcılar', () => firstValueFrom(this.identity.getUserSummary()), v => this.users.set(v)),
      run(this.can(ADMIN_PERMISSIONS.rolesView), 'Roller', () => firstValueFrom(this.identity.getAllRoles()),
        v => this.roleCount.set((v as { isDeleted?: boolean }[] ?? []).filter(r => !r.isDeleted).length)),
      run(this.can(ADMIN_PERMISSIONS.permissionView), 'İzinler', () => firstValueFrom(this.identity.getPermissions()),
        v => this.permissionCount.set((v as { isDeleted?: boolean }[] ?? []).filter(p => !p.isDeleted).length)),
      run(this.isSystemAdmin(), 'Abonelik ve ödeme', () => firstValueFrom(this.billing.getOverview('')), v => this.billingOverview.set(v)),
      run(this.can(ADMIN_PERMISSIONS.supportView), 'Destek', () => firstValueFrom(this.support.getAll(1, 1, false)), v => this.openSupport.set(v?.totalCount ?? 0)),
      run(this.can(ADMIN_PERMISSIONS.coachingView), 'Koçluk', () => firstValueFrom(this.coaching.getOverview(5)), v => this.coachingOverview.set(v)),
      run(this.can(ADMIN_PERMISSIONS.speedReadingPlatformAnalytics), 'Hızlı Okuma kullanımı',
        () => firstValueFrom(this.speedReading.getPlatformUsage(monthAgo, new Date().toISOString())), v => this.usage.set(v)),
      run(this.isSystemAdmin(), 'maxkampüs', () => firstValueFrom(this.maxkampus.getOverview()), v => this.maxkampusOverview.set(v))
    ]);
    this.failed.set(failed);
    this.loading.set(false);
  }

  subscriptionName(item: BillingAdminSubscription): string {
    return [item.accountName, item.subscription?.planName].filter(Boolean).join(', ') || 'Abonelik';
  }

  subscriptionEnd(item: BillingAdminSubscription): string {
    const date = item.subscription?.endsAt ? new Date(item.subscription.endsAt) : null;
    return date && !Number.isNaN(date.getTime()) ? new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long' }).format(date) : '';
  }

  share(part: number | undefined, whole: number | undefined): number {
    return whole ? Math.round((part ?? 0) / whole * 100) : 0;
  }

  /** Chart rows of the analytics service → last n points with a short Turkish day label. */
  private series(rows: { name: string; series: { value: number }[] }[] | undefined, take: number): AdminPoint[] {
    const day = new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short' });
    return (rows ?? []).slice(-take).map(row => {
      const date = new Date(row.name);
      return { label: Number.isNaN(date.getTime()) ? row.name : day.format(date), value: row.series?.[0]?.value ?? 0 };
    });
  }
}

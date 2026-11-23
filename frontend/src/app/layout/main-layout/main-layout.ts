import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { ROLE_LABELS } from '../../core/models/auth.model';
import { CartStore } from '../../core/services/cart.store';
import { LoadingService } from '../../core/services/loading.service';
import { NAV_ITEMS } from '../nav-menu';

/**
 * Khung chính: header (thương hiệu, giỏ hàng, tài khoản) + sidebar lọc theo quyền + vùng nội dung.
 * Sidebar dùng chung `AuthStore.canAccess` với guard nên người dùng không bao giờ thấy mục mình không vào được.
 */
@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './main-layout.html',
})
export class MainLayout {
  private readonly authStore = inject(AuthStore);

  protected readonly cartStore = inject(CartStore);
  protected readonly loading = inject(LoadingService);

  protected readonly account = this.authStore.account;
  protected readonly isAuthenticated = this.authStore.isAuthenticated;
  protected readonly displayName = this.authStore.displayName;

  protected readonly sidebarOpen = signal(false);

  /** Nhãn vai trò bằng tiếng Việt để hiển thị cạnh tên đăng nhập. */
  protected readonly roleText = computed(() => {
    const roles = this.account()?.roles ?? [];

    return roles.length === 0 ? '' : roles.map((role) => ROLE_LABELS[role] ?? role).join(', ');
  });

  protected readonly navItems = computed(() =>
    NAV_ITEMS.filter((item) =>
      this.authStore.canAccess({
        roles: item.roles,
        permissions: item.permissions,
        accessMode: item.accessMode,
      }),
    ),
  );

  constructor() {
    // Nạp giỏ hàng ngay khi vào khung chính để badge trên header luôn đúng (lỗi do errorInterceptor báo).
    this.cartStore.load().subscribe({ error: () => undefined });
  }

  protected toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  protected closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  protected logout(): void {
    this.authStore.logout();
  }
}

import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from '../../core/auth/auth.store';
import { CartStore } from '../../core/services/cart.store';

/**
 * Khung CÔNG KHAI (landing) cho các trang KHÔNG cần đăng nhập:
 * Trang chủ · Mua sắm · Về chúng tôi · Định hướng phát triển.
 *
 * Bố cục: thanh điều hướng NGANG cố định ở trên cùng (thương hiệu + các tiêu đề + nút "Đăng nhập"),
 * phía dưới là vùng nội dung theo route (`<router-outlet/>`) và chân trang.
 * Nút "Đăng nhập" trỏ sang khung xác thực `/login` (AuthLayout) — vẫn dùng lại nguyên luồng đăng nhập.
 */
@Component({
  selector: 'app-public-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './public-layout.html',
})
export class PublicLayout {
  /** Các tiêu đề trên thanh điều hướng; `route` dùng trực tiếp cho `routerLink`. */
  protected readonly navLinks = [
    { label: 'Trang chủ', route: '/' },
    { label: 'Mua sắm', route: '/shop' },
    { label: 'Về chúng tôi', route: '/about' },
    { label: 'Định hướng phát triển', route: '/development' },
  ] as const;

  /** Giỏ hàng dùng chung — badge trên header đọc trực tiếp từ store (khách vãng lai vẫn có giỏ tạm). */
  protected readonly cartStore = inject(CartStore);

  private readonly authStore = inject(AuthStore);

  /** Đã đăng nhập ⇒ header hiện TÊN NGƯỜI DÙNG thay cho nút "Đăng nhập". */
  protected readonly isAuthenticated = this.authStore.isAuthenticated;
  protected readonly displayName = this.authStore.displayName;
  protected readonly account = this.authStore.account;
  protected readonly isStaff = this.authStore.isStaff;

  /** Menu người dùng (bấm vào tên để mở/đóng). */
  protected readonly userMenuOpen = signal(false);

  /** Chữ cái đầu của tên đăng nhập — dùng làm avatar nhỏ. */
  protected readonly userInitial = computed(() => {
    const name = this.displayName();

    return name.length === 0 ? '?' : name.slice(0, 1).toUpperCase();
  });

  protected toggleUserMenu(): void {
    this.userMenuOpen.update((open) => !open);
  }

  protected closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  protected logout(): void {
    this.userMenuOpen.set(false);
    this.authStore.logout();
  }

  /** Menu ngang thu gọn trên màn hình nhỏ (mở/đóng bằng nút ☰). */
  protected readonly menuOpen = signal(false);

  /** Năm hiện tại cho dòng bản quyền ở chân trang. */
  protected readonly currentYear = new Date().getFullYear();

  protected toggleMenu(): void {
    this.menuOpen.update((open) => !open);
  }

  protected closeMenu(): void {
    this.menuOpen.set(false);
  }
}

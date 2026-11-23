import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';

/** Trang 403 — người dùng đã đăng nhập nhưng không đủ vai trò/quyền cho route (guard điều hướng tới đây). */
@Component({
  selector: 'app-forbidden',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="sh-card mx-auto max-w-xl p-8 text-center">
      <p class="text-4xl" aria-hidden="true">⛔</p>
      <h1 class="sh-page-title mt-3">Không đủ quyền truy cập</h1>
      <p class="mt-2 text-sm text-slate-600">
        Tài khoản <strong>{{ username() }}</strong> không có vai trò/quyền cần thiết cho màn hình này.
        Nếu bạn cho rằng đây là nhầm lẫn, hãy liên hệ quản trị hệ thống để được cấp quyền.
      </p>
      <div class="mt-5 flex justify-center gap-2">
        <a
          routerLink="/dashboard"
          class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          Về trang tổng quan
        </a>
        <a
          routerLink="/catalog/products"
          class="rounded-md border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-50"
        >
          Xem sản phẩm
        </a>
      </div>
    </section>
  `,
})
export class Forbidden {
  private readonly authStore = inject(AuthStore);

  protected readonly username = this.authStore.displayName;
}

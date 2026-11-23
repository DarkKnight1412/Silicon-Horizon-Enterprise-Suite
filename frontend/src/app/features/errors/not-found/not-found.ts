import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Trang 404 — dùng cho route `**` và khi resolver không tìm thấy dữ liệu (sản phẩm/đơn không tồn tại). */
@Component({
  selector: 'app-not-found',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="sh-card mx-auto max-w-xl p-8 text-center">
      <p class="text-4xl" aria-hidden="true">🧭</p>
      <h1 class="sh-page-title mt-3">Không tìm thấy nội dung</h1>
      <p class="mt-2 text-sm text-slate-600">
        Đường dẫn không tồn tại, hoặc dữ liệu bạn tìm (sản phẩm, đơn hàng) đã bị xoá / không thuộc quyền
        xem của tài khoản này.
      </p>
      <div class="mt-5 flex justify-center gap-2">
        <a
          routerLink="/dashboard"
          class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700"
        >
          Về trang tổng quan
        </a>
        <a
          routerLink="/shop"
          class="rounded-md border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-50"
        >
          Xem sản phẩm
        </a>
      </div>
    </section>
  `,
})
export class NotFound {}

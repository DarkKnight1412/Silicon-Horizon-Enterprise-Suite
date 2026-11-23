import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ConfirmDialogHost } from './shared/components/confirm-dialog/confirm-dialog-host';
import { ToastHost } from './shared/components/toast-host/toast-host';

/**
 * Component gốc: đặt `<router-outlet/>` và hai "host" dùng chung toàn ứng dụng
 * (toast + hộp thoại xác nhận) để mọi màn hình — kể cả guard canDeactivate — đều có chỗ hiển thị.
 *
 * Template đặt INLINE (thay cho nội dung welcome mặc định của scaffold trong `app.html`):
 * `app.html` cũ chỉ là trang giới thiệu của Angular CLI nên không còn được tham chiếu và có thể xoá.
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastHost, ConfirmDialogHost],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styleUrl: './app.scss',
  template: `
    <!-- Vùng nội dung theo route -->
    <router-outlet />

    <!-- Toast toàn cục: hiện cho cả màn hình đăng nhập lẫn khu vực đã đăng nhập -->
    <app-toast-host />

    <!-- Hộp thoại xác nhận dùng chung (ConfirmService) — phục vụ cả canDeactivate của form sản phẩm -->
    <app-confirm-dialog-host />
  `,
})
export class App {}


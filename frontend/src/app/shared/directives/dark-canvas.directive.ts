import { Directive, DOCUMENT, OnDestroy, inject } from '@angular/core';

/**
 * Đổi NỀN GỐC của trang sang tối khi layout tối được gắn vào DOM (và trả lại như cũ khi bị gỡ).
 *
 * Vì sao cần: `styles.scss` đặt nền `html/body` màu sáng (`#f1f5f9`), mà nền của thẻ `html` mới là nền
 * được vẽ cho TOÀN BỘ canvas của trang — kể cả vùng "kéo quá" đầu/cuối (overscroll) và khoảng trống khi
 * nội dung ngắn hơn khung nhìn. Nếu chỉ đặt `bg-slate-950` cho layout thì khi cuộn hết lên trên/xuống
 * dưới vẫn thấy một vệt sáng ở mép trang.
 *
 * Directive thêm class `sh-dark` vào `<html>` (định nghĩa trong `styles.scss`) khi layout tối mount và
 * gỡ ra khi unmount ⇒ khu vực quản trị (tông sáng) không bị đổi màu.
 *
 * Cách dùng: gắn vào phần tử GỐC của layout tối — `<div shDarkCanvas class="...">`.
 */
@Directive({
  selector: '[shDarkCanvas]',
})
export class DarkCanvasDirective implements OnDestroy {
  private readonly document = inject(DOCUMENT);

  constructor() {
    this.document.documentElement.classList.add('sh-dark');
  }

  ngOnDestroy(): void {
    this.document.documentElement.classList.remove('sh-dark');
  }
}

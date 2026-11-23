import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * TRANG CHỦ (công khai) — màn hình hiển thị NGAY khi mở đường dẫn gốc của ứng dụng (`/`).
 *
 * Bố cục: khối hero dùng một ảnh lớn làm NỀN (phủ lớp tối để chữ trắng nổi rõ), sau đó là các khối
 * "thông tin và hình ảnh" giới thiệu CỬA HÀNG (trang này dành cho khách hàng, không lộ chi tiết nội bộ).
 *
 * Muốn đổi ảnh nền: chỉ cần ghi đè `public/images/background.png` (không phải sửa code),
 * hoặc đổi đường dẫn trong `heroBackground` bên dưới.
 */
@Component({
  selector: 'app-home',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
})
export class Home {
  /** Giá trị CSS cho ảnh nền hero (ảnh đặt trong `public/images/`). */
  protected readonly heroBackground = "url('/images/background.png')";

  /** Vài điểm nổi bật dành cho KHÁCH HÀNG, hiển thị dạng lưới thẻ. */
  protected readonly highlights = [
    {
      icon: '🚚',
      title: 'Giao hàng nhanh',
      description: 'Giao toàn quốc, nhận hàng tại cửa hàng, đóng gói cẩn thận và đúng hẹn.',
    },
    {
      icon: '🛡️',
      title: 'Hàng chính hãng',
      description: 'Nguồn gốc rõ ràng, bảo hành theo nhà sản xuất, hỗ trợ đổi mới minh bạch.',
    },
    {
      icon: '💬',
      title: 'Tư vấn chọn linh kiện',
      description: 'Giúp bạn chọn CPU, RAM, SSD, mainboard tương thích và vừa ngân sách.',
    },
    {
      icon: '🏷️',
      title: 'Giá tốt cho khách sỉ',
      description: 'Chính sách giá linh hoạt, ưu đãi hấp dẫn cho đại lý và khách mua nhiều.',
    },
  ] as const;
}

import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * ĐỊNH HƯỚNG PHÁT TRIỂN (công khai) — lộ trình 3 giai đoạn của Apex Circuit Electronics.
 * Nội dung chủ yếu là THÔNG TIN và HÌNH ẢNH, KHÔNG lộ chi tiết vận hành nội bộ.
 */
@Component({
  selector: 'app-development',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './development.html',
})
export class Development {
  /** Lộ trình các giai đoạn; `status` hiển thị dạng nhãn (badge). */
  protected readonly phases = [
    {
      phase: 'Giai đoạn 1',
      title: 'Cửa hàng uy tín về linh kiện & phụ tùng PC',
      status: 'Đã hoàn thành',
      description:
        'Trở thành nhà cung cấp, cửa hàng đáng tin cậy về linh kiện và phụ tùng máy tính, với hàng chính hãng và dịch vụ rõ ràng.',
      points: [
        'Danh mục linh kiện & phụ tùng PC đầy đủ',
        'Mua sắm online, đặt hàng và giao nhận tận nơi',
        'Hàng sẵn kho, giao nhanh từ nhiều chi nhánh',
      ],
    },
    {
      phase: 'Giai đoạn 2',
      title: 'Nhà cung cấp sản phẩm công nghệ tích hợp',
      status: 'Đang triển khai',
      description:
        'Mở rộng từ linh kiện sang các sản phẩm công nghệ tích hợp, đáp ứng nhu cầu của thời đại công nghiệp hóa, hiện đại hóa.',
      points: [
        'Mở rộng danh mục sản phẩm công nghệ',
        'Bảo hành, đổi trả và hậu mãi tận tâm',
        'Hợp tác với nhiều nhà cung cấp uy tín',
      ],
    },
    {
      phase: 'Giai đoạn 3',
      title: 'Doanh nghiệp lớn, tự chủ cung ứng & sản xuất',
      status: 'Định hướng',
      description:
        'Trở thành công ty lớn, chủ động nguồn cung và có khả năng tự tạo ra sản phẩm để cung cấp cho thị trường.',
      points: [
        'Chủ động chuỗi cung ứng và nguồn hàng',
        'Nghiên cứu, phát triển và sản xuất sản phẩm riêng',
        'Phân phối rộng khắp thị trường',
      ],
    },
  ] as const;
}
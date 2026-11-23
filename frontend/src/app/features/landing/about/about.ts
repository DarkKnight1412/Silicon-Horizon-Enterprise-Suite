import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * VỀ CHÚNG TÔI (công khai) — giới thiệu CỬA HÀNG cho khách hàng.
 * Nội dung chủ yếu là THÔNG TIN và HÌNH ẢNH, KHÔNG lộ chi tiết vận hành nội bộ.
 */
@Component({
  selector: 'app-about',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './about.html',
})
export class About {
  /** Giá trị cốt lõi hiển thị dạng lưới thẻ. */
  protected readonly values = [
    {
      icon: '🎯',
      title: 'Hàng chính hãng',
      description: 'Sản phẩm có nguồn gốc rõ ràng, đầy đủ bảo hành từ nhà sản xuất.',
    },
    {
      icon: '🔍',
      title: 'Giá minh bạch',
      description: 'Giá niêm yết rõ ràng, hoá đơn đầy đủ, không phát sinh phí ẩn.',
    },
    {
      icon: '⚡',
      title: 'Phục vụ nhanh',
      description: 'Tư vấn nhanh, đóng gói kỹ và giao hàng đúng hẹn.',
    },
    {
      icon: '🤝',
      title: 'Đồng hành lâu dài',
      description: 'Hỗ trợ kỹ thuật trước và sau mua; ưu đãi cho khách sỉ và đại lý.',
    },
  ] as const;

  /** Cam kết dành cho khách hàng, hiển thị dạng thẻ (icon + tiêu đề + mô tả). */
  protected readonly commitments = [
    {
      icon: '✅',
      title: 'Hàng chính hãng 100%',
      description: 'Tất cả sản phẩm đều chính hãng, có hoá đơn và bảo hành rõ ràng.',
    },
    {
      icon: '🔄',
      title: 'Hỗ trợ đổi mới',
      description: 'Đổi mới theo chính sách của nhà sản xuất và quy định của cửa hàng.',
    },
    {
      icon: '🎧',
      title: 'Hỗ trợ kỹ thuật',
      description: 'Đồng hành cùng bạn trong suốt thời gian sử dụng và bảo hành.',
    },
  ] as const;
}

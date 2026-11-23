/**
 * DTO voucher — khớp `Features/Pricing/Dto/VoucherDtos.cs`.
 * Chỉ dùng endpoint công khai POST /api/v1/vouchers/validate để kiểm tra mã trước khi đặt hàng.
 */

/** POST /api/v1/vouchers/validate */
export interface ValidateVoucherRequest {
  code: string;
  /** Tạm tính của các dòng được chọn (VND). */
  orderAmount: number;
  /** Đơn có sản phẩm đang khuyến mãi hay không — dùng cho điều kiện isStackable. */
  hasPromotion: boolean;
  /** Chỉ dùng cho khách vãng lai đã có hồ sơ khách nhưng chưa đăng nhập. */
  customerId: number | null;
}

/**
 * Kết quả kiểm tra voucher. Mã sai vẫn trả 200 với `isValid = false` kèm `message` tiếng Việt
 * (thông điệp của fn_VoucherCheck: hết hạn, hết lượt, sai nhóm khách, chưa đủ giá trị đơn...).
 */
export interface VoucherValidationDto {
  isValid: boolean;
  message: string | null;
  discountAmount: number;
}

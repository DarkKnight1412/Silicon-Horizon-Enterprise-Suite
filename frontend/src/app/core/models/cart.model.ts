/**
 * DTO giỏ hàng — khớp `Features/Cart/Dto/CartDtos.cs` và Database/README.md mục 4.
 * Chủ giỏ: khách đã đăng nhập (customerId ép từ token) hoặc khách vãng lai (header `X-Cart-Session`).
 */

/** Một dòng giỏ hàng (`CartItemDto`). */
export interface CartItemDto {
  cartItemId: number;
  productId: number;
  sku: string;
  name: string;
  imageUrl: string | null;
  quantity: number;
  /** Dòng có được tick để đặt hàng hay không (proc tự bỏ tick món hết hàng). */
  isSelected: boolean;
  basePrice: number;
  unitPrice: number;
  promotionId: number | null;
  discountPerUnit: number;
  lineTotal: number;
  /** Tồn khả dụng tức thời — chặn tăng số lượng vượt giá trị này. */
  availableQty: number;
}

/** Tổng tiền các dòng ĐANG ĐƯỢC TICK (`CartTotalsDto`). */
export interface CartTotalsDto {
  selectedCount: number;
  subTotal: number;
  discountTotal: number;
  total: number;
}

/** Cảnh báo tồn kho (`CartWarningDto`). */
export interface CartWarningDto {
  productId: number;
  name: string;
  /** OUT_OF_STOCK = đã bị bỏ tick; QUANTITY_REDUCED = đã giảm số lượng theo tồn. */
  code: string;
  oldQuantity: number;
  newQuantity: number;
}

/** Toàn bộ giỏ hàng (3 result set của usp_Cart_Get gộp lại). */
export interface CartDto {
  items: CartItemDto[];
  totals: CartTotalsDto;
  warnings: CartWarningDto[];
}

/** POST /api/v1/cart/items */
export interface AddCartItemRequest {
  productId: number;
  quantity: number;
  /** true = đặt đúng số lượng; false = cộng dồn vào số lượng đang có. */
  replace: boolean;
}

/** Kết quả thêm vào giỏ (`AddedCartItemDto`). */
export interface AddedCartItemDto {
  cartId: number;
  productId: number;
  quantity: number;
}

/** PATCH /api/v1/cart/items/{productId} — bỏ trống = giữ nguyên; quantity <= 0 = xoá dòng. */
export interface UpdateCartItemRequest {
  quantity?: number | null;
  isSelected?: boolean | null;
}

/** POST /api/v1/cart/select-all */
export interface SelectAllCartItemsRequest {
  isSelected: boolean;
}

/**
 * POST /api/v1/cart/items/bulk — thêm NHIỀU sản phẩm trong MỘT giao dịch
 * (dùng cho tab "Tự build PC": khách chốt cấu hình rồi bấm "Đặt mua" một lần).
 */
export interface AddCartItemsRequest {
  items: AddCartItemInput[];
}

/** Một dòng trong `AddCartItemsRequest`. */
export interface AddCartItemInput {
  productId: number;
  quantity: number;
}

/** Kết quả thêm nhiều sản phẩm (`AddedCartItemsResultDto`). */
export interface AddedCartItemsResultDto {
  itemCount: number;
  items: AddedCartItemDetailDto[];
}

/** Một dòng vừa được thêm vào giỏ. */
export interface AddedCartItemDetailDto {
  productId: number;
  name: string;
  /** Số lượng vừa thêm. */
  addedQuantity: number;
  /** Số lượng hiện có trong giỏ sau khi thêm. */
  cartQuantity: number;
}


/** Nhãn tiếng Việt của mã cảnh báo giỏ hàng. */
export const CART_WARNING_LABELS: Readonly<Record<string, string>> = {
  OUT_OF_STOCK: 'đã hết hàng nên được bỏ chọn',
  QUANTITY_REDUCED: 'đã được giảm số lượng theo tồn khả dụng',
};

/** Khoá localStorage giữ mã phiên giỏ tạm của khách vãng lai. */
export const CART_SESSION_STORAGE_KEY = 'sh.cart-session';

/** Tạo mã phiên giỏ tạm (gửi ở header X-Cart-Session cho khách chưa đăng nhập). */
export function createCartSessionToken(): string {
  const random = globalThis.crypto?.randomUUID?.();
  return random ?? `cart-${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
}

/** Giỏ hàng rỗng dùng làm state ban đầu. */
export function emptyCart(): CartDto {
  return {
    items: [],
    totals: { selectedCount: 0, subTotal: 0, discountTotal: 0, total: 0 },
    warnings: [],
  };
}

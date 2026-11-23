/** Lớp Tailwind cho badge trạng thái sản phẩm (0 Nháp, 1 Đang bán, 2 Ngừng kinh doanh). */
export const PRODUCT_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-slate-200 text-slate-700',
  1: 'bg-emerald-100 text-emerald-800',
  2: 'bg-amber-100 text-amber-800',
};

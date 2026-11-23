import { PERMISSIONS, ROLES } from '../core/models/auth.model';
import type { NavItem } from '../core/models/common.model';

/**
 * Cấu hình menu sidebar. `roles`/`permissions` ở đây được `MainLayout` lọc qua
 * `AuthStore.canAccess(...)` — cùng logic với guard nên menu và quyền vào route luôn khớp nhau.
 *
 * Lưu ý: mục "Đơn hàng của tôi" chỉ dành cho vai trò Customer vì backend giới hạn
 * `GET /api/v1/orders` bằng `[Authorize(Roles = "Customer")]` (nhân viên dùng /api/v1/staff/orders,
 * quản trị dùng /api/v1/admin/orders — chưa nằm trong phạm vi màn hình lần này).
 */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Tổng quan', icon: '🏠', route: '/dashboard' },
  { label: 'Sản phẩm', icon: '🖥️', route: '/catalog/products' },
  { label: 'Kiểm tra tương thích', icon: '🧩', route: '/catalog/compatibility' },
  { label: 'Giỏ hàng', icon: '🛒', route: '/cart' },
  { label: 'Đơn hàng của tôi', icon: '🧾', route: '/orders', roles: [ROLES.Customer] },
  {
    label: 'Quản trị sản phẩm',
    icon: '⚙️',
    route: '/admin/products',
    roles: [ROLES.Admin],
    permissions: [PERMISSIONS.CatalogManage],
  },
  {
    label: 'Danh mục',
    icon: '🗂️',
    route: '/admin/categories',
    roles: [ROLES.Admin],
    permissions: [PERMISSIONS.CatalogManage],
  },
  {
    label: 'Thương hiệu',
    icon: '🏷️',
    route: '/admin/brands',
    roles: [ROLES.Admin],
    permissions: [PERMISSIONS.CatalogManage],
  },
  // ── Nhóm KHO · SERIAL (backend: InventoryRoles — TK/QL/AD, thao tác tạo là TK/AD) ──
  {
    label: 'Tồn kho',
    icon: '📦',
    route: '/inventory/stock',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.StockAdjustApprove],
  },
  {
    label: 'Thẻ kho',
    icon: '📒',
    route: '/inventory/movements',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.StockReceipt],
  },
  {
    label: 'Phiếu nhập kho',
    icon: '🧾',
    route: '/inventory/receipts',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.StockReceipt],
  },
  {
    label: 'Chuyển kho',
    icon: '🔁',
    route: '/inventory/transfers',
    roles: [ROLES.Storekeeper, ROLES.Admin],
    permissions: [PERMISSIONS.StockTransfer],
  },
  {
    label: 'Kiểm kê',
    icon: '📋',
    route: '/inventory/stock-takes',
    roles: [ROLES.Storekeeper, ROLES.Admin],
    permissions: [PERMISSIONS.StockTake],
  },
  {
    label: 'Tra cứu serial',
    icon: '🔖',
    route: '/inventory/serials',
    // Xem serial: mọi nhân viên + Admin; đổi trạng thái serial chỉ QL/AD (ẩn nút bên trong màn hình).
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.SerialManage],
  },

  // ── Nhóm NHÀ CUNG CẤP (đọc: TK/QL/AD; tạo/sửa: AD; công nợ & thanh toán: QL/AD) ──
  {
    label: 'Nhà cung cấp',
    icon: '🚚',
    route: '/suppliers/list',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.StockReceipt],
  },

  // ── Nhóm BẢO HÀNH (tiếp nhận: TN/TV/AD; xử lý: TK/TV/AD; xem: mọi nhân viên) ──
  {
    label: 'Phiếu bảo hành',
    icon: '🛠️',
    route: '/warranty/tickets',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.WarrantyReceive],
  },
  {
    label: 'Tra cứu bảo hành',
    icon: '🔎',
    route: '/warranty/lookup',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.WarrantyReceive],
  },

  // ── Nhóm ĐỔI TRẢ (xem: mọi nhân viên; duyệt/hoàn tiền: QL/AD; kiểm tra: TK/AD) ──
  {
    label: 'Yêu cầu đổi trả',
    icon: '↩️',
    route: '/returns',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.ReturnReview],
  },

  // ── Nhóm ĐƠN HÀNG NHÂN VIÊN + POS (danh sách: NV; POS: TN/TV/QL/AD) ──
  {
    label: 'Đơn của chi nhánh',
    icon: '🏪',
    route: '/staff-orders/list',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.OrderConfirm],
  },
  {
    label: 'Bán tại quầy (POS)',
    icon: '🧮',
    route: '/staff-orders/pos',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.OrderCreate],
  },

  // ── Nhóm THANH TOÁN / CÔNG NỢ (sao kê công nợ khách: QL, AD) ──
  {
    label: 'Công nợ khách hàng',
    icon: '💰',
    route: '/payments/ledger',
    roles: [ROLES.Manager, ROLES.Admin],
    accessMode: 'roles',
  },

  // ── Nhóm NHÂN SỰ (xem: NV/QL/AD; xếp ca & điều chỉnh công: QL/AD) ──
  {
    label: 'Xếp ca làm việc',
    icon: '🗓️',
    route: '/hr/schedules',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.ScheduleManage],
  },
  {
    label: 'Chấm công',
    icon: '⏱️',
    route: '/hr/attendance',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.AttendanceAdjust],
  },

  // ── Nhóm HOA HỒNG (xem: NV/QL/AD; điều chỉnh & chốt kỳ: AD) ──
  {
    label: 'Hoa hồng theo kỳ',
    icon: '📈',
    route: '/commission',
    roles: [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
    permissions: [PERMISSIONS.CommissionManage],
  },

  // ── Nhóm NHẬT KÝ (QL chỉ chi nhánh mình — backend tự ép; AD toàn hệ thống) ──
  {
    label: 'Nhật ký hệ thống',
    icon: '🧷',
    route: '/audit',
    roles: [ROLES.Manager, ROLES.Admin],
    accessMode: 'roles',
  },

  // ── Nhóm BÁO CÁO (doanh thu/bảo hành: QL, AD · tồn kho/serial: TK, QL, AD · công nợ: AD) ──
  {
    label: 'Báo cáo doanh thu',
    icon: '📊',
    route: '/reports/revenue',
    roles: [ROLES.Manager, ROLES.Admin],
  },
  {
    label: 'Báo cáo tồn kho',
    icon: '📉',
    route: '/reports/inventory',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
  },
  {
    label: 'Serial theo trạng thái',
    icon: '🔢',
    route: '/reports/serial-status',
    roles: [ROLES.Storekeeper, ROLES.Manager, ROLES.Admin],
  },
  {
    label: 'Báo cáo bảo hành',
    icon: '🛡️',
    route: '/reports/warranty',
    roles: [ROLES.Manager, ROLES.Admin],
  },
  {
    label: 'Báo cáo công nợ',
    icon: '⚖️',
    route: '/reports/debt',
    roles: [ROLES.Admin],
    accessMode: 'roles',
  },
];

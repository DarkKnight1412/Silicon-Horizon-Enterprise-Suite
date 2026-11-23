/**
 * DTO tài khoản — khớp `SiliconHorizon.Application.Features.Auth.Dto`:
 * LoginRequest, RegisterCustomerRequest, RefreshTokenRequest, AuthTokensDto, CurrentAccountDto, RegisteredCustomerDto.
 */

/** POST /api/auth/login — đăng nhập bằng tên đăng nhập hoặc email. */
export interface LoginRequest {
  username: string;
  password: string;
}

/** POST /api/auth/register — khách tự đăng ký tài khoản. */
export interface RegisterCustomerRequest {
  username: string;
  password: string;
  phone: string;
  fullName: string;
  email: string | null;
}

/** POST /api/auth/refresh. */
export interface RefreshTokenRequest {
  refreshToken: string;
}

/** Cặp token trả về sau đăng nhập / làm mới token. */
export interface AuthTokensDto {
  tokenType: string;
  accessToken: string;
  refreshToken: string;
  /** ISO 8601 UTC. */
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
}

/** GET /api/me — thông tin tài khoản đang đăng nhập. */
export interface CurrentAccountDto {
  userId: number;
  username: string;
  email: string | null;
  phone: string | null;
  /** 0 = Khách hàng, 1 = Nhân viên. */
  userType: number;
  roles: string[];
  permissions: string[];
  customerId: number | null;
  employeeId: number | null;
  branchId: number | null;
  /** 0 Cashier, 1 TechAdvisor, 2 Storekeeper, 3 Manager. */
  employeePosition: number | null;
}

/** Kết quả đăng ký khách hàng mới (201 Created). */
export interface RegisteredCustomerDto {
  userId: number;
  customerId: number;
}

/** Mã vai trò — khớp seed `dbo.Roles` (RoleCodes.cs). */
export const ROLES = {
  Customer: 'Customer',
  Cashier: 'Cashier',
  TechAdvisor: 'TechAdvisor',
  Storekeeper: 'Storekeeper',
  Manager: 'Manager',
  Admin: 'Admin',
} as const;

export type RoleCode = (typeof ROLES)[keyof typeof ROLES];

/** Mã quyền — khớp `PermissionCodes.cs` (bảng Permissions có thể chưa seed đủ). */
export const PERMISSIONS = {
  OrderCreate: 'order.create',
  OrderConfirm: 'order.confirm',
  OrderPack: 'order.pack',
  OrderShip: 'order.ship',
  OrderCancelRequest: 'order.cancel.request',
  OrderCancelApprove: 'order.cancel.approve',
  StockReceipt: 'stock.receipt',
  StockTransfer: 'stock.transfer',
  StockTake: 'stock.take',
  StockAdjustApprove: 'stock.adjust.approve',
  SerialManage: 'serial.manage',
  CatalogManage: 'catalog.manage',
  PriceManage: 'price.manage',
  PromotionManage: 'promotion.manage',
  WarrantyReceive: 'warranty.receive',
  WarrantyProcess: 'warranty.process',
  ReturnRequest: 'return.request',
  ReturnReview: 'return.review',
  ReturnInspect: 'return.inspect',
  ReturnRefund: 'return.refund',
  ScheduleManage: 'hr.schedule.manage',
  AttendanceAdjust: 'hr.attendance.adjust',
  CommissionManage: 'commission.manage',
  AuditView: 'audit.view',
  SettingsManage: 'settings.manage',
} as const;

export type PermissionCode = (typeof PERMISSIONS)[keyof typeof PERMISSIONS];

/** Nhãn tiếng Việt của vai trò (hiển thị trên header/sidebar). */
export const ROLE_LABELS: Readonly<Record<string, string>> = {
  [ROLES.Customer]: 'Khách hàng',
  [ROLES.Cashier]: 'Thu ngân',
  [ROLES.TechAdvisor]: 'Tư vấn kỹ thuật',
  [ROLES.Storekeeper]: 'Thủ kho',
  [ROLES.Manager]: 'Quản lý chi nhánh',
  [ROLES.Admin]: 'Quản trị hệ thống',
};

/** Vị trí nhân viên (CurrentAccountDto.employeePosition). */
export const EMPLOYEE_POSITION_LABELS: Readonly<Record<number, string>> = {
  0: 'Thu ngân',
  1: 'Tư vấn kỹ thuật',
  2: 'Thủ kho',
  3: 'Quản lý chi nhánh',
};

/** Nhóm vai trò "bán hàng tại quầy" — dùng để quyết định hiện các nút xử lý đơn. */
export const SALES_ROLES: readonly RoleCode[] = [ROLES.Cashier, ROLES.TechAdvisor, ROLES.Manager, ROLES.Admin];

/** Nhóm vai trò kho — đóng gói, gán serial. */
export const WAREHOUSE_ROLES: readonly RoleCode[] = [ROLES.Storekeeper, ROLES.Admin];

/** Nhóm vai trò duyệt — hủy đơn đã xác nhận, điều chỉnh tồn, hoàn tiền. */
export const APPROVER_ROLES: readonly RoleCode[] = [ROLES.Manager, ROLES.Admin];

/** true nếu danh sách vai trò có ít nhất một vai trò nhân viên (không phải khách hàng). */
export function isStaffRoles(roles: readonly string[]): boolean {
  return roles.some((role) => role !== ROLES.Customer);
}

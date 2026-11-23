import type { SelectOption } from '../../core/models/common.model';

/**
 * DTO của module BÁO CÁO, khớp `SiliconHorizon.Application.Features.Reports.Dto`
 * (RevenueReportDtos.cs, InventoryReportDtos.cs, SerialStatusReportDtos.cs, DebtReportDtos.cs,
 * WarrantyReportDtos.cs) — Database/README.md mục 9, BRD mục 11.
 *
 * Quy ước phạm vi (BRD 2.2): Quản lý chi nhánh / Thủ kho chỉ xem số liệu chi nhánh mình (API ép
 * `branchId` từ token), Admin xem toàn hệ thống và lọc được theo `branchId`. Riêng báo cáo công nợ
 * chỉ dành cho Admin vì proc không có tham số chi nhánh.
 *
 * `from`/`to` của báo cáo doanh thu và bảo hành là BẮT BUỘC, tính theo giờ Việt Nam và bao gồm cả
 * ngày `to` (khác với các danh sách dùng chặn trên không bao gồm).
 */

// ── Báo cáo doanh thu ───────────────────────────────────────────────────────

/** Một dòng doanh thu gộp theo ngày/tháng/chi nhánh (`usp_Report_Revenue`). */
export interface RevenueGroupDto {
  /** `yyyy-MM-dd` (day), `yyyy-MM` (month) hoặc `BranchId` dạng chuỗi (branch). */
  groupKey: string;
  orders: number;
  /** Tổng doanh thu trước khi trừ hoàn tiền. */
  grossRevenue: number;
  /** Tổng tiền đã hoàn cho khách. */
  refunds: number;
  /** Doanh thu thuần = grossRevenue − refunds. */
  netRevenue: number;
  cost: number;
  grossProfit: number;
}

/** Một dòng doanh thu theo danh mục (khi `groupBy = category`). */
export interface RevenueCategoryDto {
  categoryId: number;
  categoryName: string;
  quantity: number;
  revenue: number;
}

/** Kết quả GET /api/v1/reports/revenue. */
export interface RevenueReportDto {
  groupBy: string;
  /** `yyyy-MM-dd`. */
  fromDate: string;
  /** `yyyy-MM-dd`. */
  toDate: string;
  /** `null` = toàn hệ thống. */
  branchId: number | null;
  groups: RevenueGroupDto[];
  categories: RevenueCategoryDto[];
}

/** Tham số GET /api/v1/reports/revenue — `from`/`to` bắt buộc. */
export interface RevenueReportQuery {
  fromDate: string | null;
  toDate: string | null;
  /** `day` | `month` | `branch` | `category`. */
  groupBy: string | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

// ── Báo cáo tồn kho ─────────────────────────────────────────────────────────

/** Giá trị tồn theo chi nhánh + danh mục. */
export interface InventoryValueDto {
  branchId: number;
  branchName: string;
  categoryId: number;
  categoryName: string;
  physicalQty: number;
  reservedQty: number;
  availableQty: number;
  stockValue: number;
}

/** Hàng tồn lâu không xuất (`lastIssueAt = null` = chưa từng xuất). */
export interface SlowMovingStockDto {
  branchId: number;
  productId: number;
  sku: string;
  productName: string;
  physicalQty: number;
  lastIssueAt: string | null;
}

/** Kết quả GET /api/v1/reports/inventory (không có tham số ngày). */
export interface InventoryReportDto {
  branchId: number | null;
  slowDays: number;
  values: InventoryValueDto[];
  slowMoving: SlowMovingStockDto[];
}

/** Tham số GET /api/v1/reports/inventory. */
export interface InventoryReportQuery {
  /** Mặc định 90 ngày theo proc. */
  slowDays?: number | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

// ── Báo cáo serial theo trạng thái ──────────────────────────────────────────

/** Số lượng serial theo trạng thái (trạng thái không có serial nào sẽ vắng mặt). */
export interface SerialStatusCountDto {
  /** 0 Trong kho … 9 Đã hủy. */
  status: number;
  statusName: string;
  quantity: number;
}

/** Tham số GET /api/v1/reports/serial-status. */
export interface SerialStatusReportQuery {
  productId?: number | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

// ── Báo cáo công nợ ─────────────────────────────────────────────────────────

/** Công nợ phải thu của một khách. */
export interface CustomerDebtDto {
  customerId: number;
  fullName: string;
  phone: string | null;
  creditLimit: number;
  /** Dương = khách còn nợ. */
  balance: number;
  /** Phần quá hạn (đã tới `dueDate` nhưng chưa thanh toán). */
  overdue: number;
}

/** Công nợ phải trả một nhà cung cấp. */
export interface SupplierDebtDto {
  supplierId: number;
  supplierName: string;
  balance: number;
  overdue: number;
}

/** Kết quả GET /api/v1/reports/debt (chỉ Admin, không có tham số). */
export interface DebtReportDto {
  customers: CustomerDebtDto[];
  suppliers: SupplierDebtDto[];
}

// ── Báo cáo bảo hành ────────────────────────────────────────────────────────

/** Số phiếu bảo hành theo trạng thái. */
export interface WarrantyStatusCountDto {
  /** 0 Đã tiếp nhận … 6 Đã trả khách. */
  status: number;
  statusName: string;
  tickets: number;
}

/** Sản phẩm có nhiều phiếu bảo hành nhất (top 20). */
export interface WarrantyTopProductDto {
  productId: number;
  sku: string;
  productName: string;
  tickets: number;
  /** Số lượng đã bán — dùng để tính tỷ lệ lỗi. */
  soldQuantity: number;
}

/** Kết quả GET /api/v1/reports/warranty. */
export interface WarrantyReportDto {
  fromDate: string;
  toDate: string;
  branchId: number | null;
  statuses: WarrantyStatusCountDto[];
  topDefectiveProducts: WarrantyTopProductDto[];
  /** `null` khi chưa có phiếu nào hoàn tất trong kỳ. */
  avgHandlingDays: number | null;
}

/** Tham số GET /api/v1/reports/warranty — `from`/`to` bắt buộc. */
export interface WarrantyReportQuery {
  fromDate: string | null;
  toDate: string | null;
  /** Chỉ Admin. */
  branchId?: number | null;
}

/** Cách gộp của báo cáo doanh thu (`groupBy`). */
export const REVENUE_GROUP_BY_OPTIONS: readonly SelectOption<string>[] = [
  { value: 'day', label: 'Theo ngày' },
  { value: 'month', label: 'Theo tháng' },
  { value: 'branch', label: 'Theo chi nhánh' },
  { value: 'category', label: 'Theo danh mục' },
];

/** Lớp Tailwind cho badge trạng thái serial trong báo cáo (0 Trong kho … 9 Đã hủy). */
export const SERIAL_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-emerald-100 text-emerald-800',
  1: 'bg-sky-100 text-sky-800',
  2: 'bg-cyan-100 text-cyan-800',
  3: 'bg-indigo-100 text-indigo-800',
  4: 'bg-violet-100 text-violet-800',
  5: 'bg-teal-100 text-teal-800',
  6: 'bg-blue-100 text-blue-800',
  7: 'bg-rose-100 text-rose-800',
  8: 'bg-amber-100 text-amber-800',
  9: 'bg-slate-200 text-slate-700',
};

/** Lớp Tailwind cho badge trạng thái phiếu bảo hành (0 Đã tiếp nhận … 6 Đã trả khách). */
export const WARRANTY_STATUS_CLASSES: Readonly<Record<number, string>> = {
  0: 'bg-sky-100 text-sky-800',
  1: 'bg-indigo-100 text-indigo-800',
  2: 'bg-violet-100 text-violet-800',
  3: 'bg-emerald-100 text-emerald-800',
  4: 'bg-teal-100 text-teal-800',
  5: 'bg-rose-100 text-rose-800',
  6: 'bg-slate-200 text-slate-700',
};

/** `Date` → `yyyy-MM-dd` giờ địa phương (giá trị cho `input type="date"`). */
export function toReportDate(date: Date): string {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');

  return `${date.getFullYear()}-${month}-${day}`;
}

/** Ngày đầu tháng hiện tại — mặc định cho "từ ngày" của báo cáo doanh thu/bảo hành. */
export function firstDayOfMonth(): string {
  const now = new Date();

  return toReportDate(new Date(now.getFullYear(), now.getMonth(), 1));
}

/** Ngày hôm nay — mặc định cho "đến ngày" của báo cáo doanh thu/bảo hành. */
export function today(): string {
  return toReportDate(new Date());
}

/** Nhãn khóa nhóm của báo cáo doanh thu (branch trả `BranchId` dạng chuỗi). */
export function revenueGroupKeyLabel(groupBy: string, groupKey: string): string {
  return groupBy === 'branch' ? `Chi nhánh #${groupKey}` : groupKey;
}

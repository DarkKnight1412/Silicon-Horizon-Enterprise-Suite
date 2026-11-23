/**
 * Chi nhánh dùng cho KHU VỰC KHÁCH HÀNG — khớp
 * `SiliconHorizon.Application.Features.Catalog.Dto.BranchReferenceDto` (GET /api/v1/branches).
 *
 * Endpoint này là CÔNG KHAI nên chỉ trả chi nhánh ĐANG HOẠT ĐỘNG và KHÔNG có trường quản trị
 * (isActive/thời điểm tạo…) — khác `BranchDto` của màn hình quản trị trong `category.model.ts`.
 */
export interface BranchReferenceDto {
  id: number;

  /** Mã chi nhánh (duy nhất — `UQ_Branches_Code`). */
  code: string;

  name: string;

  address: string;

  phone: string | null;
}

import type { SelectOption } from './common.model';

/**
 * Thông số kỹ thuật sản phẩm — khớp cột các bảng Specs_* trong Database/table_and_data.sql
 * và khoá JSON camelCase mà `usp_Internal_SaveSpecs` đọc (Database/02_sp_foundation_catalog_cart.sql).
 */

/** 0 Generic, 1 CPU, 2 RAM, 3 Storage, 4 Monitor, 5 Mainboard, 6 PSU (Categories.SpecType). */
export type SpecType = 0 | 1 | 2 | 3 | 4 | 5 | 6;

export const SPEC_TYPE_LABELS: Readonly<Record<number, string>> = {
  0: 'Thông số chung (Generic)',
  1: 'CPU',
  2: 'RAM',
  3: 'Ổ cứng (Storage)',
  4: 'Màn hình (Monitor)',
  5: 'Bo mạch chủ (Mainboard)',
  6: 'Nguồn (PSU)',
};

export const SPEC_TYPE_OPTIONS: readonly SelectOption<number>[] = [
  { value: 1, label: SPEC_TYPE_LABELS[1] ?? 'CPU' },
  { value: 2, label: SPEC_TYPE_LABELS[2] ?? 'RAM' },
  { value: 3, label: SPEC_TYPE_LABELS[3] ?? 'Storage' },
  { value: 4, label: SPEC_TYPE_LABELS[4] ?? 'Monitor' },
  { value: 5, label: SPEC_TYPE_LABELS[5] ?? 'Mainboard' },
  { value: 6, label: SPEC_TYPE_LABELS[6] ?? 'PSU' },
];

/** Kiểu điều khiển của một trường thông số. */
export type SpecFieldKind = 'text' | 'number' | 'boolean' | 'select';

/** Mô tả một trường thông số — `key` chính là tên trường camelCase gửi lên API. */
export interface SpecFieldDef {
  key: string;
  label: string;
  kind: SpecFieldKind;
  required: boolean;
  /** Lựa chọn cho trường select. */
  options?: readonly string[];
  hint?: string;
  min?: number;
  max?: number;
  step?: number;
}

/** Loại cổng kết nối của màn hình (Specs_Monitor_Ports.PortType). */
export const MONITOR_PORT_TYPES: readonly string[] = ['HDMI', 'DP', 'TypeC'];

/** Chuẩn hoá giá trị SpecType nhận từ API (number) về union an toàn. */
export function toSpecType(value: number | null | undefined): SpecType {
  switch (value) {
    case 1:
    case 2:
    case 3:
    case 4:
    case 5:
    case 6:
      return value;
    default:
      return 0;
  }
}

/**
 * Định nghĩa trường theo từng SpecType (khớp đúng khoá OPENJSON của `usp_Internal_SaveSpecs`).
 * Lưu ý Storage: cột [Interface] được gửi với khoá JSON `interface` (không phải `interfaceType`).
 */
const SPEC_FIELDS_CPU: readonly SpecFieldDef[] = [
  { key: 'socket', label: 'Socket', kind: 'text', required: true, hint: 'Ví dụ: AM5, LGA1700' },
  { key: 'cores', label: 'Số nhân', kind: 'number', required: true, min: 1, max: 200 },
  { key: 'threads', label: 'Số luồng', kind: 'number', required: true, min: 1, max: 400 },
  { key: 'baseClockGhz', label: 'Xung cơ bản (GHz)', kind: 'number', required: true, min: 0.5, max: 10, step: 0.01 },
  { key: 'boostClockGhz', label: 'Xung tối đa (GHz)', kind: 'number', required: false, min: 0, max: 10, step: 0.01 },
  { key: 'cacheMb', label: 'Cache (MB)', kind: 'number', required: true, min: 1, max: 512 },
  { key: 'tdpW', label: 'TDP (W)', kind: 'number', required: true, min: 1, max: 500 },
  { key: 'hasCooler', label: 'Kèm tản nhiệt', kind: 'boolean', required: false },
  { key: 'hasIntegratedGpu', label: 'Có iGPU', kind: 'boolean', required: false },
];

const SPEC_FIELDS_RAM: readonly SpecFieldDef[] = [
  { key: 'memoryType', label: 'Chuẩn bộ nhớ', kind: 'select', required: true, options: ['DDR4', 'DDR5'] },
  { key: 'capacityGbPerKit', label: 'Dung lượng mỗi kit (GB)', kind: 'number', required: true, min: 1, max: 512 },
  { key: 'modulesPerKit', label: 'Số thanh mỗi kit', kind: 'number', required: true, min: 1, max: 4, hint: 'Bảng chỉ cho phép 1, 2 hoặc 4' },
  { key: 'busMhz', label: 'Bus (MHz)', kind: 'number', required: true, min: 1000, max: 12000 },
  { key: 'casLatency', label: 'CAS Latency', kind: 'number', required: true, min: 1, max: 60 },
  { key: 'formFactor', label: 'Form factor', kind: 'select', required: true, options: ['DIMM', 'SODIMM'] },
  { key: 'hasRgb', label: 'Có LED RGB', kind: 'boolean', required: false },
];

const SPEC_FIELDS_STORAGE: readonly SpecFieldDef[] = [
  { key: 'storageType', label: 'Loại ổ', kind: 'select', required: true, options: ['SSD NVMe', 'SSD SATA', 'HDD'] },
  { key: 'interface', label: 'Giao tiếp', kind: 'text', required: true, hint: 'Ví dụ: PCIe Gen4 x4, SATA III' },
  { key: 'formFactor', label: 'Form factor', kind: 'text', required: true, hint: 'Ví dụ: M.2 2280, 2.5in, 3.5in' },
  { key: 'capacityGb', label: 'Dung lượng (GB)', kind: 'number', required: true, min: 1, max: 100000 },
  { key: 'readMbps', label: 'Tốc độ đọc (MB/s)', kind: 'number', required: false, min: 0, max: 20000 },
  { key: 'writeMbps', label: 'Tốc độ ghi (MB/s)', kind: 'number', required: false, min: 0, max: 20000 },
];

const SPEC_FIELDS_MONITOR: readonly SpecFieldDef[] = [
  { key: 'sizeInch', label: 'Kích thước (inch)', kind: 'number', required: true, min: 5, max: 100, step: 0.1 },
  { key: 'resolution', label: 'Độ phân giải', kind: 'select', required: true, options: ['FHD', '2K', '4K'] },
  { key: 'panelType', label: 'Tấm nền', kind: 'select', required: true, options: ['IPS', 'VA', 'OLED'] },
  { key: 'refreshRateHz', label: 'Tần số quét (Hz)', kind: 'number', required: true, min: 30, max: 1000 },
  { key: 'responseTimeMs', label: 'Thời gian phản hồi (ms)', kind: 'number', required: true, min: 0.1, max: 50, step: 0.1 },
  { key: 'srgbPercent', label: 'Độ phủ sRGB (%)', kind: 'number', required: false, min: 1, max: 100 },
];

const SPEC_FIELDS_MAINBOARD: readonly SpecFieldDef[] = [
  { key: 'socket', label: 'Socket', kind: 'text', required: true, hint: 'Ví dụ: AM5, LGA1700' },
  { key: 'chipset', label: 'Chipset', kind: 'text', required: true, hint: 'Ví dụ: B650, Z790' },
  { key: 'memoryType', label: 'Chuẩn bộ nhớ', kind: 'select', required: true, options: ['DDR4', 'DDR5'] },
  { key: 'ramSlots', label: 'Số khe RAM', kind: 'number', required: true, min: 1, max: 16 },
  { key: 'maxBusMhz', label: 'Bus RAM tối đa (MHz)', kind: 'number', required: true, min: 1000, max: 12000 },
  { key: 'formFactor', label: 'Form factor', kind: 'select', required: true, options: ['ATX', 'mATX', 'ITX'] },
  { key: 'm2Slots', label: 'Số khe M.2', kind: 'number', required: false, min: 0, max: 16 },
  { key: 'sataPorts', label: 'Số cổng SATA', kind: 'number', required: false, min: 0, max: 32 },
];

const SPEC_FIELDS_PSU: readonly SpecFieldDef[] = [
  { key: 'powerW', label: 'Công suất (W)', kind: 'number', required: true, min: 100, max: 3000 },
  {
    key: 'efficiencyRating',
    label: 'Chứng nhận hiệu suất',
    kind: 'select',
    required: false,
    options: ['80+ White', '80+ Bronze', '80+ Silver', '80+ Gold', '80+ Platinum', '80+ Titanium'],
  },
  { key: 'modularType', label: 'Kiểu dây', kind: 'select', required: false, options: ['Full', 'Semi', 'Non'] },
];

/** Bảng tra trường thông số theo SpecType (0 = Generic không có thông số riêng). */
export const SPEC_FIELD_DEFS: Readonly<Record<SpecType, readonly SpecFieldDef[]>> = {
  0: [],
  1: SPEC_FIELDS_CPU,
  2: SPEC_FIELDS_RAM,
  3: SPEC_FIELDS_STORAGE,
  4: SPEC_FIELDS_MONITOR,
  5: SPEC_FIELDS_MAINBOARD,
  6: SPEC_FIELDS_PSU,
};

/** Các trường thông số của một SpecType (rỗng với Generic). */
export function specFieldsFor(specType: SpecType): readonly SpecFieldDef[] {
  return SPEC_FIELD_DEFS[specType];
}

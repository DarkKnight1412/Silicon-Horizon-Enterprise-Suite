import {
  FormArray,
  FormControl,
  FormGroup,
  Validators,
  type AbstractControl,
  type ValidatorFn,
} from '@angular/forms';
import { specFieldsFor, type SpecFieldDef, type SpecType } from '../../../core/models/spec.model';

/**
 * Nhóm form ĐỘNG cho thông số kỹ thuật (Specs_*).
 * Dùng form KHÔNG định kiểu vì cấu trúc nhóm thay đổi theo SpecType của danh mục;
 * các trường cố định của sản phẩm vẫn dùng typed reactive forms ở component.
 */
export type SpecsFormGroup = FormGroup<Record<string, AbstractControl>>;

/** Giá trị khởi tạo của thông số — khoá có thể là tên cột SQL ("TdpW") hoặc camelCase ("tdpW"). */
export type SpecsInitialValues = Record<string, unknown> | null | undefined;

/** Một dòng cổng kết nối của màn hình (Specs_Monitor_Ports). */
export interface MonitorPortValue {
  portType: string;
  quantity: number;
}

/**
 * Tạo nhóm thông số theo SpecType. Khi sửa sản phẩm, truyền `initial` (ProductDetailDto.specifications)
 * và `ports` (ProductDetailDto.monitorPorts) để điền sẵn dữ liệu cũ.
 */
export function createSpecsFormGroup(
  specType: SpecType,
  initial?: SpecsInitialValues,
  ports?: readonly MonitorPortValue[],
): SpecsFormGroup {
  const group = new FormGroup<Record<string, AbstractControl>>({});

  for (const field of specFieldsFor(specType)) {
    group.addControl(field.key, createControl(field, findInitialValue(initial, field.key)));
  }

  if (specType === 4) {
    group.addControl('ports', createPortsArray(ports));
  }

  return group;
}

/** Thêm một dòng cổng kết nối (chỉ dùng cho SpecType = 4 Monitor). */
export function addPortRow(group: SpecsFormGroup, portType = 'HDMI', quantity = 1): void {
  portsArrayOf(group)?.push(createPortGroup(portType, quantity));
}

/** Xoá một dòng cổng kết nối theo chỉ số. */
export function removePortRow(group: SpecsFormGroup, index: number): void {
  portsArrayOf(group)?.removeAt(index);
}

/** FormArray chứa các dòng cổng kết nối (null nếu không phải màn hình). */
export function portsArrayOf(group: SpecsFormGroup): FormArray<AbstractControl> | null {
  const control = group.get('ports');
  return control instanceof FormArray ? (control as FormArray<AbstractControl>) : null;
}

/** Chuẩn hoá `ProductDetailDto.monitorPorts` (khoá PortType/Quantity) sang dạng dùng cho form. */
export function toMonitorPortValues(rows: readonly Record<string, unknown>[]): MonitorPortValue[] {
  return rows
    .map((row) => ({
      portType: String(findInitialValue(row, 'portType') ?? 'HDMI'),
      quantity: Number(findInitialValue(row, 'quantity') ?? 1),
    }))
    .filter((row) => row.portType.length > 0);
}

/**
 * Dựng payload `specs` gửi lên API (đúng khoá camelCase mà `usp_Internal_SaveSpecs` đọc):
 * - bỏ các trường để trống (backend dùng ISNULL cho cột có mặc định);
 * - số gửi dạng number, boolean dạng true/false (KHÔNG gửi chuỗi, tránh lỗi ép kiểu của OPENJSON);
 * - Monitor kèm mảng `ports`.
 * Trả null khi danh mục là Generic (SpecType = 0) vì proc bỏ qua @SpecsJson trong trường hợp này.
 */
export function buildSpecsPayload(
  group: SpecsFormGroup,
  specType: SpecType,
): Record<string, unknown> | null {
  if (specType === 0) {
    return null;
  }

  const payload: Record<string, unknown> = {};

  for (const field of specFieldsFor(specType)) {
    const control = group.get(field.key);
    if (control === null) {
      continue;
    }

    const value: unknown = control.value;

    if (field.kind === 'boolean') {
      payload[field.key] = value === true;
      continue;
    }

    if (value === null || value === undefined || value === '') {
      continue;
    }

    payload[field.key] = field.kind === 'number' ? Number(value) : String(value);
  }

  if (specType === 4) {
    payload['ports'] = collectPorts(group);
  }

  return payload;
}

function collectPorts(group: SpecsFormGroup): MonitorPortValue[] {
  const array = portsArrayOf(group);
  if (array === null) {
    return [];
  }

  return array.controls
    .map((control) => ({
      portType: String(control.get('portType')?.value ?? '').trim(),
      quantity: Number(control.get('quantity')?.value ?? 1),
    }))
    .filter((row) => row.portType.length > 0);
}

function createControl(field: SpecFieldDef, value: unknown): AbstractControl {
  if (field.kind === 'boolean') {
    return new FormControl<boolean>(value === true);
  }

  if (field.kind === 'number') {
    const validators: ValidatorFn[] = [];

    if (field.required) {
      validators.push(Validators.required);
    }
    if (field.min !== undefined) {
      validators.push(Validators.min(field.min));
    }
    if (field.max !== undefined) {
      validators.push(Validators.max(field.max));
    }

    return new FormControl<number | null>(toNumberOrNull(value), validators);
  }

  return new FormControl<string>(toText(value), field.required ? [Validators.required] : []);
}

function createPortsArray(ports: readonly MonitorPortValue[] | undefined): FormArray<AbstractControl> {
  const array = new FormArray<AbstractControl>([]);

  for (const port of ports ?? []) {
    array.push(createPortGroup(port.portType, port.quantity));
  }

  return array;
}

function createPortGroup(portType: string, quantity: number): FormGroup<Record<string, AbstractControl>> {
  return new FormGroup<Record<string, AbstractControl>>({
    portType: new FormControl<string>(portType, [Validators.required]),
    quantity: new FormControl<number | null>(quantity, [Validators.required, Validators.min(1)]),
  });
}

/** Tìm giá trị khởi tạo theo tên trường, KHÔNG phân biệt hoa/thường (SQL trả "TdpW" còn form dùng "tdpW"). */
function findInitialValue(source: SpecsInitialValues, key: string): unknown {
  if (source === null || source === undefined) {
    return undefined;
  }

  const wanted = key.toLowerCase();

  for (const [name, value] of Object.entries(source)) {
    if (name.toLowerCase() === wanted) {
      return value;
    }
  }

  return undefined;
}

function toNumberOrNull(value: unknown): number | null {
  if (value === null || value === undefined || value === '') {
    return null;
  }

  const parsed = Number(value);

  return Number.isFinite(parsed) ? parsed : null;
}

function toText(value: unknown): string {
  return value === null || value === undefined ? '' : String(value);
}

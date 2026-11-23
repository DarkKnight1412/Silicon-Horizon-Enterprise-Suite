import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { ReactiveFormsModule, type AbstractControl } from '@angular/forms';
import { MONITOR_PORT_TYPES, specFieldsFor, type SpecType } from '../../../core/models/spec.model';
import { addPortRow, portsArrayOf, removePortRow, type SpecsFormGroup } from './spec-form.builder';

/**
 * Bộ trường thông số ĐỘNG theo `SpecType` của danh mục (mục 5 của yêu cầu):
 * Generic không có trường riêng; Monitor có thêm FormArray các cổng kết nối (portType, quantity).
 * Component nhận sẵn `SpecsFormGroup` từ form sản phẩm nên không tự tạo control —
 * form cha giữ quyền quyết định khi nào cần dựng lại nhóm (khi đổi danh mục).
 */
@Component({
  selector: 'app-spec-fields',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="space-y-4">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h3 class="text-sm font-semibold text-slate-700">{{ legend() }}</h3>
        @if (isMonitor()) {
          <button
            type="button"
            class="rounded-md border border-slate-300 px-3 py-1.5 text-xs font-medium text-slate-700 hover:bg-slate-50"
            (click)="addPort()"
          >
            + Thêm cổng kết nối
          </button>
        }
      </div>

      @if (fields().length === 0) {
        <p class="rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-500">
          Danh mục này không có bộ thông số riêng (SpecType = Generic).
        </p>
      } @else {
        <div [formGroup]="group()" class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          @for (field of fields(); track field.key) {
            <div>
              <label class="block text-sm font-medium text-slate-700" [attr.for]="'spec-' + field.key">
                {{ field.label }}
                @if (field.required) {
                  <span class="text-rose-600">*</span>
                }
              </label>

              @switch (field.kind) {
                @case ('select') {
                  <select
                    class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                    [class.sh-input-invalid]="fieldError(field.key) !== null"
                    [formControlName]="field.key"
                    [attr.id]="'spec-' + field.key"
                  >
                    <option value="">-- Chọn --</option>
                    @for (option of field.options ?? []; track option) {
                      <option [value]="option">{{ option }}</option>
                    }
                  </select>
                }
                @case ('boolean') {
                  <label class="mt-2 flex items-center gap-2 text-sm text-slate-700">
                    <input
                      type="checkbox"
                      class="h-4 w-4 rounded border-slate-300"
                      [formControlName]="field.key"
                      [attr.id]="'spec-' + field.key"
                    />
                    Có
                  </label>
                }
                @case ('number') {
                  <input
                    type="number"
                    class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                    [class.sh-input-invalid]="fieldError(field.key) !== null"
                    [formControlName]="field.key"
                    [attr.id]="'spec-' + field.key"
                    [attr.min]="field.min ?? null"
                    [attr.max]="field.max ?? null"
                    [attr.step]="field.step ?? 1"
                  />
                }
                @default {
                  <input
                    type="text"
                    class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
                    [class.sh-input-invalid]="fieldError(field.key) !== null"
                    [formControlName]="field.key"
                    [attr.id]="'spec-' + field.key"
                  />
                }
              }

              @if (field.hint) {
                <p class="mt-1 text-xs text-slate-500">{{ field.hint }}</p>
              }

              @if (fieldError(field.key); as message) {
                <p class="sh-field-error">{{ message }}</p>
              }
            </div>
          }
        </div>
      }
      @if (isMonitor()) {
        @if (ports(); as rows) {
          @if (rows.length === 0) {
            <p class="rounded-md bg-slate-50 px-3 py-2 text-sm text-slate-500">
              Màn hình chưa có cổng kết nối nào — bấm "Thêm cổng kết nối" nếu cần khai báo.
            </p>
          } @else {
            <div class="overflow-x-auto rounded-md border border-slate-200">
              <table class="min-w-full divide-y divide-slate-200 text-sm">
                <thead class="bg-slate-50 text-left text-xs uppercase text-slate-500">
                  <tr>
                    <th class="px-3 py-2">Cổng kết nối</th>
                    <th class="px-3 py-2">Số lượng</th>
                    <th class="px-3 py-2"></th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-slate-100">
                  @for (row of rows.controls; track $index) {
                    <tr [formGroup]="portRow(row)">
                      <td class="px-3 py-2">
                        <select
                          class="w-full rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                          formControlName="portType"
                        >
                          @for (portType of portTypes; track portType) {
                            <option [value]="portType">{{ portType }}</option>
                          }
                        </select>
                      </td>
                      <td class="px-3 py-2">
                        <input
                          type="number"
                          min="1"
                          class="w-24 rounded-md border border-slate-300 px-2 py-1.5 text-sm"
                          formControlName="quantity"
                        />
                      </td>
                      <td class="px-3 py-2 text-right">
                        <button
                          type="button"
                          class="rounded-md px-2 py-1 text-xs font-medium text-rose-600 hover:bg-rose-50"
                          (click)="removePort($index)"
                        >
                          Xoá
                        </button>
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        }
      }
    </div>
  `,
})
export class SpecFields {
  /** Nhóm control động của thông số (form sản phẩm tạo bằng createSpecsFormGroup). */
  readonly group = input.required<SpecsFormGroup>();

  /** Loại thông số của danh mục đang chọn. */
  readonly specType = input.required<SpecType>();

  /** Tiêu đề khối thông số. */
  readonly legend = input('Thông số kỹ thuật');

  protected readonly fields = computed(() => specFieldsFor(this.specType()));
  protected readonly portTypes = MONITOR_PORT_TYPES;
  protected readonly isMonitor = computed(() => this.specType() === 4);
  protected readonly ports = computed(() => portsArrayOf(this.group()));

  protected control(key: string): AbstractControl | null {
    return this.group().get(key);
  }

  /** Ép kiểu control của một dòng cổng kết nối về FormGroup để template dùng [formGroup]. */
  protected portRow(control: AbstractControl) {
    return control as SpecsFormGroup;
  }

  /** Thông điệp lỗi của một trường (chỉ hiện khi người dùng đã chạm vào ô). */
  protected fieldError(key: string): string | null {
    const control = this.control(key);

    if (control === null || !control.invalid || (!control.dirty && !control.touched)) {
      return null;
    }

    const errors: Record<string, unknown> = control.errors ?? {};

    if (errors['required'] !== undefined) {
      return 'Trường này là bắt buộc.';
    }
    if (errors['min'] !== undefined) {
      return `Giá trị nhỏ nhất là ${String((errors['min'] as { min?: number }).min ?? '')}.`;
    }
    if (errors['max'] !== undefined) {
      return `Giá trị lớn nhất là ${String((errors['max'] as { max?: number }).max ?? '')}.`;
    }

    return 'Giá trị không hợp lệ.';
  }

  protected addPort(): void {
    addPortRow(this.group());
  }

  protected removePort(index: number): void {
    removePortRow(this.group(), index);
  }
}



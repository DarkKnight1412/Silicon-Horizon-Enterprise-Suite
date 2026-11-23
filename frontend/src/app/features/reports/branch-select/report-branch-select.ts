import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import type { BranchDto } from '../../../core/models/category.model';

/**
 * Ô chọn chi nhánh dùng chung cho các màn BÁO CÁO.
 *
 * ⚠️ CHỈ Admin mới thấy ô này: `GET /api/v1/admin/branches` dùng `[Authorize(Roles = Admin)]`, còn
 * Quản lý chi nhánh / Thủ kho bị backend ép `branchId` từ token (BRD 2.2) — hiển thị ô chọn cho họ
 * sẽ gây 403 khi nạp danh sách chi nhánh.
 *
 * Component chỉ TRÌNH BÀY (giống `app-product-picker`): cha sở hữu `FormControl` và danh sách chi nhánh.
 */
@Component({
  selector: 'app-report-branch-select',
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div>
      <label class="block text-sm font-medium text-slate-700" [attr.for]="controlId()">
        Chi nhánh (chỉ Admin lọc được)
      </label>
      <select
        [id]="controlId()"
        [formControl]="control()"
        class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
      >
        <option [ngValue]="null">Toàn hệ thống</option>
        @for (branch of branches(); track branch.id) {
          <option [ngValue]="branch.id">{{ branch.code }} · {{ branch.name }}</option>
        }
      </select>
    </div>
  `,
})
export class ReportBranchSelect {
  /** Control do cha sở hữu (giá trị `number | null`; `null` = toàn hệ thống). */
  readonly control = input.required<FormControl<number | null>>();
  readonly branches = input.required<readonly BranchDto[]>();
  /** Id của `<select>` để `<label for>` trỏ đúng. */
  readonly controlId = input('reportBranchId');
}

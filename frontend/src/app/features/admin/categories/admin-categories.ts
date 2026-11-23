import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import type { CategoryDto, SetCategoryStatusRequest } from '../../../core/models/category.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { SPEC_TYPE_LABELS, SPEC_TYPE_OPTIONS } from '../../../core/models/spec.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { AdminApi } from '../admin-api.service';

/**
 * Quản trị danh mục sản phẩm (dbo.Categories).
 *
 * Lưu ý: backend hiện chỉ có GET (phân trang), POST và PATCH trạng thái — KHÔNG có PUT nên màn hình
 * chỉ cho tạo mới và bật/ngừng hoạt động. `SpecType` quyết định bộ thông số hiển thị ở form sản phẩm
 * nên chọn sai sẽ khiến sản phẩm không có trường thông số phù hợp.
 */
@Component({
  selector: 'app-admin-categories',
  imports: [ReactiveFormsModule, RouterLink, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="space-y-4">
      <header class="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 class="sh-page-title">Danh mục sản phẩm</h1>
          <p class="text-sm text-slate-500">{{ result().totalCount }} danh mục</p>
        </div>
        <a
          routerLink="/admin/products"
          class="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
        >
          ← Quản trị sản phẩm
        </a>
      </header>

      <form
        class="sh-card grid gap-3 p-4 sm:grid-cols-2 lg:grid-cols-5"
        [formGroup]="form"
        (ngSubmit)="create()"
      >
        <div>
          <label class="block text-sm font-medium text-slate-700" for="code">
            Mã danh mục <span class="text-rose-600">*</span>
          </label>
          <input
            id="code"
            type="text"
            placeholder="MAINBOARD"
            formControlName="code"
            class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 font-mono text-sm uppercase"
          />
          @if (form.controls.code.touched && form.controls.code.invalid) {
            <p class="sh-field-error">Mã gồm chữ HOA, số, dấu gạch dưới (2–30 ký tự).</p>
          }
        </div>

        <div class="lg:col-span-2">
          <label class="block text-sm font-medium text-slate-700" for="name">
            Tên danh mục <span class="text-rose-600">*</span>
          </label>
          <input
            id="name"
            type="text"
            formControlName="name"
            class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
          @if (form.controls.name.touched && form.controls.name.invalid) {
            <p class="sh-field-error">Tên danh mục là bắt buộc (tối đa 100 ký tự).</p>
          }
        </div>

        <div>
          <label class="block text-sm font-medium text-slate-700" for="specType">
            Bộ thông số <span class="text-rose-600">*</span>
          </label>
          <select
            id="specType"
            formControlName="specType"
            class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          >
            <option [ngValue]="null">-- Chọn --</option>
            @for (option of specTypeOptions; track option.value) {
              <option [ngValue]="option.value">{{ option.label }}</option>
            }
          </select>
        </div>

        <div class="flex items-end">
          <button
            type="submit"
            class="w-full rounded-md bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
            [disabled]="submitting()"
          >
            {{ submitting() ? 'Đang lưu…' : '+ Thêm danh mục' }}
          </button>
        </div>
      </form>

      @if (loading()) {
        <div class="sh-card p-8 text-center text-sm text-slate-500">Đang tải danh mục…</div>
      } @else {
        <div class="sh-card overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th class="px-3 py-2">Mã</th>
                <th class="px-3 py-2">Tên</th>
                <th class="px-3 py-2">Bộ thông số</th>
                <th class="px-3 py-2">Trạng thái</th>
                <th class="px-3 py-2 text-right"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (category of result().items; track category.id) {
                <tr>
                  <td class="px-3 py-2 font-mono text-xs text-slate-600">{{ category.code }}</td>
                  <td class="px-3 py-2 font-medium text-slate-800">{{ category.name }}</td>
                  <td class="px-3 py-2 text-slate-600">{{ specTypeLabel(category.specType) }}</td>
                  <td class="px-3 py-2">
                    <span
                      class="rounded-full px-2 py-1 text-xs font-medium"
                      [class]="
                        category.isActive
                          ? 'bg-emerald-100 text-emerald-800'
                          : 'bg-slate-200 text-slate-700'
                      "
                    >
                      {{ category.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                    </span>
                  </td>
                  <td class="px-3 py-2 text-right">
                    <button
                      type="button"
                      class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50"
                      (click)="toggleStatus(category)"
                    >
                      {{ category.isActive ? 'Ngừng dùng' : 'Cho dùng lại' }}
                    </button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        <div class="sh-card">
          <app-pagination
            [pageNumber]="result().pageNumber"
            [pageSize]="result().pageSize"
            [totalCount]="result().totalCount"
            [totalPages]="result().totalPages"
            (pageChange)="load($event, result().pageSize)"
            (pageSizeChange)="load(1, $event)"
          />
        </div>
      }
    </section>
  `,
})
export class AdminCategories {
  private readonly adminApi = inject(AdminApi);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly specTypeOptions = SPEC_TYPE_OPTIONS;
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly result = signal<PagedResult<CategoryDto>>(emptyPagedResult<CategoryDto>(20));

  protected readonly form = new FormGroup({
    code: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/^[A-Z0-9_]{2,30}$/)],
    }),
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    specType: new FormControl<number | null>(null, { validators: [Validators.required] }),
  });

  constructor() {
    this.load(1, 20);
  }

  protected load(pageNumber: number, pageSize: number): void {
    this.loading.set(true);

    this.adminApi.listCategories({ pageNumber, pageSize }).subscribe({
      next: (page) => {
        this.result.set(page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected create(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const specType = value.specType;

    if (specType === null) {
      return;
    }

    this.submitting.set(true);

    this.adminApi
      .createCategory({
        code: value.code.trim().toUpperCase(),
        name: value.name.trim(),
        specType,
        parentId: null,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.notifications.success('Đã thêm danh mục', value.name.trim());
          this.form.reset({ code: '', name: '', specType: null });
          this.load(1, this.result().pageSize);
        },
        error: () => this.submitting.set(false),
      });
  }

  protected async toggleStatus(category: CategoryDto): Promise<void> {
    const request: SetCategoryStatusRequest = { isActive: !category.isActive };

    const accepted = await this.confirm.ask({
      title: request.isActive ? 'Cho dùng lại danh mục' : 'Ngừng dùng danh mục',
      message: `${category.name} — danh mục ngừng dùng sẽ không chọn được khi tạo sản phẩm mới.`,
      confirmLabel: 'Xác nhận',
      danger: !request.isActive,
    });

    if (!accepted) {
      return;
    }

    this.adminApi.setCategoryStatus(category.id, request).subscribe({
      next: () => {
        this.notifications.success('Đã cập nhật danh mục', category.name);
        this.load(this.result().pageNumber, this.result().pageSize);
      },
    });
  }

  protected specTypeLabel(specType: number): string {
    return SPEC_TYPE_LABELS[specType] ?? '—';
  }
}


import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { of } from 'rxjs';
import type { BrandDto } from '../../../core/models/category.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { uniqueCodeValidator } from '../../../shared/validators/unique-code.validator';
import { AdminApi } from '../admin-api.service';

/**
 * Quản trị thương hiệu (dbo.Brands) — có đủ CRUD: tạo, sửa tên (PUT), đổi trạng thái (PATCH).
 * "Xoá" thương hiệu = đặt `isActive = false` vì thương hiệu đã gắn với sản phẩm.
 *
 * Validator `uniqueCodeValidator` được dùng lại cho tên thương hiệu: kiểm tra trùng ngay trên danh
 * sách đã tải (không cần endpoint riêng) và backend vẫn là nơi kiểm tra cuối cùng (trùng → 409).
 */
@Component({
  selector: 'app-admin-brands',
  imports: [ReactiveFormsModule, RouterLink, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="space-y-4">
      <header class="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h1 class="sh-page-title">Thương hiệu</h1>
          <p class="text-sm text-slate-500">{{ result().totalCount }} thương hiệu</p>
        </div>
        <a
          routerLink="/admin/products"
          class="rounded-md border border-slate-300 px-3 py-1.5 text-sm text-slate-700 hover:bg-slate-50"
        >
          ← Quản trị sản phẩm
        </a>
      </header>

      <div class="sh-card flex flex-wrap items-end gap-3 p-4">
        <div class="min-w-56 flex-1">
          <label class="block text-sm font-medium text-slate-700" for="keyword">Tìm thương hiệu</label>
          <input
            id="keyword"
            type="search"
            [formControl]="keyword"
            (keyup.enter)="load(1, result().pageSize)"
            class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
          />
        </div>
        <button
          type="button"
          class="rounded-md border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-50"
          (click)="load(1, result().pageSize)"
        >
          Tìm
        </button>
      </div>

      <form class="sh-card flex flex-wrap items-end gap-3 p-4" [formGroup]="createForm" (ngSubmit)="create()">
        <div class="min-w-56 flex-1">
          <label class="block text-sm font-medium text-slate-700" for="brandName">
            Tên thương hiệu mới <span class="text-rose-600">*</span>
          </label>
          <input
            id="brandName"
            type="text"
            formControlName="name"
            class="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm"
            [class.sh-input-invalid]="createForm.controls.name.touched && createForm.controls.name.invalid"
          />
          @if (createForm.controls.name.touched && createForm.controls.name.invalid) {
            <p class="sh-field-error">
              @if (createForm.controls.name.errors?.['codeTaken']) {
                Thương hiệu này đã tồn tại.
              } @else {
                Tên thương hiệu là bắt buộc (tối đa 100 ký tự).
              }
            </p>
          }
        </div>
        <button
          type="submit"
          class="rounded-md bg-brand-600 px-4 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
          [disabled]="submitting()"
        >
          {{ submitting() ? 'Đang lưu…' : '+ Thêm thương hiệu' }}
        </button>
      </form>


      @if (loading()) {
        <div class="sh-card p-8 text-center text-sm text-slate-500">Đang tải thương hiệu…</div>
      } @else {
        <div class="sh-card overflow-x-auto">
          <table class="min-w-full divide-y divide-slate-200 text-sm">
            <thead class="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th class="px-3 py-2">#</th>
                <th class="px-3 py-2">Tên thương hiệu</th>
                <th class="px-3 py-2">Trạng thái</th>
                <th class="px-3 py-2 text-right"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-slate-100">
              @for (brand of result().items; track brand.id) {
                <tr>
                  <td class="px-3 py-2 text-slate-500">{{ brand.id }}</td>
                  <td class="px-3 py-2">
                    @if (editingId() === brand.id) {
                      <input
                        type="text"
                        [formControl]="editName"
                        class="w-full rounded-md border border-slate-300 px-2 py-1 text-sm"
                      />
                    } @else {
                      <span class="font-medium text-slate-800">{{ brand.name }}</span>
                    }
                  </td>
                  <td class="px-3 py-2">
                    <span
                      class="rounded-full px-2 py-1 text-xs font-medium"
                      [class]="brand.isActive ? 'bg-emerald-100 text-emerald-800' : 'bg-slate-200 text-slate-700'"
                    >
                      {{ brand.isActive ? 'Đang dùng' : 'Ngừng dùng' }}
                    </span>
                  </td>
                  <td class="px-3 py-2 text-right">
                    <div class="flex justify-end gap-2">
                      @if (editingId() === brand.id) {
                        <button
                          type="button"
                          class="rounded-md bg-brand-600 px-2 py-1 text-xs font-medium text-white hover:bg-brand-700"
                          (click)="saveName(brand)"
                        >
                          Lưu
                        </button>
                        <button
                          type="button"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50"
                          (click)="cancelEdit()"
                        >
                          Huỷ
                        </button>
                      } @else {
                        <button
                          type="button"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50"
                          (click)="startEdit(brand)"
                        >
                          Sửa tên
                        </button>
                        <button
                          type="button"
                          class="rounded-md border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-50"
                          (click)="toggleStatus(brand)"
                        >
                          {{ brand.isActive ? 'Ngừng dùng' : 'Cho dùng lại' }}
                        </button>
                      }
                    </div>
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
export class AdminBrands {
  private readonly adminApi = inject(AdminApi);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);

  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly editingId = signal<number | null>(null);
  protected readonly result = signal<PagedResult<BrandDto>>(emptyPagedResult<BrandDto>(20));

  protected readonly keyword = new FormControl('', { nonNullable: true });
  protected readonly editName = new FormControl('', { nonNullable: true });

  protected readonly createForm = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
  });

  constructor() {
    // Chống trùng tên ngay trên danh sách đã tải (backend vẫn kiểm tra lần cuối → 409).
    this.createForm.controls.name.addAsyncValidators(
      uniqueCodeValidator((value) =>
        of(!this.result().items.some((brand) => brand.name.toLowerCase() === value.toLowerCase())),
      ),
    );

    this.load(1, 20);
  }

  protected load(pageNumber: number, pageSize: number): void {
    this.loading.set(true);

    const keyword = this.keyword.value.trim();

    this.adminApi
      .listBrands({
        keyword: keyword.length > 0 ? keyword : null,
        pageNumber,
        pageSize,
      })
      .subscribe({
        next: (page) => {
          this.result.set(page);
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }

  protected create(): void {
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    const name = this.createForm.controls.name.value.trim();
    this.submitting.set(true);

    this.adminApi.createBrand({ name }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.notifications.success('Đã thêm thương hiệu', name);
        this.createForm.reset({ name: '' });
        this.load(1, this.result().pageSize);
      },
      error: () => this.submitting.set(false),
    });
  }

  protected startEdit(brand: BrandDto): void {
    this.editName.setValue(brand.name);
    this.editingId.set(brand.id);
  }

  protected cancelEdit(): void {
    this.editingId.set(null);
  }

  protected saveName(brand: BrandDto): void {
    const name = this.editName.value.trim();

    if (name.length === 0 || name === brand.name) {
      this.cancelEdit();
      return;
    }

    this.adminApi.updateBrand(brand.id, { name }).subscribe({
      next: () => {
        this.editingId.set(null);
        this.notifications.success('Đã đổi tên thương hiệu', name);
        this.load(this.result().pageNumber, this.result().pageSize);
      },
    });
  }

  protected async toggleStatus(brand: BrandDto): Promise<void> {
    const isActive = !brand.isActive;

    const accepted = await this.confirm.ask({
      title: isActive ? 'Cho dùng lại thương hiệu' : 'Ngừng dùng thương hiệu',
      message: `${brand.name} — thương hiệu ngừng dùng sẽ không chọn được khi tạo sản phẩm mới.`,
      confirmLabel: 'Xác nhận',
      danger: !isActive,
    });

    if (!accepted) {
      return;
    }

    this.adminApi.setBrandStatus(brand.id, { isActive }).subscribe({
      next: () => {
        this.notifications.success('Đã cập nhật thương hiệu', brand.name);
        this.load(this.result().pageNumber, this.result().pageSize);
      },
    });
  }
}

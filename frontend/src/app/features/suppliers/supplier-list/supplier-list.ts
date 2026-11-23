import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { ROLES } from '../../../core/models/auth.model';
import { formatDateTime } from '../../../core/models/common.model';
import { emptyPagedResult, type PagedResult } from '../../../core/models/paged-result.model';
import { ConfirmService } from '../../../core/services/confirm.service';
import { NotificationService } from '../../../core/services/notification.service';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { Pagination } from '../../../shared/components/pagination/pagination';
import { SupplierApi } from '../supplier-api.service';
import {
  supplierActiveLabel,
  type CreateSupplierRequest,
  type SupplierDto,
  type UpdateSupplierRequest,
} from '../supplier.model';

/**
 * Danh mục nhà cung cấp (`dbo.Suppliers`) — Database/README.md mục 6, SUP-01.
 *
 * Quyền (đọc từ SuppliersController):
 * - Xem danh sách: Thủ kho + Quản lý + Admin.
 * - Tạo/sửa: CHỈ Admin ⇒ form tạo/sửa chỉ hiện khi `isAdmin()`; vai trò khác xem ở chế độ chỉ đọc.
 * - "Ngừng hợp tác" = `isActive = false` bằng PUT (không xoá cứng vì NCC đã gắn phiếu nhập/công nợ);
 *   NCC ngừng hoạt động sẽ bị `usp_PurchaseReceipt_Create` từ chối.
 */
@Component({
  selector: 'app-supplier-list',
  imports: [ReactiveFormsModule, RouterLink, EmptyState, Pagination],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './supplier-list.html',
})
export class SupplierList {
  private readonly supplierApi = inject(SupplierApi);
  private readonly authStore = inject(AuthStore);
  private readonly confirm = inject(ConfirmService);
  private readonly notifications = inject(NotificationService);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly formatMoment = formatDateTime;
  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly result = signal<PagedResult<SupplierDto>>(emptyPagedResult<SupplierDto>(20));

  /** NCC đang sửa (null = đang tạo mới). */
  protected readonly editingId = signal<number | null>(null);

  protected readonly isAdmin = computed(() => this.authStore.hasRole(ROLES.Admin));

  protected readonly filters = new FormGroup({
    keyword: new FormControl('', { nonNullable: true }),
    isActive: new FormControl<boolean | null>(null),
  });

  protected readonly form = this.fb.group({
    code: this.fb.control('', {
      validators: [Validators.required, Validators.pattern(/^[A-Z0-9_-]{2,30}$/)],
    }),
    name: this.fb.control('', { validators: [Validators.required, Validators.maxLength(200)] }),
    taxCode: this.fb.control('', { validators: [Validators.maxLength(20)] }),
    phone: this.fb.control('', { validators: [Validators.maxLength(20)] }),
    address: this.fb.control('', { validators: [Validators.maxLength(300)] }),
    paymentTermDays: this.fb.control(0, {
      validators: [Validators.min(0), Validators.max(365)],
    }),
    isActive: this.fb.control(true),
  });

  constructor() {
    this.load(1, this.result().pageSize);
  }

  protected load(pageNumber: number, pageSize: number): void {
    const value = this.filters.getRawValue();
    const keyword = value.keyword.trim();

    this.loading.set(true);

    this.supplierApi
      .listSuppliers({
        keyword: keyword.length > 0 ? keyword : null,
        isActive: value.isActive,
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

  protected resetFilters(): void {
    this.filters.reset({ keyword: '', isActive: null });
    this.load(1, this.result().pageSize);
  }

  protected activeLabel(isActive: boolean): string {
    return supplierActiveLabel(isActive);
  }

  /** Tạo mới hoặc cập nhật tuỳ `editingId` (PUT không đổi được mã NCC). */
  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const editingId = this.editingId();

    this.submitting.set(true);

    if (editingId === null) {
      const request: CreateSupplierRequest = {
        code: value.code.trim().toUpperCase(),
        name: value.name.trim(),
        taxCode: normalize(value.taxCode),
        phone: normalize(value.phone),
        address: normalize(value.address),
        paymentTermDays: value.paymentTermDays,
      };

      this.supplierApi.createSupplier(request).subscribe({
        next: () => {
          this.submitting.set(false);
          this.notifications.success('Đã thêm nhà cung cấp', request.name);
          this.resetForm();
        },
        error: () => this.submitting.set(false),
      });
      return;
    }

    const request: UpdateSupplierRequest = {
      name: value.name.trim(),
      taxCode: normalize(value.taxCode),
      phone: normalize(value.phone),
      address: normalize(value.address),
      paymentTermDays: value.paymentTermDays,
      isActive: value.isActive,
    };

    this.supplierApi.updateSupplier(editingId, request).subscribe({
      next: () => {
        this.submitting.set(false);
        this.notifications.success('Đã cập nhật nhà cung cấp', request.name);
        this.resetForm();
      },
      error: () => this.submitting.set(false),
    });
  }

  protected startEdit(supplier: SupplierDto): void {
    this.editingId.set(supplier.id);
    this.form.reset({
      code: supplier.code,
      name: supplier.name,
      taxCode: supplier.taxCode ?? '',
      phone: supplier.phone ?? '',
      address: supplier.address ?? '',
      paymentTermDays: supplier.paymentTermDays,
      isActive: supplier.isActive,
    });
  }

  protected cancelEdit(): void {
    this.resetForm();
  }

  /** Ngừng/cho hợp tác lại — PUT cần toàn bộ trường nên gửi lại thông tin hiện có. */
  protected async toggleActive(supplier: SupplierDto): Promise<void> {
    const isActive = !supplier.isActive;

    const accepted = await this.confirm.ask({
      title: isActive ? 'Cho hợp tác lại' : 'Ngừng hợp tác',
      message: isActive
        ? `${supplier.name} sẽ chọn được khi lập phiếu nhập kho.`
        : `${supplier.name} sẽ bị từ chối khi lập phiếu nhập mới (phiếu và công nợ cũ giữ nguyên).`,
      confirmLabel: 'Xác nhận',
      danger: !isActive,
    });

    if (!accepted) {
      return;
    }

    this.supplierApi
      .updateSupplier(supplier.id, {
        name: supplier.name,
        taxCode: supplier.taxCode,
        phone: supplier.phone,
        address: supplier.address,
        paymentTermDays: supplier.paymentTermDays,
        isActive,
      })
      .subscribe({
        next: () => {
          this.notifications.success('Đã cập nhật nhà cung cấp', supplier.name);
          this.load(this.result().pageNumber, this.result().pageSize);
        },
      });
  }

  private resetForm(): void {
    this.editingId.set(null);
    this.form.reset({
      code: '',
      name: '',
      taxCode: '',
      phone: '',
      address: '',
      paymentTermDays: 0,
      isActive: true,
    });
    this.load(this.result().pageNumber, this.result().pageSize);
  }
}

/** Ô văn bản rỗng ⇒ gửi `null` để backend lưu NULL thay vì chuỗi trắng. */
function normalize(value: string): string | null {
  const trimmed = value.trim();
  return trimmed.length > 0 ? trimmed : null;
}

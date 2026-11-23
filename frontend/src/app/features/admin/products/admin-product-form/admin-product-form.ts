import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import {
  FormArray,
  FormControl,
  FormGroup,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
  type ValidationErrors,
} from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { isApiError, type ApiError } from '../../../../core/models/api-error.model';
import type { CategoryDto } from '../../../../core/models/category.model';
import { formatVnd, slugify } from '../../../../core/models/common.model';
import {
  PRODUCT_STATUS_LABELS,
  PRODUCT_STATUS_OPTIONS,
  type CreateProductRequest,
  type ProductDetailDto,
  type ProductImageInput,
  type ReferenceItemDto,
  type UpdateProductRequest,
} from '../../../../core/models/product.model';
import {
  SPEC_TYPE_LABELS,
  toSpecType,
  type SpecType,
} from '../../../../core/models/spec.model';
import { PRODUCT_STATUS_CLASSES } from '../../../../core/models/status-classes';
import { NotificationService } from '../../../../core/services/notification.service';
import { SpecFields } from '../../../../shared/components/spec-fields/spec-fields';
import {
  buildSpecsPayload,
  createSpecsFormGroup,
  toMonitorPortValues,
  type SpecsFormGroup,
} from '../../../../shared/components/spec-fields/spec-form.builder';
import { priceValidator } from '../../../../shared/validators/price.validator';
import { slugValidator } from '../../../../shared/validators/slug.validator';
import { uniqueCodeValidator } from '../../../../shared/validators/unique-code.validator';
import { AdminApi } from '../../admin-api.service';
import { CatalogApi } from '../../../catalog/catalog-api.service';
import type { PendingChangesAware } from '../../pending-changes.guard';

/** Nhóm control của một ảnh sản phẩm (ProductImages). */
type ImageFormGroup = FormGroup<{
  url: FormControl<string>;
  sortOrder: FormControl<number | null>;
  isPrimary: FormControl<boolean>;
}>;

/**
 * Form sản phẩm ĐỘNG theo `SpecType` của danh mục (UC-25, PRD-01/02).
 *
 * Điểm chính:
 * - Typed reactive forms cho các trường cố định; nhóm thông số kỹ thuật là form ĐỘNG
 *   (`createSpecsFormGroup`) dựng lại mỗi khi đổi danh mục, giữ lại các giá trị trùng tên.
 * - Monitor (SpecType = 4) có thêm FormArray `ports: [{portType, quantity}]`.
 * - Ảnh là FormArray (url, sortOrder, isPrimary).
 * - `hasUnsavedChanges()` phục vụ `pendingChangesGuard` (canDeactivate) — chống mất dữ liệu.
 * - Lỗi 400 từ ProblemDetails được map về ĐÚNG field (kể cả field động `specs.socket`).
 */
@Component({
  selector: 'app-admin-product-form',
  imports: [ReactiveFormsModule, RouterLink, SpecFields],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-product-form.html',
})
export class AdminProductForm implements PendingChangesAware {
  private readonly catalog = inject(CatalogApi);
  private readonly adminApi = inject(AdminApi);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly fb = inject(NonNullableFormBuilder);

  /** Route param `:id` (bind qua withComponentInputBinding); rỗng = tạo mới. */
  readonly id = input('');

  protected readonly isEdit = computed(() => this.id().length > 0);
  protected readonly productId = computed(() => {
    const parsed = Number(this.id());
    return Number.isFinite(parsed) && parsed > 0 ? parsed : null;
  });

  protected readonly loading = signal(false);
  protected readonly submitting = signal(false);
  protected readonly apiError = signal<ApiError | null>(null);

  protected readonly categories = signal<CategoryDto[]>([]);
  protected readonly brands = signal<ReferenceItemDto[]>([]);
  protected readonly statusOptions = PRODUCT_STATUS_OPTIONS;
  protected readonly formatPrice = formatVnd;

  /** Loại thông số của danh mục đang chọn + nhóm form động tương ứng. */
  protected readonly specType = signal<SpecType>(0);
  protected readonly specs = signal<SpecsFormGroup>(createSpecsFormGroup(0));

  /** Ảnh sản phẩm (FormArray). */
  protected readonly images = new FormArray<ImageFormGroup>([]);

  /** Trạng thái hiện tại của sản phẩm khi sửa (để hiện badge). */
  protected readonly currentStatus = signal<number | null>(null);

  /** Theo dõi thay đổi chưa lưu cho canDeactivate. */
  private readonly dirty = signal(false);
  private readonly saved = signal(false);

  /** Form chính (typed reactive form) — các trường cố định của Products. */
  protected readonly form = this.fb.group({
    sku: this.fb.control('', {
      validators: [Validators.required, Validators.maxLength(50)],
    }),
    name: this.fb.control('', { validators: [Validators.required, Validators.maxLength(250)] }),
    slug: this.fb.control('', { validators: [Validators.required, slugValidator()] }),
    brandId: this.fb.control<number | null>(null, { validators: [Validators.required] }),
    categoryId: this.fb.control<number | null>(null, { validators: [Validators.required] }),
    shortDescription: this.fb.control('', { validators: [Validators.maxLength(500)] }),
    description: this.fb.control(''),
    listPrice: this.fb.control<number | null>(null, {
      validators: [Validators.required, priceValidator({ min: 0, max: 1_000_000_000 })],
    }),
    lastImportPrice: this.fb.control<number | null>(null, {
      validators: [priceValidator({ min: 0, max: 1_000_000_000 })],
    }),
    requiresSerial: this.fb.control(true),
    minStockDefault: this.fb.control(0, { validators: [Validators.min(0)] }),
    warrantyMonths: this.fb.control(0, { validators: [Validators.min(0), Validators.max(120)] }),
    warrantyTerms: this.fb.control('', { validators: [Validators.maxLength(1000)] }),
    status: this.fb.control(0),
  });

  protected readonly specTypeLabel = computed(
    () => SPEC_TYPE_LABELS[this.specType()] ?? 'Thông số chung',
  );

  protected readonly statusLabel = computed(() => {
    const status = this.currentStatus();
    return status === null ? null : (PRODUCT_STATUS_LABELS[status] ?? null);
  });

  protected readonly statusClass = computed(() => {
    const status = this.currentStatus();
    return status === null ? '' : (PRODUCT_STATUS_CLASSES[status] ?? '');
  });

  constructor() {
    this.loadLookups();

    // Đánh dấu form "có thay đổi" khi bất kỳ control nào đổi giá trị.
    this.form.valueChanges.subscribe(() => this.dirty.set(true));

    // Đổi danh mục ⇒ dựng lại nhóm thông số theo SpecType của danh mục mới.
    this.form.controls.categoryId.valueChanges.subscribe((categoryId) => {
      if (categoryId !== null) {
        this.applyCategory(categoryId, null, null);
      }
    });

    // Sửa sản phẩm: gắn thêm validator bất đồng bộ chống trùng SKU là KHÔNG cần (SKU không đổi được),
    // nhưng khi tạo mới thì cần kiểm tra trùng trước khi gửi lên backend.
    if (!this.isEdit()) {
      this.form.controls.sku.addAsyncValidators(
        uniqueCodeValidator((value) => this.catalog.isSkuAvailable(value)),
      );
    }

    const id = this.productId();
    if (id !== null) {
      this.loadProduct(id);
    }
  }

  /** Chỉ báo cho canDeactivate: form đã thay đổi và chưa lưu thành công. */
  hasUnsavedChanges(): boolean {
    return this.dirty() && !this.saved();
  }

  /** Gợi ý slug từ tên sản phẩm (bỏ dấu tiếng Việt) — người dùng vẫn sửa lại được. */
  protected suggestSlug(): void {
    const name = this.form.controls.name.value.trim();

    if (name.length > 0) {
      this.form.controls.slug.setValue(slugify(name));
    }
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Ảnh sản phẩm (FormArray)
  // ───────────────────────────────────────────────────────────────────────────

  protected addImage(): void {
    this.images.push(this.createImageGroup());
    this.dirty.set(true);
  }

  protected removeImage(index: number): void {
    this.images.removeAt(index);
    this.dirty.set(true);
  }

  protected imageGroupAt(index: number): ImageFormGroup {
    return this.images.at(index);
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Lưu
  // ───────────────────────────────────────────────────────────────────────────

  protected submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.notifications.warning(
        'Dữ liệu chưa hợp lệ',
        'Vui lòng kiểm tra các trường được đánh dấu đỏ trước khi lưu.',
      );
      return;
    }

    const value = this.form.getRawValue();
    const brandId = value.brandId;
    const categoryId = value.categoryId;

    if (brandId === null) {
      return;
    }

    const specs = buildSpecsPayload(this.specs(), this.specType());
    const images = this.buildImages();

    this.submitting.set(true);
    this.apiError.set(null);

    if (this.isEdit()) {
      const id = this.productId();
      if (id === null) {
        this.submitting.set(false);
        return;
      }

      const request: UpdateProductRequest = {
        name: value.name.trim(),
        slug: value.slug.trim(),
        brandId,
        shortDescription: optional(value.shortDescription),
        description: optional(value.description),
        listPrice: value.listPrice ?? 0,
        requiresSerial: value.requiresSerial,
        minStockDefault: value.minStockDefault,
        warrantyMonths: value.warrantyMonths,
        warrantyTerms: optional(value.warrantyTerms),
        specs,
        images,
      };

      this.catalog.updateProduct(id, request).subscribe({
        next: () => {
          this.notifications.success('Đã lưu sản phẩm', value.name.trim());
          this.afterSave(id);
        },
        error: (error: unknown) => this.handleError(error),
      });

      return;
    }

    if (categoryId === null) {
      this.submitting.set(false);
      return;
    }

    const request: CreateProductRequest = {
      sku: value.sku.trim().toUpperCase(),
      name: value.name.trim(),
      slug: value.slug.trim(),
      brandId,
      categoryId,
      shortDescription: optional(value.shortDescription),
      description: optional(value.description),
      listPrice: value.listPrice ?? 0,
      lastImportPrice: value.lastImportPrice,
      requiresSerial: value.requiresSerial,
      minStockDefault: value.minStockDefault,
      warrantyMonths: value.warrantyMonths,
      warrantyTerms: optional(value.warrantyTerms),
      status: value.status,
      specs,
      images,
    };

    this.catalog.createProduct(request).subscribe({
      next: (created) => {
        this.notifications.success('Đã tạo sản phẩm', `Mã sản phẩm #${created.productId}`);
        this.afterSave(created.productId);
      },
      error: (error: unknown) => this.handleError(error),
    });
  }

  /** Lỗi từ server hiển thị ngay dưới trường tương ứng (map từ ProblemDetails.errors). */
  protected serverError(field: string): string | null {
    const control = this.form.get(field);
    const errors: ValidationErrors | null = control === null ? null : control.errors;
    const message: unknown = errors === null ? undefined : errors['server'];

    return typeof message === 'string' ? message : null;
  }

  // ───────────────────────────────────────────────────────────────────────────
  // Nạp dữ liệu
  // ───────────────────────────────────────────────────────────────────────────

  /** Danh mục (cần `specType`) và thương hiệu cho form. */
  private loadLookups(): void {
    this.adminApi.listCategories({ pageNumber: 1, pageSize: 200 }).subscribe({
      next: (page) => this.categories.set(page.items),
      error: () => this.categories.set([]),
    });

    this.catalog.getBrands().subscribe({
      next: (items) => this.brands.set(items),
      error: () => this.brands.set([]),
    });
  }

  /**
   * Áp SpecType của danh mục: dựng lại nhóm thông số.
   * - Khi người dùng ĐỔI danh mục: giữ lại giá trị của các trường trùng tên giữa hai bộ thông số.
   * - Khi nạp sản phẩm để SỬA: truyền `initialSpecs`/`initialPorts` từ ProductDetailDto.
   */
  private applyCategory(
    categoryId: number,
    initialSpecs: Record<string, unknown> | null,
    initialPorts: readonly Record<string, unknown>[] | null,
  ): void {
    const category = this.categories().find((item) => item.id === categoryId);
    const nextType = toSpecType(category?.specType ?? 0);

    if (initialSpecs === null && nextType === this.specType()) {
      return;
    }

    const kept = initialSpecs ?? buildSpecsPayload(this.specs(), this.specType());
    const ports = initialPorts === null ? undefined : toMonitorPortValues(initialPorts);

    this.specType.set(nextType);
    this.specs.set(createSpecsFormGroup(nextType, kept, ports));
    this.watchSpecs();
  }

  private loadProduct(id: number): void {
    this.loading.set(true);

    this.catalog.getProduct(id).subscribe({
      next: (product) => {
        this.currentStatus.set(product.status);

        this.form.patchValue({
          sku: product.sku,
          name: product.name,
          slug: product.slug,
          brandId: product.brandId,
          categoryId: product.categoryId,
          shortDescription: product.shortDescription ?? '',
          description: product.description ?? '',
          listPrice: product.listPrice,
          requiresSerial: product.requiresSerial,
          minStockDefault: 0,
          warrantyMonths: product.warrantyMonths,
          warrantyTerms: product.warrantyTerms ?? '',
          status: product.status,
        });

        this.applyCategory(product.categoryId, product.specifications, product.monitorPorts);
        this.setImages(product);

        this.loading.set(false);
        // Dữ liệu vừa nạp KHÔNG phải thay đổi của người dùng ⇒ reset cờ để canDeactivate không hỏi oan.
        this.dirty.set(false);
      },
      error: (error: unknown) => {
        this.loading.set(false);
        if (isApiError(error)) {
          this.apiError.set(error);
        }
      },
    });
  }

  private setImages(product: ProductDetailDto): void {
    this.images.clear();

    for (const image of product.images) {
      this.images.push(this.createImageGroup(image.url, image.sortOrder, image.isPrimary));
    }
  }

  private createImageGroup(
    url = '',
    sortOrder: number | null = null,
    isPrimary = false,
  ): ImageFormGroup {
    return this.fb.group({
      url: this.fb.control(url),
      sortOrder: this.fb.control<number | null>(sortOrder),
      isPrimary: this.fb.control(isPrimary),
    });
  }

  /** Nhóm thông số mới cũng phải được theo dõi thay đổi (phục vụ canDeactivate). */
  private watchSpecs(): void {
    this.specs().valueChanges.subscribe(() => this.dirty.set(true));
  }

  private buildImages(): ProductImageInput[] {
    return this.images.controls
      .map((group, index) => {
        const value = group.getRawValue();

        return {
          url: value.url.trim(),
          sortOrder: value.sortOrder ?? index,
          isPrimary: value.isPrimary,
        };
      })
      .filter((image) => image.url.length > 0);
  }

  private afterSave(productId: number): void {
    this.submitting.set(false);
    // Đánh dấu đã lưu để canDeactivate cho phép điều hướng rời trang.
    this.saved.set(true);
    this.dirty.set(false);
    void this.router.navigate(['/admin/products', productId, 'edit']);
  }

  private handleError(error: unknown): void {
    this.submitting.set(false);

    if (!isApiError(error)) {
      return;
    }

    this.apiError.set(error);
    this.applyServerFieldErrors(error);
  }

  /**
   * Map `errors` của ValidationProblemDetails về đúng control:
   * - khoá `specs.socket` / `Specs.Socket` ⇒ control động trong nhóm thông số;
   * - khoá khác ⇒ tìm control theo tên KHÔNG phân biệt hoa/thường (binder ASP.NET có thể trả PascalCase).
   * Lỗi gắn vào khoá `server` để template hiển thị riêng, không lẫn với lỗi validate phía client.
   */
  private applyServerFieldErrors(error: ApiError): void {
    const fieldErrors = error.fieldErrors;

    if (fieldErrors === null) {
      return;
    }

    for (const [key, messages] of Object.entries(fieldErrors)) {
      const message = messages[0] ?? 'Dữ liệu không hợp lệ.';
      const parts = key.split('.');
      const head = parts[0] ?? '';

      if (head.toLowerCase() === 'specs' && parts.length > 1) {
        const specControl = this.specs().get(parts[1] ?? '');
        if (specControl !== null) {
          specControl.setErrors({ ...(specControl.errors ?? {}), server: message });
          specControl.markAsTouched();
        }
        continue;
      }

      const wanted = head.toLowerCase();
      const name = Object.keys(this.form.controls).find(
        (candidate) => candidate.toLowerCase() === wanted,
      );

      if (name === undefined) {
        continue;
      }

      const control = this.form.get(name);
      if (control === null) {
        continue;
      }

      control.setErrors({ ...(control.errors ?? {}), server: message });
      control.markAsTouched();
    }
  }
}

function optional(value: string): string | null {
  const trimmed = value.trim();

  return trimmed.length === 0 ? null : trimmed;
}

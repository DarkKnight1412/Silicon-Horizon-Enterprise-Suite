import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Observable, defer, finalize, switchMap, tap } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { ApiService, type ApiRequestOptions } from '../http/api.service';
import {
  CART_SESSION_STORAGE_KEY,
  createCartSessionToken,
  emptyCart,
  type AddCartItemRequest,
  type CartDto,
  type SelectAllCartItemsRequest,
  type UpdateCartItemRequest,
} from '../models/cart.model';

/**
 * STATE TOÀN CỤC của giỏ hàng: badge trên header và trang giỏ cùng đọc một nguồn dữ liệu.
 *
 * Chủ giỏ do backend quyết định: khách đã đăng nhập (customerId ép từ token) hoặc khách vãng lai
 * (header `X-Cart-Session`). Store luôn gửi header này nên giỏ vãng lai vẫn dùng được trước khi đăng nhập;
 * sau khi đăng nhập gọi `merge()` để gộp vào giỏ tài khoản (POST /api/v1/cart/merge).
 */
@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly api = inject(ApiService);
  private readonly authStore = inject(AuthStore);

  private readonly _cart = signal<CartDto>(emptyCart());
  private readonly _loading = signal(false);
  private readonly _sessionToken = signal<string>(readOrCreateSessionToken());

  readonly cart = this._cart.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly items = computed(() => this._cart().items);
  readonly totals = computed(() => this._cart().totals);
  readonly warnings = computed(() => this._cart().warnings);
  readonly isEmpty = computed(() => this._cart().items.length === 0);

  /** Tổng số lượng sản phẩm trong giỏ — dùng cho badge trên header. */
  readonly itemCount = computed(() => this._cart().items.reduce((sum, item) => sum + item.quantity, 0));

  /** Số dòng đang được tick để đặt hàng. */
  readonly selectedCount = computed(() => this._cart().totals.selectedCount);

  constructor() {
    // Đăng xuất ⇒ xoá giỏ đang hiển thị (giỏ trên server vẫn còn, sẽ nạp lại khi đăng nhập).
    effect(() => {
      if (!this.authStore.isAuthenticated()) {
        this._cart.set(emptyCart());
      }
    });
  }

  /** Mã phiên giỏ tạm (chỉ có ý nghĩa với khách vãng lai). */
  sessionToken(): string {
    return this._sessionToken();
  }

  /** GET /api/v1/cart — đọc giỏ hiện tại (proc tự bỏ tick món hết hàng và giảm số lượng theo tồn). */
  load(): Observable<CartDto> {
    return this.run(this.fetch());
  }

  /** POST /api/v1/cart/items — thêm sản phẩm (replace = true để đặt đúng số lượng). */
  add(productId: number, quantity = 1, replace = false): Observable<CartDto> {
    const body: AddCartItemRequest = { productId, quantity, replace };
    return this.mutate(this.api.post<unknown>('/v1/cart/items', body, this.options()));
  }

  /** PATCH /api/v1/cart/items/{productId} — đổi số lượng (quantity <= 0 để xoá dòng). */
  updateQuantity(productId: number, quantity: number): Observable<CartDto> {
    const body: UpdateCartItemRequest = { quantity };
    return this.mutate(this.api.patch<unknown>(`/v1/cart/items/${productId}`, body, this.options()));
  }

  /** PATCH /api/v1/cart/items/{productId} — tick / bỏ tick một dòng. */
  setSelected(productId: number, isSelected: boolean): Observable<CartDto> {
    const body: UpdateCartItemRequest = { isSelected };
    return this.mutate(this.api.patch<unknown>(`/v1/cart/items/${productId}`, body, this.options()));
  }

  /** DELETE /api/v1/cart/items/{productId} — xoá dòng khỏi giỏ (idempotent). */
  remove(productId: number): Observable<CartDto> {
    return this.mutate(this.api.delete<unknown>(`/v1/cart/items/${productId}`, this.options()));
  }

  /** POST /api/v1/cart/select-all */
  selectAll(isSelected: boolean): Observable<CartDto> {
    const body: SelectAllCartItemsRequest = { isSelected };
    return this.mutate(this.api.post<unknown>('/v1/cart/select-all', body, this.options()));
  }

  /** POST /api/v1/cart/merge — gộp giỏ tạm vào giỏ tài khoản sau khi đăng nhập. */
  merge(): Observable<CartDto> {
    return this.mutate(this.api.post<unknown>('/v1/cart/merge', null, this.options()));
  }

  /** Xoá state cục bộ (không gọi API). */
  reset(): void {
    this._cart.set(emptyCart());
  }

  /** Header X-Cart-Session dùng cho mọi request giỏ hàng. */
  private options(): ApiRequestOptions {
    return { headers: { 'X-Cart-Session': this._sessionToken() } };
  }

  private fetch(): Observable<CartDto> {
    return this.api.get<CartDto>('/v1/cart', this.options()).pipe(tap((cart) => this._cart.set(cart)));
  }

  /** Gọi API thay đổi giỏ rồi nạp lại để state luôn khớp tồn kho thực tế. */
  private mutate(call$: Observable<unknown>): Observable<CartDto> {
    return this.run(call$.pipe(switchMap(() => this.fetch())));
  }

  /** Bật cờ bận cục bộ của giỏ hàng trong lúc request chạy. */
  private run(source: Observable<CartDto>): Observable<CartDto> {
    return defer(() => {
      this._loading.set(true);
      return source.pipe(finalize(() => this._loading.set(false)));
    });
  }
}

/** Đọc mã phiên giỏ tạm đã lưu, chưa có thì sinh mới và ghi lại (bọc try/catch cho chế độ riêng tư). */
function readOrCreateSessionToken(): string {
  try {
    const stored = globalThis.localStorage?.getItem(CART_SESSION_STORAGE_KEY);
    if (stored !== undefined && stored !== null && stored.length > 0) {
      return stored;
    }

    const created = createCartSessionToken();
    globalThis.localStorage?.setItem(CART_SESSION_STORAGE_KEY, created);
    return created;
  } catch {
    return createCartSessionToken();
  }
}

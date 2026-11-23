import { inject } from '@angular/core';
import type { CanDeactivateFn } from '@angular/router';
import { ConfirmService } from '../../core/services/confirm.service';

/**
 * Hợp đồng tối thiểu mà guard cần ở component — dùng interface thay vì import trực tiếp component
 * để tránh phụ thuộc vòng (routes → component → guard) và để tái dùng cho mọi form khác.
 */
export interface PendingChangesAware {
  hasUnsavedChanges(): boolean;
}

/**
 * Chống mất dữ liệu form (canDeactivate): nếu form đã thay đổi mà người dùng chưa lưu,
 * hiện hộp thoại xác nhận qua `ConfirmService` (dùng chung với các chỗ khác trong app).
 * Trả false để HUỶ điều hướng khi người dùng chọn ở lại.
 */
export const pendingChangesGuard: CanDeactivateFn<PendingChangesAware> = async (component) => {
  if (!component.hasUnsavedChanges()) {
    return true;
  }

  const confirm = inject(ConfirmService);

  return confirm.ask({
    title: 'Rời khỏi trang?',
    message:
      'Bạn có thay đổi chưa lưu. Nếu rời khỏi trang bây giờ, các thay đổi đó sẽ bị mất.',
    confirmLabel: 'Rời trang',
    cancelLabel: 'Ở lại',
    danger: true,
  });
};

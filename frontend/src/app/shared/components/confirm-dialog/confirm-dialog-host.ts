import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ConfirmService } from '../../../core/services/confirm.service';
import { ConfirmDialog } from './confirm-dialog';

/**
 * Cầu nối giữa `ConfirmService` (API dạng Promise: `await confirm.ask(...)`) và `ConfirmDialog`.
 * Đặt một lần trong App để mọi màn hình — kể cả guard `canDeactivate` — đều có hộp thoại hiển thị.
 */
@Component({
  selector: 'app-confirm-dialog-host',
  imports: [ConfirmDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (state(); as current) {
      <app-confirm-dialog
        [open]="current.open"
        [title]="current.title"
        [message]="current.message"
        [confirmLabel]="current.confirmLabel ?? 'Đồng ý'"
        [cancelLabel]="current.cancelLabel ?? 'Huỷ'"
        [danger]="current.danger ?? false"
        (confirmed)="service.accept()"
        (cancelled)="service.dismiss()"
      />
    }
  `,
})
export class ConfirmDialogHost {
  protected readonly service = inject(ConfirmService);
  protected readonly state = this.service.current;
}

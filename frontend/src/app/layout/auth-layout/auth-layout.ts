import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Khung cho trang xác thực (đăng nhập / đăng ký): nền tối giản, thẻ căn giữa. */
@Component({
  selector: 'app-auth-layout',
  imports: [RouterOutlet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
  <div class="grid min-h-screen place-items-center bg-gradient-to-br from-slate-100 via-white to-brand-50 p-4">
    <div class="w-full max-w-md">
      <div class="mb-6 flex flex-col items-center gap-3">
        <img
          src="/icons/logo.png"
          alt="Silicon Horizon"
          class="h-16 w-16"
        />
        <p class="whitespace-nowrap text-xl font-bold tracking-wide text-slate-800">
          Silicon Horizon Enterprise Suite
        </p>
      </div>

      <router-outlet />
    </div>
  </div>
`,
})
export class AuthLayout { }

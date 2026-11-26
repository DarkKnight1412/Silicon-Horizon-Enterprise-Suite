import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { formatVnd } from '../../../../core/models/common.model';
import {
  specValue,
  type PcBuilderCategoryDto,
  type PcBuilderProductDto,
} from '../../../../core/models/pc-build.model';
import { CartStore } from '../../../../core/services/cart.store';
import { NotificationService } from '../../../../core/services/notification.service';
import { PcBuildApi } from '../../../pc-builds/pc-build-api.service';

/** Nhóm linh kiện khách chọn theo thứ tự. */
type BuilderStep = 1 | 2 | 3 | 4 | 5 | 6;

/** Lựa chọn của khách cho một nhóm: sản phẩm + số lượng (RAM: số KIT, mỗi kit gồm nhiều thanh). */
interface BuilderSelection {
  product: PcBuilderProductDto;
  quantity: number;
}

/** Kết quả đánh giá tương thích của một linh kiện với các lựa chọn trước đó. */
interface OptionCheck {
  ok: boolean;
  /** Lý do bị ẩn — hiển thị cho khách biết vì sao món này không được gợi ý. */
  reason: string;
}

/** Một dòng trong bảng tổng chi tiết cấu hình. */
interface SummaryRow {
  step: BuilderStep;
  categoryCode: string;
  categoryName: string;
  selection: BuilderSelection | null;
  required: boolean;
}

/** Các bước BẮT BUỘC (màn hình là tùy chọn). */
const REQUIRED_CODES: readonly string[] = ['MAINBOARD', 'CPU', 'RAM', 'STORAGE', 'PSU'];

/** Bước đầu (bo mạch chủ) và bước cuối (màn hình — tùy chọn) trong luồng tự build. */
const FIRST_STEP = 1;
const LAST_STEP = 6;

/** Dự phòng công suất nguồn cho bo mạch/RAM/ổ cứng/quạt (W) — dùng trong công thức ở `recommendedWattage`. */
const PSU_HEADROOM_W = 150;

/**
 * TAB "TỰ BUILD PC" — khách tự chọn linh kiện theo thứ tự:
 * Bo mạch chủ → CPU → RAM → Ổ cứng → Nguồn → Màn hình (màn hình tùy chọn).
 *
 * Điểm cốt lõi: tại mỗi bước CHỈ GỢI Ý những linh kiện PHÙ HỢP với các lựa chọn trước đó, theo đúng 4 quy tắc
 * mà backend cũng kiểm tra (`usp_Compat_Check`):
 *   1. SOCKET     — CPU phải cùng socket với bo mạch chủ.
 *   2. CHUẨN RAM  — RAM phải cùng DDR (DDR4/DDR5) với bo mạch; chỉ nhận thanh DIMM cho máy bàn.
 *   3. SỐ KHE RAM — số thanh (số kit × số thanh/kit) không vượt số khe của bo mạch; khách chỉnh được số kit.
 *   4. NGUỒN      — công suất nguồn ≥ TDP CPU × 2 + 150 W (nguồn chạy ~50% tải cho bền và còn dư nâng cấp).
 * Thêm 2 gợi ý mềm: ổ cứng lọc theo số khe M.2/SATA của bo mạch; màn hình không ràng buộc.
 *
 * CHỌN XONG ⇒ TỰ ĐỘNG sang bước kế tiếp (khách không phải bấm "Tiếp tục" — mỗi nhóm chỉ có MỘT linh kiện,
 * chọn món khác là thay thế món cũ). NGOẠI LỆ duy nhất: bước RAM dừng lại khi bo mạch còn khe để gắn thêm kit
 * — khách chỉnh SỐ KIT rồi bấm "Đã xong" để đi tiếp.
 * Đổi lựa chọn ở bước trước ⇒ linh kiện đã chọn mà không còn phù hợp bị TỰ ĐỘNG BỎ và báo rõ lý do.
 * Bảng tổng chi tiết luôn hiện toàn bộ cấu hình + thành tiền; nút "Đặt mua" thêm TẤT CẢ linh kiện đã chọn
 * vào giỏ bằng MỘT request (`POST /api/v1/cart/items/bulk` — backend ghi trong một giao dịch).
 */
@Component({
  selector: 'app-pc-builder',
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pc-builder.html',
})
export class PcBuilder {
  private readonly api = inject(PcBuildApi);
  private readonly cart = inject(CartStore);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);

  protected readonly formatPrice = formatVnd;

  /** Danh mục linh kiện (6 nhóm) tải từ GET /api/v1/pc-builds/builder/catalog. */
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly categories = signal<PcBuilderCategoryDto[]>([]);

  /** Bước đang mở (1..6) — chỉ hiện gợi ý của bước này để khách chọn tuần tự. */
  protected readonly activeStep = signal<BuilderStep>(1);

  /** Lựa chọn hiện tại theo mã nhóm (MAINBOARD, CPU, RAM, STORAGE, PSU, MONITOR). */
  protected readonly selection = signal<Record<string, BuilderSelection>>({});

  /** Thông báo tự động bỏ linh kiện khi khách đổi lựa chọn ở bước trước. */
  protected readonly notice = signal<string | null>(null);

  protected readonly submitting = signal(false);

  constructor() {
    this.loadCatalog();
  }

  // ── Tải danh mục ─────────────────────────────────────────────────────────

  private loadCatalog(): void {
    this.loading.set(true);
    this.loadFailed.set(false);

    this.api.getBuilderCatalog().subscribe({
      next: (catalog) => {
        this.categories.set([...catalog.categories].sort((a, b) => a.stepOrder - b.stepOrder));
        this.loading.set(false);
      },
      error: () => {
        this.categories.set([]);
        this.loading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  protected retry(): void {
    this.loadCatalog();
  }

  /** Nhóm linh kiện của một bước (null nếu danh mục chưa tải xong). */
  protected stepOf(step: BuilderStep): PcBuilderCategoryDto | null {
    return this.categories().find((category) => category.stepOrder === step) ?? null;
  }

  /** Tên nhóm linh kiện tiếng Việt (dùng cho thông báo). */
  protected categoryLabel(code: string): string {
    return this.categories().find((category) => category.categoryCode === code)?.categoryName ?? code;
  }

  /** Bước tương ứng một mã nhóm (null nếu danh mục chưa tải xong). */
  protected stepOfCode(code: string): BuilderStep | null {
    const category = this.categories().find((item) => item.categoryCode === code);

    return category ? (category.stepOrder as BuilderStep) : null;
  }

  /** Tên các nhóm BẮT BUỘC còn thiếu — nhắc khách ở bảng tổng chi tiết. */
  protected missingRequiredLabels(): string[] {
    return REQUIRED_CODES.filter((code) => this.selectedOf(code) === null).map((code) => this.categoryLabel(code));
  }

  /** Bước còn thiếu linh kiện bắt buộc ĐẦU TIÊN (null = đã đủ) — nút "Chọn ngay". */
  protected firstMissingStep(): BuilderStep | null {
    const missing = REQUIRED_CODES.find((code) => this.selectedOf(code) === null);

    return missing ? this.stepOfCode(missing) : null;
  }

  /** Lựa chọn hiện tại của một nhóm. */
  protected selectedOf(code: string): BuilderSelection | null {
    return this.selection()[code] ?? null;
  }

  /** Mở một bước bất kỳ (khách bấm vào thanh tiến trình để quay lại sửa). */
  protected goStep(step: BuilderStep): void {
    this.activeStep.set(step);
    this.notice.set(null);
  }

  // ── Các con số ràng buộc: số khe RAM và công suất nguồn ──────────────────

  private pick(selection: Record<string, BuilderSelection>, code: string): BuilderSelection | null {
    return selection[code] ?? null;
  }

  /** Số khe RAM của bo mạch trong bộ lựa chọn (0 = chưa chọn bo mạch). */
  private ramSlotsOf(selection: Record<string, BuilderSelection>): number {
    const mainboard = this.pick(selection, 'MAINBOARD');

    return mainboard ? (specValue<number>(mainboard.product.specs, 'ramSlots') ?? 0) : 0;
  }

  /** Số thanh RAM bị chiếm = số kit × số thanh mỗi kit. */
  private ramModulesOf(selection: Record<string, BuilderSelection>): number {
    const ram = this.pick(selection, 'RAM');

    return ram ? (specValue<number>(ram.product.specs, 'modulesPerKit') ?? 1) * ram.quantity : 0;
  }

  /**
   * Công suất nguồn KHUYẾN NGHỊ = TDP CPU × 2 + 150 W, làm tròn lên bội số 50 W.
   * Nhân 2 để nguồn chạy quanh 50% tải (bền, êm) và còn dư cho card đồ hoạ/nâng cấp sau này.
   */
  private recommendedWattageOf(selection: Record<string, BuilderSelection>): number {
    const cpu = this.pick(selection, 'CPU');

    if (!cpu) {
      return 0;
    }

    const tdp = specValue<number>(cpu.product.specs, 'tdpW') ?? 0;

    return tdp <= 0 ? 0 : Math.ceil((tdp * 2 + PSU_HEADROOM_W) / 50) * 50;
  }

  /** Số khe RAM của bo mạch đang chọn (hiển thị ở bước RAM). */
  protected ramSlots(): number {
    return this.ramSlotsOf(this.selection());
  }

  /** Số thanh RAM đang dùng theo lựa chọn hiện tại. */
  protected ramModulesUsed(): number {
    return this.ramModulesOf(this.selection());
  }

  /** Công suất nguồn khuyến nghị cho cấu hình đang chọn (hiển thị ở bước Nguồn). */
  protected recommendedWattage(): number {
    return this.recommendedWattageOf(this.selection());
  }

  /** Số KIT RAM tối đa chọn được với một sản phẩm RAM (theo số khe của bo mạch). */
  protected maxRamKits(product: PcBuilderProductDto): number {
    const perKit = specValue<number>(product.specs, 'modulesPerKit') ?? 1;
    const slots = this.ramSlots();

    if (perKit <= 0 || slots <= 0) {
      return 1;
    }

    return Math.max(1, Math.floor(slots / perKit));
  }

  // ── Đánh giá tương thích: quyết định gợi ý món nào ở mỗi bước ─────────────

  private static readonly okCheck: OptionCheck = { ok: true, reason: '' };

  private static badCheck(reason: string): OptionCheck {
    return { ok: false, reason };
  }

  /**
   * Kiểm tra một linh kiện có phù hợp với các lựa chọn TRƯỚC ĐÓ hay không.
   * `selection` cho phép truyền bộ lựa chọn "sắp áp dụng" (dùng khi kiểm tra lại sau khi đổi bo mạch chủ).
   */
  private evaluate(
    category: PcBuilderCategoryDto,
    product: PcBuilderProductDto,
    selection: Record<string, BuilderSelection> = this.selection(),
  ): OptionCheck {
    const mainboard = this.pick(selection, 'MAINBOARD');
    const specs = product.specs;

    switch (category.categoryCode) {
      case 'CPU': {
        if (!mainboard) {
          return PcBuilder.okCheck;
        }

        const mbSocket = specValue<string>(mainboard.product.specs, 'socket');
        const cpuSocket = specValue<string>(specs, 'socket');

        return cpuSocket === mbSocket
          ? PcBuilder.okCheck
          : PcBuilder.badCheck(`khác socket với bo mạch (${mbSocket})`);
      }

      case 'RAM': {
        if (!mainboard) {
          return PcBuilder.okCheck;
        }

        const mbDdr = specValue<string>(mainboard.product.specs, 'memoryType');
        const ramDdr = specValue<string>(specs, 'memoryType');
        const form = specValue<string>(specs, 'formFactor') ?? '';
        const perKit = specValue<number>(specs, 'modulesPerKit') ?? 1;
        const slots = this.ramSlotsOf(selection);

        if (ramDdr !== mbDdr) {
          return PcBuilder.badCheck(`là ${ramDdr}, bo mạch chỉ hỗ trợ ${mbDdr}`);
        }

        if (form !== 'DIMM') {
          return PcBuilder.badCheck('là thanh SO-DIMM (chỉ dùng cho laptop)');
        }

        if (perKit > slots) {
          return PcBuilder.badCheck(`cần ${perKit} khe, bo mạch chỉ có ${slots} khe`);
        }

        return PcBuilder.okCheck;
      }

      case 'STORAGE': {
        if (!mainboard) {
          return PcBuilder.okCheck;
        }

        const m2 = specValue<number>(mainboard.product.specs, 'm2Slots') ?? 0;
        const sata = specValue<number>(mainboard.product.specs, 'sataPorts') ?? 0;
        const form = specValue<string>(specs, 'formFactor') ?? '';
        const isM2 = form.startsWith('M.2');

        if (isM2 && m2 <= 0) {
          return PcBuilder.badCheck('dùng khe M.2 nhưng bo mạch không có khe M.2');
        }

        if (!isM2 && sata <= 0) {
          return PcBuilder.badCheck('dùng cổng SATA nhưng bo mạch không có cổng SATA');
        }

        return PcBuilder.okCheck;
      }

      case 'PSU': {
        const needed = this.recommendedWattageOf(selection);

        if (needed <= 0) {
          return PcBuilder.okCheck;
        }

        const power = specValue<number>(specs, 'powerW') ?? 0;

        return power >= needed
          ? PcBuilder.okCheck
          : PcBuilder.badCheck(`chỉ ${power}W trong khi cấu hình cần tối thiểu ~${needed}W`);
      }

      default:
        // Bo mạch chủ (bước đầu) và màn hình (tùy chọn) không ràng buộc gì.
        return PcBuilder.okCheck;
    }
  }

  /** Gợi ý của bước đang mở — CHỈ gồm linh kiện phù hợp với các lựa chọn trước đó. */
  protected options(): PcBuilderProductDto[] {
    const category = this.stepOf(this.activeStep());

    return category ? category.products.filter((product) => this.evaluate(category, product).ok) : [];
  }

  /** Vì sao một số linh kiện KHÔNG được gợi ý ở bước đang mở (nhóm theo lý do). */
  protected hiddenReasons(): { reason: string; count: number }[] {
    const category = this.stepOf(this.activeStep());

    if (!category) {
      return [];
    }

    const counters = new Map<string, number>();

    for (const product of category.products) {
      const check = this.evaluate(category, product);

      if (!check.ok) {
        counters.set(check.reason, (counters.get(check.reason) ?? 0) + 1);
      }
    }

    return [...counters.entries()].map(([reason, count]) => ({ reason, count }));
  }

  // ── Chọn / bỏ chọn linh kiện ─────────────────────────────────────────────

  /**
   * Chọn linh kiện cho một nhóm rồi TỰ ĐỘNG sang bước kế tiếp (mỗi nhóm chỉ có một linh kiện — chọn món khác
   * là thay thế món cũ). Riêng bước RAM dừng lại khi bo mạch còn khe để gắn thêm kit (khách bấm "Đã xong").
   * Đổi bo mạch chủ sẽ tự bỏ các lựa chọn không còn phù hợp.
   */
  protected select(categoryCode: string, product: PcBuilderProductDto): void {
    const next: Record<string, BuilderSelection> = { ...this.selection() };
    const existing = next[categoryCode];

    next[categoryCode] = { product, quantity: existing?.quantity ?? 1 };

    const removed: string[] = [];

    if (categoryCode === 'MAINBOARD') {
      for (const code of ['CPU', 'RAM', 'STORAGE', 'PSU']) {
        const current = next[code];
        const category = this.categories().find((item) => item.categoryCode === code);

        if (current && category && !this.evaluate(category, current.product, next).ok) {
          delete next[code];
          removed.push(this.categoryLabel(code));
        }
      }
    }

    this.selection.set(next);

    const removedNotice = this.autoRemovedNotice(removed);

    // Bước RAM: dừng lại cho khách chỉnh số kit (nếu còn khe để gắn thêm) rồi bấm "Đã xong".
    if (categoryCode === 'RAM' && this.needsRamQuantity()) {
      this.notice.set(removedNotice);
      return;
    }

    this.activeStep.set(Math.min(LAST_STEP, this.activeStep() + 1) as BuilderStep);

    // Thông báo phải đặt SAU khi chuyển bước, vì chuyển bước mở một bước mới và xoá thông báo cũ.
    this.notice.set(
      removedNotice ??
        (this.activeStep() === LAST_STEP && this.canSubmit()
          ? 'Đã đủ 5 nhóm linh kiện bắt buộc — bạn có thể bấm “Đặt mua” ở cột phải, hoặc thêm màn hình nếu cần.'
          : null),
    );
  }

  /** Câu thông báo khi việc đổi bo mạch chủ làm bỏ đi một số linh kiện đã chọn. */
  private autoRemovedNotice(removed: readonly string[]): string | null {
    return removed.length === 0
      ? null
      : `Đã tự bỏ ${removed.join(', ')} vì không còn phù hợp với bo mạch vừa chọn.`;
  }

  /** Bỏ linh kiện của một nhóm (giữ nguyên các nhóm khác). */
  protected clear(categoryCode: string): void {
    const next = { ...this.selection() };
    delete next[categoryCode];

    this.selection.set(next);
    this.notice.set(null);
  }

  /** Đổi số KIT RAM — luôn chặn theo số khe của bo mạch. */
  protected setRamQuantity(kits: number): void {
    const ram = this.selectedOf('RAM');

    if (!ram) {
      return;
    }

    const safe = Math.min(Math.max(1, Math.floor(kits)), this.maxRamKits(ram.product));

    this.selection.set({ ...this.selection(), RAM: { product: ram.product, quantity: safe } });
    this.notice.set(null);
  }

  /** Bước RAM có cần DỪNG LẠI cho khách chốt số kit không (bo mạch còn khe để gắn thêm kit). */
  protected needsRamQuantity(): boolean {
    const ram = this.selectedOf('RAM');

    return ram !== null && this.maxRamKits(ram.product) > 1;
  }

  /** Bước RAM đang mở VÀ đã có RAM được chọn ⇒ hiện khối chỉnh số kit + nút "Đã xong". */
  protected ramPanelVisible(): boolean {
    return this.stepOf(this.activeStep())?.categoryCode === 'RAM' && this.selectedOf('RAM') !== null;
  }

  /** Nút "Đã xong" ở bước RAM: chốt số kit đang chọn rồi sang bước kế tiếp. */
  protected doneRam(): void {
    const ram = this.selectedOf('RAM');

    if (!ram) {
      this.nextStep();
      return;
    }

    const perKit = specValue<number>(ram.product.specs, 'capacityGbPerKit') ?? 0;
    const modulesPerKit = specValue<number>(ram.product.specs, 'modulesPerKit') ?? 1;
    const modules = modulesPerKit * ram.quantity;
    const capacity = perKit * ram.quantity;

    this.nextStep();
    this.notice.set(
      `Đã chốt RAM: ${ram.quantity} kit · ${modules} thanh${capacity > 0 ? ` · ${capacity}GB` : ''}` +
        ` — dùng ${modules}/${this.ramSlots()} khe của bo mạch.`,
    );
  }

  // ── Bảng tổng chi tiết + đặt mua cả cấu hình ─────────────────────────────

  /** Toàn bộ 6 bước cho bảng tổng (bước chưa chọn vẫn hiện để khách thấy còn thiếu gì). */
  protected summary(): SummaryRow[] {
    return this.categories().map((category) => ({
      step: category.stepOrder as BuilderStep,
      categoryCode: category.categoryCode,
      categoryName: category.categoryName,
      selection: this.selectedOf(category.categoryCode),
      required: REQUIRED_CODES.includes(category.categoryCode),
    }));
  }

  /** Thành tiền một dòng (0 khi chưa chọn). */
  protected lineTotal(selection: BuilderSelection | null): number {
    return selection ? selection.product.price * selection.quantity : 0;
  }

  /** Tổng tiền cấu hình đang chọn (giá hiện hành, đã gồm khuyến mãi và bảng giá nhóm khách). */
  protected totalPrice(): number {
    return Object.values(this.selection()).reduce((sum, item) => sum + this.lineTotal(item), 0);
  }

  /** Số nhóm linh kiện đã chọn (6 nhóm tối đa). */
  protected chosenCount(): number {
    return Object.keys(this.selection()).length;
  }

  /** Đã đủ 5 bước bắt buộc (bo mạch chủ · CPU · RAM · ổ cứng · nguồn) chưa. */
  protected canSubmit(): boolean {
    return !this.submitting() && REQUIRED_CODES.every((code) => this.selectedOf(code) !== null);
  }

  /** Cảnh báo tương thích còn lại của cấu hình đang chọn (rỗng = cấu hình đang hợp lệ). */
  protected conflicts(): string[] {
    const problems: string[] = [];

    if (!this.selectedOf('MAINBOARD')) {
      return problems;
    }

    for (const category of this.categories()) {
      const current = this.selectedOf(category.categoryCode);

      if (!current) {
        continue;
      }

      const check = this.evaluate(category, current.product);

      if (!check.ok) {
        problems.push(`${current.product.name}: ${check.reason}`);
      }
    }

    if (this.ramModulesUsed() > this.ramSlots()) {
      problems.push(
        `Số thanh RAM đang chọn (${this.ramModulesUsed()}) vượt số khe của bo mạch (${this.ramSlots()}).`,
      );
    }

    return problems;
  }

  /**
   * "Đặt mua" cả cấu hình: gửi MỘT request (POST /api/v1/cart/items/bulk) để thêm mọi linh kiện đã chọn
   * vào giỏ trong một giao dịch, rồi chuyển sang trang giỏ để khách kiểm tra và chốt đơn.
   */
  protected submit(): void {
    if (!this.canSubmit()) {
      this.notifications.warning(
        'Chưa đủ linh kiện để đặt mua',
        'Cần chọn ít nhất bo mạch chủ, CPU, RAM, ổ cứng và nguồn (màn hình là tùy chọn).',
      );
      return;
    }

    const items = Object.values(this.selection()).map((item) => ({
      productId: item.product.productId,
      quantity: item.quantity,
    }));

    this.submitting.set(true);

    this.cart.addItems(items).subscribe({
      next: () => {
        this.submitting.set(false);
        this.notifications.success(
          'Đã thêm cấu hình tự build vào giỏ hàng',
          `${items.length} linh kiện bạn đã chọn được thêm — kiểm tra rồi chốt đơn nhé.`,
        );
        void this.router.navigate(['/cart']);
      },
      error: () => this.submitting.set(false),
    });
  }

  /** Xoá toàn bộ cấu hình để làm lại từ đầu. */
  protected reset(): void {
    this.selection.set({});
    this.activeStep.set(1);
    this.notice.set(null);
  }

  /** Sang bước kế tiếp ("Tiếp tục" / "Bỏ qua bước này"). */
  protected nextStep(): void {
    this.goStep(Math.min(LAST_STEP, this.activeStep() + 1) as BuilderStep);
  }

  /** Quay lại bước trước. */
  protected prevStep(): void {
    this.goStep(Math.max(FIRST_STEP, this.activeStep() - 1) as BuilderStep);
  }

  /** Đang ở bước cuối (màn hình — tùy chọn) nên không còn bước để "Tiếp tục". */
  protected isLastStep(): boolean {
    return this.activeStep() >= LAST_STEP;
  }

  /** Nhãn nút chuyển bước: chưa chọn gì ở bước này thì nói rõ đây là bỏ qua để khách yên tâm. */
  protected nextStepLabel(): string {
    const category = this.stepOf(this.activeStep());

    return category && this.selectedOf(category.categoryCode) ? 'Tiếp tục' : 'Bỏ qua bước này';
  }

  /** Dòng thông số ngắn hiển thị trên thẻ linh kiện — khác nhau theo nhóm. */
  protected specLine(categoryCode: string, product: PcBuilderProductDto): string {
    const specs = product.specs;
    const parts: (string | null)[] = [];

    switch (categoryCode) {
      case 'MAINBOARD':
        parts.push(
          specValue<string>(specs, 'socket'),
          specValue<string>(specs, 'memoryType'),
          `${specValue<number>(specs, 'ramSlots') ?? 0} khe RAM`,
          specValue<string>(specs, 'chipset'),
          specValue<string>(specs, 'formFactor'),
        );
        break;

      case 'CPU':
        parts.push(
          specValue<string>(specs, 'socket'),
          `${specValue<number>(specs, 'cores') ?? 0} nhân · ${specValue<number>(specs, 'tdpW') ?? 0}W TDP`,
          specValue<boolean>(specs, 'hasIntegratedGpu') ? 'có đồ hoạ tích hợp' : 'cần card đồ hoạ rời',
        );
        break;

      case 'RAM':
        parts.push(
          specValue<string>(specs, 'memoryType'),
          `${specValue<number>(specs, 'capacityGbPerKit') ?? 0}GB x ${specValue<number>(specs, 'modulesPerKit') ?? 1} thanh`,
          `${specValue<number>(specs, 'busMhz') ?? 0}MHz`,
          specValue<string>(specs, 'formFactor'),
        );
        break;

      case 'STORAGE':
        parts.push(
          specValue<string>(specs, 'storageType'),
          specValue<string>(specs, 'interface'),
          specValue<string>(specs, 'formFactor'),
          `${specValue<number>(specs, 'capacityGb') ?? 0}GB`,
        );
        break;

      case 'PSU':
        parts.push(
          `${specValue<number>(specs, 'powerW') ?? 0}W`,
          specValue<string>(specs, 'efficiencyRating'),
          specValue<string>(specs, 'modularType'),
        );
        break;

      case 'MONITOR':
        parts.push(
          `${specValue<number>(specs, 'sizeInch') ?? 0} inch`,
          specValue<string>(specs, 'resolution'),
          `${specValue<number>(specs, 'refreshRateHz') ?? 0}Hz`,
          specValue<string>(specs, 'panelType'),
        );
        break;
    }

    return parts.filter((part): part is string => part !== null && part.length > 0).join(' · ');
  }
}

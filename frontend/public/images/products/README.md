# Ảnh sản phẩm — QUY ƯỚC ĐẶT TÊN

Mỗi ảnh sản phẩm là một file trong thư mục này và **tên file = mã SKU viết thường**:

| SKU (dbo.Products.Sku) | Tên file | Giá trị lưu ở `dbo.ProductImages.Url` |
|---|---|---|
| `CPU-INTEL-I9-14900K` | `cpu-intel-i9-14900k.jpg` | `/images/products/cpu-intel-i9-14900k.jpg` |
| `RAM-KG-FB-32-D5-6000` | `ram-kg-fb-32-d5-6000.jpg` | `/images/products/ram-kg-fb-32-d5-6000.jpg` |

- Định dạng khuyến nghị: `jpg`, `png`, `webp` (tên file giữ đúng phần mở rộng thật).
- Thêm file mới ⇒ **restart dev server** (`npm start`) mới được phục vụ.
- Ảnh chính của sản phẩm: `ProductImages.IsPrimary = 1` (một dòng duy nhất); muốn nhiều ảnh thì thêm dòng `SortOrder` tăng dần.
- Chi tiết + biến thể: xem `Database/README.md` mục 3.3.

> ⚠️ Từ khi có **upload ảnh qua trang quản trị** (`POST /api/admin/products/images`), ảnh sản phẩm thật
> được API lưu ở `src/SiliconHorizon.Api/wwwroot/images/products/` và `ProductImages.Url` là URL đầy đủ
> trỏ về API — bạn **KHÔNG cần copy ảnh vào thư mục này nữa**; nhân viên chỉ cần chọn file ở form sản phẩm.
> Thư mục này chỉ còn dùng cho **ảnh đặt sẵn trong mã nguồn** (như bộ placeholder `*.svg` theo danh mục).

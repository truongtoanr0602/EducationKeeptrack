# Chuyển PDF từ SeaweedFS sang R2 — #30

Phạm vi là PDF báo cáo tháng. Ảnh bài nộp vẫn thuộc T12. Công cụ nằm ở `tools/R2Migrate`.

## Trạng thái — đọc trước

- **Chưa có lần chuyển dữ liệu thật nào được thực hiện.** Không có kho SeaweedFS chứa PDF thật để kiểm kê. Theo tiêu chí của ticket, kết quả kiểm kê của kho cũ là **rỗng**.
- **Kiểm kê kho cũ (13/11/2026): rỗng.** Máy dev duy nhất của dự án không có volume `report-objects`, không có container hay image SeaweedFS (Docker Desktop: 6 volume không liên quan, các container `ekt-tuition*` chỉ là PostgreSQL đã dừng trước khi có tính năng PDF). Chủ dự án xác nhận không có môi trường nào khác từng chạy bản compose có SeaweedFS. Commit `02ee9c5` (#28) bỏ SeaweedFS chỉ vài ngày sau khi nó được thêm vào (`5a19109`), nên không có dữ liệu PDF nào cần chuyển. Điều kiện "chưa có PDF cũ" của ticket được đáp ứng bằng kiểm kê rỗng cộng bài diễn tập bên dưới.
- **Đã diễn tập** toàn bộ quy trình bằng PDF do chính ứng dụng xuất, trên bucket R2 test: kho "cũ" và kho "mới" là hai prefix của cùng một bucket. Bài diễn tập chứng minh logic sao chép, xác minh, chuyển đổi và quay lui trên API R2.
- **Chưa kiểm chứng:** hành vi riêng của SeaweedFS (HTTP thường, path-style của nó). Cấu hình nguồn dựa trên `compose.yaml` trước commit `02ee9c5`: `http://s3:8333`, region `us-east-1`, bucket `ekt-reports`, không có prefix. Cần một lần chạy thử trên SeaweedFS thật hoặc container cũ trước khi cutover.
- Ticket #30 chưa được đóng bằng tài liệu này.

## Công cụ

Tất cả cấu hình qua biến môi trường; công cụ không in bí mật, lỗi của SDK/driver chỉ hiện tên loại lỗi.

| Biến | Ý nghĩa |
| --- | --- |
| `R2M_DATABASE` | Chuỗi kết nối PostgreSQL. Chỉ đọc schema `reporting` |
| `SRC_ENDPOINT` `SRC_BUCKET` `SRC_ACCESS_KEY` `SRC_SECRET_KEY` | Kho cũ. `SRC_REGION` mặc định `us-east-1`, `SRC_KEY_PREFIX` mặc định rỗng |
| `SRC_ALLOW_HTTP=true` | Bắt buộc nếu nguồn là `http://` (chỉ nguồn được phép, không bao giờ cho đích) |
| `DST_ENDPOINT` `DST_BUCKET` `DST_ACCESS_KEY` `DST_SECRET_KEY` | Bucket R2 private đã cấp trước. `DST_REGION` mặc định `auto`, `DST_KEY_PREFIX` mặc định rỗng |

**Prefix phải khớp ứng dụng.** `SRC_KEY_PREFIX` phải bằng `Reporting__KeyPrefix` của backend cũ (thường rỗng) và `DST_KEY_PREFIX` phải bằng `Reporting__KeyPrefix` mà backend sẽ dùng sau cutover. Nếu lệch, `verify` vẫn báo đạt nhưng ứng dụng sẽ không thấy PDF nào (404). Chỉ cấu hình các kho mà lệnh cần: `inventory --store src` và `backup` không đòi biến `DST_*`.

```bash
dotnet build -c Release tools/R2Migrate
dotnet tools/R2Migrate/bin/Release/net10.0/R2Migrate.dll <lệnh> [tuỳ chọn] [--report file.json]
```

| Lệnh | Tác dụng | Ghi dữ liệu? |
| --- | --- | --- |
| `inventory [--store src\|dst] [--against src\|dst] [--deep]` | Đối chiếu export `ready` với object trong kho. Báo thiếu, mồ côi, sai kích thước; `--deep` tải về tính SHA-256. Với `--against`, export chỉ có ở kho kia được tách ra `OnlyInOther`, không tính là thiếu | Không |
| `copy --writes-paused` | Sao chép nguồn → đích. Kiểm tra đích trước nên export chỉ có trên R2 không cần object nguồn | Đích |
| `copy --reverse --writes-paused` | Sao chép đích → nguồn (quay lui) | Nguồn |
| `verify [--require-both]` | Hai cổng, xem dưới. Mã thoát theo `DestinationComplete`, hoặc theo `RollbackReady` nếu có `--require-both` | Không |
| `backup --dir D [--store src\|dst]` | Lưu bytes đã xác minh và `manifest.json` vào thư mục cục bộ | Thư mục |
| `restore --dir D [--to src\|dst]` | Khôi phục vào kho (dùng prefix thử) và xác minh lại SHA-256 | Kho chọn |

**Hai cổng của `verify`:**

- `DestinationComplete`: mọi tham chiếu có ở R2 và khớp SHA-256/kích thước. Đây là điều kiện để đổi cấu hình (cutover).
- `RollbackReady`: mọi tham chiếu khớp ở **cả hai** kho. Đây là điều kiện để quay lui an toàn và để ngừng kho cũ. Export tạo sau khi đã chuyển sang R2 (kể cả từ #28) làm cổng này tắt cho đến khi chạy `copy --reverse`; coi đó là "nợ quay lui", không phải lỗi.

**Trạng thái hỗn hợp.** Môi trường đã chạy #28 có các export chỉ tồn tại ở R2 cạnh các export cũ còn ở SeaweedFS. Công cụ xử lý đúng trường hợp này: `copy` không đòi object nguồn cho export đã có bản khớp ở đích, và `inventory --against dst` không coi chúng là thiếu.

Mã thoát: `0` đạt, `1` dùng sai hoặc cấu hình/kết nối lỗi, `2` đã chạy nhưng phát hiện vấn đề chặn bước đó.

Quy tắc không đổi:

- SHA-256 tính trên bytes thật rồi so với metadata (không phân biệt hoa/thường); không dùng ETag.
- **Không bao giờ xoá** ở kho nào. **Không ghi đè**: đối tượng đã có và khớp thì bỏ qua; có nhưng khác thì ghi là *xung đột*, giữ nguyên và trả mã `2`.
- Sau mỗi lần ghi đều đọc lại và kiểm tra SHA-256.
- Chỉ export `ready` là tham chiếu. Object mồ côi (lần xuất lỗi, `pending` bị bỏ dở) được báo cáo, **không** được sao chép.
- `copy` từ chối chạy nếu thiếu `--writes-paused` hoặc nếu còn export `pending` có lease còn hạn.
- Giữ nguyên key và content type. Metadata nằm trong PostgreSQL và dùng chung key, nên đổi kho không cần sửa dữ liệu.

## Cách tạm dừng ghi PDF

Công cụ chỉ kiểm tra được lease còn hạn; việc dừng ghi do người vận hành. Cách an toàn: dừng container backend (hoặc bỏ cấu hình `Reporting__*` để xuất PDF báo lỗi cấu hình) rồi chạy `copy --writes-paused`. Các module khác không phụ thuộc kho PDF nên vẫn có thể phục vụ nếu chỉ tắt tính năng xuất.

## Checklist cutover

1. Chuẩn bị bucket R2 private (tắt public access) và token chỉ cho bucket đó, quyền Object Read & Write.
2. Chạy `inventory --store src --against dst --deep` trên kho cũ, lưu báo cáo. Nếu `References = 0`: ghi nhận kho rỗng, bỏ qua bước sao chép và chỉ làm bước 9.
3. Nếu có `MissingObjects`, `LengthMismatches` hoặc `HashMismatches`: dừng, điều tra. Không cutover khi nguồn đã hỏng. `OnlyInOther` không chặn: đó là export đã nằm sẵn trên R2.
4. `backup --dir <thư mục> --store src`. Giữ bản sao ở nơi độc lập với kho cũ.
5. `restore --dir <thư mục> --to dst` với `DST_KEY_PREFIX=restore-test/<ngày>/`. Phải ra `Success=true`. Giữ prefix này đến khi xác minh xong rồi xoá thủ công trong bảng điều khiển R2.
6. Tạm dừng ghi PDF (xem trên). Giữ kho cũ ở chế độ chỉ đọc.
7. `copy --writes-paused`. Phải ra `Success=true`; chạy lại lần hai phải `Copied=0` (idempotent).
8. `verify`. Chỉ khi `DestinationComplete=true` mới được đổi cấu hình; `MissingInB` hoặc `MismatchedInB` khác rỗng là chặn. `MissingInA` khác rỗng chỉ là nợ quay lui.
9. Đổi cấu hình backend sang R2 (`Reporting__Endpoint`, `Bucket`, `AccessKey`, `SecretKey`, `Region=auto` và **`Reporting__KeyPrefix` bằng đúng `DST_KEY_PREFIX` đã dùng ở bước 7**), khởi động lại.
10. Smoke test **trước khi mở ghi**: tải một PDF cũ bằng giáo viên sở hữu (SHA-256 khớp metadata); giáo viên khác phải nhận 404; tài khoản bị khoá phải bị từ chối.
11. Mở ghi. Xuất một PDF mới và tải được. Kho cũ vẫn được giữ nguyên.

## Rollback

**Trước khi R2 có ghi mới** (chưa xuất PDF nào sau cutover): đặt lại cấu hình về kho cũ và khởi động lại. Không cần sao chép, vì kho cũ không bị thay đổi.

**Sau khi R2 đã có ghi mới:**

1. Tạm dừng ghi PDF.
2. `verify` — kỳ vọng `DestinationComplete=true` và `MissingInA` liệt kê đúng các export tạo sau cutover.
3. `copy --reverse --writes-paused` để đưa phần chênh lệch về kho cũ. Phải `Success=true`.
4. `verify --require-both`: `RollbackReady=true`.
5. Đặt cấu hình về kho cũ, khởi động lại và chạy lại smoke test của bước 10. Mọi tham chiếu của export mới vẫn hợp lệ vì key không đổi.

Nếu `copy --reverse` báo `Conflicts` hoặc `CorruptAtSource` thì dừng và xử lý thủ công; công cụ sẽ không ghi đè.

## Điều kiện ngừng kho cũ

Chỉ ngừng khi **tất cả** đều đúng, và vẫn không xoá volume hay bucket cũ trong quy trình này:

- Hai lần `verify --require-both` cách nhau ít nhất một ngày vận hành đều `RollbackReady=true` (chạy `copy --reverse` trước nếu có nợ quay lui).
- Backup độc lập đã khôi phục thử thành công (bước 5).
- Smoke test và các xuất/tải mới trên R2 chạy ổn trong thời gian theo dõi do chủ dự án quyết định.
- Chủ dự án xác nhận bằng văn bản. Việc xoá kho cũ là quyết định riêng, không thuộc công cụ.

## Bằng chứng đã có

Test tự động (cần `TEST_DATABASE_CONNECTION` và `TEST_S3_*`):

- `MigrationToolTests` (17 test, không cần mạng): quy tắc kiểm kê, cổng tạm dừng ghi, không ghi đè, idempotent, nguồn thiếu/hỏng, hai cổng của `verify`, trạng thái hỗn hợp (export chỉ có trên R2), quay lui, backup/restore, từ chối khoá không an toàn, ràng buộc HTTPS của đích.
- `MigrationRehearsalTests` (1 test, R2 thật, 8 bước):
  1. Kiểm kê chế độ chỉ đọc với hai export thật do ứng dụng xuất.
  2. Sao chép, chạy lại không ghi thêm, `verify` cho `RollbackReady`. Sau đó sao lưu kho cũ ra thư mục, khôi phục vào prefix thứ ba và xác minh từng byte.
  3. Chuyển ứng dụng sang kho mới: tải PDF cũ khớp SHA-256; giáo viên khác nhận 404.
  4. Export mới ghi vào kho mới; `DestinationComplete` vẫn đúng nhưng `RollbackReady` sai vì kho cũ thiếu.
  5. Quay lui sau ghi mới: đồng bộ ngược, chuyển lại kho cũ, tải được cả export cũ và mới.
  6. Một bản sao bị làm hỏng: `DestinationComplete` sai, chạy lại `copy` báo xung đột và không ghi đè.
  7. Tài khoản bị khoá bị từ chối.
- Thử CLI trên cơ sở dữ liệu dev: `inventory` báo 26 tham chiếu thiếu trong prefix trống và trả mã `2`; `copy` không có `--writes-paused` bị từ chối; nguồn `http://` không có `SRC_ALLOW_HTTP` bị từ chối; đầu ra không chứa khoá.

## Còn thiếu để đóng #30

- Thử cấu hình nguồn HTTP/path-style trên chính SeaweedFS. Việc này chỉ cần nếu sau này xuất hiện một kho SeaweedFS có dữ liệu thật; hiện không có.
- Một lần diễn tập trên môi trường gần production, gồm cả khôi phục từ backup, do người vận hành thực hiện.
- Quyết định chính thức về thời điểm ngừng kho cũ.

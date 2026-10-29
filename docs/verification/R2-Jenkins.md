# Kiểm chứng PDF/R2 qua Jenkins — #29

Thiết kế: [R2 Jenkins](../superpowers/specs/2026-10-27-r2-jenkins-design.md). Phạm vi là PDF; chuyển dữ liệu cũ vẫn thuộc #30.

## Agent và credentials

Dùng agent Linux chuyên cho dữ liệu test, .NET SDK trong `global.json`, Node 22+, npm, Bash, GNU date, curl, Docker Engine/Compose. Agent cần quyền cài system dependencies Playwright (root hoặc sudo không hỏi mật khẩu), truy cập NuGet/npm và R2 HTTPS. Pipeline cài hai bản Chromium theo đúng package .NET và UI trước khi chạy tests; cache browser nằm trong thư mục run.

Cấp trước bucket R2 private dành riêng cho test. Không dùng production; tắt public access. Tạo Jenkins **Secret text** IDs:

| ID | Giá trị |
| --- | --- |
| `ekt-ci-postgres-password` | Mật khẩu PostgreSQL test |
| `ekt-ci-demo-password` | Mật khẩu giáo viên demo đủ mạnh |
| `ekt-ci-r2-endpoint` | Endpoint HTTPS của account R2, không có path bucket |
| `ekt-ci-r2-bucket` | Tên bucket test đã cấp trước |
| `ekt-ci-r2-access-key` | S3 access key chỉ cho bucket test |
| `ekt-ci-r2-secret-key` | S3 secret key tương ứng |

Key cần Put/Get/Head/List/Delete object trong bucket test; công cụ không gọi CreateBucket/DeleteBucket. Chỉ người quản trị Jenkins nhập secrets; không đưa vào repository, dòng lệnh hoặc artifacts.

## Luồng và điều kiện đạt

`Prepare run → Build → Install browsers → API tests → Browser tests → Stop backend → Cleanup R2/database → Evidence gate`.

Mỗi build tạo thư mục `.local/ci/<uuid>/` riêng và state mới bằng `CreateNew`. Prefix vật lý `ekt-ci/<run-id>/` có nhánh `api/<fixture-id>/` và `browser/`. CLI cleanup xác thực ID/prefix trước truy cập mạng, chỉ List/Delete đúng prefix và có timeout hai phút. Fixture API tự cleanup; bước post còn dọn object mồ côi của run nếu test chưa dispose. Script chờ backend sẵn sàng, chạy browser tuần tự và dừng/chờ backend thoát trước khi post cleanup.

Evidence gate yêu cầu các ca PDF thật, chống tải trái owner, token hết hạn, khóa/thu hồi phiên, lỗi mạng/credentials, retry và cleanup giữ nguyên nhánh khác. Browser phải có ca xuất/tải và retry trên cả desktop/mobile; bytes tải về khớp độ dài và SHA-256 metadata. Skipped/failed/retried cases, thiếu báo cáo hoặc cleanup không thành công đều trả exit code khác 0. Báo cáo phải được tạo sau thời điểm init run.

Jenkins archive bắt buộc `evidence.json`, chỉ gồm run ID, trạng thái và các số đếm. Báo cáo TRX/browser JSON và logs thô nằm trong workspace private, không được archive vì có thể chứa token, nội dung hoặc exception. Không archive PDF, cookies, trace hay screenshots. Khi gate thất bại sau khi đọc được state, CLI xuất trạng thái `Failed` an toàn và trả mã lỗi; không sao chép thông báo lỗi thô.

Nếu agent bị mất hoặc bị kill cứng, `post` có thể không chạy. Người vận hành cần khôi phục state, dừng mọi writer của run rồi chạy cleanup thủ công. Không đánh dấu nghiệm thu Jenkins thành công chỉ từ build/local tests.

## Tái hiện local trên Linux

Nạp sáu giá trị test vào environment bằng cơ chế secrets local; tên R2 là `TEST_S3_ENDPOINT`, `TEST_S3_BUCKET`, `TEST_S3_ACCESS_KEY`, `TEST_S3_SECRET_KEY`; hai tên còn lại là `POSTGRES_PASSWORD`, `DEMO_PASSWORD`. Không bật `set -x`.

Từ thư mục gốc repo, build .NET và `npm ci && npm run typecheck && npm run build` trong `ui`, sau đó:

```bash
set +x
umask 077
export R2CI_DIR=".local/ci/$(cat /proc/sys/kernel/random/uuid)"
export COMPOSE_PROJECT_NAME="ekt-ci-$(cat /proc/sys/kernel/random/uuid)"
mkdir -p "$R2CI_DIR"
export PLAYWRIGHT_BROWSERS_PATH="$PWD/$R2CI_DIR/browsers"
tool="$PWD/tools/R2Ci/bin/Release/net10.0/R2Ci.dll"
dotnet "$tool" browser
(cd ui && npx playwright install --with-deps chromium)
status=0
bash tools/ci/r2-tests.sh || status=$?
dotnet "$tool" cleanup "$R2CI_DIR/state.json" "$R2CI_DIR/cleanup.json" || status=1
docker compose -f compose.ci.yaml down -v || status=1
dotnet "$tool" evidence "$R2CI_DIR/state.json" "$R2CI_DIR/api.trx" \
  "$R2CI_DIR/browser.json" "$R2CI_DIR/cleanup.json" "$R2CI_DIR/evidence.json" || status=1
exit "$status"
```

Không chạy build chồng lên testhost/backend trên Windows: DLL có thể bị khóa. Với PostgreSQL test có sẵn, có thể chạy API trực tiếp bằng `TEST_DATABASE_CONNECTION`, `TEST_S3_*`, `TEST_S3_RUN_PREFIX` lấy từ CLI `prefix`; backend browser nhận cùng cấu hình qua `Reporting__*` và `Reporting__KeyPrefix=<run-prefix>browser/`. Các lệnh `cleanup`/`evidence` giữ nguyên như trên.

## Khôi phục cleanup

Giữ `state.json` của run bị gián đoạn. Dừng backend/testhost của đúng run, nạp credentials đúng bucket test, rồi chạy `cleanup` với state đó. Không tự sửa prefix hoặc dùng prefix cha. Cleanup có thể chạy lại; CLI ghi trạng thái thất bại trước khi gọi storage để kết quả thành công cũ không được dùng lại sau lỗi. Không xóa bucket hoặc kho dữ liệu cũ.

`Reporting__KeyPrefix` mặc định rỗng, giữ đường dẫn cũ. Đổi prefix của deployment đang có dữ liệu làm đổi đường dẫn vật lý nên phải chuyển object trước, tương tự đổi bucket; phạm vi #29 chỉ cấp prefix cho backend test dùng database mới.

## Kết quả thực tế

Đang kiểm chứng local; kết quả cuối và giới hạn Jenkins thật được cập nhật sau khi chạy xong. Chưa có Jenkins endpoint/quyền chạy job trong phiên này; chưa đóng #29.

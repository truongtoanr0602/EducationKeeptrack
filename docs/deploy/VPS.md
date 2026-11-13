# Triển khai Education Keeptrack lên VPS

Dành cho VPS **Ubuntu 22.04 hoặc 24.04, tối thiểu 2 GB RAM** (Chromium dựng PDF, .NET và PostgreSQL cùng chạy). Chạy bằng Docker Compose: `db` (PostgreSQL 18), `migrate` (chạy một lần), `backend` (.NET 10 + Chromium), `ui` (nginx, kết thúc TLS). PDF lưu trên Cloudflare R2, email gửi qua SMTP.

> Không bao giờ đặt `APP_ENVIRONMENT=Development` trên VPS: chế độ đó nới lỏng cookie bảo mật và bật lệnh tạo tài khoản demo. Trên VPS dùng dữ liệu thật: Admin tạo bằng `--setup-admin`, giáo viên đăng ký qua giao diện.

## Đã kiểm chứng và chưa kiểm chứng

Đã chạy thật trên Docker (máy dev), cả hai tệp compose, image build từ Dockerfile:

- Đường Production + HTTPS (chứng chỉ tự ký): chuyển hướng HTTP sang HTTPS, header HSTS/CSP, `--setup-admin` in khoá TOTP, đăng nhập Admin bằng mật khẩu + mã TOTP, cookie phiên `Secure; HttpOnly; SameSite=Strict`, thao tác ghi không có CSRF bị từ chối, đăng ký giáo viên gửi email xác minh qua SMTP từ trong container với liên kết dựng từ `PUBLIC_URL`.
- Nghiệp vụ trong container Linux: giờ Việt Nam lưu đúng UTC, tính học phí, xuất PDF qua Chromium lên R2 rồi tải về (SHA-256 khớp). Lần chạy này phát hiện và sửa lỗi quyền file Playwright khiến user không phải root không xuất được PDF.
- `tools/deploy/backup.sh` và `restore.sh` (khôi phục vào database tạm).

**Chưa kiểm chứng** vì không có trên máy dev, cần kiểm lúc triển khai thật: gửi email qua Gmail thật (thử bằng máy chủ SMTP giả), cấp và gia hạn chứng chỉ Let's Encrypt, hai hook gia hạn, `tools/deploy/update.sh` (cần `git pull` từ GitHub), chạy trên Ubuntu có 2 GB RAM. Khi chuyển hướng HTTP sang HTTPS, máy dev dùng cổng 8443 nên liên kết chuyển hướng không kèm cổng; trên VPS dùng cổng 443 nên không sao.

## 0. Chuẩn bị trước

| Cần có | Ghi chú |
| --- | --- |
| Tên miền | Tạo bản ghi **A** trỏ tới IP của VPS, ví dụ `keeptrack.example.com`. Chờ DNS cập nhật (`nslookup <tên miền>` ra đúng IP). |
| VPS Ubuntu 22.04/24.04, ≥ 2 GB RAM | Thêm swap 2 GB ở mục 1 (bắt buộc). |
| Bucket R2 **production** riêng | Private, tắt public access. Token **Object Read & Write** chỉ cho bucket này. **Không dùng lại `ekt-reports-test`.** |
| Gmail App Password | Bật xác minh 2 bước cho tài khoản Gmail, tạo App Password 16 ký tự (Google Account → Security → App passwords). |
| Khoá SSH, không đăng nhập root bằng mật khẩu | Khuyến nghị bắt buộc. |

## 1. Chuẩn bị máy chủ

```bash
# Tường lửa: chỉ mở SSH, HTTP (chuyển hướng + gia hạn chứng chỉ) và HTTPS.
sudo ufw allow OpenSSH && sudo ufw allow 80/tcp && sudo ufw allow 443/tcp && sudo ufw enable

# Docker Engine chính thức (https://docs.docker.com/engine/install/ubuntu/)
sudo apt-get update && sudo apt-get install -y ca-certificates curl git certbot
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt-get update && sudo apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo usermod -aG docker "$USER"   # đăng xuất rồi đăng nhập lại để có hiệu lực
```

**Thêm swap 2 GB ngay bây giờ** (bắt buộc với VPS 2 GB: lệnh `update.sh` build image trong khi db và backend vẫn đang chạy, đúng lúc dễ bị hệ điều hành kill vì hết RAM):

```bash
sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile && sudo mkswap /swapfile && sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
```

Kiểm tra mạng nội bộ của Compose không đụng gì: `docker network ls` rồi `docker network inspect` các mạng sẵn có. Compose dùng subnet `172.30.110.0/24`; nếu trùng, đổi cả subnet trong `compose.yaml` và `ReverseProxy__KnownProxy` cho khớp.

## 2. Lấy mã nguồn và cấu hình

```bash
git clone https://github.com/truongtoanr0602/EducationKeeptrack.git
cd EducationKeeptrack
cp .env.example .env && chmod 600 .env
nano .env
```

Điền trong `.env` (xem chú thích trong `.env.example`):

- `POSTGRES_PASSWORD`: chuỗi mạnh, ví dụ `openssl rand -base64 33`.
- `APP_ENVIRONMENT=Production`.
- `R2_ENDPOINT`, `R2_BUCKET`, `S3_ACCESS_KEY`, `S3_SECRET_KEY`: của bucket production. `R2_KEY_PREFIX` để trống.
- `PUBLIC_URL=https://<tên miền>` (không có dấu `/` cuối). Mọi liên kết trong email dựng từ giá trị này; sai thì liên kết xác minh bị hỏng.
- `TLS_CERT_PATH`, `TLS_KEY_PATH`: điền ở mục 3.
- `HTTPS_BIND=0.0.0.0`, `HTTPS_PORT=443`, `HTTP_BIND=0.0.0.0`, `HTTP_PORT=80`.
- `SMTP_HOST=smtp.gmail.com`, `SMTP_PORT=587`, `SMTP_USER=<gmail>`, `SMTP_PASSWORD=<App Password viết liền, không có dấu cách>` (Gmail hiển thị 16 ký tự thành bốn nhóm có khoảng trắng; hãy bỏ khoảng trắng), `SMTP_FROM=<gmail>`. Chỉ hỗ trợ STARTTLS cổng 587, không hỗ trợ cổng 465. Nếu để trống `SMTP_HOST`, **không email nào được gửi**: giáo viên không xác minh được email và không đặt lại được mật khẩu.

Backend từ chối khởi động nếu `SMTP_HOST` đã đặt mà thiếu người gửi/mật khẩu hoặc `PUBLIC_URL` không phải https; lỗi hiện ở `docker compose logs backend`.

## 3. Chứng chỉ TLS (Let's Encrypt)

Phải làm trước khi khởi động ứng dụng vì cổng 80 cần trống:

```bash
sudo certbot certonly --standalone -d keeptrack.example.com --agree-tos -m <email-của-bạn>
```

Điền vào `.env`:

```
TLS_CERT_PATH=/etc/letsencrypt/live/keeptrack.example.com/fullchain.pem
TLS_KEY_PATH=/etc/letsencrypt/live/keeptrack.example.com/privkey.pem
```

**Gia hạn:** gói `certbot` tự có bộ hẹn giờ gia hạn. Hai file trong `live/` là symlink và Docker gắn file theo đích lúc tạo container, nên sau khi gia hạn phải tạo lại container `ui` thì nginx mới dùng chứng chỉ mới. Cổng 80 cũng phải trống lúc gia hạn. Tạo hai hook (thay đường dẫn repo cho đúng):

```bash
sudo tee /etc/letsencrypt/renewal-hooks/pre/ekt-stop-ui.sh >/dev/null <<'EOF'
#!/bin/sh
cd /home/<user>/EducationKeeptrack && docker compose -f compose.yaml -f compose.https.yaml stop ui
EOF
sudo tee /etc/letsencrypt/renewal-hooks/post/ekt-recreate-ui.sh >/dev/null <<'EOF'
#!/bin/sh
cd /home/<user>/EducationKeeptrack && docker compose -f compose.yaml -f compose.https.yaml up -d --force-recreate ui
EOF
sudo chmod +x /etc/letsencrypt/renewal-hooks/pre/ekt-stop-ui.sh /etc/letsencrypt/renewal-hooks/post/ekt-recreate-ui.sh
sudo certbot renew --dry-run     # phải báo thành công; ứng dụng gián đoạn vài giây
```

## 4. Khởi động

```bash
docker compose -f compose.yaml -f compose.https.yaml up -d --build
docker compose -f compose.yaml -f compose.https.yaml ps
docker compose -f compose.yaml -f compose.https.yaml logs --tail 50 backend
```

Lần đầu build mất vài phút (tải .NET SDK, Node, Chromium). `migrate` chạy xong rồi thoát với mã 0; `backend`, `db`, `ui` ở trạng thái `running`. Backend từ chối khởi động nếu còn migration chưa áp dụng.

## 5. Tạo Admin đầu tiên

Nhập mật khẩu bằng `read -s` để không nằm trong lịch sử lệnh (≥ 12 ký tự):

```bash
read -rp "Email Admin: " AE; read -rsp "Mật khẩu Admin: " AP; echo
docker compose -f compose.yaml -f compose.https.yaml run --rm -e AdminSetup__Email="$AE" -e AdminSetup__Password="$AP" backend --setup-admin
unset AE AP
```

Lệnh in **khoá TOTP một lần duy nhất**: thêm ngay vào ứng dụng xác thực (Google Authenticator, Authy). Mất khoá này thì không đăng nhập Admin được. Sau đó mở `https://<tên miền>`, đăng nhập Admin bằng mật khẩu và mã 6 số.

Việc đầu tiên của Admin: khai báo thông tin chuyển khoản dịch vụ (màn **Hướng dẫn thanh toán**) để giáo viên biết chuyển tiền đi đâu.

## 6. Giáo viên đầu tiên

1. Giáo viên mở trang, **Đăng ký**, nhận email xác minh (kiểm tra cả thư rác) và bấm liên kết.
2. Giáo viên gửi yêu cầu đối chiếu thanh toán dịch vụ.
3. Admin đối chiếu khoản chuyển khoản thủ công, duyệt và cấp kỳ sử dụng (và bật AI nếu cần).

Không có tài khoản demo trên VPS; lệnh `--provision-teacher` chỉ chạy ở Development.

## 7. Kiểm tra sau triển khai

- [ ] `http://<tên miền>` chuyển hướng sang HTTPS; trình duyệt không cảnh báo chứng chỉ.
- [ ] Đăng nhập Admin với TOTP thành công (chứng minh cookie Secure và CSRF chạy qua TLS).
- [ ] Đăng ký một giáo viên thử bằng email thật: email xác minh tới trong vài phút và liên kết bắt đầu bằng `https://<tên miền>/`.
- [ ] Cấp kỳ cho giáo viên thử, tạo lớp, học sinh, buổi học và điểm danh (kiểm tra múi giờ Việt Nam: ngày trên giao diện đúng).
- [ ] Xuất PDF báo cáo tháng và tải về: mở được, có tiếng Việt.
- [ ] `docker compose ... ps` không có container khởi động lại liên tục.
- [ ] Chạy một lần `bash tools/deploy/backup.sh` và `bash tools/deploy/restore.sh backups/<file>` thành công.

## 8. Sao lưu

PostgreSQL giữ mọi dữ liệu nghiệp vụ. Sao lưu hằng ngày:

```bash
crontab -e
# 02:30 mỗi ngày (giờ máy chủ), giữ 14 ngày:
30 2 * * * cd /home/<user>/EducationKeeptrack && bash tools/deploy/backup.sh >> backups/backup.log 2>&1
```

- **Chép bản sao ra ngoài VPS** (máy khác, hoặc bucket R2/S3 **riêng** cho sao lưu, ví dụ bằng `rclone`). Bản sao chỉ nằm trên chính VPS thì mất cùng VPS.
- **Diễn tập khôi phục** ít nhất một lần: `bash tools/deploy/restore.sh backups/ekt-<thời-gian>.dump` khôi phục vào database tạm `ekt_restore_test`, kiểm tra rồi xoá; không đụng database đang chạy.
- PDF nằm trên R2, không nằm trong bản sao PostgreSQL. Bật tính năng bảo vệ/phiên bản của bucket nếu cần giữ lịch sử; việc chuyển kho PDF xem `docs/verification/T30-r2-migration.md`.
- Khoá phiên (`session-keys`) nằm trong volume Docker. Mất volume này chỉ khiến mọi người phải đăng nhập lại.

### Khôi phục thật (sau sự cố)

```bash
C="docker compose -f compose.yaml -f compose.https.yaml"
$C stop backend ui
$C exec -T db psql -U education_keeptrack -d postgres -c "DROP DATABASE education_keeptrack" -c "CREATE DATABASE education_keeptrack OWNER education_keeptrack"
$C exec -T db pg_restore -U education_keeptrack -d education_keeptrack --no-owner < backups/ekt-<thời-gian>.dump
$C up -d
```

## 9. Cập nhật phiên bản

```bash
bash tools/deploy/update.sh
```

Script tự sao lưu trước (migration chỉ đi tiến), kéo `master`, build lại, khởi động lại và kiểm tra `GET /api/auth/csrf` trả 200. Thất bại sau khi migration đã chạy thì **không** quay lại mã cũ trên cơ sở dữ liệu mới: khôi phục bản sao lưu vừa tạo (mục trên) rồi `git checkout <commit-cũ>` và `up -d --build`.


## 10. Xử lý sự cố

| Dấu hiệu | Cách xử lý |
| --- | --- |
| Backend dừng ngay khi khởi động | `docker compose ... logs backend`. Thường do `Identity:Email` không hợp lệ (`PUBLIC_URL` không phải https, thiếu `SMTP_FROM`/mật khẩu) hoặc còn migration chưa áp dụng. |
| Không nhận được email | `docker compose ... logs backend \| grep "was not delivered"`. Dòng đó chỉ ghi tên loại lỗi, ví dụ `SmtpException`. Kiểm tra App Password, `SMTP_FROM` phải là chính địa chỉ Gmail, cổng 587. Giáo viên có thể bấm "gửi lại email xác minh". Gmail giới hạn khoảng 500 email mỗi ngày. |
| Liên kết trong email sai tên miền | Sửa `PUBLIC_URL` rồi `up -d`. |
| `502` hoặc trang trắng | `ps` và `logs ui backend`. Backend đang khởi động lại thì chờ; nếu lỗi kéo dài xem log backend. |
| Cảnh báo chứng chỉ hết hạn | Chạy `sudo certbot renew --dry-run`, kiểm tra hai hook ở mục 3. |
| Xuất PDF báo "chưa cấu hình kho" | Kiểm tra `R2_*`, `S3_*` trong `.env`; endpoint không chứa tên bucket. |
| Hết dung lượng đĩa | `docker system df`, xoá bản sao cũ trong `backups/`, `docker image prune`. Log container đã tự giới hạn 10 MB × 3. |

Xem log: `docker compose -f compose.yaml -f compose.https.yaml logs -f --tail 100 backend`.

## 11. Bảo mật tối thiểu

- `.env` mang quyền `600`, không đưa vào Git, không gửi qua chat.
- Chỉ mở cổng 22, 80, 443; PostgreSQL không publish ra ngoài.
- Bật cập nhật bảo mật tự động của Ubuntu (`unattended-upgrades`).
- Dùng bucket R2 production riêng và token chỉ cho bucket đó; xoay vòng token khi nghi lộ.
- Thay App Password Gmail ngay nếu nghi lộ (thu hồi trong Google Account).

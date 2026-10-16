# React UI và PostgreSQL theo module

**Trạng thái: accepted — 16/10/2026.** Người dùng đã chốt React + TypeScript + Vite cho UI và PostgreSQL cho dữ liệu, bên cạnh backend .NET Modular Monolith theo đề cương. UI cần tương tác nhiều và chuyển mẫu Stitch thành component; chấp nhận duy trì thêm TypeScript ngoài C# thay vì chọn Blazor để giảm số ngôn ngữ.

Dùng một PostgreSQL database, schema và DbContext riêng cho từng module. Giao tiếp qua hợp đồng/sự kiện, không đọc ghi trực tiếp bảng module khác. Schema phân chia trách nhiệm module; kiểm tra sở hữu theo giáo viên vẫn bắt buộc, không coi schema là ranh giới tenant.

Phiên bản runtime, provider EF Core, thư viện UI cụ thể và môi trường gửi email sẽ được chọn theo khả năng tương thích khi triển khai; chưa được quyết định bằng ADR này.


# Billing sở hữu đơn giá hiệu lực và phí từng buổi

**Trạng thái: accepted — 17/10/2026.** [T04](https://github.com/truongtoanr0602/EducationKeeptrack/issues/5) quy định lưu đơn giá theo ngày hiệu lực và phí từng buổi tại Billing. Ranh giới module trong spec tổng ban đầu là đề xuất; T04 cụ thể hóa quyền sở hữu đơn giá. Classroom tiếp tục sở hữu lớp, học sinh, buổi và điểm danh.

Đặt đơn giá cạnh phí từng buổi giúp Billing giữ căn cứ giá bất biến khi sửa điểm danh và khi nhận sự kiện trễ. Classroom cung cấp hợp đồng đọc ảnh chụp nhất quán và sự kiện bền vững; Billing không truy vấn schema hay DbContext Classroom. Các tham chiếu liên module là định danh được kiểm tra qua hợp đồng, không có khóa ngoại xuyên schema.

Mỗi buổi đã diễn ra giữ đơn giá đầu tiên được xác định theo ngày buổi tại Việt Nam. Trước khi thêm giá mới, Billing bảo vệ cả các buổi đã diễn ra nhưng chưa kịp nhận sự kiện. Không tự áp ngược giá vào lịch sử đã có căn cứ; thay đổi tài chính quá khứ thuộc thao tác điều chỉnh riêng có lý do. Phí, phiên bản tổng hợp và inbox/outbox của Billing được lưu trong cùng giao dịch để nhận lại sự kiện hoặc khởi động lại không tính trùng.

# Đánh giá ngoại tuyến mô hình cá nhân hoá

Sinh bởi `tools/MlEval`. Mỗi con số là trung bình ± độ lệch chuẩn trên **30 hạt giống độc lập**. Dữ liệu hoàn toàn **mô phỏng**, không có học sinh thật. Mỗi lịch sử gồm 6 lần đánh giá, mỗi lần 5 nhiệm vụ; "đạt" nghĩa là đúng từ 80% (4/5).

## Bộ sinh đúng giả định (hai trạng thái biết/chưa biết, đúng dạng BKT)

### Nhiệm vụ A: dự đoán lần đánh giá kế tiếp có đạt hay không

| Phương pháp | AUC ↑ | Log loss ↓ | Brier ↓ | Độ chính xác ↑ |
|---|---|---|---|---|
| Hằng số (tỷ lệ đạt chung) | 0.500 ± 0.000 | 0.666 ± 0.014 | 0.237 ± 0.007 | 0.619 ± 0.023 |
| Lần gần nhất | 0.858 ± 0.012 | 0.683 ± 0.034 | 0.177 ± 0.009 | 0.816 ± 0.013 |
| Trung bình các lần | 0.847 ± 0.013 | 0.899 ± 0.053 | 0.242 ± 0.014 | 0.683 ± 0.020 |
| Quy tắc mức đạt hiện tại (hiệu chỉnh) | 0.724 ± 0.013 | 0.549 ± 0.014 | 0.188 ± 0.005 | 0.664 ± 0.027 |
| BKT, tham số mặc định | 0.862 ± 0.012 | 0.487 ± 0.026 | 0.139 ± 0.008 | 0.842 ± 0.010 |
| BKT, tham số học từ dữ liệu huấn luyện | 0.862 ± 0.012 | 0.469 ± 0.029 | 0.137 ± 0.008 | 0.843 ± 0.010 |

Tham số BKT học được (trung bình 30 lần): PInit 0.31, PLearn 0.24, PGuess 0.20, PSlip 0.10.

### Nhiệm vụ B: chọn 2 trong 8 kỹ năng cần ôn (độ chính xác chọn đúng kỹ năng yếu, precision@2 ↑)

| Phương pháp | Precision@2 |
|---|---|
| Chọn ngẫu nhiên | 0.313 ± 0.034 |
| Thấp nhất theo trung bình các lần | 0.703 ± 0.026 |
| Quy tắc + ngẫu nhiên | 0.509 ± 0.029 |
| Chỉ xác suất BKT thấp nhất | 0.719 ± 0.025 |
| Quy tắc + xếp hạng BKT (mặc định) | 0.722 ± 0.025 |
| Quy tắc + xếp hạng BKT (đã học tham số) | 0.722 ± 0.025 |

## Bộ sinh sai giả định (năng lực liên tục, tăng dần, có quên; không có trạng thái rời rạc)

### Nhiệm vụ A: dự đoán lần đánh giá kế tiếp có đạt hay không

| Phương pháp | AUC ↑ | Log loss ↓ | Brier ↓ | Độ chính xác ↑ |
|---|---|---|---|---|
| Hằng số (tỷ lệ đạt chung) | 0.500 ± 0.000 | 0.634 ± 0.014 | 0.221 ± 0.007 | 0.673 ± 0.020 |
| Lần gần nhất | 0.855 ± 0.013 | 0.461 ± 0.023 | 0.142 ± 0.008 | 0.808 ± 0.014 |
| Trung bình các lần | 0.871 ± 0.012 | 0.474 ± 0.022 | 0.146 ± 0.008 | 0.794 ± 0.015 |
| Quy tắc mức đạt hiện tại (hiệu chỉnh) | 0.677 ± 0.014 | 0.529 ± 0.016 | 0.173 ± 0.007 | 0.778 ± 0.013 |
| BKT, tham số mặc định | 0.879 ± 0.011 | 0.479 ± 0.029 | 0.147 ± 0.010 | 0.819 ± 0.013 |
| BKT, tham số học từ dữ liệu huấn luyện | 0.879 ± 0.011 | 0.440 ± 0.020 | 0.136 ± 0.007 | 0.824 ± 0.012 |

Tham số BKT học được (trung bình 30 lần): PInit 0.26, PLearn 0.09, PGuess 0.27, PSlip 0.20.

### Nhiệm vụ B: chọn 2 trong 8 kỹ năng cần ôn (độ chính xác chọn đúng kỹ năng yếu, precision@2 ↑)

| Phương pháp | Precision@2 |
|---|---|
| Chọn ngẫu nhiên | 0.672 ± 0.019 |
| Thấp nhất theo trung bình các lần | 0.980 ± 0.007 |
| Quy tắc + ngẫu nhiên | 0.792 ± 0.021 |
| Chỉ xác suất BKT thấp nhất | 0.980 ± 0.007 |
| Quy tắc + xếp hạng BKT (mặc định) | 0.980 ± 0.007 |
| Quy tắc + xếp hạng BKT (đã học tham số) | 0.897 ± 0.033 |

## Ảnh hưởng của lượng dữ liệu huấn luyện (bộ sinh đúng giả định)

AUC của nhiệm vụ A trên cùng 300 lịch sử kiểm tra, khi học tham số từ N lịch sử huấn luyện.

| N lịch sử huấn luyện | BKT mặc định | BKT đã học tham số |
|---|---|---|
| 5 | 0.859 ± 0.009 | 0.858 ± 0.009 |
| 10 | 0.857 ± 0.010 | 0.858 ± 0.010 |
| 20 | 0.859 ± 0.013 | 0.859 ± 0.013 |
| 50 | 0.860 ± 0.012 | 0.859 ± 0.012 |
| 100 | 0.861 ± 0.009 | 0.861 ± 0.009 |
| 200 | 0.858 ± 0.010 | 0.858 ± 0.010 |

## Hạn chế

- Dữ liệu mô phỏng; kết quả cho biết mô hình hoạt động ra sao khi giả định của bộ sinh đúng hoặc sai ở mức đã nêu, không phải bằng chứng về hiệu quả trên học sinh thật.
- Bộ sinh "đúng giả định" có lợi cho BKT vì dữ liệu được tạo bởi chính dạng mô hình đó; bộ "sai giả định" là phép thử công bằng hơn nhưng vẫn do người viết tự chọn.
- Mức "đạt" và nhãn "kỹ năng yếu" lấy theo ngưỡng 80% của dự án; đổi ngưỡng sẽ đổi số liệu.
- Chưa đo tác động của kế hoạch ôn lên kết quả học sinh (cần thử nghiệm có đối chứng với lớp thật).
- Ở nhiệm vụ B, nhãn thật là trạng thái của lần đánh giá kế tiếp, có yếu tố ngẫu nhiên (chuyển trạng thái, đoán, nhầm) nên không phương pháp nào đạt 1,0; mức trần thực tế thấp hơn.
- Chênh lệch giữa các phương pháp mạnh (AUC, precision@2) nhiều chỗ nhỏ hơn độ lệch chuẩn giữa các hạt giống; chỉ các khác biệt lớn hơn rõ rệt (ví dụ so với ngẫu nhiên, hằng số hay quy tắc) mới nên được xem là có ý nghĩa. Chưa chạy kiểm định ghép cặp.
- Lợi thế đo được của BKT là xác suất được hiệu chỉnh tốt hơn (log loss, Brier), không phải thứ hạng tốt hơn (AUC). Học tham số từ dữ liệu không cải thiện AUC ở mọi cỡ mẫu thử, và khi dữ liệu sai giả định còn làm nhiệm vụ B kém đi.

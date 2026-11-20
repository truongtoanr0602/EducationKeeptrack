using EducationKeeptrack.Learning.Domain;
namespace EducationKeeptrack.Learning.Infrastructure;

/// <summary>
/// Content of the downloadable template: Toán 2 – HK1, Kết nối tri thức, after doc/khung-giao-an-toan-2-hk1.md
/// (30 lessons, skills E1–E9 and a few sample questions of every level and type).
/// </summary>
public static class Toan2Hk1Sample
{
    public static readonly ImportInfo Info = new("Toán", "Lớp 2", "Học kỳ 1", "Kết nối tri thức với cuộc sống");

    public static readonly (string Code, string Name, string Strand, string Criteria)[] Skills =
    [
        ("E1", "Cấu tạo số (chục – đơn vị), so sánh số", "Số và phép tính", "Đọc, viết, so sánh, sắp xếp số đến 100; nêu được cấu tạo chục – đơn vị."),
        ("E2", "Tia số, số liền trước – số liền sau", "Số và phép tính", "Đọc vị trí số trên tia số, điền số còn thiếu; tìm đúng số liền trước, số liền sau."),
        ("E3", "Tên thành phần phép tính; hơn, kém nhau bao nhiêu", "Số và phép tính", "Gọi đúng số hạng, tổng, số bị trừ, số trừ, hiệu; dùng phép trừ để tìm hơn/kém nhau bao nhiêu."),
        ("E4", "Cộng, trừ qua 10 trong phạm vi 20", "Số và phép tính", "Thuộc bảng cộng, trừ qua 10; tách – gộp để đủ 10, không đếm ngón tay."),
        ("E5", "Cộng, trừ có nhớ trong phạm vi 100", "Số và phép tính", "Cộng, trừ có nhớ một lần với số có hai chữ số; nhớ sang hàng chục đúng; thử lại bằng phép tính ngược."),
        ("E6", "Đặt tính thẳng cột", "Số và phép tính", "Đặt tính đúng hàng (đơn vị thẳng cột đơn vị, chục thẳng cột chục), tính từ phải sang trái."),
        ("E7", "Giải bài toán có lời văn một bước", "Giải toán", "Chọn đúng phép tính (thêm/bớt, nhiều hơn/ít hơn, hơn kém); trình bày đủ lời giải – phép tính – đáp số."),
        ("E8", "Đo lường: kg, lít, giờ – phút, ngày – tháng", "Đo lường", "Đọc cân, ca đong, đồng hồ, tờ lịch; dùng đúng đơn vị kg, lít, giờ, phút."),
        ("E9", "Hình phẳng, độ dài đường gấp khúc", "Hình học", "Nhận biết điểm, đoạn thẳng, đường thẳng, đường cong, đường gấp khúc, hình tứ giác; tính độ dài đường gấp khúc (cm).")
    ];

    private const string T1 = "Chủ đề 1: Ôn tập và bổ sung", T2 = "Chủ đề 2: Phép cộng, trừ (qua 10) trong phạm vi 20",
        T3 = "Chủ đề 3: Làm quen với khối lượng, dung tích", T4 = "Chủ đề 4: Phép cộng, trừ (có nhớ) trong phạm vi 100",
        T5 = "Chủ đề 5: Làm quen với hình phẳng", T6 = "Chủ đề 6: Ngày – giờ, giờ – phút, ngày – tháng", T7 = "Chủ đề 7: Ôn tập học kì 1";

    public static readonly (int No, string Topic, string Title, string Knowledge, string SkillsText, string Outcomes, string Competencies, string Codes)[] Lessons =
    [
        (1, T1, "Bài 1: Ôn tập các số đến 100", "Cấu tạo chục – đơn vị; so sánh, thứ tự số", "Đọc, viết, so sánh (>, <, =), xếp thứ tự; tách số thành chục + đơn vị", "Đọc, viết, so sánh và sắp xếp được các số đến 100; nêu được cấu tạo chục – đơn vị.", "TD, GT; cẩn thận khi viết số", "E1"),
        (2, T1, "Bài 2: Tia số. Số liền trước, số liền sau", "Tia số, vạch chia, số liền trước/sau", "Đọc vị trí số, điền số còn thiếu trên tia số, tìm số liền trước/sau", "Dùng được tia số; tìm đúng số liền trước, số liền sau.", "TD, CC; kiên nhẫn quan sát", "E2"),
        (3, T1, "Bài 3: Các thành phần của phép cộng, phép trừ", "Số hạng, tổng, số bị trừ, số trừ, hiệu", "Gọi đúng tên thành phần; viết phép tính từ tranh/tình huống", "Gọi đúng tên các thành phần của phép cộng, phép trừ.", "GT (dùng thuật ngữ), TD", "E3"),
        (4, T1, "Bài 4: Hơn, kém nhau bao nhiêu", "Hơn/kém nhau bao nhiêu = so sánh bằng phép trừ", "Ghép cặp 1–1, chọn phép trừ, trả lời \"hơn/kém bao nhiêu\"", "Tìm được hai số hơn, kém nhau bao nhiêu bằng phép trừ.", "MH, GQ", "E3, E7"),
        (5, T1, "Bài 5: Ôn tập cộng, trừ (không nhớ) trong phạm vi 100", "Cộng, trừ số hai chữ số không nhớ", "Đặt tính thẳng cột, tính từ phải sang trái; nhẩm số tròn chục", "Đặt tính và tính đúng phép cộng, trừ không nhớ trong phạm vi 100.", "TD; trình bày sạch, cẩn thận", "E6"),
        (6, T1, "Bài 6: Luyện tập chung", "Hệ thống Chủ đề 1", "Vận dụng tổng hợp; giải bài toán 1 bước đơn giản", "Vận dụng tổng hợp kiến thức Chủ đề 1; giải được bài toán một bước đơn giản.", "GQ; tự tin, biết sửa lỗi", "E1, E2, E3, E6, E7"),
        (7, T2, "Bài 7: Phép cộng (qua 10) trong phạm vi 20", "Cộng có tổng 11–20; tách để gộp đủ 10 (8 + 5 = 8 + 2 + 3)", "Tách số, dùng que tính/khung 10/sơ đồ tách – gộp", "Thực hiện được phép cộng qua 10 bằng cách tách – gộp đủ 10.", "TD, CC; kiên trì", "E4"),
        (8, T2, "Bài 8: Bảng cộng (qua 10)", "Bảng cộng qua 10; tính giao hoán", "Lập bảng, nhẩm nhanh, thuộc bảng (không đếm tay)", "Thuộc bảng cộng qua 10 và nhẩm nhanh.", "TD; tự giác luyện tập", "E4"),
        (9, T2, "Bài 9: Bài toán về thêm, bớt một số đơn vị", "Dạng \"thêm/bớt\" tương ứng cộng/trừ", "Xác định cái đã cho/cái hỏi; viết lời giải – phép tính – đáp số", "Giải và trình bày được bài toán thêm, bớt một số đơn vị.", "MH, GQ, GT", "E7"),
        (10, T2, "Bài 10: Luyện tập chung (+ Test 1)", "Hệ thống cộng qua 10, thêm/bớt", "Tính nhẩm, điền số, giải toán lời văn, tự kiểm tra", "Vận dụng cộng qua 10 và bài toán thêm/bớt; tự kiểm tra bài làm.", "GQ; trung thực khi làm bài", "E4, E7"),
        (11, T2, "Bài 11: Phép trừ (qua 10) trong phạm vi 20", "Trừ có số bị trừ 11–18; trừ để về 10 rồi trừ tiếp", "Tách số trừ; dùng quan hệ cộng – trừ để thử lại", "Thực hiện được phép trừ qua 10 bằng cách trừ về 10.", "TD", "E4"),
        (12, T2, "Bài 12: Bảng trừ (qua 10)", "Bảng trừ qua 10; \"gia đình phép tính\" (cộng ↔ trừ)", "Nhẩm nhanh; lập 4 phép tính từ 1 bộ ba số", "Thuộc bảng trừ qua 10; lập được các phép tính từ một bộ ba số.", "TD, GT", "E4"),
        (13, T2, "Bài 13: Bài toán về nhiều hơn, ít hơn một số đơn vị", "Dạng \"nhiều hơn/ít hơn\" (khác \"hơn kém nhau bao nhiêu\")", "Vẽ sơ đồ đoạn thẳng, chọn phép tính, trình bày lời giải", "Giải và trình bày được bài toán nhiều hơn, ít hơn một số đơn vị.", "MH, GQ", "E7"),
        (14, T2, "Bài 14: Luyện tập chung", "Hệ thống Chủ đề 2", "Nhẩm cộng/trừ qua 10; giải toán 1 bước", "Nhẩm đúng cộng, trừ qua 10; giải được bài toán một bước.", "GQ; chủ động, cẩn thận", "E4, E7"),
        (15, T3, "Bài 15: Ki-lô-gam", "Đơn vị kg; cân đĩa, cân đồng hồ", "Đọc số cân, so sánh nặng/nhẹ, cộng trừ số đo kg, ước lượng vật 1 kg", "Đọc được số cân; cộng, trừ số đo kg; ước lượng vật nặng 1 kg.", "CC, MH; gắn với đời sống", "E8"),
        (16, T3, "Bài 16: Lít", "Đơn vị lít; bình/ca chia vạch", "Đọc dung tích trên vạch, cộng trừ số đo lít, ước lượng", "Đọc được dung tích trên vạch chia; cộng, trừ số đo lít.", "CC, MH", "E8"),
        (17, T3, "Bài 17 + 18: Thực hành, trải nghiệm kg, lít + Luyện tập chung", "Vận dụng kg, lít trong tình huống thực tế", "Cân, đong thực tế; giải toán có đơn vị kg/lít; làm việc nhóm", "Cân, đong thực tế; giải được bài toán có đơn vị kg, lít.", "GQ, CC; hợp tác, trách nhiệm", "E8, E7"),
        (18, T4, "Bài 19: Cộng (có nhớ) số có hai chữ số với số có một chữ số", "Hàng đơn vị ≥ 10 thì viết đơn vị, nhớ 1 chục (38 + 7)", "Đặt tính thẳng cột; cộng đơn vị trước, nhớ sang chục; thử lại", "Đặt tính và cộng có nhớ số có hai chữ số với số có một chữ số.", "TD; cẩn thận", "E5, E6"),
        (19, T4, "Bài 20: Cộng (có nhớ) số có hai chữ số với số có hai chữ số", "Cộng có nhớ 1 lần (47 + 25)", "Đặt tính – tính; ước lượng tròn chục để kiểm tra; giải toán lời văn", "Đặt tính và cộng có nhớ hai số có hai chữ số; kiểm tra bằng ước lượng.", "TD, GQ", "E5, E6"),
        (20, T4, "Bài 21: Luyện tập chung (+ Test 2)", "Hệ thống cộng có nhớ", "Tính, điền số, so sánh, giải toán có cộng có nhớ", "Vận dụng cộng có nhớ để tính, so sánh và giải toán.", "GQ; trung thực", "E5, E6, E7"),
        (21, T4, "Bài 22: Trừ (có nhớ) số có hai chữ số cho số có một chữ số", "Đơn vị không đủ trừ thì đổi 1 chục thành 10 đơn vị (42 − 7)", "Đặt tính, nhớ sang chục; thử lại bằng phép cộng", "Đặt tính và trừ có nhớ số có hai chữ số cho số có một chữ số.", "TD; kiên trì", "E5, E6"),
        (22, T4, "Bài 23: Trừ (có nhớ) số có hai chữ số cho số có hai chữ số", "Trừ có nhớ 1 lần (52 − 27)", "Đặt tính – tính; kiểm tra bằng phép cộng; giải toán lời văn", "Đặt tính và trừ có nhớ hai số có hai chữ số; thử lại bằng phép cộng.", "TD, GQ", "E5, E6"),
        (23, T4, "Bài 24: Luyện tập chung", "Hệ thống cộng, trừ có nhớ trong phạm vi 100", "Tính, điền dấu, giải toán 1 bước; tự tìm và sửa lỗi", "Thành thạo cộng, trừ có nhớ trong phạm vi 100; tự tìm và sửa lỗi.", "GQ; tự tin, chủ động", "E5, E6, E7"),
        (24, T5, "Bài 25: Điểm, đoạn thẳng, đường thẳng, đường cong, ba điểm thẳng hàng", "Nhận biết và gọi tên các yếu tố hình học cơ bản", "Nhận dạng trên hình và vật thật; dùng thước kiểm tra 3 điểm thẳng hàng", "Nhận biết điểm, đoạn thẳng, đường thẳng, đường cong, ba điểm thẳng hàng.", "TD, CC (thước)", "E9"),
        (25, T5, "Bài 26: Đường gấp khúc. Hình tứ giác", "Đường gấp khúc (đoạn, đỉnh); độ dài = tổng độ dài các đoạn; hình tứ giác", "Gọi tên, đếm đoạn/đỉnh; tính độ dài đường gấp khúc (cm); nhận biết tứ giác", "Tính được độ dài đường gấp khúc (cm); nhận biết hình tứ giác.", "TD, MH", "E9"),
        (26, T5, "Bài 27 + 28: Thực hành gấp, cắt, ghép, xếp hình. Vẽ đoạn thẳng + Luyện tập chung", "Các hình phẳng đã học", "Gấp, cắt, ghép; vẽ đoạn thẳng dài X cm bằng thước; đo đoạn thẳng", "Vẽ được đoạn thẳng có độ dài cho trước; gấp, cắt, ghép được hình.", "CC, GQ; khéo tay, sáng tạo", "E9"),
        (27, T6, "Bài 29: Ngày – giờ, giờ – phút", "1 ngày = 24 giờ; 1 giờ = 60 phút; kim giờ, kim phút", "Phân biệt hai kim; đọc giờ, phút trên đồng hồ", "Đọc đúng giờ, phút trên đồng hồ.", "CC, MH; biết quý thời gian", "E8"),
        (28, T6, "Bài 30 + 31 + 32: Ngày – tháng; Thực hành xem đồng hồ, xem lịch; Luyện tập chung", "Ngày, tháng, thứ trên tờ lịch", "Xem lịch tìm thứ/ngày/tháng; đọc đồng hồ trong sinh hoạt hằng ngày", "Xem được lịch để tìm thứ, ngày, tháng.", "MH, CC; đúng giờ, có kế hoạch", "E8"),
        (29, T7, "Bài 33 + 34: Ôn tập cộng, trừ trong phạm vi 20, 100 + Ôn tập hình phẳng", "Bảng cộng/trừ qua 10; cộng, trừ có nhớ; các hình phẳng", "Nhẩm, đặt tính, lời văn 1 bước; nhận dạng hình, tính độ dài đường gấp khúc", "Hệ thống phép cộng, trừ và hình phẳng của học kỳ 1.", "GQ, TD", "E4, E5, E6, E7, E9"),
        (30, T7, "Bài 35 + 36: Ôn tập đo lường + Ôn tập chung (+ Test 3)", "kg, lít, giờ – phút, ngày – tháng, cm", "Vận dụng tổng hợp; tự kiểm tra, tự đánh giá", "Đạt chuẩn đầu ra học kỳ 1; tự kiểm tra, tự đánh giá.", "GQ, GT; trung thực, tự tin", "E7, E8")
    ];

    /// <summary>Sample rows of the question sheet: Code, Type label, Level label, primary, secondary, lesson, stem, stem image, A–D, chips, answer.</summary>
    public static readonly string[][] Questions =
    [
        ["T2-001", "Trắc nghiệm", "Nhận biết", "E1", "", "1", "Số gồm 6 chục và 4 đơn vị là:", "", "46", "64", "604", "10", "", "B"],
        ["T2-002", "Điền ô", "Nhận biết", "E1", "", "1", "Điền dấu thích hợp: 49 [ ] 94", "", "", "", "", "", ">; <; =", "<"],
        ["T2-003", "Điền ô", "Nhận biết", "E2", "", "2", "Số liền trước của 40 là [ ].", "", "", "", "", "", "39; 41; 30; 50", "39"],
        ["T2-004", "Điền ô", "Thông hiểu", "E2", "", "2", "Điền số còn thiếu trên tia số: 10, 20, [ ], 40", "", "", "", "", "", "25; 30; 35; 50", "30"],
        ["T2-005", "Trắc nghiệm", "Thông hiểu", "E3", "", "3", "Trong phép tính 6 + 3 = 9, số 9 được gọi là:", "", "Số hạng", "Tổng", "Hiệu", "Số trừ", "", "B"],
        ["T2-006", "Điền ô", "Nhận biết", "E4", "", "7", "8 + 5 = [ ]", "", "", "", "", "", "12; 13; 14; 15", "13"],
        ["T2-007", "Điền ô", "Thông hiểu", "E4", "", "11", "15 − 7 = [ ]", "", "", "", "", "", "7; 8; 9; 22", "8"],
        ["T2-008", "Điền ô", "Thông hiểu", "E5", "E6", "18", "38 + 7 = [ ]", "", "", "", "", "", "35; 45; 46; 315", "45"],
        ["T2-009", "Điền ô", "Vận dụng", "E5", "E6", "22", "52 − 27 = [ ]", "", "", "", "", "", "25; 35; 79; 24", "25"],
        ["T2-010", "Trắc nghiệm", "Thông hiểu", "E6", "", "21", "Khi đặt tính 60 − 7, chữ số 7 phải thẳng cột với chữ số nào của số 60?", "", "Chữ số 6", "Chữ số 0", "", "", "", "B"],
        ["T2-011", "Trắc nghiệm", "Vận dụng", "E7", "", "9", "Lan có 15 quả táo, Lan cho bạn 6 quả. Phép tính tìm số táo Lan còn lại là:", "", "15 + 6 = 21", "15 − 6 = 9", "6 + 9 = 15", "", "", "B"],
        ["T2-012", "Điền ô", "Vận dụng cao", "E7", "E3", "13", "Hà có 12 viên bi. Hà có ít hơn Nam 4 viên bi. Nam có [ ] viên bi.", "", "", "", "", "", "8; 16; 12; 4", "16"],
        ["T2-013", "Trắc nghiệm", "Nhận biết", "E8", "", "27", "1 giờ = ... phút", "", "24", "60", "100", "12", "", "B"],
        ["T2-014", "Điền ô", "Vận dụng", "E8", "E7", "16", "Can thứ nhất có 15 lít dầu, can thứ hai có ít hơn can thứ nhất 7 lít. Can thứ hai có [ ] lít dầu.", "", "", "", "", "", "8; 22; 7; 9", "8"],
        ["T2-015", "Điền ô", "Vận dụng", "E9", "", "25", "Đường gấp khúc gồm ba đoạn dài 3 cm, 4 cm và 2 cm. Độ dài đường gấp khúc là [ ] cm.", "", "", "", "", "", "7; 8; 9; 10", "9"],
        ["T2-016", "Trắc nghiệm", "Vận dụng cao", "E1", "", "6", "Số lớn nhất có hai chữ số khác nhau là:", "", "99", "98", "89", "90", "", "B"]
    ];
}

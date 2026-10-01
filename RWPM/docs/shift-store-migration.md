# Chuyển đổi Shift sang ca áp dụng nhiều chi nhánh

## Những gì thay đổi

- `Shift` giữ nguyên khóa `ShiftId`; bỏ mã, loại, mẫu ca, StoreId, headcount và các trường ngoài giờ.
- `StoreShift` có khóa `(StoreId, ShiftId)`. Ca được sử dụng khi cả ca, liên kết và cửa hàng hoạt động; ngày làm nằm trong hiệu lực.
- Bỏ hoàn toàn chức năng sao chép. Tạo ca mới khi cần giờ riêng.
- Nghỉ được cấu hình bằng một khoảng thời gian không tính công. Ca tối đa 12 giờ, tối thiểu 2 giờ; nghỉ tối đa 60 phút; làm sau nghỉ tối thiểu 2 giờ.
- Check-in sớm và ngưỡng vắng mặt dùng `ShiftDefaults`. Dung sai đi muộn/về sớm bắt buộc lưu số riêng cho ca; form tạo điền sẵn mặc định để sửa. Migration `RequireExplicitShiftTolerances` chuyển NULL cũ thành 15 phút, giữ nguyên các số đã nhập (kể cả 0). Thay đổi mặc định không cập nhật các ca đã lưu.
- Ca đã được sử dụng không sửa giờ, nghỉ, hiệu lực hoặc chính sách. Có thể sửa tên/mô tả và các liên kết cửa hàng theo rule đăng ký tương lai.
- Bỏ lựa chọn chi nhánh trong form sửa sẽ ngừng `StoreShift`, không xóa liên kết. Có đăng ký Pending/Approved tương lai tại chi nhánh thì chặn.
- Quyền truy cập controller được giữ theo dự án hiện tại. Việc phân lại quyền HR/Store Manager là công việc riêng.

## Chuẩn bị dữ liệu trước khi cập nhật

1. Sao lưu toàn bộ database và dừng ứng dụng cũ trong lúc chuyển đổi.
2. Chạy `scripts/prepare-shift-breaks.sql` trên schema cũ để liệt kê những ca có nghỉ.
3. Điền giờ nghỉ thực tế và day offset cho mỗi ca nghỉ không tính công; thời lượng phải bằng `BreakMinutes` cũ. Đặt `Confirmed=1` sau khi rà soát.
4. Ca nghỉ được tính công: xác nhận bằng `Confirmed=1`, để khoảng nghỉ mới NULL. Việc này giữ tổng giờ được trả công; thông tin nghỉ được giữ trong archive để đối chiếu. Nếu vẫn cần mô hình nghỉ trả công thì chưa nên chuyển các ca đó sang cách tính mới.
5. Rà soát override `EarlyCheckInMinutes`/`LateThresholdMinutes` cũ trước khi chuyển về policy chung. Hai trường headcount được lưu archive, không tự biến thành nhu cầu nhân sự theo ngày.

Migration không tự đặt giờ nghỉ. Nếu thiếu bản xác nhận, migration báo lỗi và transaction không chuyển đổi schema.

## Áp dụng

Từ thư mục `RWPM`:

```powershell
dotnet ef database update
```

`Program.cs` hiện tự chạy migration khi khởi động, vì vậy phải chuẩn bị mapping trước khi chạy web bản mới.
Lệnh thiết kế migration sử dụng `DesignTimeDatabaseContextFactory`, không chạy đường startup/seeder của ứng dụng.

Migration giữ ca, đăng ký và chấm công theo ShiftId cũ. Ca cũ có StoreId được gán cửa hàng đó; StoreId NULL được gán tất cả cửa hàng đã tồn tại tại thời điểm migration. Cửa hàng tạo sau không tự nhận ca.

`ShiftLegacyArchive_20261001` giữ toàn bộ dữ liệu Shift trước chuyển đổi. `ShiftBreakConversionMapping` là bảng phục vụ chuyển đổi, không phải bảng nghiệp vụ.
Không xóa hai bảng này khi chưa hoàn tất đối chiếu. Rollback bằng bản backup đầy đủ: migration Down chủ động từ chối vì cấu trúc StoreId cũ không biểu diễn được nhiều chi nhánh.

Seeder chỉ tạo dữ liệu khi danh mục ca trống và đã có cửa hàng hoạt động. Nó không ghi đè dữ liệu hoặc kích hoạt lại các liên kết người dùng đã tắt.

## Kiểm tra

```powershell
dotnet build
dotnet run --project ../tests/ShiftChecks/ShiftChecks.csproj
```

Kiểm tra thủ công sau cập nhật: tạo ca cho nhiều chi nhánh, lọc chi nhánh, bỏ một chi nhánh khỏi ca, kiểm tra ca đêm, khoảng nghỉ qua ngày, đăng ký sai chi nhánh và ca đã dùng bị chặn sửa.

Attendance vẫn lưu giờ trong ngày và ngày làm thay vì timestamp riêng cho check-in/out. Cách tính mới hỗ trợ ca tối đa 12 giờ; các lượt chấm kéo dài nhiều ngày cần thiết kế timestamp riêng trong công việc chấm công sau này.

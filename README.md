# MODERNKEY GMTPC // CYBER_EDITION

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()
[![Platform](https://img.shields.io/badge/platform-Windows%207%20%7C%208%20%7C%2010%20%7C%2011-blue.svg)]()
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-purple.svg)]()
[![Language](https://img.shields.io/badge/C%23-7.3-orange.svg)]()
[![UI](https://img.shields.io/badge/UI-WPF%20Cyberpunk%20Dark-00f0ff.svg)]()

> **ModernKey GMTPC** là bộ gõ tiếng Việt thế hệ mới dành cho hệ điều hành Windows, được xây dựng trên nền tảng **WPF (.NET Framework 4.7.2)** với phong cách thiết kế **Cyberpunk Neon Dark Theme** hiện đại, kết hợp động cơ gõ tiếng Việt độ chính xác cao và siêu tốc.

---

## ⚡ TÍNH NĂNG NỔI BẬT

### 1. Đa dạng Kiểu gõ & Tự định nghĩa (Custom Input Method)
- Hỗ trợ đầy đủ **5 kiểu gõ tiếng Việt**:
  - **Telex**: Kiểu gõ phổ biến nhất.
  - **VNI**: Kiểu gõ dùng phím số.
  - **Simple Telex**: Telex giản lược.
  - **Tư Bình Trần**: Tích hợp nguyên bản đầy đủ (phím `1..5` dấu thanh, `6` (â), `7` (ê - gõ `tr7n` ➔ `trên`), `8` (ô), `9` (ă), `[` (ư), `]` (ơ), `dd` (đ)).
  - **Tự định nghĩa (Custom)**: Chuyển giao hoàn chỉnh từ OpenKey C++ với **33 hành động độc lập** và **4 cấu hình mẫu sẵn có** (Telex, VNI, Simple Telex, Tư Bình Trần đơn giản). Cho phép người dùng tùy ý gán phím theo phong cách cá nhân.

### 2. Bảng mã tiếng Việt phong phú
- **Unicode** dựng sẵn & **Unicode tổ hợp**.
- **TCVN3 (ABC)** tiêu chuẩn miền Bắc.
- **VNI Windows** tiêu chuẩn miền Nam.
- **BK HCM 1** và **BK HCM 2**.
- **Vietware X** và **Vietware F**.
- **UTF-8 Literal**.

### 3. Công nghệ gõ thông minh & Zero-Redundant-Backspace
- **Zero-Redundant-Backspace**: Khắc phục triệt để hiện tượng nuốt chữ hoặc xóa lấn sang từ đằng trước trong trình duyệt (Chrome, Edge) và các ứng dụng văn phòng.
- **Bảo vệ chuỗi số thuần & Ký hiệu**: Tự động nhận diện chuỗi số thuần (ví dụ `667` ➔ `67`) và ký hiệu có phím Shift (`*`, `^`, `&`) mà không bị ép dấu sai lệch.
- **Khôi phục phím thông minh**: Tự động phục hồi từ gốc khi từ gõ sai chính tả.
- **Tùy chọn dấu mới**: Hỗ trợ đặt dấu oà, uý thay vì òa, úy.

### 4. Hệ thống Gõ tắt (Macro) toàn diện
- **Auto-Caps**: Tự động nhận diện viết hoa theo ký tự gõ tắt (ví dụ `vn` ➔ `việt nam`, `Vn` ➔ `Việt nam`, `VN` ➔ `VIỆT NAM`).
- **Phím kích hoạt linh hoạt**: Hỗ trợ kích hoạt gõ tắt bằng phím **Space**, **Enter**, hoặc **Nhấp đúp Shift** (Trái/Phải).
- **Hỗ trợ Emotion / Emoji**: Đọc và hiển thị chuẩn định dạng mở rộng CESU-8.
- **Chuyển đổi dữ liệu**: Dễ dàng nhập/xuất và chuyển đổi định dạng gõ tắt từ **EVKey** và **OpenKey C++**.

### 5. Cơ chế gửi phím siêu tốc & Hỗ trợ Game / Đồ họa
- Tự động chuyển đổi giữa cơ chế gửi phím Native Input và **Clipboard Injection** an toàn.
- Cơ chế bảo vệ Clipboard toàn vẹn: Tự động sao lưu và khôi phục nội dung văn bản / hình ảnh của khay nhớ tạm sau khi dán macro.

### 6. Công cụ chuyển mã & Phím tắt toàn cục
- Tích hợp sẵn tab **Chuyển mã văn bản & Clipboard**: Cho phép đổi mã nhanh giữa mọi bảng mã và loại bỏ dấu tiếng Việt.
- Hệ thống phím nóng toàn cục tùy biến (kết hợp các phím `Ctrl`, `Shift`, `Alt`, `Win` với `F1` đến `F12`).

### 7. Portable & Tiện ích khay hệ thống (System Tray)
- Tự động nhận diện chế độ **Portable** qua file `.portable` cạnh thư mục thực thi.
- Biểu tượng khay hệ thống Cyberpunk hiển thị trạng thái `[VI]` và `[EN]` trực quan, cho phép chuyển đổi nhanh ngôn ngữ chỉ với một click chuột trái.

---

## 🛠 YÊU CẦU HỆ THỐNG & BIÊN DỊCH

### Yêu cầu
- Hệ điều hành: **Windows 7 / 8 / 8.1 / 10 / 11** (x86 / x64).
- Môi trường chạy: **.NET Framework 4.7.2** trở lên.

### Biên dịch từ mã nguồn
```bash
# Clone repository
git clone https://github.com/ghostminhtoan/ModernKey-GMTPC.git
cd ModernKey-GMTPC

# Biên dịch bản Release
dotnet build -c Release
```

File thực thi sau khi biên dịch nằm tại:
`bin/Release/net472/ModernKey.exe`

---

## 👨‍💻 TÁC GIẢ

- **GMTPC**
- Email liên hệ: [ghostminhtoan@gmail.com](mailto:ghostminhtoan@gmail.com)
- GitHub: [ghostminhtoan](https://github.com/ghostminhtoan)

---

## 📄 GIẤY PHÉP

Dự án được phát triển theo giấy phép mã nguồn mở GPL.

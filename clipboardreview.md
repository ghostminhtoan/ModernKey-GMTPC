Listed directory openkey%20all
Listed directory Modernkey%20WPF%20GMTPC
Viewed workflow.md:1-57
Viewed README.md:1-89
Listed directory Core
Listed directory Hook

Tôi đã đọc kỹ toàn bộ quy tắc code, tiêu chuẩn giao diện và luồng xử lý tại file [`workflow.md`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/workflow.md). Tôi ghi nhớ và sẽ tuân thủ tuyệt đối:

1. **Luôn trả lời bằng tiếng Việt.**
2. **Luôn kích hoạt và tuân thủ các quy chuẩn của `teamwork-preview-protocol` và `prompt-optimizer`** khi phân tích và triển khai code (ngay cả khi bạn không tag trực tiếp).
3. **Chỉ sửa code khi bạn chấp thuận / yêu cầu cụ thể.** (Hiện tại yêu cầu chỉ là phân tích & đề xuất ý tưởng, nên tuân thủ nguyên tắc không tự ý sửa code).
4. **Quy trình sau khi hoàn thành code:**
   - Biên dịch kiểm tra dứt điểm, đảm bảo **0 error / 0 warning**.
   - Tự động **commit local** (không bao giờ tự ý push GitHub trừ khi bạn yêu cầu rõ).
   - Báo cáo trạng thái chuẩn xác:
     ```text
     commit local: <mã hash>
     commit github: không
     path exe: <đường dẫn file exe>
     ```

---

Dựa trên nền tảng kiến trúc hiện có của **ModernKey GMTPC** (WPF .NET 4.7.2, C# 7.3, Low-level Keyboard Hook, Zero-Redundant-Backspace, Clipboard History Manager, giao diện Cyberpunk Dark Neon), dưới đây là **20 ý tưởng tính năng độc đáo, đột phá và có tính thực chiến rất cao**:

---

### Nhóm 1: Trí tuệ ngữ cảnh & Tối ưu gõ phím (Smart Engine)

1. **Auto-Switch Language theo Ứng dụng (Per-App / Per-Process Profile)**
   - **Mô tả:** Tự động chuyển `[VI]` hoặc `[EN]` (hoặc vô hiệu hóa tạm thời hook tiếng Việt) tùy theo cửa sổ đang kích hoạt.
   - **Giá trị:** Khi vào game (CS:GO, Valorant, Dota 2, GTA...) hoặc Terminal/IDE (VS Code, Visual Studio), app tự chuyển sang `[EN]` để tránh kẹt phím W/A/S/D hay gõ nhầm biến code. Khi chuyển về Word, Chrome, Zalo, Telegram thì tự bật lại `[VI]`.

2. **Smart Code-Context Filter (Gõ tiếng Việt an toàn cho Developer)**
   - **Mô tả:** Nhận diện người dùng đang gõ code trong IDE. Khi gõ các từ khóa lập trình (`for`, `while`, `function`, `const`, `return`, ký hiệu camelCase/snake_case), engine tự động bypass không can thiệp. Chỉ khi gõ trong chuỗi `""`, `''` hoặc comment `//`, `/* */` mới kích hoạt tiếng Việt.

3. **Smart Mixed-Language (Song ngữ Anh - Việt thông minh)**
   - **Mô tả:** Tích hợp từ điển từ vựng tiếng Anh thông dụng (nhất là thuật ngữ IT, game, văn phòng: `post`, `scale`, `server`, `client`, `game`, `pass`, `file`, `table`...).
   - **Giá trị:** Khi gõ từ tiếng Anh không bao giờ bị nhảy dấu oan (ví dụ gõ `post` không bị biến thành `pót` hay `pôst`).

4. **Game Mode Zero-Latency Toggle**
   - **Mô tả:** Phím tắt toggle nhanh "Game Mode": Unhook hoàn toàn Low-level Hook để đạt độ trễ 0ms tuyệt đối và an toàn 100% với mọi hệ thống Anti-Cheat nhạy cảm, đi kèm hiệu ứng OSD thông báo visual Cyberpunk.

5. **Hoàn tác dấu bằng một phím (One-Key Mark Undo / Restore Raw)**
   - **Mô tả:** Khi đang gõ một từ dài và bị nhảy dấu sai, người dùng chỉ cần ấn một phím định sẵn (ví dụ phím `Z` khi chưa có dấu hoặc phím tắt riêng) để đưa toàn bộ từ về lại dạng ký tự ASCII ban đầu chỉ trong 1 tick, thay vì phải bấm Backspace nhiều lần rồi gõ lại từ đầu.

---

### Nhóm 2: Nâng cấp Clipboard Manager & Xử lý dữ liệu (Next-Gen Clipboard)

6. **Bảo vệ dữ liệu nhạy cảm (Clipboard Privacy & Auto-Purge)**
   - **Mô tả:** Tự động phát hiện khi nội dung vừa copy là Password, Private Key, API Token (JWT, Bearer), mã OTP hoặc số thẻ tín dụng.
   - **Giá trị:** Tự động gắn tag `[Sensitive]`, làm mờ (blur/mask) nội dung trong cửa sổ `ClipboardWindow` để tránh bị nhìn trộm màn hình, và hẹn giờ tự xóa sau N phút.

7. **Clipboard OCR tức thời bằng Native Windows API**
   - **Mô tả:** Khi người dùng chụp màn hình (PrintScreen / Snipping Tool), Clipboard Manager tự động dùng thư viện native `Windows.Media.Ocr` (có sẵn trên Win 10/11, không cần thư viện ngoài) để nhận diện chữ trong ảnh và bổ sung ngay một bản ghi text vào lịch sử, giúp trích xuất văn bản từ ảnh siêu tốc.

8. **Ghim ghi chú tạm thời từ Clipboard (Floating Cyberpunk Sticky Card)**
   - **Mô tả:** Cho phép bấm chuột phải vào bất kỳ mục nào trong Clipboard để "Ghim nổi" (Always-on-top) thành một thẻ mini Cyberpunk nhỏ gọn trên màn hình để vừa làm việc vừa nhìn dữ liệu nhập liệu.

9. **Tự động nhận diện & Chuyển đổi mã màu (Clipboard Color Swatch)**
   - **Mô tả:** Khi copy chuỗi mã màu (`#00f0ff`, `rgb(...)`, `hsl(...)`), Clipboard Manager hiển thị ngay ô preview màu sắc trực quan, cho phép 1-click chuyển đổi định dạng HEX ⇄ RGB ⇄ HSL.

10. **Phím tắt Dán văn bản thuần túy toàn cục (Global Paste as Plain Text)**
    - **Mô tả:** Cung cấp phím tắt toàn cục (ví dụ `Ctrl + Shift + V` hoặc `Win + Alt + V`) cho toàn Windows: Tự động loại bỏ mọi format HTML, bảng biểu, màu mè rườm rà và dán văn bản thô vào bất kỳ phần mềm nào.

---

### Nhóm 3: Tự động hóa & Macro thông minh (Smart Macro & Tools)

11. **Macro động với Placeholder linh hoạt (Dynamic Snippets)**
    - **Mô tả:** Cho phép gõ tắt chứa các biến động: `{date}`, `{time}`, `{clipboard}` (nội dung clipboard hiện tại), `{cursor}` (vị trí con trỏ sau khi bung).
    - **Ví dụ:** Gõ `//today` ➔ tự bung ra ngày giờ thực tế; gõ `//sign` ➔ tự điền chữ ký kèm ngày tháng hiện tại.

12. **Tính toán biểu thức toán học trực tiếp trong dòng văn bản (Inline Math Evaluator)**
    - **Mô tả:** Khi đang gõ văn bản, nếu gõ một biểu thức toán kèm dấu `=` (ví dụ: `150000*3/2=`), ModernKey sẽ tự động tính nhẩm và điền kết quả ngay sau dấu `=` (`150000*3/2=225000`).

13. **Quick Text Transform Popup (Menu thao tác nhanh trên văn bản bôi đen)**
    - **Mô tả:** Bôi đen văn bản ở bất kỳ đâu và bấm phím tắt: hiện thanh công cụ Cyberpunk mini nổi cung cấp các thao tác 1-click: Đổi chữ HOA / thường / Title Case, Bỏ dấu tiếng Việt, Đếm số ký tự/từ, Mã hóa/Giải mã Base64/URL, Tạo slug URL (`tieu-de-bai-viet`).

14. **Gõ Emoji & Ký tự đặc biệt qua Slash Command**
    - **Mô tả:** Cho phép gõ nhanh emoji hoặc ký tự toán học bằng cú pháp gõ tắt ngắn: ví dụ `:cuoi:` hoặc `/smile` ➔ hiện popup gợi ý emoji; gõ `->` ➔ `→`, `>=` ➔ `≥`, `+-` ➔ `±`.

15. **Hỗ trợ gõ Chord (Bấm đồng thời 2 phím cùng lúc)**
    - **Mô tả:** Bấm đồng thời cặp phím (ví dụ `J + K`) để tương đương phím `Esc` hoặc `Enter`. Rất được các lập trình viên Vim/Neovim và dân văn phòng đánh máy tốc độ cao yêu thích vì không cần nhấc tay khỏi hàng phím cơ sở (Home Row).

---

### Nhóm 4: Giao diện, Trải nghiệm Cyberpunk & Trợ lý cá nhân (Cyberpunk UX)

16. **Floating Caret Indicator (Chỉ báo ngôn ngữ bám theo con trỏ soạn thảo)**
    - **Mô tả:** Hiển thị một chấm sáng Cyberpunk hoặc nhãn mini `[VI]` / `[EN]` siêu nhỏ bám sát ngay tại vị trí con trỏ văn bản (Caret) khi đang gõ hoặc khi vừa đổi ngôn ngữ, giúp mắt không cần phải liếc xuống góc thanh Taskbar.

17. **Hiệu ứng âm thanh cơ học Cyberpunk (Low-latency Audio Feedback)**
    - **Mô tả:** Tùy chọn bật âm thanh gõ phím cơ mô phỏng (Cherry Blue, Brown, tiếng click Sci-Fi Cyberpunk siêu nhẹ độ trễ dưới 5ms). Có thể bật/tắt nhanh bằng phím tắt.

18. **Dashboard Cyberpunk: Thống kê tốc độ gõ & Heatmap thời gian**
    - **Mô tả:** Một tab thống kê đồ họa Cyberpunk trực quan: Đếm tổng số từ gõ trong ngày, tốc độ gõ (WPM - Words Per Minute), biểu đồ phân bổ thời gian gõ trong ngày, tỷ lệ gõ tiếng Việt vs tiếng Anh. Mọi dữ liệu lưu trữ 100% offline nội bộ.

19. **Tích hợp Local AI Rewrite / Sửa văn phong (Ollama / Local LLM Hook)**
    - **Mô tả:** Bấm phím tắt khi đang bôi đen đoạn văn bản: gửi nhanh đến Local LLM đang chạy trên máy (qua cổng localhost của Ollama/LM Studio) để sửa lỗi diễn đạt, dịch thuật hoặc chuyển đổi văn phong trang trọng/thân thiện mà không phụ thuộc vào internet.

20. **Sync cấu hình và Macro P2P / Local Folder (Đồng bộ không qua Cloud)**
    - **Mô tả:** Cho phép trỏ thư mục dữ liệu macro, clipboard và cấu hình vào một thư mục được đồng bộ sẵn (OneDrive, Google Drive, Syncthing, LAN Share) hoặc USB Portable, tự động reload khi phát hiện file cấu hình thay đổi mà không cần khởi động lại ứng dụng.

---

Bạn đánh giá thế nào về 20 ý tưởng trên? Bạn muốn ưu tiên triển khai tính năng nào trước để tôi lên kế hoạch chi tiết và thực thi theo chuẩn `workflow.md`?

# Làm 3 4 5 6 7 8 9 11 12 13 16 17 18 20

## Tôi đã nghiên cứu kỹ toàn bộ mã nguồn hiện tại, cấu trúc dự án và xây dựng bản kế hoạch triển khai chi tiết cho 14 tính năng độc đáo (3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 16, 17, 18, 20) tại file kế hoạch 
implementation_plan.md
.

### Tóm tắt phân bổ 4 giai đoạn thực thi:
Giai đoạn 1 (Core Engine & Gõ Phím): Song ngữ Anh-Việt (3), Game Mode Zero-Latency (4), Hoàn tác 1 phím (5), Dynamic Macro {date}/{clipboard} (11), Inline Math Evaluator 125*45= (12).
Giai đoạn 2 (Clipboard Manager & Tiện ích): Che dữ liệu nhạy cảm (6), OCR ảnh tức thời (7), Sticky Note nổi (8), Color Swatch (9), Quick Text Transform Popup (13).
Giai đoạn 3 (Cyberpunk UX & Tương tác): Floating Caret Indicator (16), Âm thanh phím cơ Cyberpunk (17), Dashboard WPM & Heatmap (18).
Giai đoạn 4 (Hạ tầng & Đồng bộ): Sync Folder cấu hình qua FileSystemWatcher không qua Cloud (20).
Mời bạn xem và bấm nút Proceed (hoặc xác nhận phản hồi) để tôi bắt đầu triển khai code và kiểm định build sạch 0 error / 0 warning theo đúng quy chuẩn!


# Implementation Plan
# Kế hoạch triển khai 14 Tính Năng Độc Đáo cho ModernKey GMTPC // CYBER_EDITION

Dự án **ModernKey GMTPC** hiện tại chạy trên nền tảng **WPF (.NET Framework 4.7.2)**, C# 7.3 với giao diện Cyberpunk Dark Neon, sử dụng Low-level Windows Hook (`SetWindowsHookEx`), cơ chế gõ không nuốt phím Zero-Redundant-Backspace, và hệ thống Clipboard Manager phong phú.

Tài liệu này xác định kiến trúc, ranh giới an toàn (Blast Radius Guard), và lộ trình triển khai chi tiết cho **14 tính năng** được chọn (`3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 16, 17, 18, 20`) theo chuẩn **teamwork-preview-protocol** (Tier 3 Extended) và nguyên tắc tối giản **YAGNI / stdlib-native** của [`workflow.md`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/workflow.md).

---

## 🏛️ [Extended Architecture Preview & Lane Mapping]

- **Tech Stack & Environment**: C# 7.3 | .NET Framework 4.7.2 (WPF) | Windows API (User32, Kernel32, GDI) | Zero external package bloat.
- **Dynamic Lane Matrix**:
  - **Lane 1 (Presentation & Interface)**:
    - Cập nhật [`MainWindow.xaml`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/MainWindow.xaml) (bổ sung checkbox điều khiển & tab Thống kê/Dashboard mới).
    - Cập nhật [`ClipboardWindow.xaml`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/ClipboardWindow.xaml) (hiển thị Color Swatch, badge nhạy cảm, nút OCR, menu Sticky).
    - Tạo mới `StickyNoteWindow.xaml` (Ghim ghi chú nổi Cyberpunk).
    - Tạo mới `TextTransformWindow.xaml` (Quick transform popup trên văn bản bôi đen).
    - Tạo mới `CaretIndicatorWindow.xaml` (Chỉ báo nổi theo con trỏ văn bản).
  - **Lane 2 (Domain & Business Core)**:
    - [`Core/VietnameseEngine.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Core/VietnameseEngine.cs) & `Core/EnglishDictionary.cs`: Song ngữ Anh-Việt, một phím hoàn tác dấu, Inline Math Evaluator.
    - [`Core/MacroManager.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Core/MacroManager.cs): Dynamic snippets `{date}`, `{time}`, `{datetime}`, `{clipboard}`, `{guid}`.
    - [`Core/ClipboardHistoryManager.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Core/ClipboardHistoryManager.cs): Auto-mask sensitive data, auto-purge sau N phút, nhận diện Color Swatch, Native Windows OCR.
    - Tạo mới `Core/MathEvaluator.cs`: Thuật toán phân tích biểu thức toán học thuần C# (không dùng eval/third-party).
    - Tạo mới `Core/SoundManager.cs`: Bộ phát âm thanh gõ phím switch cơ Cyberpunk độ trễ thấp (<5ms) dùng native `System.Media.SoundPlayer` / WinMM WaveOut.
    - Tạo mới `Core/TypingStatsManager.cs`: Theo dõi số ký tự, từ, WPM thời gian thực, heatmap theo giờ.
  - **Lane 3 (Data & Infrastructure)**:
    - [`Config/SettingsManager.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Config/SettingsManager.cs): Hỗ trợ thư mục Sync Folder tùy biến và lắng nghe `FileSystemWatcher`.
    - Quản lý file `stats.json` lưu trữ thống kê offline.
  - **Lane 4 (Types & Contracts)**:
    - [`Models/AppSettings.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Models/AppSettings.cs): Các cờ cấu hình mới.
    - [`Models/ClipboardItem.cs`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/Models/ClipboardItem.cs): Bổ sung `IsSensitive`, `IsMasked`, `IsColorCode`, `ColorHexValue`.
  - **Lane 5 (Build, Ops & Tooling)**:
    - Biên dịch `dotnet build` đạt **0 Error / 0 Warning**.
    - Explicit `git add <files>` & `git commit` local.

---

## 🛡️ [Blast Radius Analysis & Review Guard Checklist]

- **Core Hook & Typing Engine Protection**:
  - `VietnameseEngine` và `KeyboardHook` là hạt nhân sống còn của bộ gõ. Mọi tính năng mới (Math inline, Smart English, One-Key Undo) chỉ can thiệp khi buffer đạt điều kiện kích hoạt, tuyệt đối không làm chậm luồng xử lý gõ phím thông thường (<1ms).
  - Tuân thủ nghiêm ngặt quy tắc tại [`workflow.md`](file:///r:/HDD%20R/ZC%20SYMLINK/USERS/source/repos/ghostminhtoan/openkey%20all/Modernkey%20WPF%20GMTPC/workflow.md): Giữ nguyên vẹn xử lý gõ số thuần (`667` ➔ `67`), bảo vệ ký tự Shift (`*`, `^`, `&`), và kiểu gõ từ bình trần (`tr7n` ➔ `trên`).
- **Threading Affinity & Memory Safety**:
  - Tất cả cửa sổ nổi mới (`StickyNoteWindow`, `TextTransformWindow`, `CaretIndicatorWindow`) được điều khiển qua `Dispatcher`, hỗ trợ unmanaged handle cleanup trong `Dispose`/`Closed`.
  - Timer tự xóa Clipboard Sensitive chạy ngầm bằng `System.Threading.Timer` không chặn UI Thread.
- **Backward Compatibility**:
  - File cấu hình `settings.ini` cũ và file gõ tắt `openkeymacro.txt` được giữ nguyên cấu trúc, chỉ thêm các key mới khi có thiết lập.

---

## 📋 Chi tiết 14 tính năng theo 4 Giai đoạn

### Giai đoạn 1: Engine & Gõ Phím Cốt Lõi (Phím tắt & Toán học)
1. **Tính năng 3: Smart Mixed-Language (Song ngữ Anh - Việt)**:
   - Tích hợp tập từ vựng tiếng Anh lập trình & thông dụng (`post`, `scale`, `server`, `client`, `game`, `pass`, `clear`, `link`, `free`, `like`, `view`, `case`, `break`, `test`, `true`, `false`, `null`, `text`, `user`...). Khi từ trong buffer khớp, bỏ qua biến đổi dấu.
2. **Tính năng 4: Game Mode Zero-Latency Toggle**:
   - Thêm phương thức `SetGameMode(bool enabled)` trong `KeyboardHook`. Khi bật, unhook phím hoàn toàn (hoặc bypass tức thời) để đạt 0ms latency.
   - Phím tắt toggle: `Ctrl + Shift + F11` (hoặc cấu hình) + OSD Cyberpunk hiển thị `[GAME MODE: ON] / [OFF]`.
3. **Tính năng 5: Hoàn tác dấu bằng một phím (One-Key Mark Undo / Restore Raw)**:
   - Khi đang gõ một từ tiếng Việt bị dấu sai, bấm phím cấu hình (mặc định phím `Z` khi chưa dấu, hoặc `Esc` hoàn tác) ➔ tự động gửi Backspace xóa từ có dấu và dán/gõ lại chuỗi ASCII nguyên bản.
4. **Tính năng 11: Dynamic Macros (Gõ tắt có Placeholder biến động)**:
   - Trong `MacroManager.TryGetMacro()`, thế các token:
     - `{date}` ➔ `dd/MM/yyyy`
     - `{time}` ➔ `HH:mm:ss`
     - `{datetime}` ➔ `dd/MM/yyyy HH:mm:ss`
     - `{clipboard}` ➔ Nội dung text đang có trong clipboard
     - `{guid}` ➔ Mã GUID mới
5. **Tính năng 12: Inline Math Evaluator (Tính toán trong dòng gõ)**:
   - Tạo `Core/MathEvaluator.cs` phân tích cú pháp biểu thức số học (`+`, `-`, `*`, `/`, `^`, `%`, `()`).
   - Khi gõ chuỗi dạng `125*45/2=` hoặc `1500+250=` kết thúc bằng `=`, engine tự động tính và gửi kết quả ra màn hình.

---

### Giai đoạn 2: Nâng cấp Clipboard Manager & Công cụ Tiện ích
6. **Tính năng 6: Bảo vệ dữ liệu nhạy cảm (Privacy Mask & Auto-Purge)**:
   - Nhận diện Regex: Password, JWT token, Bearer token, thẻ tín dụng Visa/MasterCard, mã OTP 6 số.
   - Gắn cờ `IsSensitive = true` trên `ClipboardItem`, hiển thị `•••••••• [BẢO MẬT]` kèm icon Cyberpunk Shield.
   - Hẹn giờ tự xóa item sau 5 phút nếu được kích hoạt.
7. **Tính năng 7: Clipboard OCR tức thời (Trích xuất văn bản từ ảnh)**:
   - Thêm nút "[OCR TEXT]" trên card ảnh trong `ClipboardWindow`.
   - Sử dụng Windows Native OCR (`Windows.Media.Ocr` / PowerShell WinRT lightweight interop) để trích xuất chữ và thêm bản ghi Text vào clipboard ngay lập tức.
8. **Tính năng 8: Ghim ghi chú tạm thời từ Clipboard (Floating Cyberpunk Sticky Card)**:
   - Menu chuột phải trên item clipboard: "Ghim nổi màn hình (Sticky Note)".
   - Mở cửa sổ `StickyNoteWindow.xaml` phong cách Cyberpunk, Always-on-Top, kéo thả, thu nhỏ, 1-click copy.
9. **Tính năng 9: Nhận diện & Chuyển đổi mã màu (Clipboard Color Swatch)**:
   - Nhận diện mã HEX (`#RGB`, `#RRGGBB`, `#AARRGGBB`), RGB `rgb(r,g,b)`, HSL `hsl(h,s,l)`.
   - Hiển thị ô màu swatch Cyberpunk, click để chuyển đổi nhanh định dạng HEX ⇄ RGB ⇄ HSL.
10. **Tính năng 13: Quick Text Transform Popup (Thao tác nhanh trên văn bản bôi đen)**:
    - Phím tắt toàn cục `Win + Alt + T` (hoặc `Ctrl + Shift + T`): Tự động lấy text đã bôi đen (`Ctrl+C`), hiện thanh công cụ Cyberpunk mini nổi:
      - Viết HOA, viết thường, Viết Hoa Đầu Từ (Title Case)
      - Bỏ dấu tiếng Việt
      - Đếm từ & ký tự
      - Tạo URL Slug (`tieu-de-bai-viet`)
      - Base64 / URL Encode

---

### Giai đoạn 3: Trải nghiệm Giao diện Cyberpunk & Tương tác
11. **Tính năng 16: Floating Caret Indicator (Chỉ báo ngôn ngữ bám theo con trỏ soạn thảo)**:
    - Tạo `CaretIndicatorWindow.xaml`: Một huy hiệu Cyberpunk mini `[VI]` / `[EN]` siêu nhỏ.
    - Dùng API `GetGUIThreadInfo` lấy tọa độ caret của cửa sổ đang soạn thảo, hiển thị cạnh vị trí gõ và tự động mờ dần sau 1.5 giây.
12. **Tính năng 17: Hiệu ứng âm thanh cơ học Cyberpunk (Low-latency Audio Feedback)**:
    - Tạo `Core/SoundManager.cs`: Sử dụng âm thanh click switch cơ Cyberpunk gọn nhẹ độ trễ thấp (<5ms), có tùy chọn bật/tắt trong Cài đặt.
13. **Tính năng 18: Dashboard Cyberpunk: Thống kê tốc độ gõ (WPM) & Heatmap thời gian**:
    - Tạo `Core/TypingStatsManager.cs` lưu `stats.json`.
    - Thêm Tab `THỐNG KÊ` trong `MainWindow.xaml`: Hiển thị số phím gõ trong ngày, WPM thời gian thực, biểu đồ cột phân bổ giờ trong ngày (0h-23h) phong cách Cyberpunk Neon.

---

### Giai đoạn 4: Hạ tầng Đồng bộ & Quản lý Cấu hình
14. **Tính năng 20: Sync cấu hình và Macro P2P / Local Folder (Đồng bộ không qua Cloud)**:
    - Cho phép chọn thư mục đồng bộ tùy biến (OneDrive, Google Drive, LAN, USB).
    - Tích hợp `FileSystemWatcher` trong `SettingsManager`: Tự động reload settings và macro khi có cập nhật từ máy khác mà không cần khởi động lại app.

---

## 🧪 Kế hoạch Kiểm định (Verification Plan)

### Kiểm định Biên dịch & Mã nguồn
- Chạy lệnh build:
  ```powershell
  dotnet build "Modernkey WPF GMTPC\ModernKey.csproj"
  ```
  Yêu cầu bắt buộc: **0 Warning, 0 Error**.

### Kiểm định Nghiệp vụ & Hồi quy (Regression Test)
1. **Engine Test**:
   - Gõ `tr7n` ➔ `trên` (kiểm tra rule Tư Bình Trần).
   - Gõ `667` ➔ `67` (bảo vệ chuỗi số thuần theo `workflow.md`).
   - Gõ `Shift+998` ➔ `(*` (bảo vệ ký hiệu Shift).
   - Gõ `post` ➔ ra `post`, không biến thành `pót`.
   - Gõ `150*4=` ➔ ra `600`.
   - Gõ macro `{date}` ➔ ra ngày hiện tại.
2. **UI & Window Lifecycle Test**:
   - Mở Sticky Note, Text Transform, Clipboard HUD.
   - Thử chức năng che dữ liệu nhạy cảm và Color Swatch trong Clipboard.

---

## 🚀 Đóng gói & Cam kết Git (Git Protocol)
- Chỉ stage đúng danh sách các file được chỉnh sửa/tạo mới:
  `git add <target-files>`
- Commit local theo chuẩn:
  `feat(core): implement 14 unique features (smart engine, clipboard tools, cyberpunk hud)`
- Trả về mã hash commit local và đường dẫn file thực thi `.exe`.

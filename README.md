# KTL

Ứng dụng desktop trên Windows để xử lý văn bản hành chính: tạo văn bản hàng loạt từ mẫu Word và dữ liệu
Excel, ký số PDF bằng chứng thư số trên USB token của Ban Cơ yếu Chính phủ.

Mọi xử lý chạy **trên máy người dùng**. Tài liệu không rời khỏi máy; thứ duy nhất đi ra mạng là giá trị băm
gửi tới máy chủ dấu thời gian `tsa.ca.gov.vn` khi ký số.

Phát hành thành một bộ cài NSIS (`KTL-v<phiên bản>-win-x64-Setup.exe`) mang sẵn ứng dụng self-contained và
các trình điều khiển token. Máy người dùng không cần cài .NET.

## Mục lục

- [Chức năng](#chức-năng)
- [Yêu cầu hệ thống](#yêu-cầu-hệ-thống)
- [Kiến trúc](#kiến-trúc)
- [API](#api)
- [Dữ liệu trên máy người dùng](#dữ-liệu-trên-máy-người-dùng)
- [Quan hệ với middleware của token](#quan-hệ-với-middleware-của-token)
- [Chạy khi phát triển](#chạy-khi-phát-triển)
- [Đóng gói và phát hành](#đóng-gói-và-phát-hành)
- [Luồng cài đặt](#luồng-cài-đặt)
- [Quy ước code](#quy-ước-code)
- [Đóng góp](#đóng-góp)

## Chức năng

| Màn hình | Việc |
|---|---|
| Ký số | Chọn file, thư mục và chứng thư số, ký hàng loạt PDF theo chuẩn PAdES (`ETSI.CAdES.detached`) kèm dấu thời gian, ký nối bản nên không phá chữ ký có sẵn. Popup Cấu hình đặt lý do, nơi ký, font, cỡ, màu khối chữ ký số; thêm ảnh dấu đỏ, chữ ký tươi kéo thả lên trang, lặp theo trang (tất cả, chẵn, lẻ, tuỳ chọn), chỉnh độ đậm, độ dày nét, màu mực; tuỳ chọn nhồi chữ ký số vào ảnh. Vị trí dùng chung cho mọi file, chỉnh riêng được từng file. Cấu hình nhớ trên máy. Sau khi ký tự kiểm tra lại chữ ký và chuỗi chứng thư |
| Tạo văn bản | Ghép mẫu `.docx` có trường `{ten_truong}` với dữ liệu `.xlsx`, sinh mỗi dòng một file PDF. Tự map trường với cột, xem trước từng dòng, tuỳ chọn chèn dấu đỏ và chữ ký tươi ngay khi sinh. Đặt theo trang cụ thể hoặc lặp tất cả, trang chẵn, trang lẻ, danh sách tuỳ chọn (`1,3,5-7`), có vị trí riêng cho từng trang |

Tạo văn bản có tuỳ chọn **Ký số sau khi tạo**: chạy xong, ứng dụng chuyển sang màn Ký số với đúng danh sách
file vừa sinh.

Thứ tự đúng là **tạo, rồi mới ký số**. Ghi lại file đã ký sẽ làm chữ ký mất hiệu lực.

## Yêu cầu hệ thống

Máy người dùng:

- Windows 10/11 64-bit
- Microsoft Edge WebView2 Runtime (bộ cài tự cài nếu thiếu)
- Middleware của token: bit4id Universal MW cho token đời mới, VGCA TokenManager cho token trước 2022 (bộ cài
  tự cài nếu thiếu)
- Microsoft Word, **chỉ cần cho chức năng Tạo văn bản**: PDF được xuất qua Word bằng COM

Máy phát triển:

- .NET SDK 9
- Node.js và npm 10
- NSIS 3 nếu cần đóng gói bộ cài (`winget install --id NSIS.NSIS`)

## Kiến trúc

```
ktl.be/                               Backend .NET 9
  ktl.be.api                          Controller, Program.cs (DI, CORS, API versioning)
  ktl.be.applications                 Nghiệp vụ: ký số, tạo văn bản, tải file lên
  ktl.be.infrastructure.external      PDF (PdfSharp, ký nối bản), Word (OpenXml, COM), Excel (ClosedXML),
                                      chứng thư và khoá ký (Windows Cert Store, CNG/CAPI), TSA
  ktl.be.shared                       Hằng số, ApiResponse, mã lỗi và thông báo lỗi
  ktl.be.desktop                      Vỏ desktop: chạy web host trong tiến trình, mở cửa sổ Photino (WebView2)
  installer/                          Script NSIS và bộ cài trình điều khiển
  build-desktop.ps1                   Build toàn bộ, ra bộ cài và bản zip
ktl.fe/                               Frontend Angular 21, PrimeNG 21, Tailwind 4
```

`ktl.be.desktop` gọi `ktl.be.api.Program.BuildApp`, cho Kestrel nghe ở `127.0.0.1` với cổng ngẫu nhiên rồi
mở cửa sổ trỏ vào địa chỉ đó. Frontend đã build nằm trong `wwwroot` và được phục vụ cùng origin với API. Cổng
chỉ mở trên loopback, không lộ ra mạng LAN.

Hộp thoại chọn file và thư mục là hộp thoại native của Windows. Frontend gửi yêu cầu qua
`window.external.sendMessage` (`pickFolder`, `pickPdf`, `pickWord`, `pickExcel`, `pickImage`, `openPath:`) và
nhận đường dẫn trả về qua `receiveMessage` (`DesktopService`). Chạy trong trình duyệt thường thì các nút này
không làm gì, phải dán đường dẫn hoặc kéo thả file.

Các tác vụ dài (ký, sinh file, chèn dấu) trả tiến độ bằng **Server-Sent Events**. Mỗi sự kiện có `targetType`
là `manifest` (danh sách file sắp xử lý), `file` (kết quả từng file) hoặc `summary` (tổng kết).

Mọi API trả cùng một envelope, lỗi nghiệp vụ vẫn trả HTTP 200:

```json
{ "status": 1, "data": {}, "code": 200, "message": "Ok" }
```

`status` là `1` khi thành công, `0` khi lỗi. Mã lỗi nghiệp vụ nằm trong `ErrorCodes` (dải `1025` đến `1048`),
thông báo mặc định tương ứng nằm trong `ErrorMessages`.

## API

Tiền tố: `api/v1/core/document`.

| Method | Route | Việc |
|---|---|---|
| GET | `sign/chung-thu-so` | Liệt kê chứng thư trên máy, `onlySignable=true` để chỉ lấy chứng thư ký được |
| POST | `sign/kiem-tra-token` | Ký thử để xác nhận token dùng được; bật hộp nhập PIN |
| POST | `sign/danh-sach-file` | Mở rộng danh sách file, thư mục thành danh sách PDF cần ký |
| POST | `sign/kiem-tra-vi-tri` | Kiểm tra khối chữ ký có nằm gọn trong trang của từng file |
| POST | `sign/ky-thu-muc` | Ký hàng loạt, trả SSE |
| POST | `sign/kiem-tra-chu-ky` | Kiểm tra chữ ký và dấu thời gian của một file |
| GET | `sign/xem-truoc`, `sign/xem-file` | Xem trước file PDF |
| GET | `tao-van-ban/danh-sach-header`, `tao-van-ban/map-tu-dong` | Đọc cột Excel, tự map với trường trong mẫu Word |
| POST | `tao-van-ban/kiem-tra`, `tao-van-ban/xem-truoc` | Kiểm tra cấu hình, xuất bản xem trước một dòng |
| GET | `tao-van-ban/xem-file`, `tao-van-ban/xem-anh` | Xem bản xem trước, xem ảnh dấu đỏ, chữ ký tươi |
| POST | `tao-van-ban/sinh-file` | Sinh file hàng loạt, trả SSE |
| POST | `tai-len` | Nhận file kéo thả vào ứng dụng |

Swagger có ở `/swagger` khi chạy môi trường Development. `/health` dùng để kiểm tra web host đã lên.

## Dữ liệu trên máy người dùng

| Đường dẫn | Nội dung |
|---|---|
| `%Temp%\KTL\tai-len\` | File kéo thả vào ứng dụng, tự dọn sau 24 giờ |
| `%Temp%\KTL\tao-van-ban\` | Thư mục làm việc và bản xem trước, bản xem trước tự dọn sau 60 phút |
| `<thư mục cài>\Log\Logs\` | Nhật ký NLog theo ngày, lưu trữ 30 ngày |

File kết quả nằm cạnh file gốc hoặc trong thư mục người dùng chọn, không bao giờ ghi đè file gốc:

| Chức năng | Tên file kết quả |
|---|---|
| Ký số | `<tên>_daky_<yyyyMMdd_HHmmss>.pdf` |
| Tạo văn bản | `<thư mục lưu>\<tên mẫu>_<thời điểm>\<tên mẫu>_<giá trị cột đặt tên>.pdf` |

Khi quét thư mục để ký, file đã mang hậu tố `_daky_` được bỏ qua để không ký chồng lên kết quả của lần trước.

File được kéo thả vào ứng dụng chỉ là bản sao trong `%Temp%`, nên với những file này bắt buộc phải chọn thư
mục lưu kết quả.

## Quan hệ với middleware của token

Ứng dụng đọc chứng thư qua **Windows Certificate Store** và ký qua CNG hoặc CAPI. Ứng dụng không nạp thư viện
PKCS#11 và không gọi trực tiếp phần mềm của hãng token. Middleware của hãng đăng ký một provider mật mã với
Windows, nhờ đó chứng thư trên token hiện ra trong kho như chứng thư thường.

| Token | Middleware | Provider |
|---|---|---|
| Đời mới | bit4id Universal MW | `Bit4id Universal Middleware Provider`, `Bit4id Key Storage Provider` |
| Đời cũ, trước 2022 | VGCA TokenManager (cài VGCA Client 8.3 của SafeNet) | CSP `eToken Base Cryptographic Provider`, KSP `SafeNet Smart Card Key Storage Provider` |

Máy chưa cài middleware thì ứng dụng không thấy chứng thư trên token. Bộ cài vì vậy mang sẵn cả hai.

Chứng thư chỉ được coi là ký được khi có khoá bí mật, còn hạn, có quyền ký và thuộc chuỗi CA của Ban Cơ yếu
(G1 hoặc G2). Các CA gốc và trung gian nằm ở `ktl.be/ktl.be.api/*.crt`, được ghim bằng SHA-256 trong
`SignatureConstants` và chép vào `certs\` khi build. Thiếu file nào thì mọi chữ ký thuộc nhánh đó bị coi là
không tin cậy.

Ứng dụng không chạm vào mã PIN. Hộp nhập PIN là của middleware, được gắn với cửa sổ ứng dụng để không bị
chìm phía sau. Trong lúc ký, ứng dụng kiểm tra token mỗi 2 giây; rút token giữa chừng thì dừng lô và báo rõ
file nào chưa ký.

## Chạy khi phát triển

Backend:

```bash
cd ktl.be/ktl.be.api
dotnet run --launch-profile http
```

API nghe ở `http://localhost:5247`.

Frontend:

```bash
cd ktl.fe
npm install
npm start
```

Mở `http://localhost:4200`. Cấu hình `development` gọi API ở `http://localhost:5247`
(`src/environments/environment.development.ts`). Origin được phép gọi API khai ở `AllowedHosts` trong
`appsettings.json`, phân tách bằng dấu `;`, giá trị `*` là cho phép mọi origin.

Chạy trong trình duyệt thì không có hộp thoại chọn file native. Muốn thử đúng môi trường desktop thì chạy
`build-desktop.ps1` rồi mở `publish\KTL.exe`.

Máy phát triển phải cài middleware của token thì mới liệt kê được chứng thư thật, và phải có Microsoft Word
thì mới thử được Tạo văn bản.

## Đóng gói và phát hành

```powershell
powershell -ExecutionPolicy Bypass -File ktl.be\build-desktop.ps1
```

Script lần lượt:

1. Build Angular với cấu hình `desktop`
2. Publish `ktl.be.desktop` thành một file `KTL.exe` self-contained cho `win-x64`
3. Chép frontend vào `publish\wwwroot`
4. Chép bộ cài trình điều khiển vào `publish\drivers`, tạo thư mục `publish\Log`
5. Biên dịch `ktl.be\installer\KTL.nsi` bằng `makensis`
6. Xuất hai file phát hành ra thư mục gốc repo

| File | Dùng cho |
|---|---|
| `KTL-v<phiên bản>-win-x64-Setup.exe` | Bộ cài cho người dùng. Đây là file nên phát hành |
| `KTL-v<phiên bản>-win-x64.zip` | Bản giải nén chạy ngay, có sẵn bộ cài driver trong `drivers\` nhưng không tự cài |

Phiên bản lấy từ `<Version>` trong `ktl.be.desktop.csproj`. Khi nâng phiên bản phải sửa đồng thời
`APP_VERSION` và `VIProductVersion` trong `KTL.nsi`.

Bộ cài trình điều khiển không nằm trong git vì dung lượng lớn và là phần mềm của bên thứ ba. Trước khi đóng
gói, đặt đủ ba file sau vào `ktl.be/installer/drivers/`, thiếu file nào script sẽ dừng:

| File | Nguồn |
|---|---|
| `bit4id_xpki_1.4.10.764-ng-user-vgca-pkimgr-bwc.exe` | Đơn vị cấp chứng thư số |
| `VGCAv1_Installer_1.0.21.0402.exe` | `dichvucong.ca.gov.vn`, mục Trình điều khiển thiết bị - TokenManager |
| `MicrosoftEdgeWebView2Setup.exe` | Microsoft, bản Evergreen Bootstrapper |

Chỉ dùng file lấy trực tiếp từ nhà phát hành và kiểm tra chữ ký Authenticode trước khi đưa vào. Bộ cài chạy
chúng với quyền quản trị.

Script NSIS và `build-desktop.ps1` phải lưu dạng **UTF-8 có BOM**. Thiếu BOM thì NSIS và PowerShell 5.1 đọc
nhầm bảng mã, chữ tiếng Việt trong bộ cài và trong log build bị vỡ.

## Luồng cài đặt

```
1. Welcome                      -> Next
2. Chọn thư mục cài             -> mặc định C:\Program Files\KTL
3. Tiến độ cài
   ├─ đóng KTL.exe nếu đang chạy
   ├─ chép ứng dụng, tạo lối tắt Desktop và Start Menu
   ├─ cấp quyền ghi thư mục Log cho nhóm Users (SID S-1-5-32-545)
   ├─ ghi mục gỡ cài đặt vào Apps & Features
   └─ lần lượt WebView2, bit4id, TokenManager
      ├─ máy đã có     -> bỏ qua
      └─ chưa có       -> cài ngầm, kiểm tra lại trạng thái thật trên máy
4. Finish                       -> tuỳ chọn chạy KTL ngay, đề nghị khởi động lại nếu vừa cài driver
```

Thành phần nào cài không được sẽ được liệt kê trong một hộp thoại cuối phiên. Ứng dụng vẫn được cài, chỉ là
chức năng Ký số có thể không thấy chứng thư.

Bộ cài nhận biết driver bằng trạng thái thật của máy chứ không tin mã thoát của bộ cài con: bit4id qua nhánh
registry `HKLM\SOFTWARE\bit4id` hoặc file gỡ cài đặt của nó, TokenManager qua provider bắt đầu bằng `eToken`
trong `HKLM\SOFTWARE\Microsoft\Cryptography\Defaults\Provider` hoặc `System32\eTOKCSP.dll`.

Gỡ cài đặt xoá ứng dụng, lối tắt và mục gỡ cài đặt. Middleware token được giữ lại vì là phần mềm dùng chung
của mọi ứng dụng chữ ký số trên máy.

## Quy ước code

Backend:

- Đặt code theo nhóm nghiệp vụ `Document/{Dtos,Implements,Interfaces}`. Mỗi service có interface và được đăng
  ký thủ công trong `Program.cs`.
- Controller kế thừa `BaseController`, mỗi action bọc `try` và trả `new(result)` hoặc `OkException(ex)`.
- Lỗi nghiệp vụ ném `UserFriendlyException(ErrorCodes.X, "thông báo")`. Thông báo hiển thị thẳng cho người
  dùng nên viết tiếng Việt, rõ nguyên nhân và cách xử lý.
- Hằng số đặt trong `ktl.be.shared/Constants`. Không viết cứng đường dẫn, đuôi file hay giới hạn trong service.
- Thời gian dùng `DateTimeConstants.VietnamNow`.
- Route dùng tiếng Việt không dấu, nối bằng gạch ngang (`kiem-tra-vi-tri`).
- Vị trí khối chữ ký, dấu đỏ lưu dạng **tỉ lệ 0 đến 1** so với khổ trang, không lưu theo điểm.
- Ghi file kết quả ra `*.tmp` rồi mới đổi tên, để lỗi giữa chừng không để lại file hỏng.

Frontend:

- Component standalone, quản lý state bằng `signal`, `computed`, `effect`; import chung qua `SharedImports`.
- Trang kế thừa `BaseComponent` để dùng `messageSuccess`, `messageError`, `confirmAction`,
  `isResponseSucceed`.
- Route có `data: { reuse: true }` thì giữ nguyên state khi chuyển trang (`AppRouteReuseStrategy`).
- Model đặt ở `app/models`, hằng số ở `app/shared/constants`, service gọi API ở `app/services`.
- Định dạng theo Prettier khai trong `package.json`: dòng tối đa 100 ký tự, dấu nháy đơn.

## Đóng góp

### Quy trình

1. Tạo nhánh từ `main`, đặt tên theo loại thay đổi: `feat/<mo-ta>`, `fix/<mo-ta>`, `refactor/<mo-ta>`,
   `docs/<mo-ta>`, `build/<mo-ta>`.
2. Mỗi nhánh giải quyết một việc. Không gộp sửa lỗi, tính năng mới và dọn code vào cùng một pull request.
3. Chạy đủ các bước kiểm tra bên dưới trước khi mở pull request.
4. Mở pull request vào `main`, mô tả theo mẫu ở cuối mục này, chờ ít nhất một người review.
5. Gộp bằng squash merge để lịch sử `main` mỗi thay đổi một commit.

### Commit

Viết theo [Conventional Commits](https://www.conventionalcommits.org/), phần mô tả bằng tiếng Việt:

```
feat(ky-so): thêm tuỳ chọn ký số sau khi chèn dấu
fix(tao-van-ban): sai tên file khi cột đặt tên để trống
build(installer): chuyển giao diện bộ cài sang tiếng Anh
```

Phạm vi trong ngoặc là tên chức năng (`ky-so`, `tao-van-ban`) hoặc phần hạ tầng
(`installer`, `desktop`, `api`, `fe`).

### Kiểm tra trước khi mở pull request

```bash
cd ktl.be && dotnet build ktl.be.api/ktl.be.api.sln
cd ktl.fe && npx ng build
```

Cả hai phải build thành công và không phát sinh cảnh báo mới. Thay đổi đụng tới ký số hoặc tạo văn bản phải
được chạy thử trên app desktop với file thật; thay đổi phần ký số cần thử với USB token thật và mở file
kết quả bằng Adobe Acrobat Reader để kiểm tra chữ ký hợp lệ.

Thay đổi `KTL.nsi` hoặc `build-desktop.ps1` thì chạy lại `build-desktop.ps1` và cài thử bộ cài, tốt nhất trên
máy ảo sạch.

### Những thứ không được commit

- Thư mục build: `bin/`, `obj/`, `dist/`, `publish/`, `installer-output/`
- File phát hành: `KTL-v*-Setup.exe`, `KTL-v*.zip`
- Bộ cài trình điều khiển trong `ktl.be/installer/drivers/`
- Cơ sở dữ liệu, log, chứng thư có khoá bí mật (`*.pfx`, `*.p12`), file `.env`

Các mục trên đã có trong `.gitignore`. Kiểm tra `git status` trước khi commit để không đưa nhầm file lớn lên.

### Lưu ý khi sửa

- Thêm mã lỗi thì khai cả trong `ErrorCodes` và `ErrorMessages`, tiếp nối dải số đang dùng.
- Hạn chế đổi hậu tố tên file kết quả (`_daky_` trong `SigningConstants`). Mẫu nhận diện được ghép từ chính
  hậu tố đó, nên sau khi đổi, các file kết quả cũ trên máy người dùng không còn bị bỏ qua khi quét thư mục và
  sẽ bị ký chồng lên.
- Thêm hoặc thay chứng thư CA thì cập nhật cả file `.crt` lẫn giá trị SHA-256 ghim trong `SignatureConstants`.
- Nâng phiên bản thì sửa `<Version>` trong `ktl.be.desktop.csproj` cùng `APP_VERSION`, `VIProductVersion` trong
  `KTL.nsi`.

### Mẫu mô tả pull request

```
## Thay đổi
Mô tả ngắn thay đổi và lý do.

## Cách kiểm tra
Các bước đã chạy để xác nhận thay đổi hoạt động.

## Ảnh hưởng
Màn hình, API, dữ liệu trên máy người dùng hoặc bộ cài bị ảnh hưởng (nếu có).
```


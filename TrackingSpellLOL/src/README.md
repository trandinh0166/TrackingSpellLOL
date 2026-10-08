\# League Flash Timer



\## Mô tả

Một overlay đơn giản, chỉ hiển thị 5 nút “Flash” cho các lane (TOP, JUNGLE, MID, AD, SUP).  

Người dùng tự nhấn để khởi động hẹn giờ 5 phút. Ứng dụng \*\*không\*\* đọc bộ nhớ game, \*\*không\*\* inject, \*\*không\*\* vi phạm Vanguard.



\## Yêu cầu

\- Windows 10/11 (64‑bit)  

\- .NET 6 SDK (hoặc .NET 7) – `dotnet --version` ≥ 6.0  

\- (Tùy chọn) Visual Studio 2022 để mở solution  



\## Các bước biên dịch



```powershell

\# 1. Di chuyển vào thư mục dự án (ví dụ)

cd D:\\Downloads\\LeagueFlashTimer



\# 2. Khôi phục các gói NuGet

dotnet restore



\# 3. Biên dịch Release và xuất ra thư mục release

dotnet publish -c Release -r win-x64 --self-contained false -o release


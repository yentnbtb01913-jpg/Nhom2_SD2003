# Hướng dẫn bật đăng nhập Google / Facebook

Code đã được cài sẵn. Bạn chỉ cần: (1) restore package, (2) đăng ký app Google/Facebook để lấy khoá,
(3) bỏ khoá vào cấu hình, (4) chạy lại. Mỗi nút sẽ tự hoạt động khi có đủ khoá.

> URL chạy của web (theo `Properties/launchSettings.json`): **https://localhost:54309**
> Redirect URI cần khai báo:
> - Google: `https://localhost:54309/signin-google`
> - Facebook: `https://localhost:54309/signin-facebook`

---

## Bước 0 — Restore package mới
Đã thêm 2 package vào `WisdomITNews.csproj`. Mở terminal tại thư mục project và chạy:
```bash
dotnet restore
dotnet build
```

---

## Bước 1 — Lấy khoá GOOGLE

1. Vào https://console.cloud.google.com → tạo **Project** mới (hoặc chọn project sẵn có).
2. Menu trái → **APIs & Services → OAuth consent screen**:
   - User Type: **External** → Create.
   - Điền *App name*, *User support email*, *Developer contact email* → Save and Continue.
   - Scopes: để mặc định (email, profile) → Save and Continue.
   - **Test users**: bấm Add Users, thêm chính email Google của bạn (để test khi app còn ở chế độ Testing) → Save.
3. Menu trái → **APIs & Services → Credentials → Create Credentials → OAuth client ID**:
   - Application type: **Web application**.
   - **Authorized JavaScript origins**: `https://localhost:54309`
   - **Authorized redirect URIs**: `https://localhost:54309/signin-google`
   - Create.
4. Copy **Client ID** và **Client secret**.

---

## Bước 2 — Lấy khoá FACEBOOK

1. Vào https://developers.facebook.com → **My Apps → Create App**.
   - Chọn use case **Authenticate and request data from users with Facebook Login** (hoặc loại "Consumer") → điền tên app → Create.
2. Trong app → **Add Product** → chọn **Facebook Login → Set up** (Web).
3. Vào **Facebook Login → Settings**:
   - **Valid OAuth Redirect URIs**: `https://localhost:54309/signin-facebook` → Save changes.
4. Vào **App settings → Basic**: copy **App ID** và **App Secret** (bấm Show).
5. Khi còn ở **Development mode**, chỉ tài khoản admin/developer/tester của app mới đăng nhập được — dùng chính tài khoản của bạn để test là ổn. (Muốn cho mọi người dùng thì chuyển sang Live, cần thêm Privacy Policy URL.)

---

## Bước 3 — Bỏ khoá vào cấu hình

**Cách khuyến nghị (an toàn, không lộ khoá lên source): User Secrets.**
Mở terminal tại thư mục project và chạy:
```bash
dotnet user-secrets init
dotnet user-secrets set "Authentication:Google:ClientId" "DÁN_CLIENT_ID_GOOGLE"
dotnet user-secrets set "Authentication:Google:ClientSecret" "DÁN_CLIENT_SECRET_GOOGLE"
dotnet user-secrets set "Authentication:Facebook:AppId" "DÁN_APP_ID_FACEBOOK"
dotnet user-secrets set "Authentication:Facebook:AppSecret" "DÁN_APP_SECRET_FACEBOOK"
```

**Cách nhanh hơn (kém an toàn — đừng commit):** điền thẳng vào `appsettings.json`, mục đã tạo sẵn:
```json
"Authentication": {
    "Google":   { "ClientId": "DÁN_VÀO_ĐÂY", "ClientSecret": "DÁN_VÀO_ĐÂY" },
    "Facebook": { "AppId": "DÁN_VÀO_ĐÂY", "AppSecret": "DÁN_VÀO_ĐÂY" }
}
```

---

## Bước 4 — Chạy lại
```bash
dotnet run
```
Vào trang Đăng nhập/Đăng ký → bấm nút Google/Facebook. Lần đầu chỉ cần điền khoá của bên nào thì nút bên đó chạy
(nút còn lại nếu chưa có khoá sẽ báo lỗi cho tới khi điền — đây là chủ ý để app vẫn chạy được khi thiếu khoá).

---

## Cách hoạt động (tóm tắt kỹ thuật)
- Nút → `/Account/ExternalLogin?provider=Google|Facebook` → chuyển sang Google/FB đăng nhập.
- Google/FB gọi lại `/signin-google` | `/signin-facebook`, rồi tới `ExternalLoginCallback`:
  lấy **email + tên + ảnh** → **tìm user theo email**; chưa có thì **tạo user mới** (Role = Reader,
  mật khẩu ngẫu nhiên) → set session `UserId/UserName/UserAvatar` y như đăng nhập thường → về trang chủ.
- **Không đổi cấu trúc database** (khớp theo email).

## Lưu ý bảo mật
- Đừng commit Client Secret / App Secret lên Git. Ưu tiên User Secrets.
- Khi deploy thật (không phải localhost), nhớ đổi redirect URI sang domain thật
  (vd `https://tenmiencuaban/signin-google`) trong cả Google Console, Facebook và cấu hình.

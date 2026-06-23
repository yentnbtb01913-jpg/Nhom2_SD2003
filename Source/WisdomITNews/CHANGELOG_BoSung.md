# WisdomITNews — File Bo Sung (Changelog)

## Ngay cap nhat: 08/05/2026

---

## 1. Sua lai Detail.cshtml (Trang chi tiet bai viet)

**Van de:** File `Views/Article/Detail.cshtml` chi co phan `@section Scripts`, thieu toan bo HTML body.

**Da sua:**
- Tao lai day du HTML: breadcrumb, article header, AI toolbar, AI summary box, thumbnail, article body, tags, share bar, author box, comment section, sidebar (related articles, popular, AI tools).
- Comment form: neu da dang nhap thi tu dong dien ten, khong can nhap lai.
- Comment tree: hien thi dung cay binh luan voi replies long nhau.

---

## 2. Sua _CommentNode.cshtml (Phan binh luan)

**Thay doi:**
- **Admin binh luan:** Hien thi avatar la bieu tuong tia set (&#9889;) voi background gradient do-cam, kem badge "Admin" noi bat.
- **User da dang ky:** Hien thi ten nguoi dung voi link den trang profile (`/u/username`), avatar la anh (neu da upload) hoac chu cai dau ten.
- **Khach (chua dang ky):** Hien thi ten va chu cai dau.
- **Reply form:** Neu da dang nhap thi tu dong dien ten, khong can nhap lai ho ten va email.

---

## 3. Cap nhat Header (_Layout.cshtml)

**Thay doi:**
- **Da dang nhap:** An nut "Dang nhap" va "Dang ky", thay bang dropdown tai khoan ca nhan:
  - Hien avatar nho + ten nguoi dung
  - Click mo menu: xem profile, doi avatar, dang xuat
  - Them nut "Tai khoan" tren header chinh
- **Chua dang nhap:** Van hien nut Dang nhap / Dang ky nhu cu.
- Them JavaScript xu ly dong/mo dropdown khi click.

---

## 4. Them chuc nang Upload Avatar + Trang tai khoan ca nhan

### AccountController.cs
- **MyProfile():** Trang tai khoan ca nhan cua user dang dang nhap, hien thi thong tin, bai viet, so binh luan.
- **UploadAvatar():** API upload anh dai dien (ho tro JPG, PNG, GIF, WebP, toi da 5MB), cap nhat database va session.
- **UpdateProfile():** API cap nhat ho ten va email.

### Views/Account/MyProfile.cshtml (MOI)
- Hien thi avatar lon voi overlay "Doi anh" khi hover.
- Click de chon file upload, tu dong gui len server va cap nhat realtime.
- Form chinh sua: ho ten, email, nut luu.
- Danh sach bai viet da dang.

---

## 5. Them Quan ly tai khoan nguoi dung (Admin)

### AdminController.cs
- **Users():** Danh sach tat ca tai khoan nguoi dung.
- **UserDetail(id):** Xem chi tiet mot tai khoan: thong tin, bai viet, so binh luan.
- **AdminUploadUserAvatar():** Admin co the doi avatar cho bat ky user nao.
- **DeleteUser():** Xoa tai khoan nguoi dung.

### Views/Admin/Users.cshtml (MOI)
- Bang danh sach user voi avatar, ho ten, username, email, role, ngay tao.
- 3 the thong ke: tong tai khoan, da xac nhan email, moi trong 7 ngay.
- Nut hanh dong: doi avatar, xem chi tiet, xoa.

### Views/Admin/UserDetail.cshtml (MOI)
- Trang chi tiet user: avatar lon, thong tin, nut doi avatar, danh sach bai viet.

### _AdminLayout.cshtml
- Them muc "Quan ly tai khoan" vao sidebar admin (phan "Nguoi dung").

---

## 6. Cai thien Trang chu (Home/Index.cshtml)

**Thay doi:**
- **Bai bao to hon:** Thay the article-list (nho) bang `home-articles-grid` voi card lon: anh to (200px), tieu de lon, excerpt 3 dong, metadata day du.
- **Chia 2 cot:** Phan "Tin moi nhat" chia thanh 2 cot trai-phai, bai chan ben trai, bai le ben phai.
- Card co hover effect: zoom anh, nang len, doi mau tieu de.
- Responsive: 1 cot tren mobile.

---

## 7. Bo sung CSS (wwwroot/css/site.css)

Cac class moi them:
- **Topbar dropdown:** `.topbar-account-dropdown`, `.topbar-account-toggle`, `.topbar-dropdown-menu`, `.dropdown-item`, v.v.
- **Comment admin:** `.comment-avatar-admin`, `.admin-bolt`, `.admin-badge-inline`, `.comment-user-link`, `.reply-logged-info`.
- **Profile edit:** `.avatar-upload-overlay`, `.profile-edit-section`, `.profile-section-title`.
- **Home grid:** `.home-articles-grid`, `.home-articles-col`, `.home-article-card`, `.home-card-thumb`, `.home-card-badge`, `.home-card-body`, `.home-card-title`, `.home-card-excerpt`, `.home-card-meta`.
- **Responsive:** Bo sung media queries cho mobile.

---

## Danh sach file da thay doi:

| File | Hanh dong |
|------|-----------|
| `Views/Article/Detail.cshtml` | Viet lai hoan toan |
| `Views/Shared/_CommentNode.cshtml` | Viet lai — admin bolt, user link, auto-fill reply |
| `Views/Shared/_Layout.cshtml` | Sua — dropdown tai khoan, an dang nhap khi da login |
| `Views/Shared/_AdminLayout.cshtml` | Sua — them muc "Quan ly tai khoan" |
| `Views/Home/Index.cshtml` | Viet lai — 2 cot, card lon |
| `Views/Account/MyProfile.cshtml` | **MOI** — trang tai khoan ca nhan + upload avatar |
| `Views/Admin/Users.cshtml` | **MOI** — quan ly tai khoan nguoi dung |
| `Views/Admin/UserDetail.cshtml` | **MOI** — chi tiet tai khoan |
| `Controllers/AccountController.cs` | Sua — them MyProfile, UploadAvatar, UpdateProfile |
| `Controllers/AdminController.cs` | Sua — them Users, UserDetail, AdminUploadUserAvatar, DeleteUser |
| `wwwroot/css/site.css` | Sua — them ~200 dong CSS moi |
| `CHANGELOG_BoSung.md` | **MOI** — file nay |

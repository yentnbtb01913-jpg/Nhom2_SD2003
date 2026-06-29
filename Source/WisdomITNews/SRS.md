# 3.2 Đặc tả yêu cầu hệ thống (SRS)

## 3.2.1 Đăng ký tài khoản (Reader)

### 3.2.1.1 Mô tả chức năng
• Người dùng truy cập trang đăng ký, nhập Username, Email, Password, FullName.
• Hệ thống kiểm tra: tất cả trường bắt buộc không được để trống.
• Mật khẩu phải có tối thiểu 6 ký tự.
• Username được chuyển thành lowercase và kiểm tra trùng lặp trong bảng Users.
• Email được chuyển thành lowercase và kiểm tra trùng lặp trong bảng Users.
• Mật khẩu được mã hóa bằng BCrypt trước khi lưu vào database.
• Sau khi đăng ký thành công, hệ thống tự động đăng nhập (lưu UserId, UserName, UserAvatar vào Session).
• Chuyển hướng về trang chủ (Home/Index).
• Nếu có lỗi, hiển thị thông báo lỗi tương ứng trên form đăng ký.

### 3.2.1.2 Dữ liệu liên quan
• Bảng Users: Id, Username, Email, PasswordHash, FullName, Role (= 'Reader'), IsEmailConfirmed, CreatedAt

### 3.2.1.3 Đối tượng sử dụng
• Người dùng ẩn danh (Anonymous) muốn tạo tài khoản để sử dụng các chức năng yêu cầu đăng nhập.

---

## 3.2.2 Đăng nhập tài khoản

### 3.2.2.1 Mô tả chức năng
• Người dùng truy cập trang đăng nhập, nhập Username hoặc Email và Password.
• Hệ thống kiểm tra: trường không được để trống.
• Hệ thống tìm user theo Username hoặc Email (không phân biệt hoa thường).
• Hệ thống kiểm tra mật khẩu bằng BCrypt.Verify.
• Nếu tài khoản bị khóa (IsActive = false), hiển thị thông báo và từ chối đăng nhập.
• Sau khi đăng nhập thành công, hệ thống lưu UserId, UserName, UserAvatar vào Session.
• Chuyển hướng về trang chủ (Home/Index).
• Nếu có lỗi, hiển thị thông báo "Sai tên đăng nhập / email hoặc mật khẩu".

### 3.2.2.2 Đăng nhập ngoài (Google/Facebook)
• Người dùng chọn đăng nhập bằng Google hoặc Facebook.
• Hệ thống chuyển hướng đến nhà cung cấp xác thực.
• Sau khi xác thực thành công, hệ thống lấy Email, FullName, Avatar từ nhà cung cấp.
• Nếu Email chưa tồn tại trong hệ thống, hệ thống tự động tạo tài khoản mới:
  - Username được tạo từ email (loại bỏ ký tự đặc biệt, thêm số nếu trùng).
  - Password được tạo ngẫu nhiên và mã hóa bằng BCrypt.
  - Role = 'Reader', IsEmailConfirmed = true.
• Nếu Email đã tồn tại, hệ thống đăng nhập tài khoản đó.
• Lưu thông tin vào Session và chuyển hướng về trang chủ.

### 3.2.2.3 Đăng nhập nhân viên bằng Google
• Nhân viên chọn đăng nhập bằng Google trong khu vực nhân viên.
• Hệ thống chỉ khớp Email với nhân viên ĐÃ được cấp trong bảng Admin.
• KHÔNG tự động tạo tài khoản mới cho nhân viên.
• Nếu Email không tồn tại trong bảng Admin hoặc Admin bị khóa, hiển thị thông báo lỗi.
• Sau khi đăng nhập thành công, lưu AdminId, AdminName, AdminRole vào Session.
• Chuyển hướng về Dashboard nhân viên (NhanVien/Dashboard).

### 3.2.2.4 Đăng xuất
• Người dùng nhấn nút đăng xuất.
• Hệ thống xóa tất cả Session keys (UserId, UserName, UserAvatar hoặc AdminId, AdminName, AdminRole).
• Chuyển hướng về trang chủ.

### 3.2.2.5 Dữ liệu liên quan
• Bảng Users: Id, Username, Email, PasswordHash, FullName, Role, IsActive
• Bảng Admin: Id, Username, Email, PasswordHash, FullName, Role, IsActive, LastLogin

### 3.2.2.6 Đối tượng sử dụng
• Người dùng đã có tài khoản muốn đăng nhập.
• Người dùng muốn đăng nhập bằng Google/Facebook.
• Nhân viên muốn đăng nhập bằng Google.

---

## 3.2.3 Quản lý Profile người dùng

### 3.2.3.1 Mô tả chức năng
• Người dùng đã đăng nhập có thể xem profile công khai của mình hoặc người khác qua URL /u/{username}.
• Profile hiển thị: Avatar, Cover, FullName, Bio, số lượng bài viết, số lượng bình luận, số lượng bạn bè, số lượng người theo dõi.
• Người dùng có thể xem và chỉnh sửa profile riêng qua trang MyProfile.
• Cập nhật thông tin cá nhân: FullName, Email, Bio.
• Upload Avatar: hỗ trợ JPG, PNG, GIF, WEBP, tối đa 5MB.
• Upload Cover: hỗ trợ JPG, PNG, GIF, WEBP, tối đa 10MB.
• Khi upload ảnh mới, hệ thống tự động xóa ảnh cũ (nếu có).
• Xem danh sách người theo dõi (Followers) và danh sách đang theo dõi (Following).

### 3.2.3.2 Khu cá nhân (Hub)
• Người dùng có thể truy cập khu cá nhân với 5 tab:
  - For You: Gợi ý bài viết theo lịch sử đọc + chuyên mục theo dõi + tác giả theo dõi.
  - Topics: Quản lý chuyên mục theo dõi, hiển thị bài viết từ chuyên mục đã theo dõi.
  - Following: Danh sách người đang theo dõi.
  - History: Lịch sử bài viết đã xem (tối đa 20 bài gần nhất).
  - Saved: Danh sách bài viết đã lưu.
• Lưu/Bỏ lưu bài viết (Toggle Save).
• Theo dõi/Bỏ theo dõi chuyên mục (Toggle Follow Category).

### 3.2.3.3 Dữ liệu liên quan
• Bảng Users: Id, Username, Email, FullName, AvatarUrl, CoverUrl, Bio
• Bảng Articles: AuthorUserId, Status, PublishedAt
• Bảng Comments: UserId
• Bảng Friendships: RequesterId, ReceiverId, Status
• Bảng UserFollows: FollowerId, FollowingId
• Bảng SavedArticles: UserId, ArticleId, SavedAt
• Bảng UserCategoryFollows: UserId, CategoryId
• Bảng ViewHistories: SessionId, ArticleId, ViewedAt

### 3.2.3.4 Đối tượng sử dụng
• Người dùng đã đăng nhập (Reader).

---

## 3.2.4 Quản lý bài viết (Journalist)

### 3.2.4.1 Mô tả chức năng
• Phóng viên có thể tạo bài viết mới với các thông tin: Title, Slug, Summary, Content, Thumbnail, ThumbnailAlt, CategoryId, Region.
• Phóng viên có thể chỉnh sửa bài viết đã tạo.
• Phóng viên có thể xóa bài viết của mình.
• Khi tạo/chỉnh sửa bài viết, hệ thống tự động áp dụng AI Moderation:
  - Nếu AI Score > 70: Bài viết bị từ chối tự động (Status = "Rejected"), gửi thông báo vi phạm AI cho tác giả.
  - Nếu AI Score >= 40: Bài viết được chuyển sang trạng thái chờ duyệt (Status = "PendingReview"), gửi thông báo vi phạm AI cho tác giả.
  - Nếu AI Score < 40: Bài viết được lưu nháp (Status = "draft").
• Phóng viên có thể xem danh sách bài viết của mình với các trạng thái: draft, pending_review, published, rejected.
• Phóng viên có thể xem thống kê: số lượt xem, số bình luận.

### 3.2.4.2 Dữ liệu liên quan
• Bảng Articles: Id, Title, Slug, Summary, Content, Thumbnail, ThumbnailAlt, CategoryId, AuthorUserId, Status, Region, CreatedAt, UpdatedAt
• Bảng AILogs: ArticleId, Action, ResultText, IsSuccess
• Bảng Notifications: TargetUserId, Type, ViolationContent, RelatedArticleId

### 3.2.4.3 Đối tượng sử dụng
• Phóng viên (Journalist) đã đăng nhập.

---

## 3.2.5 Quản lý bài viết (Admin/Nhân viên)

### 3.2.5.1 Mô tả chức năng
• Admin và Nhân viên có thể xem danh sách tất cả bài viết với bộ lọc: Status, Category, Author, Region, từ khóa.
• Admin và Nhân viên có thể tạo bài viết mới (tương tự Phóng viên).
• Admin và Nhân viên có thể chỉnh sửa bất kỳ bài viết nào.
• Admin và Nhân viên có thể xóa bài viết.
• Admin và Nhân viên có thể duyệt bài viết (Approve): chuyển Status từ "draft" hoặc "PendingReview" sang "published", set PublishedAt.
• Admin và Nhân viên có thể từ chối bài viết (Reject):
  - Chuyển Status sang "rejected".
  - Nhập lý do từ chối.
  - Gửi thông báo cho tác giả bài viết qua NotificationService.
• Admin và Nhân viên có thể đặt bài viết nổi bật (IsFeatured).
• Admin và Nhân viên có thể quản lý AI Logs: xem lịch sử kiểm duyệt AI, kết quả, vấn đề phát hiện.
• Admin và Nhân viên có thể quản lý Feedbacks: xem danh sách góp ý, đánh dấu đã giải quyết (IsResolved).
• Admin và Nhân viên có thể quản lý Newsletter Subscribers: xem danh sách, xóa subscriber.

### 3.2.5.2 Dữ liệu liên quan
• Bảng Articles: Id, Title, Slug, Summary, Content, Thumbnail, ThumbnailAlt, CategoryId, AuthorId, AuthorUserId, Status, IsFeatured, PublishedAt, Region
• Bảng AILogs: ArticleId, Action, PromptText, ResultText, ModelUsed, TokensUsed, IsSuccess, ErrorMsg
• Bảng FeedbackReport: PageUrl, Type, Description, IsResolved
• Bảng NewsletterSubscriber: Email, FullName, Phone, Source, Status

### 3.2.5.3 Đối tượng sử dụng
• Admin đã đăng nhập.
• Nhân viên (NhanVien) đã đăng nhập.

---

## 3.2.6 Hệ thống thông báo

### 3.2.6.1 Mô tả chức năng
• Admin và Nhân viên có thể xem danh sách thông báo với bộ lọc: từ khóa, loại (system, ai_violation, article_rejected, video_rejected, custom), ngày.
• Admin và Nhân viên có thể gửi thông báo mới:
  - Admin: gửi đến toàn bộ hệ thống (all), theo email, theo nhà báo (journalist), theo user cụ thể.
  - Nhân viên: chỉ gửi đến toàn bộ hệ thống (all) hoặc nhà báo (journalist).
• Admin và Nhân viên có thể đánh dấu thông báo đã đọc.
• Admin và Nhân viên có thể xóa thông báo (cá nhân hoặc hàng loạt).
• Admin và Nhân viên có thể xóa tất cả thông báo đã đọc.
• Phóng viên có thể xem Hộp thư (Inbox) với bộ lọc: từ khóa, loại, trạng thái, ngày.
• Phóng viên có thể đánh dấu thông báo đã đọc (khi mở hoặc đánh dấu tất cả).
• Phóng viên có thể xóa thông báo (cá nhân, hàng loạt, hoặc tất cả đã đọc).
• Hệ thống sử dụng SignalR để gửi thông báo real-time:
  - NotificationHub quản lý các nhóm: user_{userId}, email_{email}, journalists, all.
  - Khi có thông báo mới, hệ thống gửi qua SignalR đến nhóm tương ứng.
  - Phóng viên có thể đánh dấu đã đọc qua SignalHub.MarkAsRead.

### 3.2.6.2 Các loại thông báo
• System: Thông báo hệ thống gửi đến toàn bộ người dùng.
• AI Violation: Thông báo vi phạm nội dung AI gửi đến tác giả bài viết.
• Article Rejected: Thông báo bài viết bị từ chối gửi đến tác giả.
• Video Rejected: Thông báo video bị từ chối gửi đến admin.
• Custom: Thông báo tùy chỉnh.

### 3.2.6.3 Dữ liệu liên quan
• Bảng Notifications: Id, Code, Title, Content, Type, Icon, IconColor, TargetType, TargetUserId, TargetEmail, TargetRole, ViolationContent, ViolationReason, RelatedArticleId, RelatedCommentId, RelatedVideoId, IsRead, IsDeleted, SentBy, SentByAdminId, CreatedAt, ReadAt

### 3.2.6.4 Đối tượng sử dụng
• Admin, Nhân viên, Phóng viên đã đăng nhập.

---

## 3.2.7 Bình luận và tương tác

### 3.2.7.1 Mô tả chức năng
• Người dùng (có thể chưa đăng nhập) có thể bình luận bài viết.
• Người dùng nhập: Name, Email, Content.
• Hệ thống áp dụng AI Moderation cho bình luận:
  - Nếu AI Score > 70: Bình luận bị từ chối (Status = "rejected"), hiển thị thông báo cho người dùng.
  - Nếu AI Score < 70: Bình luận được lưu ở trạng thái chờ duyệt (Status = "pending").
• Người dùng có thể trả lời bình luận (Reply):
  - Nhập Name, Email, Content.
  - Áp dụng AI Moderation tương tự.
• Người dùng đã đăng nhập có thể Like/Dislike bình luận (mỗi session chỉ được vote 1 lần).
• Người dùng đã đăng nhập có thể xóa bình luận của chính mình (bao gồm cả các trả lời).
• Admin và Nhân viên có thể duyệt/từ chối bình luận (chuyển Status sang "approved" hoặc "rejected").
• Bình luận được hiển thị dạng cây (tree) với các trả lời lồng nhau.

### 3.2.7.2 Dữ liệu liên quan
• Bảng Comments: Id, ArticleId, AuthorName, AuthorEmail, Content, ParentId, ParentCommentId, Status, Likes, DislikeCount, UserId, CreatedAt
• Bảng CommentVote: Id, CommentId, SessionId, VoteType
• Bảng AILogs: ArticleId, Action, ResultText

### 3.2.7.3 Đối tượng sử dụng
• Người dùng (có thể chưa đăng nhập) để bình luận.
• Người dùng đã đăng nhập để Like/Dislike và xóa bình luận.
• Admin và Nhân viên để duyệt bình luận.

---

## 3.2.8 Chat và Kết bạn

### 3.2.8.1 Mô tả chức năng - Chat
• Người dùng đã đăng nhập có thể sử dụng Chat:
  - Xem danh sách nhóm chat của mình.
  - Tạo nhóm chat mới, thêm thành viên.
  - Chat 1-1 (Direct Message) với người khác.
  - Gửi tin nhắn văn bản, hình ảnh, file.
  - Trả lời tin nhắn (Reply).
  - Ghim tin nhắn (Pin).
  - Xóa tin nhắn của chính mình.
  - Rời nhóm chat.
  - Tìm kiếm người dùng để thêm vào nhóm hoặc nhắn tin.
• Hệ thống sử dụng SignalR (ChatHub) để gửi tin nhắn real-time.
• Hệ thống hiển thị trạng thái online của thành viên.

### 3.2.8.2 Mô tả chức năng - Kết bạn
• Người dùng có thể gửi lời mời kết bạn.
• Người dùng có thể chấp nhận/từ chối lời mời kết bạn.
• Nếu cả hai gửi lời mời cho nhau, hệ thống tự động chấp nhận.
• Người dùng có thể hủy kết bạn.
• Người dùng có thể xem danh sách bạn bè (kèm trạng thái online).
• Người dùng có thể xem danh sách lời mời đang chờ.
• Người dùng có thể xem gợi ý kết bạn (ưu tiên bạn chung).
• Người dùng có thể theo dõi (Follow) người khác.
• Người dùng có thể bỏ theo dõi (Unfollow).
• Hệ thống gửi thông báo real-time qua SignalR khi có sự kiện kết bạn.

### 3.2.8.3 Dữ liệu liên quan
• Bảng ChatGroups: Id, Name, Avatar, IsDirectMessage, CreatorType, CreatorId
• Bảng ChatMembers: Id, GroupId, MemberType, MemberId, Role, JoinedAt
• Bảng ChatMessages: Id, GroupId, SenderType, SenderId, SenderName, SenderAvatar, Content, MessageType, FileUrl, FileName, ReplyToId, IsPinned, SentAt
• Bảng Friendships: Id, RequesterId, ReceiverId, Status, CreatedAt, AcceptedAt
• Bảng UserFollows: FollowerId, FollowingId

### 3.2.8.4 Đối tượng sử dụng
• Người dùng đã đăng nhập.

---

## 3.2.9 AI và Tóm tắt

### 3.2.9.1 Mô tả chức năng
• Hệ thống tích hợp AI để:
  - Moderation nội dung: kiểm duyệt bài viết và bình luận, phát hiện vi phạm.
  - Tóm tắt bài viết: tạo tóm tắt ngắn gọn cho bài viết dài.
  - Gợi ý tiêu đề: đề xuất tiêu đề hấp dẫn cho bài viết.
  - Chat AI: người dùng có thể chat với AI để hỏi về nội dung bài viết.
• Khi người dùng yêu cầu tóm tắt bài viết:
  - Hệ thống gọi AIService.SummarizeAsync.
  - Kết quả được cache vào trường AiSummary của bài viết.
  - Các lần xem sau hiển thị tóm tắt đã cache.
• Khi người dùng yêu cầu gợi ý tiêu đề:
  - Hệ thống gọi AIService.SuggestTitlesAsync.
  - Trả về danh sách tiêu đề gợi ý.
• Khi người dùng chat với AI:
  - Hệ thống gọi AIService.ChatAsync.
  - Trả về câu trả lời từ AI.

### 3.2.9.2 Dữ liệu liên quan
• Bảng Articles: AiSummary
• Bảng AILogs: ArticleId, Action, PromptText, ResultText, ModelUsed, TokensUsed, IsSuccess

### 3.2.9.3 Đối tượng sử dụng
• Người dùng đã đăng nhập để sử dụng Chat AI.
• Admin, Nhân viên, Phóng viên để sử dụng AI Moderation và Tóm tắt.

---

## 3.2.10 RSS và Region

### 3.2.10.1 Mô tả chức năng - RSS
• Hệ thống cung cấp RSS Feed tại /rss.
• Hỗ trợ lọc theo vùng: /rss?region=ha-noi
• Hỗ trợ lọc theo chuyên mục: /rss?category=lap-trinh
• RSS Feed bao gồm: tiêu đề, link, mô tả, category, GUID, ngày đăng, thumbnail.
• Hệ thống cung cấp trang danh sách RSS feeds tại /rss/list.
• Hệ thống tạo Sitemap tại /sitemap.xml.
• Hệ thống tạo robots.txt.

### 3.2.10.2 Mô tả chức năng - Region
• Hệ thống hỗ trợ tin tức theo khu vực: Đồng Nai, Hà Nội, TP. Hồ Chí Minh, Đà Nẵng, Hải Phòng, Cần Thơ.
• Người dùng có thể chuyển vùng qua form hoặc URL trực tiếp (/dong-nai, /ha-noi, v.v.).
• Vùng được lưu vào Session (CurrentRegion).
• Trang hiển thị vùng bao gồm:
  - Danh sách bài viết của vùng đó.
  - Bài nổi bật (Featured) của vùng.
  - Bản đồ Leaflet hiển thị vị trí các bài viết.
  - Thông tin thời tiết của vùng.
• Trang chủ hiển thị bài viết ưu tiên theo vùng đã chọn.

### 3.2.10.3 Dữ liệu liên quan
• Bảng Articles: Region, CategoryId, Status, PublishedAt
• Bảng Categories: Slug, Name, IsVisible

### 3.2.10.4 Đối tượng sử dụng
• Tất cả người dùng (không cần đăng nhập).

---

## 3.2.11 Video

### 3.2.11.1 Mô tả chức năng
• Người dùng có thể xem danh sách video tại /video.
• Người dùng có thể xem chi tiết video tại /Video/Watch/{id}.
• Hệ thống hỗ trợ video từ YouTube hoặc video tự upload.
• Khi xem video, hệ thống tăng số lượt xem (Views).
• Nếu database chưa có video, hệ thống hiển thị dữ liệu mẫu.

### 3.2.11.2 Dữ liệu liên quan
• Bảng Videos: Id, Title, YouTubeId, Source, Views, PublishedAt, VideoType, VideoUrl, Status

### 3.2.11.3 Đối tượng sử dụng
• Tất cả người dùng (không cần đăng nhập).

---

## 3.2.12 Feedback

### 3.2.12.1 Mô tả chức năng
• Người dùng có thể gửi góp ý/báo cáo lỗi từ bất kỳ trang nào.
• Người dùng nhập: Type (bug, suggestion, other), Description.
• Hệ thống tự động lấy URL trang hiện tại từ Referer header.
• Feedback được lưu vào database với trạng thái chưa giải quyết (IsResolved = false).
• Hệ thống hiển thị thông báo cảm ơn sau khi gửi.

### 3.2.12.2 Dữ liệu liên quan
• Bảng FeedbackReport: PageUrl, Type, Description, CreatedAt, IsResolved

### 3.2.12.3 Đối tượng sử dụng
• Tất cả người dùng (không cần đăng nhập).

---

## 3.2.13 Trang chủ và Tìm kiếm

### 3.2.13.1 Mô tả chức năng - Trang chủ
• Trang chủ hiển thị:
  - Bài nổi bật (Featured): ưu tiên bài của vùng hiện tại.
  - Bài mới nhất (Latest): ưu tiên bài của vùng hiện tại.
  - Bài AI (AIArticles): bài từ chuyên mục AI.
  - Bài phổ biến (Popular): xếp theo lượt xem thực tế trong 24 giờ, nếu thiếu thì bù theo tổng lượt xem.
  - Danh sách chuyên mục ( visible).
  - Danh sách tags.
• Trang chủ hỗ trợ gợi ý tìm kiếm (autocomplete) khi người dùng gõ từ khóa.

### 3.2.13.2 Mô tả chức năng - Tìm kiếm
• Người dùng có thể tìm kiếm bài viết theo từ khóa.
• Hệ thống tìm trong: Title, Summary, Content.
• Kết quả hiển thị tối đa 20 bài, sắp xếp theo ngày đăng mới nhất.

### 3.2.13.3 Mô tả chức năng - Lịch sử xem
• Người dùng có thể xem lịch sử bài viết đã xem tại /lich-su.
• Hệ thống lưu lịch sử theo Session ID.
• Mỗi session lưu tối đa 20 bài gần nhất.
• Khi vượt quá 20, hệ thống xóa bài cũ nhất.

### 3.2.13.4 Dữ liệu liên quan
• Bảng Articles: Status, PublishedAt, Views, CategoryId, Region
• Bảng Categories: IsVisible, SortOrder
• Bảng Tags
• Bảng ViewHistories: SessionId, ArticleId, ViewedAt

### 3.2.13.5 Đối tượng sử dụng
• Tất cả người dùng (không cần đăng nhập).

---

## 3.2.14 Đăng ký Newsletter

### 3.2.14.1 Mô tả chức năng
• Người dùng có thể đăng ký nhận tin qua email từ trang chủ.
• Người dùng nhập Email.
• Hệ thống kiểm tra email hợp lệ và chưa đăng ký.
• Hệ thống lưu subscriber vào database.
• Hệ thống gửi email chào mừng qua EmailService.
• Nếu gửi email thất bại, hệ thống rollback (xóa subscriber) để user có thể thử lại.
• Người dùng đã đăng nhập có thể đăng ký newsletter bằng email của tài khoản.

### 3.2.14.2 Dữ liệu liên quan
• Bảng NewsletterSubscriber: Email, FullName, Phone, Source, Status, SubscribedAt

### 3.2.14.3 Đối tượng sử dụng
• Tất cả người dùng (không cần đăng nhập) để đăng ký email.
• Người dùng đã đăng nhập để đăng ký bằng email tài khoản.

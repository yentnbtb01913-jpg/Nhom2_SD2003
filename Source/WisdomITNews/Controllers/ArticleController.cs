using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WisdomITNews.Data;
using WisdomITNews.Models;
using WisdomITNews.Services;

namespace WisdomITNews.Controllers;

public class ArticleController : Controller
{
    private readonly AppDbContext _db;
    private readonly AIService _ai;
    private readonly ILogger<ArticleController> _logger;
    public ArticleController(AppDbContext db, AIService ai, ILogger<ArticleController> logger)
    {
        _db = db;
        _ai = ai;
        _logger = logger;
    }

    [HttpPost]
    [Route("/article/chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest req)
    {
        try
        {
            var (reply, success) = await _ai.ChatAsync(req?.Message ?? "");
            return Json(new { reply, success });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Chat failed");
            return Json(new { reply = "Hệ thống đang bận, vui lòng thử lại.", success = false });
        }
    }

    public async Task<IActionResult> Detail(string slug)
    {
        if (string.IsNullOrEmpty(slug)) return RedirectToAction("Index", "Home");

        var article = await _db.Articles
            .Include(a => a.Category).Include(a => a.Author).Include(a => a.AuthorUser)
            .FirstOrDefaultAsync(a => a.Slug == slug && a.Status == "published");

        if (article == null) return NotFound();

        article.Views++;
        await _db.SaveChangesAsync();

        // [H] Lưu ViewHistory + cắt bớt vượt 20
        try
        {
            HttpContext.Session.SetString("__init", "1");
            var sid = HttpContext.Session.Id;
            if (!string.IsNullOrEmpty(sid))
            {
                _db.ViewHistories.Add(new ViewHistory
                {
                    ArticleId = article.Id,
                    SessionId = sid,
                    ViewedAt = DateTime.Now
                });
                await _db.SaveChangesAsync();

                var overflow = await _db.ViewHistories
                    .Where(v => v.SessionId == sid)
                    .OrderByDescending(v => v.ViewedAt)
                    .Skip(20)
                    .ToListAsync();
                if (overflow.Count > 0)
                {
                    _db.ViewHistories.RemoveRange(overflow);
                    await _db.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Save ViewHistory failed for articleId={Id}", article.Id);
        }

        // [D] Dynamic theme theo slug
        var catSlug = article.Category?.Slug ?? "";
        string theme = "default";
        if (catSlug.Contains("game") || catSlug.Contains("cong-nghe"))
            theme = "neon";
        else if (catSlug.Contains("chien-su") || catSlug.Contains("the-gioi"))
            theme = "dark";
        ViewBag.Theme = theme;

        var tags = await _db.ArticleTags
            .Include(at => at.Tag)
            .Where(at => at.ArticleId == article.Id)
            .Select(at => at.Tag!)
            .ToListAsync();

        // [F] Comments tree — load tất cả approved comments
        var comments = await _db.Comments
            .Include(c => c.User)
            .Where(c => c.ArticleId == article.Id && c.Status == "approved")
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var related = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && a.CategoryId == article.CategoryId && a.Id != article.Id)
            .OrderByDescending(a => a.Views)
            .Take(4).ToListAsync();

        var popular = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published")
            .OrderByDescending(a => a.Views)
            .Take(5).ToListAsync();

        var hubUserId = HttpContext.Session.GetInt32("UserId");
        ViewBag.CanSave = hubUserId != null;
        ViewBag.IsSaved = hubUserId != null && await _db.SavedArticles.AnyAsync(s => s.UserId == hubUserId.Value && s.ArticleId == article.Id);
        ViewBag.AuthorProfile = article.AuthorUserId != null
            ? await _db.JournalistProfiles.FirstOrDefaultAsync(jp => jp.UserId == article.AuthorUserId.Value)
            : null;

        ViewBag.Podcast = await _db.Podcasts
            .Where(p => p.ArticleId == article.Id)
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();

        // Gating Premium: bài IsPremiumOnly chỉ mở đầy đủ cho người có gói hiệu lực.
        bool hasPremium = await WisdomITNews.Services.PremiumAccess.HasAsync(_db, hubUserId);
        ViewBag.HasPremiumAccess = hasPremium;
        ViewBag.IsPremiumLocked = article.IsPremiumOnly && !hasPremium;
                return View(new ArticleViewModel
        {
            Article = article,
            Comments = comments,
            Tags = tags,
            RelatedArticles = related,
            PopularArticles = popular
        });
    }

    // Tự load thêm bài cùng thể loại (infinite scroll ở trang chi tiết)
    public async Task<IActionResult> LoadRelated(int categoryId, int excludeId, int skip)
    {
        const int take = 6;
        var arts = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && a.CategoryId == categoryId && a.Id != excludeId)
            .OrderByDescending(a => a.PublishedAt)
            .Skip(skip).Take(take)
            .ToListAsync();
        ViewBag.Video = null;                 // không chèn video vào danh sách liên quan
        ViewBag.InsertAt = arts.Count;
        return PartialView("~/Views/Home/_FeedMore.cshtml", arts);
    }

    // "Bài báo bạn có thể thích" — liệt kê toàn bộ bài (trừ bài hiện tại), ưu tiên xem nhiều
    public async Task<IActionResult> LoadSuggested(int excludeId, int skip)
    {
        const int take = 6;
        var arts = await _db.Articles
            .Include(a => a.Category)
            .Where(a => a.Status == "published" && a.Id != excludeId)
            .OrderByDescending(a => a.Views).ThenByDescending(a => a.PublishedAt)
            .Skip(skip).Take(take)
            .ToListAsync();
        ViewBag.Video = null;
        ViewBag.InsertAt = arts.Count;
        return PartialView("~/Views/Home/_FeedMore.cshtml", arts);
    }

    // ====================== [F] Like / Dislike / Reply ======================

    [HttpPost]
    [Route("/article/like-comment/{id:int}")]
    public Task<IActionResult> LikeComment(int id) => VoteCommentAsync(id, "Like");

    [HttpPost]
    [Route("/article/dislike-comment/{id:int}")]
    public Task<IActionResult> DislikeComment(int id) => VoteCommentAsync(id, "Dislike");

    private async Task<IActionResult> VoteCommentAsync(int commentId, string voteType)
    {
        try
        {
            var comment = await _db.Comments.FirstOrDefaultAsync(c => c.Id == commentId);
            if (comment == null)
                return NotFound(new { success = false, message = "Không tìm thấy bình luận" });

            HttpContext.Session.SetString("__init", "1");
            var sessionId = HttpContext.Session.Id;
            if (string.IsNullOrEmpty(sessionId))
                return StatusCode(500, new { success = false, message = "Không tạo được session" });

            var existing = await _db.CommentVotes
                .FirstOrDefaultAsync(v => v.CommentId == commentId && v.SessionId == sessionId);

            if (existing != null)
            {
                return Ok(new
                {
                    success = false,
                    message = "Bạn đã bình chọn rồi",
                    likeCount = comment.LikeCount,
                    dislikeCount = comment.DislikeCount
                });
            }

            _db.CommentVotes.Add(new CommentVote
            {
                CommentId = commentId,
                SessionId = sessionId,
                VoteType = voteType
            });
            if (voteType == "Like") comment.LikeCount++;
            else comment.DislikeCount++;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                likeCount = comment.LikeCount,
                dislikeCount = comment.DislikeCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "VoteComment failed (commentId={Id}, type={Type})", commentId, voteType);
            return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
        }
    }

    [HttpPost]
    [Route("/article/reply-comment")]
    public async Task<IActionResult> ReplyComment([FromBody] ReplyRequest req)
    {
        if (req == null
            || req.ParentCommentId <= 0
            || string.IsNullOrWhiteSpace(req.Name)
            || string.IsNullOrWhiteSpace(req.Content))
        {
            return BadRequest(new { success = false, message = "Thiếu thông tin" });
        }

        try
        {
            var parent = await _db.Comments.FirstOrDefaultAsync(c => c.Id == req.ParentCommentId);
            if (parent == null) return NotFound(new { success = false, message = "Bình luận gốc không tồn tại" });

            int? userId = HttpContext.Session.GetInt32("UserId");

            var reply = new Comment
            {
                ArticleId = parent.ArticleId,
                AuthorName = req.Name.Trim(),
                AuthorEmail = req.Email,
                Content = req.Content.Trim(),
                ParentCommentId = req.ParentCommentId,
                ParentId = req.ParentCommentId,
                Status = "pending",
                UserId = userId
            };

            // AI moderation cho reply — không chặn nếu AI lỗi
            try
            {
                var mod = await _ai.ModerateContentAsync(reply.Content);
                if (mod.Score > 70) reply.Status = "rejected";

                _db.AILogs.Add(new AILog
                {
                    ArticleId  = parent.ArticleId,
                    Action     = "moderate_comment",
                    ResultText = $"score={mod.Score}, issues={string.Join("; ", mod.Issues)}",
                    IsSuccess  = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI moderation failed (reply)");
            }

            _db.Comments.Add(reply);
            await _db.SaveChangesAsync();

            if (reply.Status == "rejected")
                return Ok(new { success = false, status = "rejected",
                                message = "Trả lời của bạn vi phạm quy định cộng đồng và đã bị từ chối." });

            return Ok(new
            {
                success = true,
                status = reply.Status,
                message = "Trả lời đã được gửi, chờ duyệt!",
                commentId = reply.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReplyComment failed");
            return StatusCode(500, new { success = false, message = "Lỗi hệ thống" });
        }
    }

    [HttpPost]
    [Route("/article/delete-comment/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
            return Unauthorized(new { success = false, message = "Chưa đăng nhập" });

        var comment = await _db.Comments
            .Include(c => c.Replies)
            .Include(c => c.Votes)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment == null)
            return NotFound(new { success = false, message = "Không tìm thấy bình luận" });

        if (comment.UserId != userId.Value)
            return Forbid();

        foreach (var reply in comment.Replies)
        {
            var replyVotes = await _db.CommentVotes.Where(v => v.CommentId == reply.Id).ToListAsync();
            _db.CommentVotes.RemoveRange(replyVotes);
        }
        _db.Comments.RemoveRange(comment.Replies);
        _db.CommentVotes.RemoveRange(comment.Votes);
        _db.Comments.Remove(comment);
        await _db.SaveChangesAsync();

        return Ok(new { success = true, message = "Đã xoá bình luận" });
    }
}

    [Route("api")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AIService _ai;
        private readonly EmailService _email;

        public ApiController(AppDbContext db, AIService ai, EmailService email)
        {
            _db = db;
            _ai = ai;
            _email = email;
        }

        [HttpPost("summarize")]
        public async Task<IActionResult> Summarize([FromBody] SummarizeRequest req)
        {
            var result = await _ai.SummarizeAsync(req.ArticleId);
            // Cache tóm tắt vào bài để hiện hộp TL;DR ở các lần xem sau
            if (result.Success && !string.IsNullOrWhiteSpace(result.Summary))
            {
                var _art = await _db.Articles.FindAsync(req.ArticleId);
                if (_art != null) { _art.AiSummary = result.Summary; await _db.SaveChangesAsync(); }
            }
            return Ok(result);
        }

        [HttpPost("suggest-title")]
        public async Task<IActionResult> SuggestTitle([FromBody] SummarizeRequest req)
        {
            var result = await _ai.SuggestTitlesAsync(req.ArticleId);
            return Ok(result);
        }

        [HttpPost("comment")]
        public async Task<IActionResult> Comment([FromBody] CommentRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Content))
                return BadRequest(new { success = false, message = "Thiếu thông tin" });

            int? userId = null;
            try { userId = HttpContext.Session.GetInt32("UserId"); } catch { /* ignore */ }

            // Chặn bình luận nếu user đã đăng nhập nhưng CHƯA xác nhận email
            if (userId.HasValue)
            {
                var currentUser = await _db.Users.FindAsync(userId.Value);
                if (currentUser != null && !currentUser.EmailVerified)
                    return BadRequest(new { success = false, message = "Vui lòng xác nhận email trước khi bình luận." });
            }

            var comment = new Comment
            {
                ArticleId = req.ArticleId,
                AuthorName = req.Name.Trim(),
                AuthorEmail = req.Email,
                Content = req.Content.Trim(),
                ParentCommentId = req.ParentCommentId,
                ParentId = req.ParentCommentId,
                UserId = userId,
                Status = "pending"
            };

            // AI moderation cho comment — không chặn nếu AI lỗi
            try
            {
                var mod = await _ai.ModerateContentAsync(comment.Content);
                if (mod.Score > 70) comment.Status = "rejected";

                _db.AILogs.Add(new AILog
                {
                    ArticleId  = req.ArticleId,
                    Action     = "moderate_comment",
                    ResultText = $"score={mod.Score}, issues={string.Join("; ", mod.Issues)}",
                    IsSuccess  = true
                });
            }
            catch { /* AI lỗi thì vẫn cho lưu comment ở trạng thái pending */ }

            _db.Comments.Add(comment);
            await _db.SaveChangesAsync();

            if (comment.Status == "rejected")
                return Ok(new { success = false, status = "rejected",
                                message = "Bình luận của bạn vi phạm quy định cộng đồng và đã bị từ chối." });

            return Ok(new { success = true, status = comment.Status,
                            message = "Bình luận đã được gửi, chờ duyệt!" });
        }

        [HttpPost("newsletter")]
        public async Task<IActionResult> Newsletter([FromBody] NewsletterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains('@'))
                return BadRequest(new { success = false, message = "Email không hợp lệ" });

            var email = req.Email.Trim();
            var exists = await _db.NewsletterSubscribers.AnyAsync(n => n.Email == email);
            if (exists) return Ok(new { success = false, message = "Email đã đăng ký rồi!" });

            // [TRANSACTIONAL] Lưu subscriber trước; nếu SMTP lỗi thì rollback để dữ liệu nhất quán
            var subscriber = new NewsletterSubscriber { Email = email };
            _db.NewsletterSubscribers.Add(subscriber);
            await _db.SaveChangesAsync();

            bool emailOk = false;
            string? emailErr = null;
            try
            {
                var (ok, err) = await _email.SendWelcomeAsync(email);
                emailOk = ok;
                emailErr = err;
            }
            catch (Exception ex) { emailErr = ex.Message; }

            if (!emailOk)
            {
                // Rollback: xóa subscriber vừa thêm để user có thể thử lại
                _db.NewsletterSubscribers.Remove(subscriber);
                await _db.SaveChangesAsync();
                return Ok(new {
                    success = false,
                    message = "Không gửi được email xác nhận. Vui lòng thử lại sau."
                });
            }

            return Ok(new {
                success = true,
                message = "Đăng ký thành công! Vui lòng kiểm tra email chào mừng."
            });
        }

        [HttpPost("newsletter/subscribe-me")]
        public async Task<IActionResult> SubscribeMe()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return Unauthorized(new { success = false, message = "Vui lòng đăng nhập trước." });

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return BadRequest(new { success = false, message = "Tài khoản chưa có email hợp lệ." });

            var email = user.Email.Trim();
            var exists = await _db.NewsletterSubscribers.AnyAsync(n => n.Email == email);
            if (exists)
                return Ok(new { success = false, message = $"Email {email} đã đăng ký nhận tin rồi!" });

            // [TRANSACTIONAL] Lưu subscriber trước; nếu SMTP lỗi thì rollback
            var subscriber = new NewsletterSubscriber { Email = email };
            _db.NewsletterSubscribers.Add(subscriber);
            await _db.SaveChangesAsync();

            bool emailOk = false;
            try
            {
                var (ok, _) = await _email.SendWelcomeAsync(email);
                emailOk = ok;
            }
            catch { /* coi như fail, sẽ rollback */ }

            if (!emailOk)
            {
                _db.NewsletterSubscribers.Remove(subscriber);
                await _db.SaveChangesAsync();
                return Ok(new {
                    success = false,
                    message = $"Không gửi được email xác nhận tới {email}. Vui lòng thử lại sau."
                });
            }

            return Ok(new {
                success = true,
                message = $"Đăng ký thành công! Email chào mừng đã gửi tới {email}."
            });
        }
    }

    public class SummarizeRequest
    { public int ArticleId { get; set; } }
    public class ChatRequest
    { public string? Message { get; set; } }


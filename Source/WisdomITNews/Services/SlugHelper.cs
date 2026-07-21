namespace WisdomITNews.Services;

public static class SlugHelper
{
    // Đây là luồng xử lý tạo slug tiếng Việt không dấu (cho URL bài/danh mục/tag)
    // Luồng: 1) Đưa về chữ thường, thay ký tự có dấu -> không dấu (đ->d)
    //        2) Bỏ ký tự lạ, gộp khoảng trắng/gạch thành 1 dấu "-", cắt "-" ở đầu/cuối
    public static string MakeSlug(string text)
    {
        var from = "àáảãạăắặằẳẵâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđ";
        var to   = "aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd";

        var result = text.ToLower();
        for (int i = 0; i < from.Length; i++)
            result = result.Replace(from[i].ToString(), to[i].ToString());

        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9\s-]", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[\s-]+", "-");
        return result.Trim('-');
    }

    private static readonly Dictionary<string, string> _regionNames = new()
    {
        ["dong-nai"] = "Đồng Nai", ["ha-noi"] = "Hà Nội", ["ho-chi-minh"] = "TP. Hồ Chí Minh",
        ["da-nang"] = "Đà Nẵng", ["hai-phong"] = "Hải Phòng", ["can-tho"] = "Cần Thơ"
    };

    // Đây là luồng xử lý đổi slug vùng miền sang tên hiển thị (null/rỗng -> "Toàn quốc")
    // Slug vùng -> tên hiển thị. Null/rỗng -> "Toàn quốc".
    public static string RegionName(string? slug) =>
        string.IsNullOrWhiteSpace(slug) ? "Toàn quốc"
        : (_regionNames.TryGetValue(slug, out var n) ? n : slug);

    // Đây là luồng xử lý định dạng số lượt xem gọn (1.2K, 3.4M)
    public static string FormatViews(int views)
    {
        if (views >= 1_000_000) return $"{views / 1_000_000.0:0.#}M";
        if (views >= 1_000)     return $"{views / 1_000.0:0.#}K";
        return views.ToString("N0");
    }

    // Đây là luồng xử lý định dạng thời gian tương đối ("Vừa xong", "3 giờ trước", "Hôm qua"...)
    public static string FormatDate(DateTime? dt, bool relative = true)
    {
        if (dt == null) return "";
        var diff = DateTime.Now - dt.Value;
        if (!relative) return dt.Value.ToString("dd/MM/yyyy HH:mm");
        if (diff.TotalMinutes < 1)  return "Vừa xong";
        if (diff.TotalHours < 1)    return $"{(int)diff.TotalMinutes} phút trước";
        if (diff.TotalDays < 1)     return $"{(int)diff.TotalHours} giờ trước";
        if (diff.TotalDays < 2)     return "Hôm qua";
        if (diff.TotalDays < 7)     return $"{(int)diff.TotalDays} ngày trước";
        return dt.Value.ToString("dd/MM/yyyy");
    }
}

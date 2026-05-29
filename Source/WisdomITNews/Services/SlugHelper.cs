namespace WisdomITNews.Services;

public static class SlugHelper
{
    public static string MakeSlug(string text)
    {
        var from = "àáảãạăắặằẳẵâấầẩẫậèéẻẽẹêếềểễệìíỉĩịòóỏõọôốồổỗộơớờởỡợùúủũụưứừửữựỳýỷỹỵđ";
        var to   = "aaaaaaaaaaaaaaaaaeeeeeeeeeeeiiiiiooooooooooooooooouuuuuuuuuuuyyyyyd";
        var fromU= "ÀÁẢÃẠĂẮẶẰẲẴÂẤẦẨẪẬÈÉẺẼẸÊẾỀỂỄỆÌÍỈĨỊÒÓỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢÙÚỦŨỤƯỨỪỬỮỰỲÝỶỸỴĐ";

        var result = text.ToLower();
        for (int i = 0; i < from.Length; i++)
            result = result.Replace(from[i].ToString(), to[i].ToString());

        result = System.Text.RegularExpressions.Regex.Replace(result, @"[^a-z0-9\s-]", "");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"[\s-]+", "-");
        return result.Trim('-');
    }

    public static string FormatViews(int views)
    {
        if (views >= 1_000_000) return $"{views / 1_000_000.0:0.#}M";
        if (views >= 1_000)     return $"{views / 1_000.0:0.#}K";
        return views.ToString("N0");
    }

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

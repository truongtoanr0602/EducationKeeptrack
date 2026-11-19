namespace EducationKeeptrack.Learning.Application;

/// <summary>Teacher-facing wording for why an AI turn was refused, shared by the AI features of this module.</summary>
public static class AiQuotaMessages
{
    public static string For(string? reason) => reason switch
    {
        "ai_disabled" => "Tài khoản chưa được kích hoạt quyền AI. Vui lòng liên hệ Admin.",
        "no_quota" or "exhausted" => "Hạn mức lượt AI của kỳ này đã hết hoặc chưa được cấp.",
        "expired" => "Thời hạn dịch vụ của tài khoản đã hết.",
        _ => "Không thể sử dụng AI lúc này (" + reason + ")."
    };
}

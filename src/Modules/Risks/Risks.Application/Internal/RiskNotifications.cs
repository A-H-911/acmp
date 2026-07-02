using Acmp.Shared.Contracts.Notifications;
using Acmp.Shared.Domain.ValueObjects;

namespace Acmp.Modules.Risks.Application.Internal;

// Builds the bilingual in-app notifications the Risks module raises (guardrail 9). Raise (W15) targets the
// owner; escalation (BL-135) fans out to the Secretary + Chairman. The deep link targets the routed risk
// view (/risks/{key}) so the SPA navigates straight to it (AC-052/053).
internal static class RiskNotifications
{
    public const string CategoryRiskAssigned = "RiskAssigned";
    public const string CategoryRiskEscalated = "RiskEscalated";

    private static string RiskLink(string riskKey) => $"/risks/{riskKey}";

    // W15: the owner is told a risk they own has been raised (skip if they raised it themselves).
    public static NotificationMessage Assigned(string recipientUserId, string riskKey) => new(
        recipientUserId,
        LocalizedString.Create("Risk assigned to you", "أُسند إليك خطر"),
        LocalizedString.Create(
            $"Risk {riskKey} has been raised and assigned to you. Open it to plan mitigation.",
            $"تم تسجيل الخطر {riskKey} وإسناده إليك. افتحه لتخطيط المعالجة."),
        CategoryRiskAssigned, RiskLink(riskKey));

    // BL-135: a risk was escalated — the Secretary/Chairman are notified with the escalation target.
    public static NotificationMessage Escalated(string recipientUserId, string riskKey, string target) => new(
        recipientUserId,
        LocalizedString.Create("Risk escalated", "تم تصعيد خطر"),
        LocalizedString.Create(
            $"ESCALATION: risk {riskKey} has been escalated to {target} and needs attention.",
            $"تصعيد: تم تصعيد الخطر {riskKey} إلى {target} ويحتاج إلى متابعة."),
        CategoryRiskEscalated, RiskLink(riskKey));
}

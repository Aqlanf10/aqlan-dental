using System.Text;
using AqlanDentalPro.Application.Common;
using AqlanDentalPro.Domain.Entities;

namespace AqlanDentalPro.Infrastructure.Services;

/// <summary>
/// LABINV-REQ-009 — composes the message the clinic sends a lab when a case goes out.
///
/// <para>
/// The message is built on the server, not in the browser, for one reason: clinic identity
/// must come from <c>Settings</c>. Composing it client-side would mean either shipping the
/// clinic's name and phones to a screen that cannot read the settings that hold them, or —
/// far more likely — writing them into the component as literals, which is exactly the
/// hardcoding this system forbids. The clinic that renames itself would then keep sending
/// its old name to every lab.
/// </para>
///
/// <para>
/// Nothing here is transmitted by the server. It returns text; the browser opens WhatsApp
/// with it. That keeps the clinic's WhatsApp account out of the backend entirely.
/// </para>
/// </summary>
public static class LabOrderDispatchMessage
{
    /// <param name="includePatientName">
    /// From <see cref="FinanceSettingsKeys.LabWhatsAppIncludePatientName"/>. When false the
    /// lab matches the case from the printed slip instead.
    /// </param>
    public static string Compose(
        LabOrder order,
        IReadOnlyList<LabOrderItem> items,
        FinanceClinicIdentity clinic,
        bool includePatientName)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"*{clinic.Name}*");
        if (!string.IsNullOrWhiteSpace(clinic.Phones)) sb.AppendLine(clinic.Phones);
        sb.AppendLine("──────────────");
        sb.AppendLine("*أمر عمل معمل*");

        if (!string.IsNullOrWhiteSpace(order.OrderNumber))
            sb.AppendLine($"رقم الطلب: {order.OrderNumber}");

        // The file number goes out whether or not the name does: the lab needs *some*
        // stable handle to reply about, and a file number is meaningless to anyone who
        // cannot already read the clinic's records.
        if (!string.IsNullOrWhiteSpace(order.Patient?.PatientNumber))
            sb.AppendLine($"رقم الملف: {order.Patient!.PatientNumber}");

        if (includePatientName && order.Patient is not null)
        {
            var name = $"{order.Patient.FirstName} {order.Patient.LastName}".Trim();
            if (name.Length > 0) sb.AppendLine($"المريض: {name}");
        }

        if (!string.IsNullOrWhiteSpace(order.Doctor?.Name))
            sb.AppendLine($"الطبيب: {order.Doctor!.Name}");

        if (!string.IsNullOrWhiteSpace(order.ApplianceType))
            sb.AppendLine($"نوع العمل: {order.ApplianceType}");

        if (items.Count > 0)
        {
            sb.AppendLine("──────────────");
            sb.AppendLine("*البنود:*");
            var index = 1;
            foreach (var item in items.OrderBy(i => i.SortOrder))
            {
                var parts = new List<string>();
                // Arabic name first — this message goes to a Yemeni lab technician, and
                // `NameAr` exists precisely because the English `Name` is the catalogue key.
                var workType = !string.IsNullOrWhiteSpace(item.WorkType?.NameAr)
                    ? item.WorkType!.NameAr
                    : item.WorkType?.Name;
                if (!string.IsNullOrWhiteSpace(workType)) parts.Add(workType!);
                if (!string.IsNullOrWhiteSpace(item.ToothNumber)) parts.Add($"سن {item.ToothNumber}");
                if (!string.IsNullOrWhiteSpace(item.Shade)) parts.Add($"لون {item.Shade}");
                if (item.UnitsCount > 1) parts.Add($"عدد {item.UnitsCount}");

                // A line with nothing on it helps nobody; skip rather than print "‎.‎ —".
                if (parts.Count == 0) continue;

                sb.AppendLine($"{index}. {string.Join(" • ", parts)}");
                if (!string.IsNullOrWhiteSpace(item.Instructions))
                    sb.AppendLine($"   ({item.Instructions})");
                index++;
            }
        }

        sb.AppendLine("──────────────");
        if (order.SentDate.HasValue)
            sb.AppendLine($"تاريخ الإرسال: {order.SentDate:yyyy-MM-dd}");
        if (order.ExpectedDate.HasValue)
            sb.AppendLine($"موعد التسليم المتوقع: {order.ExpectedDate:yyyy-MM-dd}");

        if (string.Equals(order.Priority, "urgent", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine("⚠️ *الحالة مستعجلة*");

        if (!string.IsNullOrWhiteSpace(order.Instructions))
        {
            sb.AppendLine("──────────────");
            sb.AppendLine($"ملاحظات: {order.Instructions}");
        }

        // Cost is deliberately absent. What the clinic pays the lab is agreed in the price
        // list, and repeating it on every case turns a work order into a running quotation
        // that a technician can screenshot. The lab bills against the order number.

        return sb.ToString().TrimEnd();
    }
}

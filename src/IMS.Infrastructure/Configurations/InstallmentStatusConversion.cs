using IMS.Domain.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace IMS.Infrastructure.Configurations;

internal static class InstallmentStatusConversion
{
    public static readonly ValueConverter<InstallmentStatus, string> Converter = new(
        value => ToDatabase(value),
        value => FromDatabase(value));

    private static string ToDatabase(InstallmentStatus value) => value switch
    {
        InstallmentStatus.Pending => "Pending",
        InstallmentStatus.Paid => "Paid",
        InstallmentStatus.PartiallyPaid => "Partially Paid",
        InstallmentStatus.Overdue => "Overdue",
        InstallmentStatus.Waived => "Waived",
        _ => throw new InvalidOperationException("Unsupported installment status.")
    };

    private static InstallmentStatus FromDatabase(string value) => value switch
    {
        "Pending" => InstallmentStatus.Pending,
        "Paid" => InstallmentStatus.Paid,
        "Partially Paid" => InstallmentStatus.PartiallyPaid,
        "Overdue" => InstallmentStatus.Overdue,
        "Waived" => InstallmentStatus.Waived,
        _ => throw new InvalidOperationException("Unsupported database installment status.")
    };
}

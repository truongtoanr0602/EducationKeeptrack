namespace EducationKeeptrack.Billing.Domain;

public sealed class PaymentRecipient
{
    public string OwnerId { get; set; } = "";
    public string BankBin { get; set; } = "";
    public string BankName { get; set; } = "";
    public string AccountNo { get; set; } = "";
    public string AccountName { get; set; } = "";
    public byte[]? StaticQr { get; set; }
    public long Version { get; set; }
}

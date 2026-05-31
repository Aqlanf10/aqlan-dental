using AqlanDentalPro.Domain.Enums;

namespace AqlanDentalPro.Domain.Entities;

public class ClinicRoom : BaseEntity
{
    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public RoomType RoomType { get; set; } = RoomType.Treatment;
    public int SortOrder { get; set; } = 0;
}

namespace AqlanDentalPro.Domain.Enums;

/// <summary>
/// Classification of a supplier/vendor type.
/// Used for reporting and filtering supplier payables.
/// </summary>
public enum SupplierType
{
    DentalLab,      // معمل أسنان — External dental lab
    MedicalVendor,  // مورد مواد طبية — Medical materials/equipment vendor
    GeneralService  // خدمات عامة — General service provider
}

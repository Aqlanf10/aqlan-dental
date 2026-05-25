namespace AqlanDentalPro.Domain.Entities;

public class RecordsChecklist : BaseEntity
{
    public Guid OrthoCaseId { get; set; }

    // Extraoral photos
    public bool ExtraoralFrontal { get; set; }
    public bool ExtraoralProfile { get; set; }
    public bool ExtraoralSmile { get; set; }

    // Intraoral photos
    public bool IntraoralFrontal { get; set; }
    public bool IntraoralRight { get; set; }
    public bool IntraoralLeft { get; set; }
    public bool UpperOcclusal { get; set; }
    public bool LowerOcclusal { get; set; }

    // Radiographs
    public bool Opg { get; set; }
    public bool LateralCeph { get; set; }
    public bool Cbct { get; set; }

    // Other records
    public bool StudyModels { get; set; }
    public bool Consent { get; set; }
    public bool Contract { get; set; }

    public OrthoCase OrthoCase { get; set; } = null!;
}

from pathlib import Path

checkout_path = Path("backend/src/AqlanDentalPro.Infrastructure/Services/CheckoutService.cs")
checkout = checkout_path.read_text(encoding="utf-8")
marker = """{
    // IAuditService is injected per CLIN-22 spec for future audit-log migration."""
replacement = """{
    /// <summary>
    /// Backward-compatible constructor for existing unit/integration fixtures.
    /// Production DI selects the primary constructor above and injects the
    /// canonical clock and policy explicitly.
    /// </summary>
    public CheckoutService(
        AppDbContext db,
        ICommissionService commissionService,
        ICurrentUserService currentUser,
        IPatientAccessService patientAccessService,
        IRealTimePushService pushService,
        ILogger<CheckoutService> logger)
        : this(
            db,
            commissionService,
            currentUser,
            new ClinicClock(),
            new JourneyBusinessDatePolicy(new ClinicClock()),
            patientAccessService,
            pushService,
            logger)
    {
    }

    // IAuditService is injected per CLIN-22 spec for future audit-log migration."""
if checkout.count(marker) != 1:
    raise SystemExit("CheckoutService compatibility insertion point mismatch")
checkout_path.write_text(checkout.replace(marker, replacement, 1), encoding="utf-8")

queue_test_path = Path(
    "backend/tests/AqlanDentalPro.UnitTests/ClinicQueue/ClinicQueueDisplayTests.cs"
)
queue_test = queue_test_path.read_text(encoding="utf-8")
old = """        var auditService = new Mock<IAuditService>().Object;
        var logger = new Mock<ILogger<ClinicQueueController>>().Object;

        return new ClinicQueueController(db, pushService, smsService, whatsAppService, auditService, logger);"""
new = """        var auditService = new Mock<IAuditService>().Object;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(x => x.IsAdmin).Returns(true);
        var clinicClock = new ClinicClock();
        var businessDatePolicy = new JourneyBusinessDatePolicy(clinicClock);
        var logger = new Mock<ILogger<ClinicQueueController>>().Object;

        return new ClinicQueueController(
            db, pushService, smsService, whatsAppService, auditService,
            currentUser.Object, clinicClock, businessDatePolicy, logger);"""
if queue_test.count(old) != 1:
    raise SystemExit("ClinicQueueDisplayTests constructor pattern mismatch")
queue_test_path.write_text(queue_test.replace(old, new, 1), encoding="utf-8")

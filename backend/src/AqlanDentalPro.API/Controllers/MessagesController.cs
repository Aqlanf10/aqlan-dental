using AqlanDentalPro.Application.DTOs.Messaging;
using AqlanDentalPro.Infrastructure.Data;
using AqlanDentalPro.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize(Policy = "StaffOnly")]
public class MessagesController(MessagingService messagingService, AppDbContext db) : ControllerBase
{
    /// <summary>تطبيق الـ migrations يدوياً (Admin فقط)</summary>
    [HttpPost("ensure-schema")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> EnsureSchema()
    {
        try
        {
            // Always ensure missing columns exist, even if tables already exist
            // Add DeletedAt/DeletedBy to messaging tables
            var messagingTables = new[] { "Conversations", "ConversationParticipants", "Messages", "MessageReads" };
            foreach (var table in messagingTables)
            {
                await db.Database.ExecuteSqlRawAsync($"""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedAt') THEN
                            ALTER TABLE "{table}" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = '{table}' AND column_name = 'DeletedBy') THEN
                            ALTER TABLE "{table}" ADD COLUMN "DeletedBy" uuid NULL;
                        END IF;
                    END $$;
                """);
            }

            // Check if messaging tables already exist using raw SQL query
            bool tablesExist = false;
            try
            {
                using var cmd = db.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'Conversations')";
                await db.Database.OpenConnectionAsync();
                tablesExist = (bool?)await cmd.ExecuteScalarAsync() ?? false;
                await db.Database.CloseConnectionAsync();
            }
            catch { tablesExist = false; }
            finally { await db.Database.CloseConnectionAsync(); }

            if (tablesExist)
            {
                // Tables exist but may be missing columns — add ConversationType/PatientId/BranchId if missing
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                            ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                        END IF;
                    END $$;
                """);

                await db.Database.ExecuteSqlRawAsync("""
                    CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId");
                """);
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType");
                """);

                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
                            ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Patients_PatientId" 
                                FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Branches_BranchId') THEN
                            ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Branches_BranchId" 
                                FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE SET NULL;
                        END IF;
                    END $$;
                """);

                // Also add editing columns if missing
                await db.Database.ExecuteSqlRawAsync("""
                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                            ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                            ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
                        END IF;
                    END $$;
                """);

                return Ok(new { message = "تم تحديث أعمدة المراسلة المفقودة بنجاح" });
            }

            // Create messaging tables directly via SQL
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Conversations" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "Title" character varying(200) NOT NULL,
                    "IsGroup" boolean NOT NULL,
                    "CreatedBy" uuid NULL,
                    "LastMessageAt" timestamp with time zone NULL,
                    "LastMessagePreview" character varying(500) NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                )
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Conversations_LastMessageAt" ON "Conversations" ("LastMessageAt")
            """);

            // Add Patient conversation support columns
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                    END IF;
                END $$
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Users_CreatedBy') THEN
                        ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Users_CreatedBy" 
                            FOREIGN KEY ("CreatedBy") REFERENCES "Users"("Id") ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
                        ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Patients_PatientId" 
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Branches_BranchId') THEN
                        ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Branches_BranchId" 
                            FOREIGN KEY ("BranchId") REFERENCES "Branches"("Id") ON DELETE SET NULL;
                    END IF;
                END $$
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "ConversationParticipants" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "ConversationId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "IsAdmin" boolean NOT NULL,
                    "LastReadAt" timestamp with time zone NULL,
                    "IsMuted" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                )
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_ConversationParticipants_ConversationId_UserId" 
                    ON "ConversationParticipants" ("ConversationId", "UserId")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Conversations_ConversationId') THEN
                        ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Conversations_ConversationId" 
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_ConversationParticipants_Users_UserId') THEN
                        ALTER TABLE "ConversationParticipants" ADD CONSTRAINT "FK_ConversationParticipants_Users_UserId" 
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "Messages" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "ConversationId" uuid NOT NULL,
                    "SenderId" uuid NOT NULL,
                    "Content" text NOT NULL,
                    "AttachmentUrl" character varying(1000) NULL,
                    "AttachmentName" character varying(255) NULL,
                    "AttachmentType" character varying(50) NULL,
                    "ReplyToId" uuid NULL,
                    "IsSystemMessage" boolean NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                )
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Messages_ConversationId" ON "Messages" ("ConversationId")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Messages_CreatedAt" ON "Messages" ("CreatedAt")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Conversations_ConversationId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Conversations_ConversationId" 
                            FOREIGN KEY ("ConversationId") REFERENCES "Conversations"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Users_SenderId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Users_SenderId" 
                            FOREIGN KEY ("SenderId") REFERENCES "Users"("Id") ON DELETE RESTRICT;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Messages_Messages_ReplyToId') THEN
                        ALTER TABLE "Messages" ADD CONSTRAINT "FK_Messages_Messages_ReplyToId" 
                            FOREIGN KEY ("ReplyToId") REFERENCES "Messages"("Id") ON DELETE SET NULL;
                    END IF;
                END $$
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS "MessageReads" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "MessageId" uuid NOT NULL,
                    "UserId" uuid NOT NULL,
                    "ReadAt" timestamp with time zone NOT NULL,
                    "CreatedAt" timestamp with time zone NOT NULL,
                    "UpdatedAt" timestamp with time zone NOT NULL,
                    "IsActive" boolean NOT NULL
                )
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_MessageReads_MessageId_UserId" 
                    ON "MessageReads" ("MessageId", "UserId")
            """);

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Messages_MessageId') THEN
                        ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Messages_MessageId" 
                            FOREIGN KEY ("MessageId") REFERENCES "Messages"("Id") ON DELETE CASCADE;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_MessageReads_Users_UserId') THEN
                        ALTER TABLE "MessageReads" ADD CONSTRAINT "FK_MessageReads_Users_UserId" 
                            FOREIGN KEY ("UserId") REFERENCES "Users"("Id") ON DELETE CASCADE;
                    END IF;
                END $$
            """);

            // Record the migration in history
            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260430000000_AddMessagingSystem', '8.0.8'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260430000000_AddMessagingSystem'
                )
            """);

            // Add Phase 1-4 columns: DeletedAt/DeletedBy, ConversationType/PatientId/BranchId
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedAt') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'DeletedBy') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "DeletedBy" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'ConversationType') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "ConversationType" character varying(20) NOT NULL DEFAULT 'StaffToStaff';
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'PatientId') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "PatientId" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Conversations' AND column_name = 'BranchId') THEN
                        ALTER TABLE "Conversations" ADD COLUMN "BranchId" uuid NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedAt') THEN
                        ALTER TABLE "Messages" ADD COLUMN "DeletedAt" timestamp with time zone NULL;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'DeletedBy') THEN
                        ALTER TABLE "Messages" ADD COLUMN "DeletedBy" uuid NULL;
                    END IF;
                END $$;
            """);

            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Conversations_PatientId" ON "Conversations" ("PatientId");
            """);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE INDEX IF NOT EXISTS "IX_Conversations_ConversationType" ON "Conversations" ("ConversationType");
            """);

            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Conversations_Patients_PatientId') THEN
                        ALTER TABLE "Conversations" ADD CONSTRAINT "FK_Conversations_Patients_PatientId" 
                            FOREIGN KEY ("PatientId") REFERENCES "Patients"("Id") ON DELETE SET NULL;
                    END IF;
                END $$;
            """);

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260501010000_AddPatientConversationSupport', '8.0.8'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260501010000_AddPatientConversationSupport'
                )
            """);

            // Add IsEdited/EditedAt columns for message editing feature
            await db.Database.ExecuteSqlRawAsync("""
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'IsEdited') THEN
                        ALTER TABLE "Messages" ADD COLUMN "IsEdited" boolean NOT NULL DEFAULT false;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'Messages' AND column_name = 'EditedAt') THEN
                        ALTER TABLE "Messages" ADD COLUMN "EditedAt" timestamp with time zone NULL;
                    END IF;
                END $$;
            """);

            await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
                SELECT '20260510000000_AddMessageEditing', '8.0.8'
                WHERE NOT EXISTS (
                    SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260510000000_AddMessageEditing'
                )
            """);

            return Ok(new { message = "تم إنشاء جداول المراسلة بنجاح" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "فشل إنشاء جداول المراسلة",
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }

    /// <summary>فحص حالة جداول المراسلة (Admin فقط)</summary>
    [HttpGet("schema-status")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<ActionResult> SchemaStatus()
    {
        try
        {
            var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
            var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
            var canConnect = await db.Database.CanConnectAsync();

            return Ok(new
            {
                canConnect,
                pendingMigrations = pendingMigrations.ToList(),
                appliedMigrations = appliedMigrations.ToList(),
                conversationsExists = db.Conversations.Any(),
                messageReadsExists = db.MessageReads.Any()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
        }
    }
    /// <summary>جلب محادثاتي</summary>
    [HttpGet("conversations")]
    public async Task<ActionResult<object>> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null)
    {
        var result = await messagingService.GetMyConversationsAsync(page, pageSize, search, type);
        return Ok(new { result.Data, result.TotalCount, result.Page, result.PageSize, result.TotalPages, result.HasNextPage, result.HasPreviousPage });
    }

    /// <summary>جلب تفاصيل محادثة مع الرسائل</summary>
    [HttpGet("conversations/{conversationId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetConversation(
        Guid conversationId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var result = await messagingService.GetConversationAsync(conversationId, page, pageSize);
        if (result == null) return NotFound(new { message = "المحادثة غير موجودة أو ليس لديك صلاحية الوصول" });
        return Ok(result);
    }

    /// <summary>إنشاء محادثة جديدة</summary>
    [HttpPost("conversations")]
    public async Task<ActionResult<ConversationDetailDto>> CreateConversation([FromBody] CreateConversationRequest request)
    {
        var result = await messagingService.CreateConversationAsync(request);
        return CreatedAtAction(nameof(GetConversation), new { conversationId = result.Id }, result);
    }

    /// <summary>إرسال رسالة في محادثة</summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    public async Task<ActionResult<MessageDto>> SendMessage(Guid conversationId, [FromBody] SendMessageRequest request)
    {
        try
        {
            var result = await messagingService.SendMessageAsync(conversationId, request);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>تحديد الرسائل كمقروءة</summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid conversationId)
    {
        await messagingService.MarkAsReadAsync(conversationId);
        return NoContent();
    }

    /// <summary>عدد الرسائل غير المقروءة</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount()
    {
        var result = await messagingService.GetUnreadCountAsync();
        return Ok(result);
    }

    /// <summary>مغادرة محادثة</summary>
    [HttpPost("conversations/{conversationId:guid}/leave")]
    public async Task<IActionResult> LeaveConversation(Guid conversationId)
    {
        await messagingService.LeaveConversationAsync(conversationId);
        return NoContent();
    }

    /// <summary>
    /// إنشاء/جلب محادثة PatientFacing مع مريض — مرئية للمريض في بوابته
    /// يُستخدم من زر "راسل المريض" وصفحة الرسائل
    /// </summary>
    [HttpPost("conversations/patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreatePatientFacingConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetOrCreatePatientFacingConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// إنشاء/جلب محادثة داخلية حول مريض (StaffToPatient) — يراها الطاقم فقط
    /// يُستخدم من تبويب الرسائل الداخلية في ملف المريض
    /// </summary>
    [HttpPost("internal-patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetOrCreateInternalPatientConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetOrCreatePatientConversationAsync(patientId);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>تعديل رسالة (المرسل فقط، خلال 15 دقيقة)</summary>
    [HttpPut("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<ActionResult<MessageDto>> EditMessage(
        Guid conversationId, Guid messageId, [FromBody] EditMessageRequest request)
    {
        var (success, error, message) = await messagingService.EditMessageAsync(conversationId, messageId, request);
        if (!success)
        {
            if (message is null && error is null) return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية الوصول." });
            return BadRequest(new { message = error });
        }
        return Ok(message);
    }

    /// <summary>حذف رسالة (المرسل فقط)</summary>
    [HttpDelete("conversations/{conversationId:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> DeleteMessage(Guid conversationId, Guid messageId)
    {
        var result = await messagingService.DeleteMessageAsync(conversationId, messageId);
        if (!result) return StatusCode(StatusCodes.Status403Forbidden, new { message = "ليس لديك صلاحية الوصول." });
        return NoContent();
    }

    /// <summary>إحصائيات المراسلة</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<MessagingStatsDto>> GetStats()
    {
        var result = await messagingService.GetStatsAsync();
        return Ok(result);
    }

    /// <summary>جلب محادثة مريض الداخلية الموجودة (GET) — لا تنشئ واحدة جديدة</summary>
    [HttpGet("patient/{patientId:guid}")]
    public async Task<ActionResult<ConversationDetailDto>> GetInternalPatientConversation(Guid patientId)
    {
        try
        {
            var result = await messagingService.GetPatientConversationAsync(patientId);
            if (result == null)
                return NotFound(new { message = "لا توجد محادثة داخلية مرتبطة بهذا المريض" });
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>جلب رسائل جديدة منذ آخر رسالة (للـ polling)</summary>
    [HttpGet("conversations/{conversationId:guid}/poll")]
    public async Task<IActionResult> PollMessages(Guid conversationId, [FromQuery] string? since = null)
    {
        var result = await messagingService.GetConversationAsync(conversationId, 1, 50);
        if (result == null) return NotFound(new { message = "المحادثة غير موجودة" });

        if (since != null && DateTime.TryParse(since, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sinceDate))
        {
            result.Messages = result.Messages
                .Where(m => m.CreatedAt > sinceDate)
                .ToList();
        }

        return Ok(result);
    }
}

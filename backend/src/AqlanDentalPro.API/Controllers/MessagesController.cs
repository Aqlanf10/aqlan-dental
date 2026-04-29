using AqlanDentalPro.Application.DTOs.Messages;
using AqlanDentalPro.Application.Services;
using AqlanDentalPro.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AqlanDentalPro.API.Controllers;

[ApiController]
[Route("api/messages")]
[Authorize]
public class MessagesController(MessageService messageService, AppDbContext db) : ControllerBase
{
    // GET /api/messages/conversations
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var conversations = await messageService.GetConversationsAsync();
        return Ok(conversations);
    }

    // POST /api/messages/conversations
    [HttpPost("conversations")]
    public async Task<IActionResult> CreateConversation([FromBody] CreateConversationRequest req)
    {
        var (result, error) = await messageService.CreateConversationAsync(req);
        if (error != null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // GET /api/messages/conversations/{id}/messages
    [HttpGet("conversations/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var (result, error) = await messageService.GetMessagesAsync(id, page, pageSize);
        if (error != null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // POST /api/messages/conversations/{id}/messages
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendMessageRequest req)
    {
        var (result, error) = await messageService.SendMessageAsync(id, req);
        if (error != null) return BadRequest(new { message = error });
        return Ok(result);
    }

    // PUT /api/messages/conversations/{id}/read
    [HttpPut("conversations/{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var (success, error) = await messageService.MarkAsReadAsync(id);
        if (error != null) return BadRequest(new { message = error });
        return Ok(new { message = "تم تحديد المحادثة كمقروءة" });
    }

    // GET /api/messages/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await messageService.GetTotalUnreadCountAsync();
        return Ok(new { count });
    }

    // GET /api/messages/doctors
    [HttpGet("doctors")]
    public async Task<IActionResult> GetDoctorsForMessaging()
    {
        var doctors = await messageService.GetDoctorsForMessagingAsync();
        return Ok(doctors);
    }
}

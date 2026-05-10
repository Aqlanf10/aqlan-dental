namespace AqlanDentalPro.Domain.Constants;

/// <summary>
/// Default clinic room names used by the queue system.
/// Future sprint: move rooms to Settings or a ClinicRooms table
/// without changing the queue workflow — just replace this constant
/// with a database-driven list.
/// </summary>
public static class ClinicRoomNames
{
    public static readonly string[] DefaultRooms = ["غرفة 1", "غرفة 2", "غرفة 3"];

    /// <summary>Returns true if the room name is one of the allowed defaults.</summary>
    public static bool IsValid(string? roomName) =>
        !string.IsNullOrWhiteSpace(roomName) && DefaultRooms.Contains(roomName);
}

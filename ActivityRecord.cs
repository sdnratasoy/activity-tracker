/// <summary>
/// Local SQLite'tan okunup SQL Server'a gönderilecek kayıt modeli.
/// </summary>
public record ActivityRecord(
    long     Id,
    string   LogTime,
    string   Username,
    string   ComputerName,
    string   SessionType,
    string   AppName,
    string   WindowTitle,
    int      DurationSec,
    int      ActiveSec,
    int      IdleSec,
    int      KeyCount,
    int      MouseCount
);

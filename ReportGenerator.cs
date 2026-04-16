using Microsoft.Data.Sqlite;
using System.Text;
public static class ReportGenerator
{
    private static readonly string ReportsDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "Activity_Tracker", "Reports");

    // AppFilter'daki tüm görüntü adları — raporda "takip edilenler" bölümüne girer.
    private static readonly HashSet<string> TrackedDisplayNames =
        new(
            AppFilter.ByProcess.Values.Concat(AppFilter.ByTitle.Values),
            StringComparer.OrdinalIgnoreCase);


    public static void GenerateDailyReport(DateTime date)
    {
        Directory.CreateDirectory(ReportsDir);

        var rows = QueryUsage(date.Date, date.Date);
        var filePath = Path.Combine(ReportsDir, $"gunluk_{date:yyyy-MM-dd}.txt");

        WriteReport(
            filePath,
            title: $"GÜNLÜK KULLANIM RAPORU  —  {date:dd.MM.yyyy dddd}",
            rows);
    }


    public static void GenerateWeeklyReport(DateTime anyDayInWeek)
    {
        Directory.CreateDirectory(ReportsDir);

        int diff = ((int)anyDayInWeek.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var weekStart = anyDayInWeek.Date.AddDays(-diff);
        var weekEnd   = weekStart.AddDays(6);

        var rows = QueryUsage(weekStart, weekEnd);
        var filePath = Path.Combine(
            ReportsDir,
            $"haftalik_{weekStart:yyyy-MM-dd}_{weekEnd:yyyy-MM-dd}.txt");

        WriteReport(
            filePath,
            title: $"HAFTALIK KULLANIM RAPORU  —  {weekStart:dd.MM.yyyy} – {weekEnd:dd.MM.yyyy}",
            rows);
    }


    private static List<(string Username, string AppName, long TotalSec, long TotalKeys, long TotalMouse)> QueryUsage(
        DateTime from, DateTime to)
    {
        var result = new List<(string, string, long, long, long)>();

        using var conn = new SqliteConnection(DatabaseHelper.ConnectionString);
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT Username, AppName,
                   SUM(DurationSec) AS TotalSec,
                   SUM(KeyCount)    AS TotalKeys,
                   SUM(MouseCount)  AS TotalMouse
            FROM   ActivityLog
            WHERE  date(LogTime) BETWEEN $from AND $to
            GROUP  BY Username, AppName
            ORDER  BY Username ASC, TotalSec DESC";

        cmd.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$to",   to.ToString("yyyy-MM-dd"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2),
                reader.GetInt64(3),
                reader.GetInt64(4)));
        }

        return result;
    }


    private static void WriteReport(
        string filePath,
        string title,
        List<(string Username, string AppName, long TotalSec, long TotalKeys, long TotalMouse)> rows)
    {
        const int Width = 70;
        var sb = new StringBuilder();

        sb.AppendLine(new string('=', Width));
        sb.AppendLine(Center(title, Width));
        sb.AppendLine(new string('=', Width));
        sb.AppendLine($"  Oluşturulma : {DateTime.Now:dd.MM.yyyy HH:mm:ss}");
        sb.AppendLine(new string('-', Width));

        if (rows.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("  Bu dönemde kayıt bulunamadı.");
        }
        else
        {
            var users = rows.Select(r => r.Username).Distinct().OrderBy(u => u);

            foreach (var user in users)
            {
                var userRows = rows.Where(r => r.Username == user).ToList();

                var trackedRows = userRows
                    .Where(r => TrackedDisplayNames.Contains(r.AppName))
                    .OrderByDescending(r => r.TotalSec)
                    .ToList();

                var otherRows = userRows
                    .Where(r => !TrackedDisplayNames.Contains(r.AppName))
                    .OrderByDescending(r => r.TotalSec)
                    .ToList();

                sb.AppendLine();
                sb.AppendLine($"  KULLANICI : {user}");
                sb.AppendLine($"  {new string('─', 60)}");

                sb.AppendLine($"  {"Uygulama",-26} {"Süre",12}  {"Tuş",8}  {"Mouse",8}");
                sb.AppendLine($"  {new string('·', 58)}");

                if (trackedRows.Count > 0)
                {
                    foreach (var (_, app, sec, keys, mouse) in trackedRows)
                        sb.AppendLine($"    {app,-26} {FormatDuration(sec),10}  {keys,8}  {mouse,8}");
                }
                else
                {
                    sb.AppendLine("  (Takip listesindeki uygulamalardan kayıt yok)");
                }

                if (otherRows.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine($"  DİĞER UYGULAMALAR");
                    sb.AppendLine($"  {new string('·', 58)}");

                    foreach (var (_, app, sec, keys, mouse) in otherRows)
                        sb.AppendLine($"    {app,-26} {FormatDuration(sec),10}  {keys,8}  {mouse,8}");
                }
            }
        }

        sb.AppendLine();
        sb.AppendLine(new string('=', Width));

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }


    private static string FormatDuration(long totalSeconds)
    {
        var span = TimeSpan.FromSeconds(totalSeconds);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}s {span.Minutes:D2}dk {span.Seconds:D2}sn"
            : $"{span.Minutes:D2}dk {span.Seconds:D2}sn";
    }

    private static string Center(string text, int width)
    {
        if (text.Length >= width) return text;
        int pad = (width - text.Length) / 2;
        return text.PadLeft(text.Length + pad);
    }
}

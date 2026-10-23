using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RotaGame.Core;

public static class Export
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };

    public static void WriteJson(MeasurementSession session, string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(new { Session = session, Comparison = Comparison.Evaluate(session) }, Options), new UTF8Encoding(false));

    public static void WriteCsv(MeasurementSession session, string path)
    {
        var text = new StringBuilder("session_id,game,region,target_ip,block_id,path,block_status,sequence,utc,outcome,rtt_ms,detail,interface_index,interface_id,interface_name,gateway,prefix,wireguard,interval_ms,timeout_ms,payload_bytes\r\n");
        foreach (var b in session.Blocks)
            foreach (var s in b.Samples)
                text.AppendLine(string.Join(",", new object?[] { session.Id, session.Game, session.RegionNote, b.TargetIp, b.Id,
                    b.Path, b.Status, s.Sequence, s.At.ToString("O"), s.Outcome, s.RttMs?.ToString("0.###", CultureInfo.InvariantCulture),
                    s.Detail, s.RouteBefore.InterfaceIndex, s.RouteBefore.InterfaceId, s.RouteBefore.Name, s.RouteBefore.Gateway,
                    s.RouteBefore.Prefix, s.RouteBefore.IsWireGuard, b.Settings.IntervalMs, b.Settings.TimeoutMs, b.Settings.PayloadBytes }.Select(Csv)));
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
        // Avoid formula interpretation when opened in a spreadsheet.
        if (text.Length > 0 && "=+-@\t\r\n".Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
}

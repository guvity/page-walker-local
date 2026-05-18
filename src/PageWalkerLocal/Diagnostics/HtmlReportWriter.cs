using System.Net;
using System.Text;
using PageWalkerLocal.Brain;
using PageWalkerLocal.Core;

namespace PageWalkerLocal.Diagnostics;

public sealed class HtmlReportWriter
{
    private readonly RuntimePaths _paths;

    public HtmlReportWriter(RuntimePaths paths)
    {
        _paths = paths;
    }

    public string Write(ActionHistory history)
    {
        var file = Path.Combine(_paths.DebugDirectory, $"report-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.html");
        var html = new StringBuilder();
        html.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>PageWalkerLocal Report</title>");
        html.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;line-height:1.45}table{border-collapse:collapse;width:100%}td,th{border:1px solid #ddd;padding:6px;text-align:left}th{background:#f3f3f3}</style>");
        html.AppendLine("</head><body><h1>PageWalkerLocal Report</h1><table><thead><tr><th>Time</th><th>Action</th><th>Target</th><th>Confidence</th><th>Outcome</th><th>Reason</th></tr></thead><tbody>");
        foreach (var entry in history.Entries)
        {
            html.Append("<tr>");
            html.Append($"<td>{WebUtility.HtmlEncode(entry.Timestamp.ToString("O"))}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(entry.Action.ToString())}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(entry.TargetId ?? string.Empty)}</td>");
            html.Append($"<td>{entry.Confidence:0.00}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(entry.Outcome)}</td>");
            html.Append($"<td>{WebUtility.HtmlEncode(entry.Reason)}</td>");
            html.AppendLine("</tr>");
        }

        html.AppendLine("</tbody></table></body></html>");
        File.WriteAllText(file, html.ToString());
        return file;
    }
}

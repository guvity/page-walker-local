using PageWalkerLocal.Core;
using PageWalkerLocal.Brain;

namespace PageWalkerLocal.Perception;

public sealed class TechPageDetector
{
    private readonly List<string> _signals =
    [
        "this site can't be reached",
        "no internet",
        "err_internet_disconnected",
        "dns_probe_finished",
        "err_name_not_resolved",
        "aw, snap",
        "site can't be reached",
        "connection was reset",
        "connection timed out",
        "network changed",
        "не удается получить доступ к сайту",
        "не удаётся получить доступ к сайту",
        "нет подключения к интернету",
        "страница недоступна",
        "соединение прервано",
        "dns-адрес не найден",
        "no se puede acceder a este sitio",
        "sin conexión a internet",
        "ce site est inaccessible",
        "aucune connexion internet",
        "diese website ist nicht erreichbar",
        "keine internetverbindung",
        "não é possível acessar esse site",
        "sem internet",
        "bu siteye ulaşılamıyor",
        "internet bağlantısı yok",
        "无法访问此网站",
        "沒有網際網路連線",
        "このサイトにアクセスできません",
        "インターネットに接続されていません",
        "사이트에 연결할 수 없음",
        "인터넷에 연결되어 있지 않음"
    ];

    public bool IsTechnical(string text, List<string> matchedSignals)
    {
        var normalized = Normalize(text);
        foreach (var signal in _signals)
        {
            if (normalized.Contains(signal, StringComparison.OrdinalIgnoreCase))
            {
                matchedSignals.Add(signal);
            }
        }

        return matchedSignals.Count > 0;
    }

    public WalkerAction ActionForConfig(AppConfig config)
    {
        return config.TechnicalPageAction.Trim().ToLowerInvariant() switch
        {
            "retry" => WalkerAction.PressKey,
            "back" => WalkerAction.PressKey,
            "close_tab" => WalkerAction.CloseOwnTab,
            _ => WalkerAction.Stop
        };
    }

    private static string Normalize(string text) => text.Replace('\u2019', '\'').ToLowerInvariant();
}

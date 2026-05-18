public static class AppFilter
{
    public static readonly Dictionary<string, string> ByProcess =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome",   "Google Chrome"   },
            { "msedge",   "Microsoft Edge"  },
            { "firefox",  "Mozilla Firefox" },
            { "opera",    "Opera"           },
            { "brave",    "Brave"           },
            { "CorePlus", "corePlus"        },
            { "eba",      "EBA"             },
            { "saplogon", "SAP Logon 64"    },
            { "qdms",     "QDMS"            },
        };

    public static readonly Dictionary<string, string> ByTitle =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Jira",           "Jira"        },
            { "EBA",            "EBA"         },
            { "QDMS",           "QDMS"        },
            { "visiott",        "Visiott"     },
            { "birgimefar",     "ISG Sistemi" },
            { "SAĞLIK EMNİYET", "ISG Sistemi" },
        };

    public static string? Resolve(string processName, string windowTitle)
    {
        foreach (var (keyword, displayName) in ByTitle)
        {
            if (windowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return displayName;
        }

        if (ByProcess.TryGetValue(processName, out var nameByProcess))
            return nameByProcess;

        return null;
    }
}

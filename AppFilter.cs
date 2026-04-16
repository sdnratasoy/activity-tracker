public static class AppFilter
{

    public static readonly Dictionary<string, string> ByProcess =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "chrome",      "Google Chrome"  },
            { "msedge",      "Microsoft Edge" },
            { "CorePlus",    "corePlus"        },
            { "eba",         "EBA"             },
            { "saplogon",    "SAP Logon 64"    },
            { "qdms",        "QDMS"            },

        };

    public static readonly Dictionary<string, string> ByTitle =
        new(StringComparer.OrdinalIgnoreCase)
        {
            { "Jira",  "Jira"  },
            { "EBA",   "EBA"   },
        };

    public static string? Resolve(string processName, string windowTitle)
    {
        if (ByProcess.TryGetValue(processName, out var nameByProcess))
            return nameByProcess;

        foreach (var (keyword, displayName) in ByTitle)
        {
            if (windowTitle.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return displayName;
        }

        return null;
    }
}

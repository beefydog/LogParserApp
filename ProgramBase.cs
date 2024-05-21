using System.Security.Cryptography;

namespace LogParserApp;

public class ProgramBase
{
    private static readonly char[] _trimChars = ['"', ' '];

    public static Dictionary<string, List<string>> ParseArguments(string[] args)
    {
        var settings = new Dictionary<string, List<string>>();
        string? currentKey = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith('-'))
            {
                currentKey = arg.TrimStart('-').ToLower();
                settings[currentKey] = [];
            }
            else if (currentKey != null)
            {
                settings[currentKey].Add(arg.Trim(_trimChars));
            }
        }

        // Handle filetypes and postprocess options specified as a comma-separated list
        if (settings.TryGetValue("filetype", out List<string>? value1))
        {
            settings["filetype"] = value1.SelectMany(f => f.Split(','))
                .Select(f => f.Trim())
                .ToList();
        }

        if (settings.TryGetValue("postprocess", out List<string>? value2) && value2.Count > 1)
        {
            // Additional argument for archive path if postprocess is 'archive'
            settings["archivepath"] = [value2[1]];
            settings["postprocess"] = [value2[0]];
        }

        // Add support for the 'after' argument
        if (settings.TryGetValue("after", out List<string>? value3) && value3.Count == 0)
        {
            settings["after"] = ["true"]; // Indicate that the 'after' argument is present
        }

        return settings;
    }


}
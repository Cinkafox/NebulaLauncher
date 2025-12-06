namespace Nebula.Shared.Utils;

public static class ContentManifestParser
{
    public static List<string> ExtractModules(Stream manifestStream)
    {
        using var reader = new StreamReader(manifestStream);
        return ExtractModules(reader.ReadToEnd());
    }
    
    public static List<string> ExtractModules(string manifestContent)
    {
        var modules = new List<string>();
        var lines = manifestContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        bool inModulesSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (line.StartsWith("modules:"))
            {
                inModulesSection = true;
                continue;
            }

            if (inModulesSection)
            {
                if (line.StartsWith("- "))
                {
                    modules.Add(line.Substring(2).Trim());
                }
                else if (!line.StartsWith(" "))
                {
                    break;
                }
            }
        }

        return modules;
    }
}
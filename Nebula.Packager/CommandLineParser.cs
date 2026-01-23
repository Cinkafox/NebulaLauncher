namespace Nebula.Packager;

public class CommandLineParser
{
    public string Configuration { get; set; } = "Release";
    public string RootPath { get; set; } = string.Empty;

    public static CommandLineParser Parse(IReadOnlyList<string> args)
    {
        using var enumerator = args.GetEnumerator();
        
        var parsed = new CommandLineParser();

        while (enumerator.MoveNext())
        {
            var arg = enumerator.Current;
            
            if (arg == "--configuration")
            {
                if (!enumerator.MoveNext())
                    throw new InvalidOperationException("Missing args for --configuration");
                
                parsed.Configuration = enumerator.Current;
            }

            if (arg == "--root-path")
            {
                if(!enumerator.MoveNext())
                    throw new InvalidOperationException("Missing args for --root-path");
                
                parsed.RootPath = enumerator.Current;
            }
        }

        return parsed;
    }
}
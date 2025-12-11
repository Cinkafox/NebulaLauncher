using Nebula.Shared.Utils;

namespace Nebula.Shared.Models;

public class RobustUrl
{
    public RobustUrl(string url)
    {
        if (!UriHelper.TryParseSs14Uri(url, out var uri))
            throw new Exception("Invalid scheme");

        Uri = uri;

        HttpUri = UriHelper.GetServerApiAddress(Uri);
    }

    public Uri Uri { get; }
    public Uri HttpUri { get; }
    public RobustPath InfoUri => new(this, "info");
    public RobustPath StatusUri => new(this, "status");

    public override string ToString()
    {
        return Uri.ToString();
    }

    public static implicit operator Uri(RobustUrl url)
    {
        return url.HttpUri;
    }

    public static implicit operator RobustUrl(string url)
    {
        return new RobustUrl(url);
    }

    public static explicit operator RobustUrl(Uri uri)
    {
        return new RobustUrl(uri.ToString());
    }
}

public class RobustPath
{
    public string Path;
    public RobustUrl Url;

    public RobustPath(RobustUrl url, string path)
    {
        Url = url;
        Path = path;
    }

    public override string ToString()
    {
        return ((Uri)this).ToString();
    }

    public static implicit operator Uri(RobustPath path)
    {
        return new Uri(path.Url, path.Url.HttpUri.PathAndQuery + path.Path);
    }
}
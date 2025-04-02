namespace Browser.Networking;

public class ResourceManager
{
    private readonly string _dataPath;

    public ResourceManager(string dataPath)
    {
        this._dataPath = dataPath;
    }


    private static bool DownloadResource(string url, string path)
    {
        using var client = new HttpClient();

        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,ru;q=0.8");
        // client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        client.DefaultRequestHeaders.Add("Connection", "keep-alive");
        
        try
        {
            var response = client.GetAsync(url).Result;

            if (response.IsSuccessStatusCode)
            {
                using var fs = new FileStream(path, FileMode.OpenOrCreate);
                response.Content.CopyToAsync(fs).Wait();
                return true;
            }
            else
            {
                Console.Error.WriteLine($"Bad Url: {url}; {(int)response.StatusCode}, {response.ReasonPhrase}"); //todo include in logger
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Exception while downloading {url}: {ex.Message}");
            return false;
        }
    }

    public bool GetResource(ref Resource resource)
    {
        Uri myUri;
        
        if (resource.path.StartsWith("//")) // abspath without protocol
        {
            myUri = new Uri("https:" + resource.path);
        }
        else
        {
            if (resource.path.StartsWith("/")) // relative path with starting /
            {
                myUri = new Uri(resource.host + resource.path);
            }
            else
            {
                if (resource.path.Contains("://")) // abspath
                {
                    myUri = new Uri(resource.path);
                }
                else // relative path without starting /
                {
                    if (resource.pagePath.EndsWith("/"))
                    {
                        myUri = new Uri(resource.pagePath + resource.path);
                    }
                    else
                    {
                        var count = resource.pagePath.Count((c) => c == '/');
                        if (count == 2) // only host
                        {
                            myUri = new Uri(resource.pagePath + "/" + resource.path);
                        }
                        else
                        {
                            var c = resource.pagePath.Split("/")[^1];
                            var d = resource.pagePath.Remove(resource.pagePath.Length - c.Length);
                            myUri = new Uri(d + resource.path);
                        }
                        
                    }
                }
                
            }
        }

        var host = myUri.Host;
        var path = myUri.AbsoluteUri;

        var fileName = resource.localPath;

        if (!string.IsNullOrEmpty(fileName) && File.Exists(fileName)) return true;

        fileName = Path.Combine(_dataPath, $"{host}__{ResourceUtil.ComputeHash(path)}");
        if (DownloadResource(path, fileName))
        {
            resource.localPath = fileName;
            return true;
        }
        else
        {
            resource.localPath = null;
            return false;
        }
    }

    public void ClearCacheByTab(List<Resource> resources)
    {
        foreach (var resource in resources)
        {
            if (resource.localPath != null && File.Exists(resource.localPath))
            {
                File.Delete(resource.localPath);
            }
        }
    }
}
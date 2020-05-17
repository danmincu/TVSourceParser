using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace WebParser
{
    class Program
    {
        static string playlistTemplate = @"<?xml version=""1.0"" encoding=""UTF-8""?>
            <playlist xmlns = ""http://xspf.org/ns/0/"" xmlns:vlc=""http://www.videolan.org/vlc/playlist/ns/0/"" version=""1"">
	            <title>Playlist</title>
                <trackList>
                $trackList$
                </trackList>
            </playlist>";

        static string trackTemplate = @"<track>
            <title>$title$</title>
            <location>http://acelinux:8000/pid/$streamId$/stream.mp4</location>
			    <extension application=""http://www.videolan.org/vlc/playlist/0"">
                    <vlc:id>$idx$</vlc:id>
				    <vlc:option>network-caching=1000</vlc:option>
			    </extension>
		    </track>";

        static async System.Threading.Tasks.Task Main(string[] args)
        {
            HttpClient client = new HttpClient();
            var response = await client.GetAsync("http://cool-tv.net");
            var input = await response.Content.ReadAsStringAsync();
            string pattern = @"""(ch\/([^""]*).htm)""";

            var pages = new ConcurrentDictionary<string, string>();
            // use the following as a Concurrent HashSet since there no default implementation;
            // the byte `value` is to be ignored
            var aceStreamsCollection = new ConcurrentDictionary<string, byte>();


            RegexOptions options = RegexOptions.Multiline;
            foreach (Match m in Regex.Matches(input, pattern, options))
            {
                if (m.Groups.Count > 0)
                {
                    var siteRelativePath = m.Groups[1]?.Value;
                    if (!pages.ContainsKey(siteRelativePath))
                        pages.TryAdd(siteRelativePath, m.Groups[2]?.Value);
                }
            }

            var idx = 1;
            var tracks = new StringBuilder();

            var tasks = pages.Select(item => Task.Run(async () => {
                var aceStreamID = await getAceStreamIDAsync($"http://cool-tv.net/{item.Key}");
                if (aceStreamID != null)
                {
                    if (!aceStreamsCollection.ContainsKey(aceStreamID))
                    {
                        Console.WriteLine($"Found stream for {item.Value} #{idx}");
                        aceStreamsCollection.TryAdd(aceStreamID, 0);
                        tracks.Append(PopulateTrackTemplate(item.Value, aceStreamID, idx++));
                    }
                }
            })).ToArray();

            Task.WaitAll(tasks);

            var playList = playlistTemplate.Replace("$trackList$", tracks.ToString());
            Console.Write(playList);
            System.IO.File.WriteAllText("playlist.xspf", playList);
        }

        static async System.Threading.Tasks.Task<string> getAceStreamIDAsync(string url)
        {
            HttpClient client = new HttpClient();
            var response = await client.GetAsync(url);
            var input = await response.Content.ReadAsStringAsync();

            string pattern = @"href=""acestream:\/\/([^\/]*)\/""";

            RegexOptions options = RegexOptions.Multiline;
            foreach (Match m in Regex.Matches(input, pattern, options))
            {
                if (m.Groups.Count > 0)
                {
                    var aceStreamId = m.Groups[1]?.Value;
                    return aceStreamId;
                }
            }

            return null;
        }

        static string PopulateTrackTemplate(string title, string aceStreamID, int idx)
        {
            var s = trackTemplate.Replace("$title$", title);
            s = s.Replace("$streamId$", aceStreamID);
            s = s.Replace("$idx$", idx.ToString());
            return s;
        }

    }

}

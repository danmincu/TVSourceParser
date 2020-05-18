using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
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
            DestroyVM();

            // Start the child process.
            Process vagrant = new Process();
            // Redirect the output stream of the child process.
            vagrant.StartInfo.UseShellExecute = false;
            vagrant.StartInfo.RedirectStandardOutput = true;
            vagrant.StartInfo.FileName = "vagrant";
            vagrant.StartInfo.Arguments = "up";
            Console.WriteLine("Wait few minutes! Spinning a new VM ...");
            vagrant.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = vagrant.StandardOutput.ReadToEnd();
            vagrant.WaitForExit();
            Console.WriteLine(output);
            UpdateHostFileWithIp(ExtractVMIp(output) ?? "127.0.0.1");

            await ExtractPlaylist();

            Console.WriteLine(@"Running VLC from C:\Program Files\VideoLAN\VLC\VLC.exe");
            Process vlc = new Process();
            // Redirect the output stream of the child process.
            vlc.StartInfo.UseShellExecute = false;
            vlc.StartInfo.RedirectStandardOutput = true;
            vlc.StartInfo.FileName = @"C:\Program Files\VideoLAN\VLC\VLC.exe";
            vlc.StartInfo.Arguments = "playlist.xspf";
            vlc.Start();
        }

        private static async Task ExtractPlaylist()
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
            var tracks = new Dictionary<string, string>();

            var tasks = pages.Select(item => Task.Run(async () =>
            {
                var aceStreamID = await getAceStreamIDAsync($"http://cool-tv.net/{item.Key}");
                if (aceStreamID != null)
                {
                    if (!aceStreamsCollection.ContainsKey(aceStreamID))
                    {
                        Console.WriteLine($"Found stream for {item.Value} #{idx}");
                        aceStreamsCollection.TryAdd(aceStreamID, 0);
                        tracks.Add(item.Value, PopulateTrackTemplate(item.Value, aceStreamID, idx++));
                    }
                }
            })).ToArray();

            Task.WaitAll(tasks);
            idx = 1;
            var tracksString = string.Join('\n', tracks.Keys.OrderBy(k => k).Select(k => tracks[k].Replace("$idx$", (idx++).ToString())));
            var playList = playlistTemplate.Replace("$trackList$", tracksString);
            Console.Write(playList);
            System.IO.File.WriteAllText("playlist.xspf", playList);
        }

        static void DestroyVM()
        {
            Process vagrant = new Process();
            // Redirect the output stream of the child process.
            vagrant.StartInfo.UseShellExecute = false;
            vagrant.StartInfo.RedirectStandardOutput = true;
            vagrant.StartInfo.FileName = "vagrant";
            vagrant.StartInfo.Arguments = "destroy -f";
            Console.WriteLine("Wait few seconds! Destroying the VM (if present)...");
            vagrant.Start();
            // Do not wait for the child process to exit before
            // reading to the end of its redirected stream.
            // p.WaitForExit();
            // Read the output stream first and then wait.
            string output = vagrant.StandardOutput.ReadToEnd();
            vagrant.WaitForExit();
            Console.WriteLine(output);
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
            //s = s.Replace("$idx$", idx.ToString());
            return s;
        }

        static void UpdateHostFileWithIp(string ip)
        {
            //string pattern = @"[ ]*[0-9\.]*\s*acelinux\s*";
            string pattern = @"[0-9\.]*\sacelinux[\s\n]*";
            RegexOptions options = RegexOptions.IgnoreCase;

            var content = System.IO.File.ReadAllLines(@"c:\windows\system32\drivers\etc\hosts");

            var newContent = new StringBuilder();
            var replaced = false;
            foreach (var input in content)
            {
                if (Regex.Matches(input, pattern, options).Count > 0)
                {
                    replaced = true;
                    newContent.AppendLine($"{ip} acelinux");
                }
                else
                {
                    newContent.AppendLine(input);
                }

            }
            if (!replaced)
            {
                newContent.AppendLine($"{ip} acelinux");
            }

            Console.Write(newContent.ToString());
            System.IO.File.WriteAllText(@"c:\windows\system32\drivers\etc\hosts", newContent.ToString());
        }

        static string ExtractVMIp(string input)
        {
            string pattern = @"\s*default: IP: ([0-9.]*)";
            RegexOptions options = RegexOptions.Multiline;
            
            foreach (Match m in Regex.Matches(input, pattern, options))
            {
                if (m.Groups.Count > 0)
                {
                    Console.WriteLine($"VM IP found {m.Groups[1]?.Value}.");
                    return m.Groups[1]?.Value;
                }
            }
            return null;
        }

    }

}

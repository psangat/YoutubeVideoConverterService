using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YoutubeExtractor;

namespace YoutubeVideoConverterService
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Downloading MP3 from Youtube Playlist");
            try
            {
                new Program().AccessPlaylist().Wait();
            }
            catch (AggregateException ex)
            {

                foreach (var e in ex.InnerExceptions)
                {
                    Console.WriteLine("Error: " + e.Message);
                }
            }
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
        }

        private async Task AccessPlaylist()
        {
            UserCredential credential;
            using (var stream = new FileStream(@"C:\client_id.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                            GoogleClientSecrets.Load(stream).Secrets,
                            // This OAuth 2.0 access scope allows for read-only access to the authenticated 
                            // user's account, but not other types of account access.
                            new[] { YouTubeService.Scope.YoutubeReadonly },
                            "psangat",
                            CancellationToken.None,
                            new FileDataStore(this.GetType().ToString())
                        );
            }

            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = this.GetType().ToString()
            });

            var playlists = youtubeService.Playlists.List("snippet");

            playlists.PageToken = "";
            playlists.MaxResults = 50;
            playlists.Mine = true;
            PlaylistListResponse presponse = await playlists.ExecuteAsync();
            foreach (var currentPlayList in presponse.Items)
            {
                if (currentPlayList.Snippet.Title.Equals("Songs"))
                {
                    PlaylistItemsResource.ListRequest listRequest = youtubeService.PlaylistItems.List("contentDetails");
                    listRequest.MaxResults = 50;
                    listRequest.PlaylistId = currentPlayList.Id;
                    listRequest.PageToken = playlists.PageToken;
                    var response = await listRequest.ExecuteAsync();
                    var index = 1;
                    foreach (var playlistItem in response.Items)
                    {
                        VideosResource.ListRequest videoR = youtubeService.Videos.List("snippet, contentDetails, status");
                        videoR.Id = playlistItem.ContentDetails.VideoId;
                        var responseV = await videoR.ExecuteAsync();
                        if (responseV.Items.Count > 0)
                        {
                            string link = String.Format("https://www.youtube.com/watch?v={0}&list={1}&index={2}", videoR.Id, currentPlayList.Id, index);
                            IEnumerable<VideoInfo> videoInfos = DownloadUrlResolver.GetDownloadUrls(link);

                            try
                            {

                                VideoInfo video = videoInfos.First(info => info.VideoType == VideoType.Mp4 && info.Resolution == 0 && !String.IsNullOrEmpty(info.Title));
                                Console.WriteLine("Downloading {0}", video.Title);
                                if (video.RequiresDecryption)
                                {
                                    DownloadUrlResolver.DecryptDownloadUrl(video);
                                }

                                using (var progress = new ProgressBar())
                                {
                                    for (int i = 0; i <= 100; i++)
                                    {
                                        progress.Report((double)i / 100);
                                        Thread.Sleep(20);
                                    }
                                }
                                var audioDownloader = new VideoDownloader(video, Path.Combine(@"C:\Users\prsangat\Desktop\Songs", video.Title + ".mp3"));
                                using (var progress = new ProgressBar())
                                {
                                    audioDownloader.DownloadProgressChanged += (sender, args) => progress.Report(args.ProgressPercentage);
                                }
                                //audioDownloader.DownloadProgressChanged += (sender, args) => progre//Console.Write( "\r{0}% ", Math.Round(args.ProgressPercentage));
                                audioDownloader.Execute();
                                Console.WriteLine("Download Complete.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine();
                                Console.WriteLine(ex.ToString());
                                Console.WriteLine();
                                // throw;
                            }

                            index++;
                        }
                    }
                    playlists.PageToken = response.NextPageToken;
                }
            }
        }
    }
}


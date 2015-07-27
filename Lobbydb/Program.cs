using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NodaTime;
using PlayerIOClient;

namespace Lobbydb
{
    internal static class Program
    {
        private const string GameId = "everybody-edits-su9rn58o40itdbnw69plyw";
        private static StreamWriter _sw =
                new StreamWriter(
                    new FileStream(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.DesktopDirectory) + "/lobby_data/" + GetUtcTime() + ".txt",
                        FileMode.Create, FileAccess.Write)
                    );

        private static Client _globalClient;

        private static string UtcTime { get; } = GetUtcTime();

        public static List<RoomInfo> roomsDone = new List<RoomInfo>();
        private static readonly int DOWNLOAD_INTERVAL_SECONDS = 5*1000;

        private static CancellationTokenSource cancelToken = new CancellationTokenSource();
        private static void Main()
        {
            Console.WriteLine("Lobby snapshoter has started. Press any key to quit.");

            PlayerIO.QuickConnect.SimpleConnect(GameId, "guest", "guest", null, delegate (Client client)
            {
                _globalClient = client;
                while (true)
                {
                    Task t = null;
                    if (_globalClient.ConnectUserId != null)
                    {
                        var rooms = DownloadLobby();
                        t = _sw.WriteLineAsync(JsonConvert.SerializeObject(rooms));
                        try {
                            Task.Delay(DOWNLOAD_INTERVAL_SECONDS).Wait(cancelToken.Token);
                        } catch (OperationCanceledException)
                        {
                            Console.WriteLine("Shutting down event loop...");
                        }
                    } else
                    {
                        t.Wait();
                        break;
                    }
                }
            });

            Console.ReadKey();
            Console.WriteLine("Shutting down...");

            cancelToken.Cancel();
            _globalClient.Logout();

            try {
                _sw.Flush();
                _sw.Close();
                _sw.Dispose();
            } catch (Exception)
            {
                // may have already been disposed of in another thread
            }
        }

        // ReSharper disable once InconsistentNaming
        private static void PrintPlayerIOError(PlayerIOError error)
        {
            Console.WriteLine("ERROR: [{0}] {1}", UtcTime, error.Message);
        }

        private static string GetUtcTime()
        {
            return SystemClock.Instance.Now.InUtc().ToInstant().ToString().Replace(":", "");
        }

        private static List<RoomInfo> DownloadLobby()
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            List<RoomInfo> dataToWrite = new List<RoomInfo>();
            _globalClient.Multiplayer.ListRooms(null, null, 0, 0,
                delegate (PlayerIOClient.RoomInfo[] rooms)
                {
                    foreach (var room in rooms)
                    {
                        if (!room.RoomType.StartsWith("Lobby"))
                        {
                            if (room.Id.StartsWith("PW") || room.Id.StartsWith("BW") || room.Id.StartsWith("OW"))
                            {
                                string plays;
                                string rating;
                                string name;
                                string woots;
                                room.RoomData.TryGetValue("plays", out plays);
                                room.RoomData.TryGetValue("rating", out rating);
                                room.RoomData.TryGetValue("name", out name);
                                room.RoomData.TryGetValue("woots", out woots);
                                Console.WriteLine("Downloaded lobby: " + name);
                                var aRoom = new RoomInfo(
                                    room.Id,
                                    room.OnlineUsers,
                                    Convert.ToInt32(plays),
                                    name,
                                    Convert.ToInt32(woots));

                                dataToWrite.Add(aRoom);
                            }
                        }
                    }
                    waitHandle.Set();
                },
                PrintPlayerIOError);
            waitHandle.WaitOne();
            return dataToWrite;
        }
    }
}
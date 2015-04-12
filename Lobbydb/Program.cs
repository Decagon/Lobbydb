using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using NodaTime;
using PlayerIOClient;
using SevenZip.Compression.LZMA;
using Timer = System.Timers.Timer;

namespace Lobbydb
{
    internal static class Program
    {
        private const string GameId = "everybody-edits-su9rn58o40itdbnw69plyw";
        private static StreamWriter _sw;
        private static Client _globalClient;

        private static readonly BlockingCollection<RoomInfoWrapper> DataQueue = new BlockingCollection<
            RoomInfoWrapper>();

        private static readonly BlockingCollection<RoomInfoWrapper> JsonDataQueue = new BlockingCollection<
            RoomInfoWrapper>();

        private static readonly ManualResetEvent ChangeFileNameWaitHandle = new ManualResetEvent(true);
        private static readonly Timer DownloadLobbyInteveral = new Timer(1000);
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static string UtcTime { get; } = GetUtcTime();

        private static void Main()
        {
            Console.WriteLine("Lobby snapshoter has started. Press any key to quit.");
            var fileWriterCancellationTokenSource = new CancellationTokenSource();

            DownloadLobbyInteveral.Elapsed += DownloadLobby;

            var changeFileNameInterval = new Timer(1000*60*1); // 15 minutes
            changeFileNameInterval.Elapsed += WriteToFileQueue;

            // this will create the initial file
            changeFileNameInterval.Enabled = true;

            PlayerIO.QuickConnect.SimpleConnect(GameId, "guest", "guest", null, delegate(Client client)
            {
                _globalClient = client;
                DownloadLobbyInteveral.Enabled = true;
                try
                {
                    Task.Factory.StartNew(ReduceQueueSize(fileWriterCancellationTokenSource.Token)
                        , TaskCreationOptions.LongRunning
                        , fileWriterCancellationTokenSource.Token);
                }
                catch (ArgumentNullException)
                {
                }
            },
                PrintPlayerIOError);

            Console.ReadKey();
            Console.WriteLine("Shutting down...");

            DownloadLobbyInteveral.Enabled = false;
            changeFileNameInterval.Enabled = false;
            // ReSharper disable once RedundantArgumentNameForLiteralExpression
            WriteToFileQueue(null, null, shuttingDown: true);

            _globalClient.Logout();
            fileWriterCancellationTokenSource.Cancel();

            _sw.Flush();
            _sw.Close();
            _sw.Dispose();
        }

        private static void WriteToFileQueue(object sender, ElapsedEventArgs e)
        {
            WriteToFileQueue(null,null,false);
        }

        // ReSharper disable once InconsistentNaming
        private static void PrintPlayerIOError(PlayerIOError error)
        {
            Console.WriteLine("ERROR: [{0}] {1}", UtcTime, error.Message);
        }

        private static void ChangeFileName()
        {
            ChangeFileNameWaitHandle.Reset();
            try
            {
                _sw?.Flush();
                _sw?.Close();
            }
            catch (Exception)
            {
                // ignored
            }

            _sw =
                new StreamWriter(
                    new FileStream(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.DesktopDirectory) + "/lobby_data/" + GetUtcTime() + ".txt",
                        FileMode.Create, FileAccess.Write)
                    );
            ChangeFileNameWaitHandle.Set();
        }

        private static string GetUtcTime()
        {
            return SystemClock.Instance.Now.InUtc().ToInstant().ToString().Replace(":", "");
        }

        private static void WriteToFileQueue(object source, ElapsedEventArgs e, bool shuttingDown = false)
        {
            if (!shuttingDown)
            {
                ChangeFileName();
            }
            ChangeFileNameWaitHandle.WaitOne(); // wait when the file name is being changed
            var bufferedData = new RoomInfoWrapper[JsonDataQueue.Count];
            lock (JsonDataQueue)
            {
                JsonDataQueue.CopyTo(bufferedData, 0);
                JsonDataQueue.Clear();
            }

            var utf8Text = JsonConvert.SerializeObject(bufferedData);
            var byteText = Encoding.ASCII.GetBytes(utf8Text);

            var b = SevenZipHelper.Compress(byteText);

            _sw.Write(Encoding.UTF8.GetString(b));

           /*Console.WriteLine(
            Encoding.ASCII.GetString(
                SevenZipHelper.Decompress(
                    b)).Substring(0,400));*/

        }

        //http://stackoverflow.com/questions/472906

        // ReSharper disable once UnusedMember.Local because it will be used for decompression
        private static string GetString(byte[] bytes)
        {
            var chars = new char[bytes.Length/sizeof (char)];
            Buffer.BlockCopy(bytes, 0, chars, 0, bytes.Length);
            return new string(chars);
        }


        //http://stackoverflow.com/questions/8001133
        private static void Clear<T>(this BlockingCollection<T> blockingCollection)
        {
            if (blockingCollection == null)
            {
                throw new ArgumentNullException(nameof(blockingCollection));
            }

            while (blockingCollection.Count > 0)
            {
                T item;
                blockingCollection.TryTake(out item);
            }
        }

        private static Action<object> ReduceQueueSize(CancellationToken cancelToken)
        {
            while (true)
            {
                try
                {
                    var theData = DataQueue.Take(cancelToken);
                    lock (JsonDataQueue)
                    {
                        JsonDataQueue.Add(
                            theData, cancelToken);
                    }

                    cancelToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }
        }

        private static void DownloadLobby(object source, ElapsedEventArgs e)
        {
            _globalClient.Multiplayer.ListRooms(null, null, 0, 0,
                delegate(PlayerIOClient.RoomInfo[] rooms)
                {
                    var playersInLobby = new List<string>();
                    var roomsDone = new List<RoomInfo>();

                    for (var i = 0; i < rooms.Length; i++)
                    {
                        string plays;
                        string rating;
                        string name;
                        string woots;
                        rooms[i].RoomData.TryGetValue("plays", out plays);
                        rooms[i].RoomData.TryGetValue("rating", out rating);
                        rooms[i].RoomData.TryGetValue("name", out name);
                        rooms[i].RoomData.TryGetValue("woots", out woots);
                        RoomInfo aRoom = null;
                        if (!rooms[i].RoomType.Contains("Lobby"))
                        {
                            aRoom = new RoomInfo(
                                rooms[i].Id,
                                rooms[i].OnlineUsers,
                                Convert.ToInt32(plays),
                                name,
                                Convert.ToInt32(woots));
                        }
                        else
                        {
                            playersInLobby.Add(rooms[i].Id);
                        }

                        if (rooms[i].RoomType.Contains("Everybodyedits"))
                        {
                            roomsDone.Add(aRoom);
                        }
                    }
                    var wrapper = new RoomInfoWrapper
                    {
                        Lobby = playersInLobby,
                        Date = UtcTime,
                        Rooms = roomsDone
                    };

                    DataQueue.Add(wrapper);
                },
                PrintPlayerIOError);
        }
    }
}
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;
using NodaTime;
using PlayerIOClient;
using Timer = System.Timers.Timer;
using SevenZip.Compression.LZMA;

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
        private static readonly Timer DownloadLobbyInteveral = new Timer(3000);
        // ReSharper disable once FieldCanBeMadeReadOnly.Local
        private static string UtcTime { get; } = GetUtcTime();

        public static HashSet<string> unique_user_ids = new HashSet<string>();
        public static HashSet<string> roomsDone = new HashSet<string>();
        private static void Main()
        {
            Console.WriteLine("Lobby snapshoter has started. Press any key to quit.");
            var fileWriterCancellationTokenSource = new CancellationTokenSource();

            DownloadLobbyInteveral.Elapsed += DownloadLobby;

            var changeFileNameInterval = new Timer(1000*60*5); // 15 minutes
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
            try {
                _sw.Flush();
                _sw.Close();
                _sw.Dispose();
            } catch (Exception)
            {
                // may have already been disposed of in another thread
            }
        }

        private static void WriteToFileQueue(object sender, ElapsedEventArgs e)
        {
            WriteToFileQueue(null, null, false);
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
          lock (unique_user_ids)
            {
                lock (roomsDone)
                {
                    try {
                        _sw.WriteLine(JsonConvert.SerializeObject(unique_user_ids));
                        _sw.WriteLine(JsonConvert.SerializeObject(roomsDone));
                        unique_user_ids.Clear();
                        roomsDone.Clear();
                    } catch (Exception)
                    {
                        // not sure; null ref exception
                    }
                }
            }
            

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
                    
                    
                    foreach (var room in rooms)
                    {
                        if (room.Id.StartsWith("simple") || room.Id.StartsWith("kong") || room.Id.StartsWith("fb"))
                        {
                            unique_user_ids.Add(room.Id);
                        }
                        
                        if (!room.RoomType.StartsWith("Lobby"))
                        {
                            if (room.Id.StartsWith("PW") || room.Id.StartsWith("BW"))
                            {
                                roomsDone.Add(room.Id);
                                //Console.WriteLine(JsonConvert.SerializeObject(aRoom));
                            }
                            
                        }
                        else
                        {
                            playersInLobby.Add(room.Id);
                        }
                    }

                },
                PrintPlayerIOError);
        }
    }
}
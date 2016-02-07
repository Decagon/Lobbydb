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

        private static Client _globalClient;

        public static List<RoomInfo> roomsDone = new List<RoomInfo>();

        private static CancellationTokenSource cancelToken = new CancellationTokenSource();
        private static void Main()
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            PlayerIO.QuickConnect.SimpleConnect(GameId, "guest", "guest", null, delegate (Client client)
            {
                _globalClient = client;
                    if (_globalClient.ConnectUserId != null)
                    {
                        DownloadLobby();    
                }
                waitHandle.Set();
            });
            waitHandle.WaitOne();
        }

        // ReSharper disable once InconsistentNaming
        private static void PrintPlayerIOError(PlayerIOError error)
        {
            throw new Exception(error.ToString());
        }

        private static void DownloadLobby()
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
                                Console.WriteLine(room.Id + " " + room.OnlineUsers);
                            }
                        }
                    }
                    waitHandle.Set();
                },
                PrintPlayerIOError);
            waitHandle.WaitOne();
            //return dataToWrite;
        }
    }
}
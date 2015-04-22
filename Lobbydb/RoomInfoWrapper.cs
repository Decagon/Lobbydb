using System.Collections.Generic;

namespace Lobbydb
{
    public class RoomInfoWrapper
    {
        public RoomInfoWrapper()
        {
        }

        // ReSharper disable once UnusedMember.Global because JsonConvert needs empty constructor for reflection
        // ReSharper disable once UnusedParameter.Local
        // ReSharper disable once UnusedParameter.Local
        public RoomInfoWrapper(string date, List<RoomInfo> rooms, List<string> lobby)
        {
            Rooms = rooms;
            Date = date;
            Lobby = Lobby;
        }

        public string Date { get; set; }
        public List<string> Lobby { get; set; }
        public List<RoomInfo> Rooms { get; set; }
    }
}
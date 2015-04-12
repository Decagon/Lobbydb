namespace Lobbydb
{
    public class RoomInfo
    {
        // ReSharper disable once UnusedMember.Global
        public RoomInfo()
        {
        }

        public RoomInfo(string id, int onlineUsers, int Plays, string Name, int Woots)
        {
            Id = id;
            OnlineUsers = onlineUsers;
            this.Plays = Plays;
            this.Name = Name;
            this.Woots = Woots;
        }

        public int Plays { get; set; }
        public int Woots { get; set; }

        /// <summary>
        ///     The id of the room
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     How many users are currently in the room
        /// </summary>
        public int OnlineUsers { get; set; }

        public string Name { get; set; }
    }
}
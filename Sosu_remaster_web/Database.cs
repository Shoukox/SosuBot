using Npgsql;

namespace Sosu
{
    public class Database
    {
        private NpgsqlConnection _conn;
        private Queue<Action> _queue;
        private Thread _queueThread;

        public Database(string connString)
        {
            _conn = new NpgsqlConnection(connString);
            _queue = new Queue<Action>();
            _queueThread = new Thread(
                new ThreadStart(() => _processQueue()));

            _queueThread.Start();
            _conn.Open();
        }

        private void _processQueue()
        {

            while (true)
            {
                try
                {
                    if (_queue.Count == 0)
                    {
                        Thread.Sleep(5000);
                        continue;
                    }
                    if (_conn.State != System.Data.ConnectionState.Open)
                    {
                        _conn.Open();
                    }
                    _queue.Dequeue().Invoke();
                    Console.WriteLine($"DB processed, _queue count: {_queue.Count}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public void InsertOrUpdateOsuUsersTable(Types.osuUser osuUser, bool inserting = false)
        {
            _queue.Enqueue(
                new Action(() =>
                {
                    string commandString = "";
                    if (inserting)
                        commandString = $"INSERT INTO osuusers(id, osuname, pp) VALUES (@id, @osuname, @pp )";
                    else
                        commandString = $"UPDATE osuusers SET osuname = @osuname, pp = @pp WHERE id = @id";

                    using (NpgsqlCommand nc = new NpgsqlCommand(commandString, _conn))
                    {
                        nc.Parameters.AddWithValue("@id", osuUser.telegramId);
                        nc.Parameters.AddWithValue("@osuname", osuUser.osuName);
                        nc.Parameters.AddWithValue("@pp", osuUser.pp);
                        nc.ExecuteNonQuery();
                    }
                }));
        }

        /// <param name="add">-1 - not using | 0 - update | 1 - add</param>
        public void InsertOrUpdateOsuChatsTable(Types.Chat osuchat, bool inserting = false)
        {
            _queue.Enqueue(
               new Action(() =>
               {
                   if (osuchat.members == null) osuchat.members = new List<long>();

                   string commandString = "";

                   if (inserting)
                       commandString = $"INSERT INTO osuchats(lastbeatmapid, chatid, members, language) VALUES (@lastbeatmapid, @chatid, @members, @language )";
                   else
                       commandString = $"UPDATE osuchats SET lastbeatmapid = @lastbeatmapid, members = @members, language = @language WHERE chatid = @chatid";

                   using (NpgsqlCommand nc = new NpgsqlCommand(commandString, _conn))
                   {
                       nc.Parameters.AddWithValue("@lastbeatmapid", osuchat.lastBeatmap_id);
                       nc.Parameters.AddWithValue("@chatid", osuchat.chat.Id);
                       nc.Parameters.AddWithValue("@members", osuchat.members);
                       nc.Parameters.AddWithValue("@language", osuchat.language);
                       nc.ExecuteNonQuery();
                   }
               }));
        }
        public void DeleteFromOsuChatsTable(long chatId, bool inserting = false)
        {
            _queue.Enqueue(
               new Action(() =>
               {

                   using (NpgsqlCommand nc = new NpgsqlCommand("DELETE FROM osuchats WHERE chatid = @chatid", _conn))
                   {
                       nc.Parameters.AddWithValue("@chatid", chatId);
                       nc.ExecuteNonQuery();
                   }
               }));
        }
        public void DeleteFromOsuUsersTable(long id, bool inserting = false)
        {
            _queue.Enqueue(
               new Action(() =>
               {

                   using (NpgsqlCommand nc = new NpgsqlCommand("DELETE FROM osuusers WHERE id = @id", _conn))
                   {
                       nc.Parameters.AddWithValue("@id", id);
                       nc.ExecuteNonQuery();
                   }
               }));
        }
        public List<object[]>? GetData(string query, int count)
        {
            try
            {
                lock (_conn)
                {
                    using (var cmd = new NpgsqlCommand(query, _conn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {

                            List<object[]> data = new List<object[]>();
                            while (reader.Read())
                            {
                                var obj = new object[count];
                                for (int i = 0; i <= count - 1; i++) obj[i] = reader.GetProviderSpecificValue(i);
                                data.Add(obj);
                            }
                            return data;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }
    }
}

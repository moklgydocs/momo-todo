using Microsoft.Data.Sqlite;
using MokReport.Todo.Models;
using System.IO;

namespace MokReport.Todo.Services;

public class TodoService : IDisposable
{
    private readonly SqliteConnection _db;

    public TodoService()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MokReport", "Todo");
        Directory.CreateDirectory(path);

        _db = new SqliteConnection($"Data Source={Path.Combine(path, "todo.db")}");
        _db.Open();
        CreateTable();
    }

    private void CreateTable()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TodoItems (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Description TEXT,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                DueDate TEXT,
                ReminderTime TEXT,
                ReminderShown INTEGER NOT NULL DEFAULT 0,
                Category TEXT NOT NULL DEFAULT '�� 一般',
                Priority INTEGER NOT NULL DEFAULT 1,
                Color TEXT NOT NULL DEFAULT '',
                Repeat TEXT NOT NULL DEFAULT 'None',
                Defer TEXT NOT NULL DEFAULT 'Default'
            )
            """;
        cmd.ExecuteNonQuery();

        // Migration: add missing columns from older DB versions (for old DBs)
        try { cmd.CommandText = "ALTER TABLE TodoItems ADD COLUMN Category TEXT NOT NULL DEFAULT '📋 一般'"; cmd.ExecuteNonQuery(); }
        catch { /* column already exists */ }
        try { cmd.CommandText = "ALTER TABLE TodoItems ADD COLUMN Priority INTEGER NOT NULL DEFAULT 1"; cmd.ExecuteNonQuery(); }
        catch { /* column already exists */ }
        try { cmd.CommandText = "ALTER TABLE TodoItems ADD COLUMN Color TEXT NOT NULL DEFAULT ''"; cmd.ExecuteNonQuery(); }
        catch { /* column already exists */ }
        try { cmd.CommandText = "ALTER TABLE TodoItems ADD COLUMN Repeat TEXT NOT NULL DEFAULT 'None'"; cmd.ExecuteNonQuery(); }
        catch { /* column already exists */ }
        try { cmd.CommandText = "ALTER TABLE TodoItems ADD COLUMN Defer TEXT NOT NULL DEFAULT 'Default'"; cmd.ExecuteNonQuery(); }
        catch { /* column already exists */ }

        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Quotes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL UNIQUE,
                Author TEXT NOT NULL DEFAULT ''
            )
            """;
        cmd.ExecuteNonQuery();

        // Seed quotes if empty or incomplete
        cmd.CommandText = "SELECT COUNT(*) FROM Quotes";
        var count = (long)cmd.ExecuteScalar()!;
        if (count < 50) SeedQuotes(count);
    }

    private void SeedQuotes(long existingCount)
    {
        var allQuotes = new[]
        {
            ("种一棵树最好的时间是十年前，其次是现在。", "Dambisa Moyo"),
            ("你有多自律，就有多自由。", "佚名"),
            ("今天不想跑，所以才去跑。", "村上春树"),
            ("不积跬步，无以至千里；不积小流，无以成江海。", "荀子"),
            ("每一个不曾起舞的日子，都是对生命的辜负。", "尼采"),
            ("成功的秘诀在于坚持自己的目标和信念。", "本杰明·迪斯雷利"),
            ("不要等到明天，明天太遥远，今天就行动。", "佚名"),
            ("自律即自由。", "康德"),
            ("生活就像骑自行车，要保持平衡就得往前走。", "爱因斯坦"),
            ("与其仰望星空，不如脚踏实地。", "佚名"),
            ("你只管努力，剩下的交给时间。", "佚名"),
            ("行动是治愈恐惧的良药。", "威廉·詹姆斯"),
            ("没有人一开始就是完美的，但踏出的每一步都在变好。", "佚名"),
            ("世上无难事，只要肯登攀。", "毛泽东"),
            ("最简单的事是坚持，最难的事还是坚持。", "佚名"),
            ("不是因为有希望才坚持，而是因为坚持才有希望。", "佚名"),
            ("要么你驾驭生命，要么生命驾驭你。", "拿破仑·希尔"),
            ("优秀是一种习惯。", "亚里士多德"),
            ("日拱一卒，功不唐捐。", "曾国藩"),
            ("大事必作于细，难事必作于易。", "老子"),
            ("当你想要放弃的时候，想想当初为什么开始。", "佚名"),
            ("人最大的敌人往往是自己。", "佚名"),
            ("不要怕走得慢，只要不停下。", "佚名"),
            ("明天的你，会感谢今天拼命的自己。", "佚名"),
            ("我们坚持一件事情，并不是因为这样做了会有效果，而是坚信这样做是对的。", "哈维尔"),
            ("一个人知道自己为什么而活，就可以忍受任何一种生活。", "尼采"),
            ("万丈高楼平地起。", "谚语"),
            ("腹有诗书气自华。", "苏轼"),
            ("一个人最大的破产是绝望，最大的资产是希望。", "佚名"),
            ("发光不是太阳的专利，你也可以。", "佚名"),
            ("一个人应养成信赖自己的习惯，即使在最危急的时候。", "拿破仑"),
            ("业精于勤荒于嬉，行成于思毁于随。", "韩愈"),
            ("天行健，君子以自强不息。", "《周易》"),
            ("宝剑锋从磨砺出，梅花香自苦寒来。", "《警世贤文》"),
            ("志当存高远。", "诸葛亮"),
            ("千里之行，始于足下。", "老子"),
            ("学而不思则罔，思而不学则殆。", "孔子"),
            ("三人行，必有我师焉。", "孔子"),
            ("温故而知新，可以为师矣。", "孔子"),
            ("人生在勤，不索何获。", "张衡"),
            ("读书破万卷，下笔如有神。", "杜甫"),
            ("纸上得来终觉浅，绝知此事要躬行。", "陆游"),
            ("山重水复疑无路，柳暗花明又一村。", "陆游"),
            ("会当凌绝顶，一览众山小。", "杜甫"),
            ("长风破浪会有时，直挂云帆济沧海。", "李白"),
            ("沉舟侧畔千帆过，病树前头万木春。", "刘禹锡"),
            ("勇敢不是不害怕，而是害怕的时候你还能坚持去做。", "曼德拉"),
            ("你的时间有限，不要为别人而活。", "乔布斯"),
            ("人生最大的冒险，就是过你梦想的生活。", "奥普拉"),
            ("成功的唯一秘诀，就是坚持到最后一分钟。", "柏拉图"),
            ("想，都是问题；做，才是答案。", "佚名"),
            ("把每一件简单的事做好，就是不简单。", "张瑞敏"),
            ("苟日新，日日新，又日新。", "《大学》"),
            ("道阻且长，行则将至。", "《荀子》"),
            ("知不足，然后能自反也；知困，然后能自强也。", "《礼记》"),
            ("工欲善其事，必先利其器。", "孔子"),
            ("落红不是无情物，化作春泥更护花。", "龚自珍"),
            ("问渠那得清如许？为有源头活水来。", "朱熹"),
            ("希望是附丽于存在的，有存在，便有希望。", "鲁迅"),
            ("时间就像海绵里的水，只要愿挤，总还是有的。", "鲁迅"),
        };

        using var cmd = _db.CreateCommand();
        using var tx = _db.BeginTransaction();
        cmd.Transaction = tx;
        cmd.CommandText = "INSERT INTO Quotes (Content, Author) SELECT @c, @a WHERE NOT EXISTS (SELECT 1 FROM Quotes WHERE Content=@c)";
        var pC = cmd.CreateParameter(); pC.ParameterName = "@c"; cmd.Parameters.Add(pC);
        var pA = cmd.CreateParameter(); pA.ParameterName = "@a"; cmd.Parameters.Add(pA);

        foreach (var (content, author) in allQuotes)
        {
            pC.Value = content;
            pA.Value = author;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    public (string content, string author) GetRandomQuote()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT Content, Author FROM Quotes ORDER BY RANDOM() LIMIT 1";
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return (reader.GetString(0), reader.GetString(1));
        return ("种一棵树最好的时间是十年前，其次是现在。", "Dambisa Moyo");
    }

    public List<TodoItem> GetAll()
    {
        var items = new List<TodoItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM TodoItems ORDER BY Priority DESC, DueDate ASC, CreatedAt DESC";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadItem(reader));
        }
        return items;
    }

    public List<TodoItem> GetPendingReminders()
    {
        var items = new List<TodoItem>();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            SELECT * FROM TodoItems
            WHERE IsCompleted = 0
              AND ReminderTime IS NOT NULL
              AND ReminderTime <= @now
              AND ReminderShown = 0
            """;
        cmd.Parameters.AddWithValue("@now", DateTime.Now.ToString("O"));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            items.Add(ReadItem(reader));
        }
        return items;
    }

    public void Add(TodoItem item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TodoItems (Title, Description, IsCompleted, CreatedAt, DueDate, ReminderTime, ReminderShown, Category, Priority, Color, Repeat, Defer)
            VALUES (@t, @d, @c, @ca, @dd, @rt, @rs, @cat, @p, @co, @rpt, @df)
            """;
        AddParameters(cmd, item);
        cmd.ExecuteNonQuery();
    }

    public void Update(TodoItem item)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            UPDATE TodoItems SET
                Title=@t, Description=@d, IsCompleted=@c, DueDate=@dd,
                ReminderTime=@rt, ReminderShown=@rs, Category=@cat, Priority=@p, Color=@co, Repeat=@rpt, Defer=@df
            WHERE Id=@id
            """;
        cmd.Parameters.AddWithValue("@id", item.Id);
        AddParameters(cmd, item);
        cmd.ExecuteNonQuery();
    }

    public void Delete(int id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM TodoItems WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    public void MarkReminderShown(int id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE TodoItems SET ReminderShown=1 WHERE Id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.ExecuteNonQuery();
    }

    private static void AddParameters(SqliteCommand cmd, TodoItem item)
    {
        cmd.Parameters.AddWithValue("@t", item.Title);
        cmd.Parameters.AddWithValue("@d", item.Description as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@c", item.IsCompleted ? 1 : 0);
        cmd.Parameters.AddWithValue("@ca", item.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@dd", item.DueDate?.ToString("O") as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rt", item.ReminderTime?.ToString("O") as object ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rs", item.ReminderShown ? 1 : 0);
        cmd.Parameters.AddWithValue("@cat", item.Category);
        cmd.Parameters.AddWithValue("@p", item.Priority);
        cmd.Parameters.AddWithValue("@co", item.Color);
        cmd.Parameters.AddWithValue("@rpt", item.Repeat);
        cmd.Parameters.AddWithValue("@df", item.Defer);
    }

    private static TodoItem ReadItem(SqliteDataReader r)
    {
        return new TodoItem
        {
            Id = r.GetInt32(0),
            Title = r.GetString(1),
            Description = r.IsDBNull(2) ? null : r.GetString(2),
            IsCompleted = r.GetInt32(3) == 1,
            CreatedAt = DateTime.Parse(r.GetString(4)),
            DueDate = r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)),
            ReminderTime = r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6)),
            ReminderShown = r.GetInt32(7) == 1,
            Category = r.IsDBNull(8) ? "📋 一般" : r.GetString(8),
            Priority = r.IsDBNull(9) ? 1 : r.GetInt32(9),
            Color = r.IsDBNull(10) ? "" : r.GetString(10),
            Repeat = r.IsDBNull(11) ? "None" : r.GetString(11),
            Defer = r.IsDBNull(12) ? "Default" : r.GetString(12)
        };
    }

    public void Dispose() => _db?.Dispose();
}

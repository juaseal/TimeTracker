using Microsoft.Data.Sqlite;
using System.IO;
namespace TimeTracker;
public sealed class TimeRepository
{
    readonly string _connection;
    public TimeRepository()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dir);
        var database=Path.Combine(dir,"timetracker.db");
        var previous=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"TimeTracker","timetracker.db");
        if(!File.Exists(database)&&File.Exists(previous))File.Copy(previous,database);
        _connection = new SqliteConnectionStringBuilder
        {
            DataSource=database,
            Mode=SqliteOpenMode.ReadWriteCreate,
            Cache=SqliteCacheMode.Shared,
            Pooling=true,
            ForeignKeys=true,
            DefaultTimeout=5
        }.ToString();
        Initialize();
    }
    SqliteConnection Open()
    {
        var c=new SqliteConnection(_connection);c.Open();
        using var setup=c.CreateCommand();setup.CommandText="PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";setup.ExecuteNonQuery();
        return c;
    }
    void Initialize()
    {
        using var c = Open(); using var cmd = c.CreateCommand();
        cmd.CommandText = """
        PRAGMA journal_mode=WAL;
        PRAGMA synchronous=NORMAL;
        PRAGMA wal_autocheckpoint=1000;
        CREATE TABLE IF NOT EXISTS sessions(id INTEGER PRIMARY KEY, project TEXT NOT NULL, epic TEXT NOT NULL, activity TEXT NOT NULL, comment TEXT NOT NULL DEFAULT '', start TEXT NOT NULL, end TEXT NULL);
        CREATE INDEX IF NOT EXISTS ix_sessions_start ON sessions(start);
        CREATE INDEX IF NOT EXISTS ix_sessions_project_epic_start ON sessions(project, epic, start);
        CREATE INDEX IF NOT EXISTS ix_sessions_active ON sessions(start DESC) WHERE end IS NULL;
        DROP INDEX IF EXISTS ix_sessions_project;
        CREATE TABLE IF NOT EXISTS schedule_templates(id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE);
        CREATE TABLE IF NOT EXISTS schedule_template_days(template_id INTEGER NOT NULL, day_of_week INTEGER NOT NULL, hours REAL NOT NULL, PRIMARY KEY(template_id,day_of_week), FOREIGN KEY(template_id) REFERENCES schedule_templates(id) ON DELETE CASCADE);
        CREATE TABLE IF NOT EXISTS calendar_periods(id INTEGER PRIMARY KEY, name TEXT NOT NULL, start_mmdd TEXT NOT NULL, end_mmdd TEXT NOT NULL, template_id INTEGER NOT NULL, FOREIGN KEY(template_id) REFERENCES schedule_templates(id));
        CREATE INDEX IF NOT EXISTS ix_calendar_periods_template ON calendar_periods(template_id);
        CREATE TABLE IF NOT EXISTS weekly_adjustments(day TEXT NOT NULL, project TEXT NOT NULL, epic TEXT NOT NULL, hours REAL NOT NULL, PRIMARY KEY(day,project,epic));
        CREATE TABLE IF NOT EXISTS app_settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS recent_field_settings(field_key TEXT PRIMARY KEY, display_order INTEGER NOT NULL, is_visible INTEGER NOT NULL, is_bold INTEGER NOT NULL);
        INSERT OR IGNORE INTO app_settings(key,value) VALUES('week_start_day','1');
        INSERT OR IGNORE INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES('activity',0,1,1);
        INSERT OR IGNORE INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES('project',1,1,0);
        INSERT OR IGNORE INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES('epic',2,1,0);
        INSERT OR IGNORE INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES('comment',3,1,0);
        INSERT OR IGNORE INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES('time',4,1,0);
        DELETE FROM weekly_adjustments WHERE ABS(hours) < 0.000000001;
        DELETE FROM calendar_periods WHERE NOT EXISTS(SELECT 1 FROM schedule_templates WHERE id=calendar_periods.template_id);
        DELETE FROM schedule_template_days WHERE NOT EXISTS(SELECT 1 FROM schedule_templates WHERE id=schedule_template_days.template_id);
        PRAGMA user_version=3;
        PRAGMA optimize;
        """; cmd.ExecuteNonQuery();
        SeedCalendar(c);
    }
    static void SeedCalendar(SqliteConnection c)
    {
        using var count=c.CreateCommand();count.CommandText="SELECT COUNT(*) FROM schedule_templates";if(Convert.ToInt32(count.ExecuteScalar())>0)return;
        using var tx=c.BeginTransaction();
        long AddTemplate(string name,double monThu,double friday)
        {
            using var add=c.CreateCommand();add.Transaction=tx;add.CommandText="INSERT INTO schedule_templates(name) VALUES($n); SELECT last_insert_rowid();";add.Parameters.AddWithValue("$n",name);var id=Convert.ToInt64(add.ExecuteScalar());
            for(var day=1;day<=7;day++){using var d=c.CreateCommand();d.Transaction=tx;d.CommandText="INSERT INTO schedule_template_days(template_id,day_of_week,hours) VALUES($id,$day,$h)";d.Parameters.AddWithValue("$id",id);d.Parameters.AddWithValue("$day",day);d.Parameters.AddWithValue("$h",day<=4?monThu:day==5?friday:0);d.ExecuteNonQuery();}return id;
        }
        var regular=AddTemplate("Jornada septiembre-junio",8.5,6.5);var summer=AddTemplate("Jornada julio-agosto",7,6.5);
        void Period(string name,string start,string end,long template){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO calendar_periods(name,start_mmdd,end_mmdd,template_id) VALUES($n,$s,$e,$t)";q.Parameters.AddWithValue("$n",name);q.Parameters.AddWithValue("$s",start);q.Parameters.AddWithValue("$e",end);q.Parameters.AddWithValue("$t",template);q.ExecuteNonQuery();}
        Period("Jornada ordinaria (enero-junio)","01-01","06-30",regular);Period("Jornada de verano","07-01","08-31",summer);Period("Jornada ordinaria (septiembre-diciembre)","09-01","12-31",regular);tx.Commit();
    }
    public SessionRow? Active()
    {
        using var c=Open(); using var q=c.CreateCommand(); q.CommandText="SELECT id,project,epic,activity,comment,start,end FROM sessions WHERE end IS NULL ORDER BY start DESC LIMIT 1";
        using var r=q.ExecuteReader(); return r.Read()?Read(r):null;
    }
    public void Start(string project,string epic,string activity,string comment)
    {
        var now=DateTime.Now; using var c=Open(); using var tx=c.BeginTransaction();
        using(var stop=c.CreateCommand()){stop.Transaction=tx;stop.CommandText="UPDATE sessions SET end=$now WHERE end IS NULL";stop.Parameters.AddWithValue("$now",now.ToString("O"));stop.ExecuteNonQuery();}
        using(var add=c.CreateCommand()){add.Transaction=tx;add.CommandText="INSERT INTO sessions(project,epic,activity,comment,start) VALUES($p,$e,$a,$c,$s)";add.Parameters.AddWithValue("$p",project);add.Parameters.AddWithValue("$e",epic);add.Parameters.AddWithValue("$a",activity);add.Parameters.AddWithValue("$c",comment);add.Parameters.AddWithValue("$s",now.ToString("O"));add.ExecuteNonQuery();}
        tx.Commit();
    }
    public void Stop(){using var c=Open();using var q=c.CreateCommand();q.CommandText="UPDATE sessions SET end=$e WHERE end IS NULL";q.Parameters.AddWithValue("$e",DateTime.Now.ToString("O"));q.ExecuteNonQuery();}
    public List<ActivitySuggestion> Recent(string? project=null)
    {
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=string.IsNullOrWhiteSpace(project)
            ? "SELECT id,project,epic,activity,comment,start,end FROM (SELECT *,ROW_NUMBER() OVER(PARTITION BY project,epic,activity ORDER BY start DESC,id DESC) AS position FROM sessions) WHERE position=1 ORDER BY start DESC LIMIT 50"
            : "SELECT id,project,epic,activity,comment,start,end FROM (SELECT *,ROW_NUMBER() OVER(PARTITION BY project,epic,activity ORDER BY start DESC,id DESC) AS position FROM sessions WHERE project=$p) WHERE position=1 ORDER BY start DESC LIMIT 50";
        q.Parameters.AddWithValue("$p",project??"");using var r=q.ExecuteReader();var x=new List<ActivitySuggestion>();while(r.Read())x.Add(new(){Id=r.GetInt64(0),Project=r.GetString(1),Epic=r.GetString(2),Activity=r.GetString(3),Comment=r.GetString(4),Start=DateTime.Parse(r.GetString(5)),End=r.IsDBNull(6)?null:DateTime.Parse(r.GetString(6))});return x;
    }
    public List<SessionRow> Day(DateTime day){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,project,epic,activity,comment,start,end FROM sessions WHERE start >= $a AND start < $b ORDER BY start";q.Parameters.AddWithValue("$a",day.Date.ToString("O"));q.Parameters.AddWithValue("$b",day.Date.AddDays(1).ToString("O"));using var r=q.ExecuteReader();var x=new List<SessionRow>();while(r.Read())x.Add(Read(r));return x;}
    public void Save(SessionRow s)
    {
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=s.Id<=0
            ? "INSERT INTO sessions(project,epic,activity,comment,start,end) VALUES($p,$e,$a,$c,$s,$n); SELECT last_insert_rowid();"
            : "UPDATE sessions SET project=$p,epic=$e,activity=$a,comment=$c,start=$s,end=$n WHERE id=$id; SELECT $id;";
        q.Parameters.AddWithValue("$p",s.Project);q.Parameters.AddWithValue("$e",s.Epic);q.Parameters.AddWithValue("$a",s.Activity);q.Parameters.AddWithValue("$c",s.Comment);q.Parameters.AddWithValue("$s",s.Start.ToString("O"));q.Parameters.AddWithValue("$n",s.End is null?DBNull.Value:s.End.Value.ToString("O"));q.Parameters.AddWithValue("$id",s.Id);
        s.Id=Convert.ToInt64(q.ExecuteScalar());
    }
    public void Delete(long id){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM sessions WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
    public List<SummaryRow> Week(DateTime weekStart)
    {
        var start=weekStart.Date;var endExclusive=start.AddDays(7);var result=new Dictionary<(DateTime,string,string),TimeSpan>();
        foreach(var s in Range(start,endExclusive)){var end=s.End??DateTime.Now;var cursor=s.Start<start?start:s.Start;if(end>endExclusive)end=endExclusive;while(cursor<end){var boundary=cursor.Date.AddDays(1);var piece=end<boundary?end:boundary;var key=(cursor.Date,s.Project,s.Epic);result[key]=result.GetValueOrDefault(key)+(piece-cursor);cursor=piece;}}
        var adjustments=Adjustments(start,endExclusive);
        return result.OrderBy(x=>x.Key.Item1).ThenBy(x=>x.Key.Item2).Select(x=>new SummaryRow{Day=x.Key.Item1,Project=x.Key.Item2,Epic=x.Key.Item3,Registered=x.Value,Added=adjustments.GetValueOrDefault((x.Key.Item1,x.Key.Item2,x.Key.Item3))}).ToList();
    }
    Dictionary<(DateTime,string,string),double> Adjustments(DateTime a,DateTime b){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT day,project,epic,hours FROM weekly_adjustments WHERE day >= $a AND day < $b";q.Parameters.AddWithValue("$a",a.ToString("yyyy-MM-dd"));q.Parameters.AddWithValue("$b",b.ToString("yyyy-MM-dd"));using var r=q.ExecuteReader();var x=new Dictionary<(DateTime,string,string),double>();while(r.Read())x[(DateTime.Parse(r.GetString(0)),r.GetString(1),r.GetString(2))]=r.GetDouble(3);return x;}
    public void SaveAdjustment(SummaryRow row)
    {
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText=Math.Abs(row.Added)<0.000000001
            ? "DELETE FROM weekly_adjustments WHERE day=$d AND project=$p AND epic=$e"
            : "INSERT INTO weekly_adjustments(day,project,epic,hours) VALUES($d,$p,$e,$h) ON CONFLICT(day,project,epic) DO UPDATE SET hours=$h";
        q.Parameters.AddWithValue("$d",row.Day.ToString("yyyy-MM-dd"));q.Parameters.AddWithValue("$p",row.Project);q.Parameters.AddWithValue("$e",row.Epic);q.Parameters.AddWithValue("$h",row.Added);q.ExecuteNonQuery();
    }
    public List<string> TasksForSummary(SummaryRow row)
    {
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT activity,comment FROM sessions WHERE start >= $a AND start < $b AND project=$p AND epic=$e ORDER BY start";q.Parameters.AddWithValue("$a",row.Day.Date.ToString("O"));q.Parameters.AddWithValue("$b",row.Day.Date.AddDays(1).ToString("O"));q.Parameters.AddWithValue("$p",row.Project);q.Parameters.AddWithValue("$e",row.Epic);using var r=q.ExecuteReader();var result=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);while(r.Read()){var activity=r.GetString(0).Trim();var comment=r.GetString(1).Trim();var line=string.IsNullOrWhiteSpace(comment)?activity:$"{activity} — {comment}";if(!string.IsNullOrWhiteSpace(line)&&seen.Add(line))result.Add(line);}return result;
    }
    public List<WeekDayTotal> WeekDayTotals(DateTime day)
    {
        var start=day.Date;var summary=Week(start);return Enumerable.Range(0,7).Select(offset=>{var date=start.AddDays(offset);return new WeekDayTotal{Day=date,Total=TimeSpan.FromHours(summary.Where(x=>x.Day==date).Sum(x=>x.Registered.TotalHours+x.Added)),ExpectedHours=ExpectedHours(date)};}).ToList();
    }
    public List<ReportSlice> Report(DateTime from,DateTime to,string? project=null,string? epic=null,string? activity=null)
    {
        var endExclusive=to.Date.AddDays(1);var sessions=Range(from.Date,endExclusive);var grouped=sessions.Where(x=>(project is null||x.Project==project)&&(epic is null||x.Epic==epic)&&(activity is null||x.Activity==activity)).Select(x=>{var clippedStart=x.Start>from.Date?x.Start:from.Date;var sessionEnd=x.End??DateTime.Now;var clippedEnd=sessionEnd<endExclusive?sessionEnd:endExclusive;return new{Session=x,Hours=Math.Max(0d,(clippedEnd-clippedStart).TotalHours)};}).Where(x=>x.Hours>0).GroupBy(x=>project is null?x.Session.Project:epic is null?x.Session.Epic:activity is null?x.Session.Activity:x.Session.Comment).Select(g=>new ReportSlice{Label=string.IsNullOrWhiteSpace(g.Key)?activity is null?"Sin especificar":"Sin comentario":g.Key,Hours=g.Sum(x=>x.Hours)}).OrderByDescending(x=>x.Hours).ToList();var total=grouped.Sum(x=>x.Hours);foreach(var item in grouped)item.Percentage=total<=0?0:item.Hours/total*100;return grouped;
    }
    public int WeekStartDay()
    {
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT value FROM app_settings WHERE key='week_start_day'";return q.ExecuteScalar() is { } value&&int.TryParse(value.ToString(),out var day)&&day is >=1 and <=7?day:1;
    }
    public string Setting(string key,string defaultValue="")
    {
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT value FROM app_settings WHERE key=$key";q.Parameters.AddWithValue("$key",key);return q.ExecuteScalar()?.ToString()??defaultValue;
    }
    public bool BoolSetting(string key,bool defaultValue=false)=>bool.TryParse(Setting(key),out var value)?value:defaultValue;
    public void SaveSettings(IEnumerable<KeyValuePair<string,string>> settings)
    {
        using var c=Open();using var tx=c.BeginTransaction();foreach(var setting in settings){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO app_settings(key,value) VALUES($key,$value) ON CONFLICT(key) DO UPDATE SET value=$value";q.Parameters.AddWithValue("$key",setting.Key);q.Parameters.AddWithValue("$value",setting.Value);q.ExecuteNonQuery();}tx.Commit();
    }
    public List<RecentFieldSetting> RecentFieldSettings()
    {
        var labels=new Dictionary<string,string>{{"project","Proyecto"},{"epic","Épica"},{"activity","Actividad"},{"comment","Comentario"},{"time","Fecha y horario"}};
        using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT field_key,display_order,is_visible,is_bold FROM recent_field_settings ORDER BY display_order";using var r=q.ExecuteReader();var result=new List<RecentFieldSetting>();while(r.Read()){var key=r.GetString(0);if(labels.TryGetValue(key,out var label))result.Add(new(){FieldKey=key,Label=label,Order=r.GetInt32(1),IsVisible=r.GetInt32(2)!=0,IsBold=r.GetInt32(3)!=0});}return result;
    }
    public void SavePreferences(int weekStartDay,IEnumerable<RecentFieldSetting> fields)
    {
        using var c=Open();using var tx=c.BeginTransaction();using(var setting=c.CreateCommand()){setting.Transaction=tx;setting.CommandText="INSERT INTO app_settings(key,value) VALUES('week_start_day',$value) ON CONFLICT(key) DO UPDATE SET value=$value";setting.Parameters.AddWithValue("$value",Math.Clamp(weekStartDay,1,7).ToString());setting.ExecuteNonQuery();}
        var order=0;foreach(var field in fields){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO recent_field_settings(field_key,display_order,is_visible,is_bold) VALUES($key,$order,$visible,$bold) ON CONFLICT(field_key) DO UPDATE SET display_order=$order,is_visible=$visible,is_bold=$bold";q.Parameters.AddWithValue("$key",field.FieldKey);q.Parameters.AddWithValue("$order",order++);q.Parameters.AddWithValue("$visible",field.IsVisible?1:0);q.Parameters.AddWithValue("$bold",field.IsBold?1:0);q.ExecuteNonQuery();}tx.Commit();
    }
    public List<ScheduleTemplate> Templates(){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,name FROM schedule_templates ORDER BY name";using var r=q.ExecuteReader();var x=new List<ScheduleTemplate>();while(r.Read())x.Add(new(){Id=r.GetInt64(0),Name=r.GetString(1)});return x;}
    public List<ScheduleDay> TemplateDays(long id)
    {
        var names=new[]{"","Lunes","Martes","Miércoles","Jueves","Viernes","Sábado","Domingo"};using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT day_of_week,hours FROM schedule_template_days WHERE template_id=$id ORDER BY day_of_week";q.Parameters.AddWithValue("$id",id);using var r=q.ExecuteReader();var x=new List<ScheduleDay>();while(r.Read()){var day=r.GetInt32(0);x.Add(new(){DayOfWeek=day,Day=names[day],Hours=r.GetDouble(1)});}return x;
    }
    public long SaveTemplate(ScheduleTemplate template,IEnumerable<ScheduleDay> days)
    {
        using var c=Open();using var tx=c.BeginTransaction();using(var q=c.CreateCommand()){q.Transaction=tx;q.CommandText=template.Id==0?"INSERT INTO schedule_templates(name) VALUES($n); SELECT last_insert_rowid();":"UPDATE schedule_templates SET name=$n WHERE id=$id; SELECT $id;";q.Parameters.AddWithValue("$n",template.Name);q.Parameters.AddWithValue("$id",template.Id);template.Id=Convert.ToInt64(q.ExecuteScalar());}
        foreach(var day in days){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT INTO schedule_template_days(template_id,day_of_week,hours) VALUES($id,$d,$h) ON CONFLICT(template_id,day_of_week) DO UPDATE SET hours=$h";q.Parameters.AddWithValue("$id",template.Id);q.Parameters.AddWithValue("$d",day.DayOfWeek);q.Parameters.AddWithValue("$h",day.Hours);q.ExecuteNonQuery();}tx.Commit();return template.Id;
    }
    public void DeleteTemplate(long id){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM schedule_templates WHERE id=$id AND NOT EXISTS(SELECT 1 FROM calendar_periods WHERE template_id=$id)";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
    public List<CalendarPeriodRow> Periods(){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT p.id,p.name,p.start_mmdd,p.end_mmdd,p.template_id,t.name FROM calendar_periods p JOIN schedule_templates t ON t.id=p.template_id ORDER BY p.start_mmdd";using var r=q.ExecuteReader();var x=new List<CalendarPeriodRow>();while(r.Read())x.Add(new(){Id=r.GetInt64(0),Name=r.GetString(1),StartText=ToDisplayDate(r.GetString(2)),EndText=ToDisplayDate(r.GetString(3)),TemplateId=r.GetInt64(4),TemplateName=r.GetString(5)});return x;}
    public void SavePeriod(CalendarPeriodRow p){using var c=Open();using var q=c.CreateCommand();q.CommandText=p.Id==0?"INSERT INTO calendar_periods(name,start_mmdd,end_mmdd,template_id) VALUES($n,$s,$e,$t); SELECT last_insert_rowid();":"UPDATE calendar_periods SET name=$n,start_mmdd=$s,end_mmdd=$e,template_id=$t WHERE id=$id; SELECT $id;";q.Parameters.AddWithValue("$n",p.Name);q.Parameters.AddWithValue("$s",ToStorageDate(p.StartText));q.Parameters.AddWithValue("$e",ToStorageDate(p.EndText));q.Parameters.AddWithValue("$t",p.TemplateId);q.Parameters.AddWithValue("$id",p.Id);p.Id=Convert.ToInt64(q.ExecuteScalar());}
    public void DeletePeriod(long id){using var c=Open();using var q=c.CreateCommand();q.CommandText="DELETE FROM calendar_periods WHERE id=$id";q.Parameters.AddWithValue("$id",id);q.ExecuteNonQuery();}
    public double ExpectedHours(DateTime day)
    {
        using var c=Open();using var q=c.CreateCommand();
        q.CommandText="""
        SELECT d.hours
        FROM calendar_periods p
        JOIN schedule_template_days d ON d.template_id=p.template_id AND d.day_of_week=$day
        WHERE (p.start_mmdd<=p.end_mmdd AND $mmdd BETWEEN p.start_mmdd AND p.end_mmdd)
           OR (p.start_mmdd>p.end_mmdd AND ($mmdd>=p.start_mmdd OR $mmdd<=p.end_mmdd))
        ORDER BY p.start_mmdd
        LIMIT 1
        """;
        q.Parameters.AddWithValue("$mmdd",day.ToString("MM-dd"));q.Parameters.AddWithValue("$day",day.DayOfWeek==System.DayOfWeek.Sunday?7:(int)day.DayOfWeek);
        return q.ExecuteScalar() is { } value?Convert.ToDouble(value):0;
    }
    static string ToDisplayDate(string value)=>$"{value[3..5]}/{value[..2]}";static string ToStorageDate(string value){if(!DateTime.TryParseExact(value,"dd/MM",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var d))throw new FormatException("Usa el formato dd/MM.");return d.ToString("MM-dd");}
    List<SessionRow> Range(DateTime a,DateTime b){using var c=Open();using var q=c.CreateCommand();q.CommandText="SELECT id,project,epic,activity,comment,start,end FROM sessions WHERE start < $b AND COALESCE(end,$b) >= $a";q.Parameters.AddWithValue("$a",a.ToString("O"));q.Parameters.AddWithValue("$b",b.ToString("O"));using var r=q.ExecuteReader();var x=new List<SessionRow>();while(r.Read())x.Add(Read(r));return x;}
    static SessionRow Read(SqliteDataReader r)=>new(){Id=r.GetInt64(0),Project=r.GetString(1),Epic=r.GetString(2),Activity=r.GetString(3),Comment=r.GetString(4),Start=DateTime.Parse(r.GetString(5)),End=r.IsDBNull(6)?null:DateTime.Parse(r.GetString(6))};
}

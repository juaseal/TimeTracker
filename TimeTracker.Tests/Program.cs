using TimeTracker;

var failures=new List<string>();
Run("Iniciar la misma tarea no duplica sesiones",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    repo.Start("Proyecto","Épica","Actividad","Comentario");var started=repo.Active()!.Start;
    repo.Start("Proyecto","Épica","Actividad","Comentario");
    Equal(1,repo.Day(DateTime.Today).Count);Equal(started,repo.Active()!.Start);
});
Run("Cambiar el comentario crea una sesión",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    repo.Start("Proyecto","Épica","Actividad","Primero");repo.Start("Proyecto","Épica","Actividad","Segundo");
    Equal(2,repo.Day(DateTime.Today).Count);Equal("Segundo",repo.Active()!.Comment);
});
Run("Recupera el último comentario de la combinación exacta",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    repo.Start("Proyecto","Epica","Actividad","Primero");repo.Stop();
    repo.Start("Proyecto","Otra","Actividad","No corresponde");repo.Stop();
    repo.Start("Proyecto","Epica","Actividad","Ultimo");repo.Stop();
    Equal("Ultimo",repo.LastComment("Proyecto","Epica","Actividad"));
    Equal<string?>(null,repo.LastComment("Proyecto","Epica","Nueva"));
});
Run("Busca comentarios por contenido sin distinguir mayúsculas, recientes y únicos",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    void Add(int days,string comment)=>repo.Save(new SessionRow{Start=DateTime.Today.AddDays(days).AddHours(9),End=DateTime.Today.AddDays(days).AddHours(10),Project="P",Epic="E",Activity="A",Comment=comment});
    Add(-5,"Revisar SAP IS antiguo");Add(-3,"Reunión sobre SAP IS");Add(-2,"reunión sobre sap is");Add(-1,"Resolver SAP IS nuevo");
    var matches=repo.FindComments(" sap IS ");
    Equal(3,matches.Count);Equal("Resolver SAP IS nuevo",matches[0]);Equal("reunión sobre sap is",matches[1]);Equal("Revisar SAP IS antiguo",matches[2]);
    Equal(1,repo.FindComments("SAP IS",1).Count);Equal(0,repo.FindComments("   ").Count);Equal(0,repo.FindComments("inexistente").Count);
    Add(0,"Revisión ÉPICA al 100%_literal");
    Equal(1,repo.FindComments("épica").Count);Equal(1,repo.FindComments("%_").Count);Equal(0,repo.FindComments("SAP IS",0).Count);
});
Run("Recientes comparte límite, favoritos y formato configurado",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    for(var i=1;i<=5;i++){repo.Start("P"+i,"E"+i,"A"+i,"C"+i);repo.Stop();}
    var oldest=repo.Recent().Single(x=>x.Project=="P1");repo.SetFavorite(oldest,true);
    var fields=repo.RecentFieldSettings();foreach(var field in fields){field.IsVisible=field.FieldKey is "project" or "activity";field.IsBold=field.FieldKey=="project";}
    var project=fields.Single(x=>x.FieldKey=="project");fields.Remove(project);fields.Insert(0,project);
    repo.SavePreferences(1,3,fields);
    var feed=repo.RecentFeed();
    Equal(3,feed.Count);Equal("P1",feed[0].Project);True(feed[0].IsFavorite);Equal(2,feed[0].DisplayFields.Count);Equal("P1",feed[0].DisplayFields[0].Text);Equal(System.Windows.FontWeights.Bold,feed[0].DisplayFields[0].FontWeight);Equal("A1",feed[0].DisplayFields[1].Text);
});Run("Backup y CSV conservan el historial",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    repo.Start("Proyecto","Épica","Actividad","Comentario");repo.Stop();
    var backup=Path.Combine(fixture.Directory,"backup.db");var csv=Path.Combine(fixture.Directory,"history.csv");
    repo.BackupTo(backup);repo.ExportSessionsCsv(csv);
    True(File.Exists(backup));True(File.ReadAllText(csv).Contains("Project;Epic;Activity"));True(File.ReadAllText(csv).Contains("Comentario"));Equal(1,new TimeRepository(backup).Day(DateTime.Today).Count);
});
Run("La migración conserva la base portable más reciente",()=>{
    var directory=Path.Combine(Path.GetTempPath(),"TimeTracker.Tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
    try
    {
        var destination=Path.Combine(directory,"local.db");var legacy=Path.Combine(directory,"portable.db");
        var localRepo=new TimeRepository(destination);localRepo.Start("Antiguo","Épica","Actividad","Local");localRepo.Stop();
        var portableRepo=new TimeRepository(legacy);portableRepo.Start("Reciente","Épica","Actividad","Portable");portableRepo.Stop();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.SetLastWriteTimeUtc(destination,DateTime.UtcNow.AddDays(-2));File.SetLastWriteTimeUtc(legacy,DateTime.UtcNow);
        TimeRepository.MigratePortableDatabase(destination,legacy);
        var migrated=new TimeRepository(destination);
        Equal("Reciente",migrated.Day(DateTime.Today).Single().Project);True(File.Exists(destination+".pre-msix-migration.bak"));
    }
    finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
});Run("Redondeo SAP",()=>{
    Close(1.5,SapRounding.Apply(1.37,true,30));Close(1.0,SapRounding.Apply(1.24,true,30));Close(1.25,SapRounding.Apply(1.13,true,15));Close(1.37,SapRounding.Apply(1.37,false,30));
});
Run("Resoluciones SAP inválidas",()=>{
    var thrown=false;try{SapRounding.Apply(1,true,7);}catch(ArgumentOutOfRangeException){thrown=true;}True(thrown);
});

Run("Historial completo por proyecto con búsqueda literal y edición persistida",()=>{
    using var fixture=new RepositoryFixture();var repo=fixture.Repository;
    var old=new SessionRow{Start=new DateTime(2023,1,2,23,30,0),End=new DateTime(2023,1,3,1,0,0),Project="Proyecto ÉPICA 100%_",Epic="E",Activity="A",Comment="Original"};
    repo.Save(old);
    repo.Save(new SessionRow{Start=DateTime.Today.AddHours(8),End=DateTime.Today.AddHours(9),Project="Otro",Epic="E",Activity="A"});
    repo.Start("Proyecto reciente","E","A","En curso");
    Equal(3,repo.SearchHistory().Count);
    Equal(2,repo.SearchHistory(" PROYECTO ").Count);
    Equal(1,repo.SearchHistory("épica").Count);
    Equal(1,repo.SearchHistory("%_").Count);
    Equal(0,repo.SearchHistory("ausente").Count);
    Equal(true,repo.SearchHistory("reciente").Single().End is null);
    old.Start=old.Start.AddDays(2);old.End=old.End!.Value.AddDays(2);old.Project="Renombrado";old.Epic="Nueva épica";old.Activity="Nueva actividad";old.Comment="Editado";
    repo.Save(old);
    Equal(0,repo.SearchHistory("%_").Count);
    var saved=repo.SearchHistory("renombrado").Single();
    Equal(old.Id,saved.Id);Equal(old.Start,saved.Start);Equal(old.End,saved.End);
    Equal("Nueva épica",saved.Epic);Equal("Nueva actividad",saved.Activity);Equal("Editado",saved.Comment);
    Equal(old.Id,repo.SearchHistory(" NUEVA ÉPICA ").Single().Id);
    Equal(old.Id,repo.SearchHistory("nueva ACTIVIDAD").Single().Id);
    Equal(old.Id,repo.SearchHistory("editADO").Single().Id);
    Equal(1,repo.SearchHistory("en curso").Count);
    Equal(3,repo.SearchHistory().Count);
});
Run("History: edición directa, selección, pegado, fechas nocturnas y deshacer",HistoryGridTests.Verify);
Run("Calendarios legibles en temas oscuro y claro y vistas mes, año y década",CalendarThemeTests.Verify);

if(failures.Count>0){Console.Error.WriteLine(string.Join(Environment.NewLine,failures));return 1;}
Console.WriteLine("Todas las pruebas de regresión han pasado.");
return 0;

void Run(string name,Action test){try{test();Console.WriteLine($"PASS {name}");}catch(Exception ex){failures.Add($"FAIL {name}: {ex.Message}");}}
static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new InvalidOperationException($"Esperado {expected}; recibido {actual}.");}
static void True(bool value){if(!value)throw new InvalidOperationException("La condición esperada no se cumple.");}
static void Close(double expected,double actual){if(Math.Abs(expected-actual)>0.000001)throw new InvalidOperationException($"Esperado {expected}; recibido {actual}.");}

sealed class RepositoryFixture : IDisposable
{
    public string Directory { get; }=Path.Combine(Path.GetTempPath(),"TimeTracker.Tests",Guid.NewGuid().ToString("N"));
    public TimeRepository Repository { get; }
    public RepositoryFixture(){Repository=new TimeRepository(Path.Combine(Directory,"test.db"));}
    public void Dispose(){Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();if(System.IO.Directory.Exists(Directory))System.IO.Directory.Delete(Directory,true);}
}

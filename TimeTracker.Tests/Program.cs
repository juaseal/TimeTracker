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

using System.Windows;
using System.IO;
using System.Windows.Threading;
namespace TimeTracker;
public partial class App : Application
{
    string? _lastError; DateTime _lastErrorAt;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var repo=new TimeRepository();
        var startInWidget=repo.BoolSetting("start_in_widget");
        var main=new MainWindow(repo);
        MainWindow=main;
        if(startInWidget)main.ShowWidgetAtStartup();
        else main.Show();
    }
    void OnUnhandledException(object sender,DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            AppPaths.EnsureDataDirectory();
            if(File.Exists(AppPaths.LogPath)&&new FileInfo(AppPaths.LogPath).Length>1024*1024)File.Move(AppPaths.LogPath,AppPaths.LogPath+".previous",true);
            var detail=$"{e.Exception.GetType().FullName}: {e.Exception.Message}{Environment.NewLine}{e.Exception.StackTrace}";
            File.AppendAllText(AppPaths.LogPath,$"{DateTime.Now:O}{Environment.NewLine}{detail}{Environment.NewLine}{new string('-',60)}{Environment.NewLine}");
        }        catch { }
        var now=DateTime.Now;var shouldShow=e.Exception.Message!=_lastError||(now-_lastErrorAt)>TimeSpan.FromSeconds(5);_lastError=e.Exception.Message;_lastErrorAt=now;
        if(shouldShow)MessageBox.Show("An unexpected error occurred, but the application will remain open. You can find diagnostic information in Settings.","TaskUp",MessageBoxButton.OK,MessageBoxImage.Error);
        e.Handled=true;
    }
}

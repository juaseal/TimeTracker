using System.Windows;
using System.IO;
using System.Windows.Threading;
namespace TimeTracker;
public partial class App : Application
{
    string? _lastError; DateTime _lastErrorAt;
    void OnUnhandledException(object sender,DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            var dir=Path.Combine(AppContext.BaseDirectory,"data");Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir,"errors.log"),$"{DateTime.Now:O}{Environment.NewLine}{e.Exception}{Environment.NewLine}{new string('-',60)}{Environment.NewLine}");
        }
        catch { }
        var now=DateTime.Now;var shouldShow=e.Exception.Message!=_lastError||(now-_lastErrorAt)>TimeSpan.FromSeconds(5);_lastError=e.Exception.Message;_lastErrorAt=now;
        if(shouldShow)MessageBox.Show($"Se produjo un error, pero la aplicación seguirá abierta.\n\n{e.Exception.Message}\n\nEl detalle se guardó en errors.log.","TimeTracker",MessageBoxButton.OK,MessageBoxImage.Error);
        e.Handled=true;
    }
}

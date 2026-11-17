using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TimeTracker;

static class NowFormTests
{
    public static void Verify()
    {
        using var fixture=new RepositoryFixture();
        var repo=fixture.Repository;
        repo.Start("Conocimiento","Traspasos IA","Reunión","Anterior");repo.Stop();
        repo.Start("ProjectA","EpicA","ActivityA","CommentA");repo.Stop();
        repo.Start("ProjectB","EpicB","ActivityB","CommentB");repo.Stop();
        var window=new MainWindow(repo);
        try
        {
            window.Measure(new Size(1400,1000));window.Arrange(new Rect(0,0,1400,1000));window.UpdateLayout();
            var project=(ComboBox)window.FindName("ProjectBox");
            var epic=(ComboBox)window.FindName("EpicBox");
            var activity=(ComboBox)window.FindName("ActivityBox");
            var comment=(TextBox)window.FindName("CommentBox");
            void Drain()=>Dispatcher.CurrentDispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
            void Check(bool value,string message){if(!value)throw new Exception(message);}
            void Start()=>typeof(MainWindow).GetMethod("StartClick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,new object[]{window,new RoutedEventArgs()});
            void Type(ComboBox box,string value)
            {
                box.ApplyTemplate();
                var editor=(TextBox)box.Template.FindName("PART_EditableTextBox",box);
                editor.SelectAll();editor.SelectedText=value;Drain();
                Check(box.Text==value,"El texto escrito se sobrescribió: "+value+" => "+box.Text);
            }

            project.SelectedItem="Conocimiento";Drain();
            Check(activity.SelectedItem is ActivitySuggestion,"Falta la sugerencia antigua para reproducir el error.");
            Type(epic,"Reunión");comment.Text="Reunion con equipo";Start();Drain();
            Check(repo.Active()!.Epic=="Reunión","Start recuperó la épica antigua.");
            Check(repo.Active()!.Comment=="Reunion con equipo","Se perdió el comentario.");
            Check(epic.Text=="Reunión","Refrescar cambió la épica visible.");

            project.SelectedItem="Conocimiento";Drain();
            Type(project,"Proyecto completamente nuevo");
            Type(epic,"Nueva épica");Type(activity,"Nueva actividad");
            comment.Text="Comentario nuevo";Start();Drain();
            Check(repo.Active()!.Project=="Proyecto completamente nuevo","Start recuperó el proyecto antiguo.");
            Check(project.Text==repo.Active()!.Project,"Refrescar cambió el proyecto.");

            var recent=repo.RecentFeed().Single(x=>x.Epic=="Traspasos IA");
            // Leave a pending comment lookup, then load all fields atomically.
            Type(epic,"Temporal");
            activity.Text="Temporal";
            window.ResumeRecent(recent);Drain();
            Check(project.Text==recent.Project&&epic.Text==recent.Epic&&activity.Text==recent.Activity&&comment.Text==recent.Comment,"Recent tasks no rellenó todos los campos.");
            Check(repo.Active()!.Epic==recent.Epic,"Recent tasks inició otra épica.");
            var count=repo.Day(DateTime.Today).Count;
            window.ResumeRecent(recent);Drain();
            Check(repo.Day(DateTime.Today).Count==count,"Reanudar la misma tarea duplicó la sesión.");

            epic.Text="Cambio pendiente";comment.Text="Comentario escrito antes del callback";Drain();
            Check(comment.Text=="Comentario escrito antes del callback","Un callback pendiente sobrescribió el comentario.");
            project.SelectedItem="ProjectB";Drain();
            var taskA=repo.RecentFeed().Single(x=>x.Project=="ProjectA");
            window.ResumeRecent(taskA);Drain();
            Check(Equals(project.SelectedItem,"ProjectA"),"Recent tasks dejó seleccionado otro proyecto: "+project.SelectedItem);
            Check(Equals(epic.SelectedItem,"EpicA"),"Recent tasks no sincronizó la selección de épica.");
            Check(activity.SelectedItem is ActivitySuggestion selectedA&&selectedA.Project=="ProjectA"&&selectedA.Activity=="ActivityA","Recent tasks no sincronizó la selección de actividad.");
            project.SelectedItem="ProjectB";Drain();
            Check(project.Text=="ProjectB"&&epic.Text=="EpicB"&&activity.Text=="ActivityB","Volver a seleccionar ProjectB no actualizó el formulario.");
            Check(Equals(epic.SelectedItem,"EpicB"),"Cambiar proyecto dejó desincronizada la épica.");
            Start();Drain();
            Check(repo.Active()!.Project=="ProjectB"&&repo.Active()!.Epic=="EpicB","No se inició ProjectB.");
            Type(project,"Proyecto libre sin sugerencia");
            typeof(MainWindow).GetMethod("RefreshRecent",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);Drain();
            Check(project.SelectedIndex==-1&&project.Text=="Proyecto libre sin sugerencia","Refrescar dejó una selección antigua para texto libre.");
            Console.WriteLine("PASS Now: edición libre, inicio con valores visibles y tareas recientes.");
        }
        finally{window.Close();}
    }
}
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
namespace TimeTracker;
public partial class CalendarWindow : Window
{
    readonly TimeRepository _repo; CalendarPeriodRow? _selectedPeriod;
    public CalendarWindow(TimeRepository repo){_repo=repo;InitializeComponent();LoadCalendar();}
    void LoadCalendar(long? selectTemplate=null,long? selectPeriod=null){var currentTemplate=selectTemplate??(TemplateBox.SelectedItem as ScheduleTemplate)?.Id;var currentPeriod=selectPeriod??_selectedPeriod?.Id;var templates=_repo.Templates();var periods=_repo.Periods();TemplateBox.ItemsSource=templates;PeriodTemplateBox.ItemsSource=templates;PeriodsGrid.ItemsSource=periods;TemplateBox.SelectedItem=templates.FirstOrDefault(x=>x.Id==currentTemplate)??templates.FirstOrDefault();if(currentPeriod is not null)PeriodsGrid.SelectedItem=periods.FirstOrDefault(x=>x.Id==currentPeriod);if(PeriodsGrid.SelectedItem is null)PeriodModeText.Text="Selecciona un periodo o crea uno nuevo";}
    void TemplateSelected(object s,SelectionChangedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;TemplateModeText.Text=$"Editando: {t.Name}";TemplateNameBox.Text=t.Name;ScheduleDaysGrid.ItemsSource=new ObservableCollection<ScheduleDay>(_repo.TemplateDays(t.Id));}
    static ObservableCollection<ScheduleDay> EmptyWeek()=>new(new[]{"Lunes","Martes","Miércoles","Jueves","Viernes","Sábado","Domingo"}.Select((name,index)=>new ScheduleDay{DayOfWeek=index+1,Day=name,Hours=0}));
    void NewTemplateClick(object s,RoutedEventArgs e){TemplateBox.SelectedItem=null;TemplateModeText.Text="Creando una plantilla nueva";TemplateNameBox.Text="Nueva plantilla";ScheduleDaysGrid.ItemsSource=EmptyWeek();TemplateNameBox.Focus();TemplateNameBox.SelectAll();}
    void SaveTemplateClick(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(TemplateNameBox.Text)||ScheduleDaysGrid.ItemsSource is not IEnumerable<ScheduleDay> days)return;var t=TemplateBox.SelectedItem as ScheduleTemplate??new();t.Name=TemplateNameBox.Text.Trim();try{_repo.SaveTemplate(t,days);LoadCalendar(t.Id);TemplateModeText.Text=$"Guardada: {t.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo guardar la plantilla");}}
    void DeleteTemplateClick(object s,RoutedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;_repo.DeleteTemplate(t.Id);LoadCalendar();}
    void PeriodSelected(object s,SelectionChangedEventArgs e){if(PeriodsGrid.SelectedItem is not CalendarPeriodRow p)return;_selectedPeriod=p;PeriodModeText.Text=$"Editando: {p.Name}";PeriodNameBox.Text=p.Name;PeriodStartBox.Text=p.StartText;PeriodEndBox.Text=p.EndText;PeriodTemplateBox.SelectedItem=(PeriodTemplateBox.ItemsSource as IEnumerable<ScheduleTemplate>)?.FirstOrDefault(x=>x.Id==p.TemplateId);}
    void NewPeriodClick(object s,RoutedEventArgs e){_selectedPeriod=null;PeriodsGrid.SelectedItem=null;PeriodModeText.Text="Creando un periodo nuevo";PeriodNameBox.Text="Nuevo periodo";PeriodStartBox.Text="01/01";PeriodEndBox.Text="31/12";PeriodTemplateBox.SelectedIndex=0;PeriodNameBox.Focus();PeriodNameBox.SelectAll();}
    void SavePeriodClick(object s,RoutedEventArgs e){if(PeriodTemplateBox.SelectedItem is not ScheduleTemplate t)return;var p=_selectedPeriod??new();p.Name=PeriodNameBox.Text.Trim();p.StartText=PeriodStartBox.Text.Trim();p.EndText=PeriodEndBox.Text.Trim();p.TemplateId=t.Id;try{_repo.SavePeriod(p);_selectedPeriod=p;LoadCalendar(null,p.Id);PeriodModeText.Text=$"Guardado: {p.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo guardar el periodo");}}
    void DeletePeriodClick(object s,RoutedEventArgs e){if(_selectedPeriod is null)return;_repo.DeletePeriod(_selectedPeriod.Id);_selectedPeriod=null;LoadCalendar();NewPeriodClick(s,e);}
}

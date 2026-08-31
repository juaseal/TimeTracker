using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
namespace TimeTracker;
public partial class CalendarWindow : Window
{
    readonly TimeRepository _repo; CalendarPeriodRow? _selectedPeriod; ObservableCollection<RecentFieldSetting> _recentFields=new(); int _weekStartDay=1;
    static readonly WeekStartOption[] WeekDays={new(){Value=1,Name="Lunes"},new(){Value=2,Name="Martes"},new(){Value=3,Name="Miércoles"},new(){Value=4,Name="Jueves"},new(){Value=5,Name="Viernes"},new(){Value=6,Name="Sábado"},new(){Value=7,Name="Domingo"}};
    public CalendarWindow(TimeRepository repo){_repo=repo;InitializeComponent();LoadPreferences();LoadCalendar();}
    void LoadPreferences()
    {
        _weekStartDay=_repo.WeekStartDay();WeekStartBox.ItemsSource=WeekDays;WeekStartBox.SelectedValue=_weekStartDay;_recentFields=new ObservableCollection<RecentFieldSetting>(_repo.RecentFieldSettings());RecentFieldsGrid.ItemsSource=_recentFields;
        var appearance=AppearanceManager.Load(_repo);SelectTag(ThemeBox,appearance.Theme);SelectTag(PaletteBox,appearance.Palette);SelectTag(FontScaleBox,appearance.FontScale.ToString("0.##",CultureInfo.InvariantCulture));HighContrastBox.IsChecked=appearance.HighContrast;ReducedMotionBox.IsChecked=appearance.ReducedMotion;StartInWidgetBox.IsChecked=_repo.BoolSetting("start_in_widget");AlwaysOnTopBox.IsChecked=_repo.BoolSetting("always_on_top",true);
    }
    static void SelectTag(ComboBox box,string tag){box.SelectedItem=box.Items.OfType<ComboBoxItem>().FirstOrDefault(x=>string.Equals(x.Tag?.ToString(),tag,StringComparison.OrdinalIgnoreCase))??box.Items[0];}
    static string SelectedTag(ComboBox box,string fallback)=>(box.SelectedItem as ComboBoxItem)?.Tag?.ToString()??fallback;
    IEnumerable<ScheduleDay> InWeekOrder(IEnumerable<ScheduleDay> days)=>days.OrderBy(x=>(7+x.DayOfWeek-_weekStartDay)%7);
    void LoadCalendar(long? selectTemplate=null,long? selectPeriod=null){var currentTemplate=selectTemplate??(TemplateBox.SelectedItem as ScheduleTemplate)?.Id;var currentPeriod=selectPeriod??_selectedPeriod?.Id;var templates=_repo.Templates();var periods=_repo.Periods();TemplateBox.ItemsSource=templates;PeriodTemplateBox.ItemsSource=templates;PeriodsGrid.ItemsSource=periods;TemplateBox.SelectedItem=templates.FirstOrDefault(x=>x.Id==currentTemplate)??templates.FirstOrDefault();if(currentPeriod is not null)PeriodsGrid.SelectedItem=periods.FirstOrDefault(x=>x.Id==currentPeriod);if(PeriodsGrid.SelectedItem is null)PeriodModeText.Text="Selecciona un periodo o crea uno nuevo";}
    void TemplateSelected(object s,SelectionChangedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;TemplateModeText.Text=$"Editando: {t.Name}";TemplateNameBox.Text=t.Name;ScheduleDaysGrid.ItemsSource=new ObservableCollection<ScheduleDay>(InWeekOrder(_repo.TemplateDays(t.Id)));}
    ObservableCollection<ScheduleDay> EmptyWeek()=>new(InWeekOrder(WeekDays.Select(x=>new ScheduleDay{DayOfWeek=x.Value,Day=x.Name,Hours=0})));
    void NewTemplateClick(object s,RoutedEventArgs e){TemplateBox.SelectedItem=null;TemplateModeText.Text="Creando una plantilla nueva";TemplateNameBox.Text="Nueva plantilla";ScheduleDaysGrid.ItemsSource=EmptyWeek();TemplateNameBox.Focus();TemplateNameBox.SelectAll();}
    void SaveTemplateClick(object s,RoutedEventArgs e){if(string.IsNullOrWhiteSpace(TemplateNameBox.Text)||ScheduleDaysGrid.ItemsSource is not IEnumerable<ScheduleDay> days)return;var t=TemplateBox.SelectedItem as ScheduleTemplate??new();t.Name=TemplateNameBox.Text.Trim();try{_repo.SaveTemplate(t,days);LoadCalendar(t.Id);TemplateModeText.Text=$"Guardada: {t.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo guardar la plantilla");}}
    void DeleteTemplateClick(object s,RoutedEventArgs e){if(TemplateBox.SelectedItem is not ScheduleTemplate t)return;_repo.DeleteTemplate(t.Id);LoadCalendar();}
    void PeriodSelected(object s,SelectionChangedEventArgs e){if(PeriodsGrid.SelectedItem is not CalendarPeriodRow p)return;_selectedPeriod=p;PeriodModeText.Text=$"Editando: {p.Name}";PeriodNameBox.Text=p.Name;PeriodStartBox.Text=p.StartText;PeriodEndBox.Text=p.EndText;PeriodTemplateBox.SelectedItem=(PeriodTemplateBox.ItemsSource as IEnumerable<ScheduleTemplate>)?.FirstOrDefault(x=>x.Id==p.TemplateId);}
    void NewPeriodClick(object s,RoutedEventArgs e){_selectedPeriod=null;PeriodsGrid.SelectedItem=null;PeriodModeText.Text="Creando un periodo nuevo";PeriodNameBox.Text="Nuevo periodo";PeriodStartBox.Text="01/01";PeriodEndBox.Text="31/12";PeriodTemplateBox.SelectedIndex=0;PeriodNameBox.Focus();PeriodNameBox.SelectAll();}
    void SavePeriodClick(object s,RoutedEventArgs e){if(PeriodTemplateBox.SelectedItem is not ScheduleTemplate t)return;var p=_selectedPeriod??new();p.Name=PeriodNameBox.Text.Trim();p.StartText=PeriodStartBox.Text.Trim();p.EndText=PeriodEndBox.Text.Trim();p.TemplateId=t.Id;try{_repo.SavePeriod(p);_selectedPeriod=p;LoadCalendar(null,p.Id);PeriodModeText.Text=$"Guardado: {p.Name}";}catch(Exception ex){MessageBox.Show(ex.Message,"No se pudo guardar el periodo");}}
    void DeletePeriodClick(object s,RoutedEventArgs e){if(_selectedPeriod is null)return;_repo.DeletePeriod(_selectedPeriod.Id);_selectedPeriod=null;LoadCalendar();NewPeriodClick(s,e);}
    void MoveRecentField(int offset){if(RecentFieldsGrid.SelectedItem is not RecentFieldSetting item)return;var index=_recentFields.IndexOf(item);var target=index+offset;if(target<0||target>=_recentFields.Count)return;_recentFields.Move(index,target);RecentFieldsGrid.SelectedItem=item;RecentFieldsGrid.ScrollIntoView(item);}
    void MoveFieldUpClick(object s,RoutedEventArgs e)=>MoveRecentField(-1);
    void MoveFieldDownClick(object s,RoutedEventArgs e)=>MoveRecentField(1);
    void SavePreferencesClick(object s,RoutedEventArgs e)
    {
        RecentFieldsGrid.CommitEdit(DataGridEditingUnit.Cell,true);RecentFieldsGrid.CommitEdit(DataGridEditingUnit.Row,true);
        var weekStart=WeekStartBox.SelectedValue is int value?value:1;if(!_recentFields.Any(x=>x.IsVisible)){MessageBox.Show("Deja visible al menos un campo en Recientes.","Configuración");return;}
        _repo.SavePreferences(weekStart,_recentFields);
        var appearance=new AppearancePreferences{Theme=SelectedTag(ThemeBox,"light"),Palette=SelectedTag(PaletteBox,"violet"),FontScale=double.TryParse(SelectedTag(FontScaleBox,"1"),NumberStyles.Float,CultureInfo.InvariantCulture,out var scale)?scale:1,HighContrast=HighContrastBox.IsChecked==true,ReducedMotion=ReducedMotionBox.IsChecked==true};
        _repo.SaveSettings(new Dictionary<string,string>{{"appearance_theme",appearance.Theme},{"appearance_palette",appearance.Palette},{"font_scale",appearance.FontScale.ToString(CultureInfo.InvariantCulture)},{"high_contrast",appearance.HighContrast.ToString()},{"reduced_motion",appearance.ReducedMotion.ToString()},{"start_in_widget",(StartInWidgetBox.IsChecked==true).ToString()},{"always_on_top",(AlwaysOnTopBox.IsChecked==true).ToString()}});AppearanceManager.Apply(appearance);
        _weekStartDay=weekStart;var selected=(TemplateBox.SelectedItem as ScheduleTemplate)?.Id;LoadCalendar(selected);PreferencesStatusText.Text="Preferencias guardadas";
    }
}

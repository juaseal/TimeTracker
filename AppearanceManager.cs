using System.Globalization;
using System.Windows;
using System.Windows.Media;
namespace TimeTracker;
public sealed class AppearancePreferences
{
    public string Theme { get; set; } = "light";
    public string Palette { get; set; } = "violet";
    public double FontScale { get; set; } = 1;
    public bool HighContrast { get; set; }
    public bool ReducedMotion { get; set; }
}
public static class AppearanceManager
{
    public static AppearancePreferences Load(TimeRepository repo)=>new()
    {
        Theme=repo.Setting("appearance_theme","light"),Palette=repo.Setting("appearance_palette","violet"),
        FontScale=double.TryParse(repo.Setting("font_scale","1"),NumberStyles.Float,CultureInfo.InvariantCulture,out var scale)?Math.Clamp(scale,.9,1.5):1,
        HighContrast=repo.BoolSetting("high_contrast"),ReducedMotion=repo.BoolSetting("reduced_motion")
    };
    public static void Apply(AppearancePreferences preferences)
    {
        var resources=Application.Current.Resources;var dark=preferences.Theme=="dark";var contrast=preferences.HighContrast||SystemParameters.HighContrast;
        var accent=preferences.Palette switch{"blue"=>"#2563EB","green"=>"#16825D","orange"=>"#D97706",_=>"#6857E5"};
        var accentHover=preferences.Palette switch{"blue"=>"#1D4ED8","green"=>"#106B4B","orange"=>"#B45309",_=>"#5948D1"};
        Set(resources,"Accent",accent);Set(resources,"AccentHover",accentHover);Set(resources,"AccentSoft",dark?"#302B55":preferences.Palette switch{"blue"=>"#E8F0FE","green"=>"#E8F6F0","orange"=>"#FFF2DF",_=>"#EEEAFE"});
        Set(resources,"AppBackground",dark?"#0F172A":"#F3F5F9");Set(resources,"Surface",dark?"#172033":"#FFFFFF");Set(resources,"SurfaceMuted",dark?"#202A3D":"#F8F9FC");
        Set(resources,"Border",contrast?(dark?"#FFFFFF":"#111827"):(dark?"#344054":"#DDE2EA"));Set(resources,"BorderStrong",contrast?(dark?"#FFFFFF":"#111827"):(dark?"#475467":"#CBD2DD"));
        Set(resources,"TextPrimary",dark?"#F2F4F7":"#182230");Set(resources,"TextSecondary",contrast?(dark?"#FFFFFF":"#111827"):(dark?"#B3BDD0":"#667085"));
        Set(resources,"Success",dark?"#32D39A":"#16825D");Set(resources,"SuccessSoft",dark?"#153E35":"#E8F6F0");Set(resources,"Danger",dark?"#FF7188":"#C9364E");Set(resources,"DangerSoft",dark?"#4A2330":"#FDECEF");
        Set(resources,"HoverSurface",dark?"#293449":"#F2F4F7");Set(resources,"HeaderSurface",dark?"#202A3D":"#F7F8FB");Set(resources,"GridLine",dark?"#344054":"#E9EDF2");Set(resources,"AlternateRow",dark?"#1B2639":"#FBFCFE");Set(resources,"SelectionSurface",dark?"#39355F":"#E7E3FC");Set(resources,"OnAccentText","#FFFFFF");
        SetSystemBrushes(resources);
        SetFontSizes(resources,preferences.FontScale);
    }
    static void SetSystemBrushes(ResourceDictionary resources)
    {
        Alias(resources,SystemColors.ControlBrushKey,"Surface");
        Alias(resources,SystemColors.ControlLightBrushKey,"SurfaceMuted");
        Alias(resources,SystemColors.ControlLightLightBrushKey,"Surface");
        Alias(resources,SystemColors.ControlDarkBrushKey,"BorderStrong");
        Alias(resources,SystemColors.ControlDarkDarkBrushKey,"BorderStrong");
        Alias(resources,SystemColors.ControlTextBrushKey,"TextPrimary");
        Alias(resources,SystemColors.WindowBrushKey,"Surface");
        Alias(resources,SystemColors.WindowTextBrushKey,"TextPrimary");
        Alias(resources,SystemColors.HighlightBrushKey,"Accent");
        Alias(resources,SystemColors.HighlightTextBrushKey,"OnAccentText");
        Alias(resources,SystemColors.GrayTextBrushKey,"TextSecondary");
        Alias(resources,SystemColors.MenuBrushKey,"Surface");
        Alias(resources,SystemColors.MenuTextBrushKey,"TextPrimary");
    }
    static void Alias(ResourceDictionary resources,object key,string source)=>resources[key]=resources[source];
    static void SetFontSizes(ResourceDictionary resources,double scale)
    {
        resources["FontSizeTiny"]=10d*scale;
        resources["FontSizeSmall"]=11d*scale;
        resources["FontSizeCaption"]=12d*scale;
        resources["BaseFontSize"]=13d*scale;
        resources["FontSizeSubhead"]=14d*scale;
        resources["FontSizeSection"]=15d*scale;
        resources["FontSizeMedium"]=16d*scale;
        resources["FontSizeLarge"]=17d*scale;
        resources["FontSizeCardTitle"]=18d*scale;
        resources["FontSizeReportTitle"]=19d*scale;
        resources["FontSizeWindowTitle"]=20d*scale;
        resources["FontSizePageTitle"]=23d*scale;
        resources["FontSizeWidgetClock"]=32d*scale;
        resources["FontSizeClock"]=40d*scale;
    }
    static void Set(ResourceDictionary resources,string key,string color)=>resources[key]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
}

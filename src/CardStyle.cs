using System;
using Rectangle=System.Drawing.Rectangle;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;
using System.Windows.Interop;
using System.Windows.Input;
using Microsoft.Win32;
using Brush=System.Windows.Media.Brush;
using Color=System.Windows.Media.Color;
using Point=System.Windows.Point;

namespace CompactCounter {
public static class NativeWindow {
 [StructLayout(LayoutKind.Sequential)]struct MonitorInfo{public int Size;public ActiveChat.Rect Monitor,Work;public uint Flags;}
 [DllImport("user32.dll")]static extern IntPtr MonitorFromWindow(IntPtr h,uint flags);
 [DllImport("user32.dll",CharSet=CharSet.Auto)]static extern bool GetMonitorInfo(IntPtr h,ref MonitorInfo info);
 [DllImport("user32.dll")]public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]public static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
 public static bool Bounds(IntPtr h,out Rectangle r){ActiveChat.Rect b;r=Rectangle.Empty;if(h==IntPtr.Zero||!ActiveChat.GetWindowRect(h,out b)||b.Left<=-30000||b.Top<=-30000||b.Right<=b.Left||b.Bottom<=b.Top)return false;r=Rectangle.FromLTRB(b.Left,b.Top,b.Right,b.Bottom);return true;}
 public static Rectangle WorkArea(IntPtr h){var m=new MonitorInfo{Size=Marshal.SizeOf(typeof(MonitorInfo))};if(GetMonitorInfo(MonitorFromWindow(h,2),ref m))return Rectangle.FromLTRB(m.Work.Left,m.Work.Top,m.Work.Right,m.Work.Bottom);return new Rectangle(0,0,(int)SystemParameters.PrimaryScreenWidth,(int)SystemParameters.PrimaryScreenHeight);}
 public static void Attach(Window window,IntPtr owner){if(owner==IntPtr.Zero)return;var helper=new WindowInteropHelper(window);helper.Owner=owner;SetWindowPos(helper.Handle,new IntPtr(-2),0,0,0,0,0x13);}
}
public static class Ui {
 public static readonly bool Dark=IsDark();
 public static Brush Surface=B(Dark?"#252525":"#FAFAF9"),Text=B(Dark?"#F3F3F3":"#232424"),Muted=B(Dark?"#ACAFAD":"#717673"),Line=B(Dark?"#434743":"#E1E5E1"),Hover=B(Dark?"#363A36":"#ECEFEB"),Accent=B(Dark?"#A5DCC0":"#286749"),Tint=B(Dark?"#34463B":"#EAF2EC");
 static bool IsDark(){try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))return k!=null&&Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}catch{return false;}}
 static Brush B(string hex){var b=(SolidColorBrush)new BrushConverter().ConvertFromString(hex);b.Freeze();return b;}
 public static TextBlock Label(string text,double size,Brush color){return new TextBlock{Text=text,FontSize=size,Foreground=color,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};}
 public static void Theme(FrameworkElement target){
  target.Resources["Surface"]=Surface;target.Resources["Ink"]=Text;target.Resources["Muted"]=Muted;target.Resources["Line"]=Line;target.Resources["Hover"]=Hover;target.Resources["Tint"]=Tint;target.Resources["Accent"]=Accent;
  var styles=(ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
   <Style TargetType='Button'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='Transparent'/><Setter Property='Padding' Value='12,8'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='box' Background='{TemplateBinding Background}' CornerRadius='8' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Tint}'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/><Setter TargetName='box' Property='BorderThickness' Value='1'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='TextBox'><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='BorderBrush' Value='{DynamicResource Line}'/><Setter Property='Padding' Value='12,8'/><Setter Property='FontSize' Value='13'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='TextBox'><Border x:Name='box' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='9' Padding='{TemplateBinding Padding}'><Grid><ScrollViewer x:Name='PART_ContentHost'/><TextBlock x:Name='watermark' Text='{TemplateBinding Tag}' Foreground='{DynamicResource Muted}' Visibility='Collapsed' IsHitTestVisible='False'/></Grid></Border><ControlTemplate.Triggers><Trigger Property='Text' Value=''><Setter TargetName='watermark' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Accent}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ListBox'><Setter Property='Background' Value='Transparent'/><Setter Property='BorderThickness' Value='0'/><Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/><Setter Property='ScrollViewer.CanContentScroll' Value='True'/><Setter Property='VirtualizingStackPanel.IsVirtualizing' Value='True'/></Style>
   <Style TargetType='ListBoxItem'><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Margin' Value='0,0,0,4'/><Setter Property='Padding' Value='12,10'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='box' CornerRadius='10' Padding='{TemplateBinding Padding}' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Hover}'/></Trigger><Trigger Property='IsSelected' Value='True'><Setter TargetName='box' Property='Background' Value='{DynamicResource Tint}'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ToolTip'><Setter Property='Background' Value='{DynamicResource Surface}'/><Setter Property='Foreground' Value='{DynamicResource Ink}'/><Setter Property='Padding' Value='10,7'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ToolTip'><Border CornerRadius='8' Background='{TemplateBinding Background}' BorderBrush='{DynamicResource Line}' BorderThickness='1' Padding='{TemplateBinding Padding}'><ContentPresenter/></Border></ControlTemplate></Setter.Value></Setter></Style>
   <Style TargetType='ScrollBar'><Setter Property='Width' Value='8'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Track x:Name='PART_Track' IsDirectionReversed='True'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType='Thumb'><Border Margin='2' CornerRadius='3' Background='{DynamicResource Line}'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></ControlTemplate></Setter.Value></Setter></Style>
  </ResourceDictionary>");target.Resources.MergedDictionaries.Add(styles);
 }
 public static Button Button(string text,Action action){var b=new Button{Content=text,HorizontalContentAlignment=HorizontalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(b,text);b.Click+=(s,e)=>action();return b;}
 public static Border Divider(){return new Border{Height=1,Background=Line,Margin=new Thickness(4,7,4,7)};}
}
public class SurfaceWindow : Window {
 public readonly Border Surface;public readonly Grid ContentGrid=new Grid();
 public IntPtr Handle{get{return new WindowInteropHelper(this).Handle;}}
 public bool IsDisposed{get;private set;} public bool IsClosing{get;private set;}public event EventHandler FormClosed;
 public SurfaceWindow(double width,double height){
  Width=width;Height=height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Topmost=false;
  FontFamily=new FontFamily("Segoe UI, Microsoft YaHei UI");FontSize=13;Foreground=Ui.Text;Ui.Theme(this);UseLayoutRounding=true;
  Surface=new Border{Background=Ui.Surface,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(16),Margin=new Thickness(8),Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=14,Opacity=.13,ShadowDepth=3}};
  Surface.Child=ContentGrid;Content=Surface;Closed+=(s,e)=>{IsDisposed=true;if(FormClosed!=null)FormClosed(this,EventArgs.Empty);};
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape){Close();e.Handled=true;}};
 }
 protected override void OnClosing(System.ComponentModel.CancelEventArgs e){IsClosing=true;base.OnClosing(e);if(e.Cancel)IsClosing=false;}
 protected void DragHeader(System.Windows.UIElement header){header.MouseLeftButtonDown+=(s,e)=>{if(e.ButtonState==MouseButtonState.Pressed){try{DragMove();}catch(InvalidOperationException){}}};}
 public Grid Header(string title,Action close){var g=new Grid{Margin=new Thickness(22,16,18,10)};g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var t=Ui.Label(title,17,Ui.Text);t.FontWeight=FontWeights.SemiBold;g.Children.Add(t);DragHeader(t);var b=Ui.Button("×",close);b.Width=30;b.Height=30;b.Padding=new Thickness(0);b.FontSize=19;System.Windows.Automation.AutomationProperties.SetName(b,"关闭");Grid.SetColumn(b,1);g.Children.Add(b);return g;}
 public void Dispose(){if(!IsDisposed&&!IsClosing)Close();}
}
public sealed class NoticeWindow : SurfaceWindow {
 public NoticeWindow(Window owner,string title,string message):base(370,210){Title=title;Owner=owner;ShowActivated=true;WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;ContentGrid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});ContentGrid.RowDefinitions.Add(new RowDefinition());ContentGrid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});ContentGrid.Children.Add(Header(title,Close));var text=Ui.Label(message,13,Ui.Muted);text.TextWrapping=TextWrapping.Wrap;text.Margin=new Thickness(24,4,24,14);Grid.SetRow(text,1);ContentGrid.Children.Add(text);var done=Ui.Button("知道了",Close);done.Background=Ui.Tint;done.HorizontalAlignment=HorizontalAlignment.Right;done.Margin=new Thickness(20,0,22,18);Grid.SetRow(done,2);ContentGrid.Children.Add(done);}
}
}
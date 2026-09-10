using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DesktopRoach
{
    internal static class Native
    {
        public const int ExtendedStyle = -20, Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
        [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect32 rect);
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, System.Text.StringBuilder text, int count);
        [StructLayout(LayoutKind.Sequential)] public struct Rect32 { public int Left, Top, Right, Bottom; }
    }
    internal class Overlay : Window
    {
        public readonly Scene Surface;
        private IntPtr handle;
        public Overlay(Simulation world, Forms.Screen screen)
        {
            Title = "桌面蟑螂 · 覆盖层";
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
            ShowInTaskbar = false; ShowActivated = false;
            Left = screen.Bounds.Left; Top = screen.Bounds.Top; Width = screen.Bounds.Width; Height = screen.Bounds.Height;
            Surface = new Scene(world) { IsOverlay = true, Interactive = false };
            Content = Surface;
            SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(this).Handle;
                Native.SetWindowPos(handle, new IntPtr(-1), screen.Bounds.Left, screen.Bounds.Top, screen.Bounds.Width, screen.Bounds.Height, 0x10);
                SetTool(Tool.Observe);
            };
        }
        public void SetTool(Tool tool)
        {
            Surface.SelectedTool = tool; Surface.Interactive = tool != Tool.Observe;
            if (handle == IntPtr.Zero) return;
            int style = Native.GetWindowLong(handle, Native.ExtendedStyle) | Native.ToolWindow | Native.NoActivate;
            style = tool == Tool.Observe ? style | Native.Transparent : style & ~Native.Transparent;
            Native.SetWindowLong(handle, Native.ExtendedStyle, style);
            Surface.InvalidateVisual();
        }
    }
    internal class ToolPalette : Window
    {
        private readonly List<Button> buttons = new List<Button>();
        public ToolPalette(Action<Tool> select, Action hide, Action control, Action openPets)
        {
            Title = "桌面蟑螂 · 工具"; Width = 500; Height = 65; WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize; Topmost = true; ShowInTaskbar = false;
            Background = Scene.Brush("#14261D"); Foreground = Scene.Brush("#DDECE2");
            var stack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            string[] names = { "日常办公", "蟑螂拍", "杀虫喷雾", "蟑螂药", "扫把", "拖把" };
            string[] icons = { "\uE7B3", "\uE80A", "\uE9CE", "\uE7B8", "\uEA99", "\uE81B" };
            for (int i = 0; i < names.Length; i++)
            {
                Tool tool = (Tool)i;
                var button = MakeButton(icons[i], names[i]);
                if(i>0) button.Content=new Image { Source=Art.ToolFrame(tool,0),Width=37,Height=37 };
                button.Click += delegate { select(tool); };
                buttons.Add(button); stack.Children.Add(button);
            }
            var petButton=MakeButton("\uE902","桌宠小队"); petButton.Click+=delegate { openPets(); }; stack.Children.Add(petButton);
            var settings = MakeButton("\uE713", "打开控制台"); settings.Click += delegate { control(); }; stack.Children.Add(settings);
            var close = MakeButton("\uE711", "暂停并隐藏"); close.Click += delegate { hide(); }; stack.Children.Add(close);
            Content = stack;
            var area = SystemParameters.WorkArea; Left = area.Right - Width - 22; Top = area.Bottom - Height - 20;
            KeyDown += delegate(object sender, KeyEventArgs e) { if(e.Key == Key.Escape) select(Tool.Observe); };
            MouseLeftButtonDown += delegate { DragMove(); };
        }
        public void UpdateTool(Tool tool)
        {
            for(int i=0;i<buttons.Count;i++) buttons[i].Background=Scene.Brush(i==(int)tool?"#4C735D":"#233A2D");
        }
        private Button MakeButton(string icon, string tooltip)
        {
            return new Button { Content = new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 19 }, ToolTip = tooltip, Width = 46, Height = 43, Margin = new Thickness(3), Background = Scene.Brush("#233A2D"), Foreground = Scene.Brush("#DDECE2"), BorderThickness = new Thickness(0), Cursor = Cursors.Hand };
        }
    }
    internal class Controller
    {
        private readonly Application app;
        private readonly Window window;
        private readonly Scene preview;
        private readonly Simulation world;
        private readonly List<Overlay> overlays = new List<Overlay>();
        private ToolPalette palette;
        private PetWindow petWindow;
        private bool compact;
        private readonly Forms.NotifyIcon tray;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch stopwatch = Stopwatch.StartNew();
        private readonly string savePath;
        private readonly Button[] toolButtons, difficultyButtons;
        private IntPtr handle;
        private Tool selectedTool;
        private double lastFrame, uiTime, savedAt, systemCheckAt;
        private bool desktop, suspended, locked, exiting, escapeRegistered, emergencyRegistered, demoMode;
        public Controller(Application app, bool demo)
        {
            this.app = app; demoMode = demo;
            world = new Simulation(Environment.TickCount);
            savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopRoach", "ecosystem.xml");
            if(!demo && !world.Load(savePath) && world.Load(savePath+".bak")) world.LastEvent="主存档损坏，已从备份恢复";
            using (Stream xaml = Assembly.GetExecutingAssembly().GetManifestResourceStream("ControlWindow.xaml")) window = (Window)XamlReader.Load(xaml);
            app.MainWindow = window;
            Find<TextBlock>("SaveText").Text="本地存档 · v0.3";
            preview = new Scene(world); Find<Grid>("SceneHost").Children.Add(preview);
            toolButtons = new[] { "ObserveTool", "SwatterTool", "SprayTool", "BaitTool", "BroomTool", "MopTool" }.Select(Find<Button>).ToArray();
            for(int i=0;i<toolButtons.Length;i++)
            {
                Tool tool = (Tool)i; toolButtons[i].Click += delegate { SelectTool(tool); };
                if(i>0) { var panel=(StackPanel)toolButtons[i].Content; panel.Children.RemoveAt(0); panel.Children.Insert(0,new Image { Source=Art.ToolFrame(tool,0),Width=30,Height=32,Margin=new Thickness(0,0,8,0) }); }
            }
            Find<Button>("PetsButton").Click+=delegate { OpenPets(); };
            difficultyButtons = new[]{"EasyButton","NormalButton","HardButton"}.Select(Find<Button>).ToArray();
            for(int i=0;i<difficultyButtons.Length;i++) { int value=i; difficultyButtons[i].Click += delegate { world.Data.Difficulty=value; UpdateSelection(); }; }
            Find<Button>("PauseButton").Click += delegate { TogglePause(); };
            Find<Button>("DesktopButton").Click += delegate { if(desktop) StopDesktop(); else StartDesktop(); };
            Find<Button>("ResetButton").Click += delegate
            {
                if(MessageBox.Show(window,"清空当前蟑螂、污物和清理记录，并重置桌宠体力、派遣与照顾记录？","重新开始",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes) { world.Reset(); Refresh(); }
            };
            Find<CheckBox>("ObjectsCheck").Click += delegate { foreach(var overlay in overlays) overlay.Surface.ShowObjects = Find<CheckBox>("ObjectsCheck").IsChecked == true; preview.ShowObjects=Find<CheckBox>("ObjectsCheck").IsChecked==true; };
            window.KeyDown += delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) SelectTool(Tool.Observe); };
            window.SourceInitialized += delegate
            {
                handle = new WindowInteropHelper(window).Handle;
                HwndSource.FromHwnd(handle).AddHook(Hook);
                emergencyRegistered = Native.RegisterHotKey(handle, 1, 0x4000 | 0x0002 | 0x0004, 0x51);
                if(!emergencyRegistered) world.LastEvent="紧急快捷键被占用，桌面入侵不可用";
            };
            window.Closing += delegate { Exit(); };
            tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "桌面蟑螂", Visible = true };
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开控制台",null,delegate { OpenControl(); });
            menu.Items.Add("桌宠小队",null,delegate { OpenPets(); });
            menu.Items.Add("暂停 / 继续",null,delegate { TogglePause(); });
            menu.Items.Add("暂停并隐藏",null,delegate { EmergencyHide(); });
            menu.Items.Add("退出",null,delegate { window.Close(); });
            tray.ContextMenuStrip = menu; tray.DoubleClick += delegate { OpenControl(); };
            SystemEvents.SessionSwitch += SessionChanged; SystemEvents.PowerModeChanged += PowerChanged; SystemEvents.DisplaySettingsChanged += DisplayChanged;
            timer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += Tick; timer.Start();
            SelectTool(Tool.Observe); window.Show(); Refresh();
            if (demo)
            {
                world.Data.Pollution = 18;
                world.Add(ItemKind.Egg,870,220); world.Add(ItemKind.Stain,1010,540); world.Spawn(true,560,500);
                world.DispatchPet(0,PetJob.Hunt); world.DispatchPet(2,PetJob.Clean);
                world.Data.Pets[0].Energy=72; world.Data.Pets[2].Energy=54;
            }
        }
        private T Find<T>(string name) where T : class { return window.FindName(name) as T; }
        private IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message==0x0312)
            {
                if(wParam.ToInt32()==1) EmergencyHide();
                if(wParam.ToInt32()==2) SelectTool(Tool.Observe);
                handled=true;
            }
            return IntPtr.Zero;
        }
        private void SelectTool(Tool tool)
        {
            if(escapeRegistered) { Native.UnregisterHotKey(handle,2); escapeRegistered=false; }
            if(desktop && tool!=Tool.Observe)
            {
                escapeRegistered = Native.RegisterHotKey(handle,2,0x4000,0x1B);
                if(!escapeRegistered) { world.LastEvent="Esc 快捷键被占用，已保持日常办公模式"; tool=Tool.Observe; }
            }
            selectedTool=tool; preview.SelectedTool=tool;
            foreach(var overlay in overlays) overlay.SetTool(tool);
            if(palette!=null) palette.UpdateTool(tool);
            UpdateSelection();
        }
        private void UpdateSelection()
        {
            for(int i=0;i<toolButtons.Length;i++)
            {
                bool active=i==(int)selectedTool;
                toolButtons[i].Background=Scene.Brush(active?"#294B38":"#001B2924");
                toolButtons[i].Foreground=Scene.Brush(active?"#B4EDCA":"#ABBFB0");
                toolButtons[i].BorderBrush=Scene.Brush(active?"#4C7157":"#001B2924");
            }
            for(int i=0;i<difficultyButtons.Length;i++)
            { difficultyButtons[i].Background=Scene.Brush(i==world.Data.Difficulty?"#B1DEC2":"#1B2924"); difficultyButtons[i].Foreground=Scene.Brush(i==world.Data.Difficulty?"#153623":"#ABBFB0"); }
        }
        private void StartDesktop()
        {
            if(!emergencyRegistered) { world.LastEvent="无法注册 Ctrl+Shift+Q，请释放该快捷键后重新启动"; Refresh(); return; }
            desktop=true; world.Paused=false;
            foreach(var screen in Forms.Screen.AllScreens)
            {
                var overlay = new Overlay(world,screen); overlays.Add(overlay); overlay.Surface.ShowObjects = Find<CheckBox>("ObjectsCheck").IsChecked==true; overlay.Show();
            }
            palette=new ToolPalette(SelectTool,EmergencyHide,OpenControl,OpenPets); palette.Show();
            SelectTool(Tool.Observe);
            Find<TextBlock>("DesktopButtonText").Text="返回演练场";
            Find<TextBlock>("ModeText").Text="桌面入侵中";
            world.LastEvent="蟑螂已进入桌面"; window.WindowState=WindowState.Minimized; Refresh();
        }
        private void StopDesktop()
        {
            desktop=false;
            foreach(var overlay in overlays) overlay.Close(); overlays.Clear();
            if(palette!=null) { palette.Close(); palette=null; }
            SelectTool(Tool.Observe); suspended=false;
            Find<TextBlock>("DesktopButtonText").Text="开启桌面入侵"; Find<TextBlock>("ModeText").Text="演练模式";
        }
        private void OpenControl()
        {
            SelectTool(Tool.Observe); window.Show(); window.WindowState=WindowState.Normal; window.Activate();
        }
        public void OpenPets()
        {
            OpenControl();
            if(petWindow==null) { petWindow=new PetWindow(window,world); if(compact) petWindow.SetCompactSize(); petWindow.Closed+=delegate { petWindow=null; }; petWindow.Show(); }
            else { petWindow.WindowState=WindowState.Normal; petWindow.Activate(); }
        }
        private void EmergencyHide() { world.Paused=true; StopDesktop(); world.LastEvent="已暂停，桌面覆盖层已隐藏"; OpenControl(); Refresh(); }
        private void TogglePause() { world.Paused=!world.Paused; if(world.Paused) SelectTool(Tool.Observe); Refresh(); }
        private void Tick(object sender,EventArgs e)
        {
            double now=stopwatch.Elapsed.TotalSeconds; double dt=Math.Min(.1,now-lastFrame); lastFrame=now;
            if(now-systemCheckAt>1)
            {
                systemCheckAt=now;
                bool shouldSuspend=locked || desktop && Find<CheckBox>("FullscreenCheck").IsChecked==true && IsFullscreen();
                if(shouldSuspend!=suspended)
                {
                    suspended=shouldSuspend;
                    if(suspended) SelectTool(Tool.Observe);
                    foreach(var overlay in overlays) { if(suspended) overlay.Hide(); else overlay.Show(); }
                    if(palette!=null) { if(suspended) palette.Hide(); else palette.Show(); }
                }
            }
            if(!suspended) world.Tick(dt);
            if(window.WindowState!=WindowState.Minimized) preview.Frame(dt);
            if(!suspended) foreach(var overlay in overlays) overlay.Surface.Frame(dt);
            if(now-uiTime>.2) { uiTime=now; Refresh(); }
            if(now-savedAt>30) { savedAt=now; Save(); }
        }
        private bool IsFullscreen()
        {
            IntPtr foreground=Native.GetForegroundWindow();
            if(foreground==handle || overlays.Any(o=>new WindowInteropHelper(o).Handle==foreground) || palette!=null && new WindowInteropHelper(palette).Handle==foreground) return false;
            var name=new System.Text.StringBuilder(256); Native.GetClassName(foreground,name,name.Capacity);
            if(name.ToString()=="Progman" || name.ToString()=="WorkerW") return false;
            Native.Rect32 rect;
            if(!Native.GetWindowRect(foreground,out rect)) return false;
            var bounds=Forms.Screen.FromHandle(foreground).Bounds;
            return rect.Left<=bounds.Left && rect.Top<=bounds.Top && rect.Right>=bounds.Right && rect.Bottom>=bounds.Bottom;
        }
        private void Refresh()
        {
            if(petWindow!=null) petWindow.Refresh();
            Find<TextBlock>("RoachCount").Text=world.Data.Roaches.Count.ToString("00");
            Find<TextBlock>("BabyCount").Text="幼虫 "+world.Data.Roaches.Count(r=>r.Baby);
            Find<TextBlock>("EggCount").Text=world.Data.Items.Count(i=>i.Kind==ItemKind.Egg).ToString("00");
            Find<TextBlock>("DirtCount").Text=world.Data.Items.Count(i=>i.Kind!=ItemKind.Egg && i.Kind!=ItemKind.Bait).ToString("00");
            Find<TextBlock>("PollutionText").Text=world.Data.Pollution.ToString("0")+"%";
            Find<ProgressBar>("PollutionBar").Value=world.Data.Pollution;
            Find<TextBlock>("HealthText").Text=world.Data.Pollution<25?"桌面清爽":world.Data.Pollution<60?"需要清洁":"污染严重";
            Find<TextBlock>("SessionScore").Text=world.Data.Kills+" 击杀 / "+world.Data.Cleaned+" 清扫";
            Find<TextBlock>("ElapsedText").Text=TimeSpan.FromSeconds(world.Data.Elapsed).ToString(@"hh\:mm\:ss");
            Find<TextBlock>("EventText").Text=world.LastEvent;
            Find<TextBlock>("PauseIcon").Text=world.Paused?"\uE768":"\uE769";
            Find<TextBlock>("ActivityText").Text=suspended?"环境已休眠":world.Paused?"生态已暂停":"生态运行中";
        }
        private void Save()
        {
            if(demoMode) return;
            try { world.Save(savePath); Find<TextBlock>("SaveText").Text="已保存 "+DateTime.Now.ToString("HH:mm")+" · v0.3"; }
            catch(IOException) { Find<TextBlock>("SaveText").Text="保存失败"; }
            catch(UnauthorizedAccessException) { Find<TextBlock>("SaveText").Text="保存失败"; }
            catch(InvalidOperationException) { Find<TextBlock>("SaveText").Text="保存失败"; }
        }
        private void SessionChanged(object sender,SessionSwitchEventArgs e) { app.Dispatcher.BeginInvoke(new Action(delegate { if(e.Reason==SessionSwitchReason.SessionLock) locked=true; else if(e.Reason==SessionSwitchReason.SessionUnlock) locked=false; })); }
        private void PowerChanged(object sender,PowerModeChangedEventArgs e) { app.Dispatcher.BeginInvoke(new Action(delegate { if(e.Mode==PowerModes.Suspend) locked=true; else if(e.Mode==PowerModes.Resume) locked=false; })); }
        private void DisplayChanged(object sender,EventArgs e) { app.Dispatcher.BeginInvoke(new Action(delegate { if(desktop) { EmergencyHide(); world.LastEvent="显示器配置已更改，覆盖层已暂停"; } })); }
        private void Exit()
        {
            if(exiting) return; exiting=true; timer.Stop(); Save(); StopDesktop();
            if(emergencyRegistered) Native.UnregisterHotKey(handle,1);
            SystemEvents.SessionSwitch-=SessionChanged; SystemEvents.PowerModeChanged-=PowerChanged; SystemEvents.DisplaySettingsChanged-=DisplayChanged;
            tray.Visible=false; tray.Dispose(); app.Shutdown();
        }
        public void SetCompactSize() { compact=true; window.Width=920; window.Height=670; if(petWindow!=null) petWindow.SetCompactSize(); }
        public void VerifyPetCommands() { OpenPets(); petWindow.VerifyCommands(); }
        public void ExportPreview(string path)
        {
            window.UpdateLayout(); preview.InvalidateVisual();
            var root=(FrameworkElement)(petWindow!=null?petWindow.Content:window.Content);
            root.UpdateLayout();
            var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);
            var visual=new DrawingVisual();
            using(var dc=visual.RenderOpen()) dc.DrawRectangle(new VisualBrush(root),null,new Rect(0,0,root.ActualWidth,root.ActualHeight));
            bitmap.Render(visual);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using(var file=File.Create(path)) encoder.Save(file);
        }
    }
    internal static class Program
    {
        [STAThread] public static int Main(string[] args)
        {
            if(args.Contains("--self-test")) return SelfTests.Run();
            int export=Array.IndexOf(args,"--export-assets");
            if(export>=0 && args.Length>export+1) { Art.Export(Path.GetFullPath(args[export+1])); return 0; }
            int skills=Array.IndexOf(args,"--export-skills");
            if(skills>=0 && args.Length>skills+1) { Art.ExportSkills(Path.GetFullPath(args[skills+1]),.5); return 0; }
            bool created;
            using(var mutex=new Mutex(true,"Local\\DesktopRoach.App",out created))
            {
                if(!created) { MessageBox.Show("桌面蟑螂已在运行，请从系统托盘打开控制台。","桌面蟑螂"); return 0; }
                Native.SetProcessDPIAware();
                var app=new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e)
                {
                    try { File.WriteAllText(Path.Combine(Path.GetTempPath(),"DesktopRoach-error.log"),e.Exception.ToString()); } catch(IOException) { }
                    e.Handled=true; MessageBox.Show("程序发生异常，桌面覆盖层将退出。\n"+e.Exception.Message,"桌面蟑螂"); app.Shutdown(1);
                };
                try
                {
                    var controller=new Controller(app,args.Contains("--demo") || args.Contains("--snapshot") || args.Contains("--ui-test"));
                    if(args.Contains("--compact")) controller.SetCompactSize();
                    if(args.Contains("--pets")) controller.OpenPets();
                    if(args.Contains("--ui-test"))
                    {
                        controller.VerifyPetCommands();
                        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-test-results.txt"),"PASS: WPF routed button events for dispatch, feed, care, company, recall, selection. No native OS input was exercised.");
                        app.Shutdown(); return 0;
                    }
                    int shot=Array.IndexOf(args,"--snapshot");
                    if(shot>=0 && args.Length>shot+1)
                    {
                        var capture=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
                        capture.Tick+=delegate { capture.Stop(); controller.ExportPreview(Path.GetFullPath(args[shot+1])); app.Shutdown(); }; capture.Start();
                    }
                    app.Run();
                }
                catch(Exception ex) { File.WriteAllText(Path.Combine(Path.GetTempPath(),"DesktopRoach-error.log"),ex.ToString()); return 1; }
                mutex.ReleaseMutex(); return 0;
            }
        }
    }
}

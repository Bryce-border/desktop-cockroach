using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace DesktopRoach
{
    internal static class DesktopPlacement
    {
        public static void Place(Window window,Point anchor)
        {
            var source=PresentationSource.FromVisual(window);
            if(source==null || source.CompositionTarget==null) return;
            var matrix=source.CompositionTarget.TransformFromDevice;
            var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)anchor.X,(int)anchor.Y)).WorkingArea;
            Point topLeft=matrix.Transform(new Point(area.Left,area.Top)),bottomRight=matrix.Transform(new Point(area.Right,area.Bottom));
            Point desired=matrix.Transform(anchor);
            window.MaxHeight=Math.Max(100,bottomRight.Y-topLeft.Y-16);
            window.Left=Simulation.Clamp(desired.X-window.Width,topLeft.X+8,Math.Max(topLeft.X+8,bottomRight.X-window.Width-8));
            window.Top=Simulation.Clamp(desired.Y-window.ActualHeight-10,topLeft.Y+8,Math.Max(topLeft.Y+8,bottomRight.Y-window.ActualHeight-8));
        }
    }
    internal class PetQuickWindow : Window
    {
        private readonly Simulation world;
        private readonly TextBlock heading,status,notice;
        private readonly StackPanel list;
        private readonly Dictionary<string,Button> actions=new Dictionary<string,Button>();
        private int selected;
        private bool closing,closed;
        private DispatcherOperation pendingDismiss;
        internal bool IsUnavailable { get { return closing || closed; } }
        public PetQuickWindow(Simulation simulation,int id,ResourceDictionary resources)
        {
            world=simulation; selected=id;
            Title="桌面蟑螂 · 召唤与任务"; Width=340; SizeToContent=SizeToContent.Height;
            WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; ShowInTaskbar=false; Topmost=true;
            Background=Scene.Brush("#192521"); Foreground=Scene.Brush("#E7EFEB"); FontFamily=new FontFamily("Microsoft YaHei UI"); Resources=resources;
            var body=new StackPanel { Margin=new Thickness(14) };
            var header=new DockPanel();
            var close=IconButton("\uE711","关闭"); close.Click+=delegate { Dismiss(); }; DockPanel.SetDock(close,Dock.Right); header.Children.Add(close);
            heading=new TextBlock { FontSize=17,VerticalAlignment=VerticalAlignment.Center }; header.Children.Add(heading); body.Children.Add(header);
            list=new StackPanel { Margin=new Thickness(0,10,0,8) }; body.Children.Add(list);
            if(id<0)
                for(int i=0;i<6;i++)
                {
                    int petId=i; var row=new DockPanel();
                    row.Children.Add(new Image { Source=Art.Pet(i),Width=37,Height=42,Margin=new Thickness(0,0,10,0) });
                    row.Children.Add(new TextBlock { Tag=petId,VerticalAlignment=VerticalAlignment.Center,FontSize=12 });
                    var button=new Button { Content=row,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(8,3,8,3),Margin=new Thickness(0,0,0,4),ToolTip=PetCatalog.Skills[i] };
                    button.Click+=delegate { selected=petId; Refresh(); }; list.Children.Add(button);
                }
            status=new TextBlock { TextWrapping=TextWrapping.Wrap,Foreground=Scene.Brush("#B5CEBE"),Margin=new Thickness(0,0,0,10),FontSize=12,Height=32 }; body.Children.Add(status);
            var jobs=new System.Windows.Controls.Primitives.UniformGrid { Columns=3 };
            AddAction(jobs,"hunt","\uE80A","灭虫",delegate { world.DispatchPet(selected,PetJob.Hunt); });
            AddAction(jobs,"clean","\uEA99","清扫",delegate { world.DispatchPet(selected,PetJob.Clean); });
            AddAction(jobs,"rest","\uE769","休息",delegate { world.DispatchPet(selected,PetJob.Rest); }); body.Children.Add(jobs);
            var care=new System.Windows.Controls.Primitives.UniformGrid { Columns=3,Margin=new Thickness(0,6,0,0) };
            AddAction(care,"feed","\uE7F4","喂食",delegate { world.FeedPet(selected); });
            AddAction(care,"care","\uE734","照顾",delegate { world.CarePet(selected); });
            AddAction(care,"recall","\uE72B","召回",delegate { world.RecallPet(selected); }); body.Children.Add(care);
            notice=new TextBlock { TextWrapping=TextWrapping.Wrap,FontSize=11,Foreground=Scene.Brush("#99B6A6"),Margin=new Thickness(0,10,0,0),Height=34 }; body.Children.Add(notice);
            Content=new ScrollViewer { Background=Background,Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
            KeyDown+=delegate(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { Dismiss(); e.Handled=true; } };
            Refresh();
        }
        internal void Dismiss()
        {
            if(!IsUnavailable) Close();
        }
        protected override void OnClosing(CancelEventArgs e)
        {
            // Closing an active WPF window emits Deactivated before Closed.
            closing=true;
            CancelPendingDismiss();
            base.OnClosing(e);
            if(e.Cancel) closing=false;
        }
        protected override void OnClosed(EventArgs e)
        {
            closed=true; CancelPendingDismiss(); base.OnClosed(e);
        }
        protected override void OnActivated(EventArgs e)
        {
            CancelPendingDismiss(); base.OnActivated(e);
        }
        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            if(IsUnavailable || pendingDismiss!=null || Dispatcher.HasShutdownStarted) return;
            pendingDismiss=Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate
            {
                pendingDismiss=null;
                if(!IsUnavailable && !IsActive) Dismiss();
            }));
        }
        private void CancelPendingDismiss()
        {
            if(pendingDismiss!=null) { pendingDismiss.Abort(); pendingDismiss=null; }
        }
        internal void ShowAt(Point anchor)
        {
            if(IsUnavailable) return;
            Show();
            if(IsUnavailable) return;
            UpdateLayout(); DesktopPlacement.Place(this,anchor);
            if(!IsUnavailable) Activate();
        }
        internal static Button IconButton(string icon,string tooltip)
        {
            return new Button { Content=new TextBlock { Text=icon,FontFamily=new FontFamily("Segoe MDL2 Assets"),FontSize=16 },ToolTip=tooltip,Width=32,Height=32,Padding=new Thickness(0) };
        }
        private void AddAction(Panel panel,string key,string icon,string name,Action command)
        {
            var content=new StackPanel { Orientation=Orientation.Horizontal };
            content.Children.Add(new TextBlock { Text=icon,FontFamily=new FontFamily("Segoe MDL2 Assets"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0) });
            content.Children.Add(new TextBlock { Text=name });
            var button=new Button { Content=content,Height=34,Padding=new Thickness(4),Margin=new Thickness(0,0,4,0) };
            button.Click+=delegate { if(!IsUnavailable && selected>=0) command(); Refresh(); }; actions.Add(key,button); panel.Children.Add(button);
        }
        public void Refresh()
        {
            if(IsUnavailable) return;
            heading.Text="桌宠 · "+world.Data.Pets.Count(p=>p.Deployed)+" / 2 已派出";
            foreach(Button row in list.Children)
            {
                var text=(TextBlock)((DockPanel)row.Content).Children[1]; int id=(int)text.Tag; var p=world.Data.Pets[id];
                text.Text=PetCatalog.Names[id]+"  ·  体力 "+p.Energy.ToString("0")+"\n"+(p.Deployed?JobName(p.Job)+" · "+p.Status:"待命");
                row.Background=Scene.Brush(selected==id?"#385942":"#23312B");
            }
            foreach(var b in actions.Values) b.IsEnabled=selected>=0;
            if(selected<0) { status.Text="未选择桌宠"; notice.Text=world.LastEvent; return; }
            var pet=world.Data.Pets[selected];
            status.Text=PetCatalog.Names[selected]+" · 体力 "+pet.Energy.ToString("0")+" · "+(pet.Deployed?JobName(pet.Job):"待命");
            bool slot=pet.Deployed||world.Data.Pets.Count(p=>p.Deployed)<2;
            actions["hunt"].IsEnabled=actions["clean"].IsEnabled=!world.Paused&&slot&&(pet.Id==2||pet.Energy>=10);
            actions["rest"].IsEnabled=!world.Paused&&slot;
            actions["recall"].IsEnabled=pet.Deployed;
            actions["feed"].IsEnabled=!world.Paused&&pet.FeedCooldown<=0&&(pet.Energy<100||pet.Hunger<100);
            actions["care"].IsEnabled=!world.Paused&&pet.CareCooldown<=0&&(pet.Energy<100||pet.Affection<100);
            actions["feed"].ToolTip=pet.FeedCooldown>0?"冷却 "+Math.Ceiling(pet.FeedCooldown)+" 秒":"恢复体力与饱食";
            actions["care"].ToolTip=pet.CareCooldown>0?"冷却 "+Math.Ceiling(pet.CareCooldown)+" 秒":"恢复体力与亲密";
            notice.Text=world.Paused?"生态已暂停":!slot?"已派满两只，可先召回一只":world.LastEvent;
        }
        private static string JobName(PetJob job) { return job==PetJob.Hunt?"灭虫":job==PetJob.Clean?"清扫":"休息"; }
        internal void VerifyCommands()
        {
            selected=0; world.RecallPet(0); world.Data.Pets[0].Energy=60; Refresh();
            actions["hunt"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Job!=PetJob.Hunt||!world.Data.Pets[0].Deployed) throw new InvalidOperationException("Quick summon failed");
            actions["clean"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Job!=PetJob.Clean) throw new InvalidOperationException("Quick task change failed");
            actions["rest"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Job!=PetJob.Rest) throw new InvalidOperationException("Quick rest failed");
            actions["recall"].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Deployed||actions["recall"].IsEnabled) throw new InvalidOperationException("Quick recall failed");
        }
        internal static void VerifyLifecycle(Simulation world,ResourceDictionary resources)
        {
            for(int i=0;i<8;i++)
            {
                var popup=new PetQuickWindow(world,0,resources);
                int closedCount=0; popup.Closed+=delegate { closedCount++; };
                // Reproduce the nested loss-of-focus notification from the reported stack trace.
                popup.Closing+=delegate { popup.OnDeactivated(EventArgs.Empty); popup.Dismiss(); };
                popup.ShowAt(new Point(600,600));
                popup.VerifyCommands();
                popup.Dismiss(); popup.Dismiss(); popup.Refresh(); popup.ShowAt(new Point(600,600));
                if(closedCount!=1 || !popup.IsUnavailable) throw new InvalidOperationException("Popup closing must be idempotent");
            }
            var pending=new PetQuickWindow(world,0,resources);
            pending.OnDeactivated(EventArgs.Empty);
            if(pending.pendingDismiss==null) throw new InvalidOperationException("Loss of focus must defer closing");
            pending.OnActivated(EventArgs.Empty);
            if(pending.pendingDismiss!=null) throw new InvalidOperationException("Reactivation must cancel pending close");
            pending.OnDeactivated(EventArgs.Empty); pending.Dismiss();
            if(pending.pendingDismiss!=null || !pending.IsUnavailable) throw new InvalidOperationException("Explicit closing must cancel deferred callback");
            var deferred=new PetQuickWindow(world,0,resources);
            deferred.OnDeactivated(EventArgs.Empty);
            var frame=new DispatcherFrame();
            deferred.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate { frame.Continue=false; }));
            Dispatcher.PushFrame(frame);
            if(!deferred.IsUnavailable) { deferred.Dismiss(); throw new InvalidOperationException("Deferred loss-of-focus callback must close the popup"); }
        }
    }
    // Small no-activate windows receive clicks only over pet bodies; the full overlay stays click-through.
    internal class PetHitWindow : Window
    {
        public readonly int PetId;
        public readonly Overlay Overlay;
        public PetHitWindow(Overlay overlay,int id,Action<int,Point> clicked)
        {
            Overlay=overlay; PetId=id; Width=88; Height=106; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Scene.Brush("#01000000"); Topmost=true; ShowInTaskbar=false; ShowActivated=false;
            Title="桌面蟑螂 · 桌宠点击区域"; Cursor=Cursors.Hand;
            SourceInitialized+=delegate
            {
                var handle=new WindowInteropHelper(this).Handle;
                Native.SetWindowLong(handle,Native.ExtendedStyle,Native.GetWindowLong(handle,Native.ExtendedStyle)|Native.ToolWindow|Native.NoActivate);
            };
            MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) { clicked(PetId,PointToScreen(e.GetPosition(this))); e.Handled=true; };
        }
        public void Follow()
        {
            Rect bounds=Overlay.Surface.PetBounds(PetId);
            Point a=Overlay.Surface.PointToScreen(bounds.TopLeft),b=Overlay.Surface.PointToScreen(bounds.BottomRight);
            Native.SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),(int)a.X,(int)a.Y,Math.Max(1,(int)(b.X-a.X)),Math.Max(1,(int)(b.Y-a.Y)),0x10);
        }
    }
}

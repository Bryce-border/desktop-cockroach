using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace DesktopRoach
{
    internal class PetWindow : Window
    {
        private readonly Simulation world;
        private readonly Button[] cards=new Button[6];
        private readonly TextBlock[] cardStatus=new TextBlock[6];
        private readonly ProgressBar[] cardEnergy=new ProgressBar[6];
        private readonly TextBlock title,stats,team,message,skill;
        private readonly Image portrait;
        private readonly ComboBox job;
        private readonly Button dispatch,recall,feed,care,company;
        private int selected;
        public PetWindow(Window owner,Simulation simulation)
        {
            world=simulation; Owner=owner; Title="桌宠小队 · 桌面蟑螂";
            Width=920; Height=775; MinWidth=820; MinHeight=720; WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=Scene.Brush("#191C1D"); Foreground=Scene.Brush("#E7EFEB"); FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=13;
            Resources=owner.Resources;
            var root=new Grid { Margin=new Thickness(24),Background=Scene.Brush("#191C1D") };
            root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            var header=new Grid { Margin=new Thickness(0,0,0,18) };
            header.Children.Add(new TextBlock { Text="桌宠小队",FontSize=26,FontWeight=FontWeights.SemiBold });
            team=new TextBlock { HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Foreground=Scene.Brush("#A6C9B3") }; header.Children.Add(team); root.Children.Add(header);
            var gallery=new UniformGrid { Columns=3,Rows=2,Margin=new Thickness(-5,0,-5,16) }; Grid.SetRow(gallery,1); root.Children.Add(gallery);
            for(int i=0;i<6;i++)
            {
                int id=i;
                var content=new Grid(); content.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });
                content.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto }); content.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
                content.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
                var image=new Image { Source=Art.Pet(i),Height=88,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,0,4) }; content.Children.Add(image);
                var label=new TextBlock { Text=PetCatalog.Names[i],FontSize=16,FontWeight=FontWeights.SemiBold,HorizontalAlignment=HorizontalAlignment.Center }; Grid.SetRow(label,1); content.Children.Add(label);
                cardStatus[i]=new TextBlock { FontSize=11,Foreground=Scene.Brush("#B5C8BA"),HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(0,5,0,7) }; Grid.SetRow(cardStatus[i],2); content.Children.Add(cardStatus[i]);
                cardEnergy[i]=new ProgressBar { Height=4,Maximum=100,BorderThickness=new Thickness(0),Background=Scene.Brush("#3B4741"),Foreground=Scene.Brush("#9CDBB8") }; Grid.SetRow(cardEnergy[i],3); content.Children.Add(cardEnergy[i]);
                cards[i]=new Button { Content=content,Margin=new Thickness(5),Padding=new Thickness(12,8,12,10),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch,ToolTip=PetCatalog.Skills[i] };
                cards[i].Click+=delegate { selected=id; SyncJob(); Refresh(); }; gallery.Children.Add(cards[i]);
            }
            var details=new Grid { Margin=new Thickness(0,0,0,12) };
            details.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(100) }); details.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
            portrait=new Image { Width=88,Height=108,Stretch=Stretch.Uniform }; details.Children.Add(portrait);
            var body=new StackPanel { Margin=new Thickness(16,0,0,0) }; Grid.SetColumn(body,1); details.Children.Add(body);
            title=new TextBlock { FontSize=19,FontWeight=FontWeights.SemiBold }; body.Children.Add(title);
            stats=new TextBlock { Foreground=Scene.Brush("#B0C4B6"),FontSize=12,Margin=new Thickness(0,7,0,12) }; body.Children.Add(stats);
            skill=new TextBlock { Foreground=Scene.Brush("#DBC49A"),FontSize=11,TextWrapping=TextWrapping.Wrap,MinHeight=32,Margin=new Thickness(0,0,0,8) }; body.Children.Add(skill);
            var jobs=new StackPanel { Orientation=Orientation.Horizontal };
            job=new ComboBox { Width=118,Height=36,FontSize=13,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,9,0),ItemsSource=new[]{"休息","消灭蟑螂","打扫卫生"} }; jobs.Children.Add(job);
            dispatch=Command("\uE768","派出",delegate { world.DispatchPet(selected,(PetJob)job.SelectedIndex); }); jobs.Children.Add(dispatch);
            recall=Command("\uE72B","召回",delegate { world.RecallPet(selected); }); jobs.Children.Add(recall);
            body.Children.Add(jobs); Grid.SetRow(details,2); root.Children.Add(details);
            var bottom=new StackPanel(); Grid.SetRow(bottom,3); root.Children.Add(bottom);
            var actions=new UniformGrid { Columns=3 };
            feed=Command("\uE7F4","喂食",delegate { world.FeedPet(selected); }); feed.ToolTip="体力 +28，饱食 +35，冷却 30 秒"; actions.Children.Add(feed);
            care=Command("\uE734","照顾",delegate { world.CarePet(selected); }); care.ToolTip="体力 +12，亲密 +15，冷却 20 秒"; actions.Children.Add(care);
            company=Command("\uEB51","陪伴",delegate { world.AccompanyPet(selected); }); company.ToolTip="陪伴 15 秒，持续恢复体力共 18 点，暂停工作"; actions.Children.Add(company);
            bottom.Children.Add(actions);
            message=new TextBlock { FontSize=11,Foreground=Scene.Brush("#99AD9F"),Margin=new Thickness(2,12,0,0),TextTrimming=TextTrimming.CharacterEllipsis }; bottom.Children.Add(message);
            Content=new Border { Background=Scene.Brush("#191C1D"),Child=root }; SyncJob(); Refresh();
        }
        private Button Command(string icon,string label,Action action)
        {
            var panel=new StackPanel { Orientation=Orientation.Horizontal };
            panel.Children.Add(new TextBlock { Text=icon,FontFamily=new FontFamily("Segoe MDL2 Assets"),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center });
            panel.Children.Add(new TextBlock { Text=label });
            var button=new Button { Content=panel,Height=36,Margin=new Thickness(0,0,8,0),Padding=new Thickness(14,0,14,0) };
            button.Click+=delegate { action(); Refresh(); }; return button;
        }
        private static void SetLabel(Button button,string label) { ((TextBlock)((StackPanel)button.Content).Children[1]).Text=label; }
        private void SyncJob() { job.SelectedIndex=(int)world.Data.Pets[selected].Job; }
        internal void VerifyCommands()
        {
            selected=0; world.RecallPet(0); world.RecallPet(2); world.Data.Pets[0].Energy=30;
            SyncJob(); Refresh(); job.SelectedIndex=(int)PetJob.Hunt;
            dispatch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(!world.Data.Pets[0].Deployed || world.Data.Pets[0].Job!=PetJob.Hunt) throw new InvalidOperationException("Dispatch button did not assign pet");
            feed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Energy!=58 || feed.IsEnabled) throw new InvalidOperationException("Feed button did not update energy and cooldown");
            care.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Energy!=70 || care.IsEnabled) throw new InvalidOperationException("Care button did not update energy and cooldown");
            company.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].CompanyLeft!=15) throw new InvalidOperationException("Company button did not start timed interaction");
            recall.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(world.Data.Pets[0].Deployed || world.Data.Pets[0].CompanyLeft!=0) throw new InvalidOperationException("Recall button did not stop pet");
            cards[4].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(selected!=4 || portrait.Source!=Art.Pet(4)) throw new InvalidOperationException("Pet selector did not change portrait");
        }
        internal void SetCompactSize() { Width=820; Height=720; }
        public void Refresh()
        {
            int count=world.Data.Pets.Count(p=>p.Deployed); team.Text="已派出 "+count+" / 2"+(world.Paused?"  ·  已暂停":"");
            for(int i=0;i<6;i++)
            {
                var p=world.Data.Pets[i]; cardEnergy[i].Value=p.Energy;
                cardStatus[i].Text=(p.Deployed?p.Status:PetCatalog.Talents[i])+" · 体力 "+p.Energy.ToString("0")+(p.Clones.Count>0?" · 分身 "+p.Clones.Count:"");
                cards[i].Background=Scene.Brush(i==selected?"#294337":"#23292B"); cards[i].BorderBrush=Scene.Brush(i==selected?"#9BCEB1":"#3B4440");
            }
            var pet=world.Data.Pets[selected]; portrait.Source=Art.Pet(selected); title.Text=PetCatalog.Names[selected]+" / "+PetCatalog.Talents[selected];
            skill.Text=PetCatalog.Skills[selected];
            if(selected==1) skill.Text+="  蓄力 "+pet.SpentProgress.ToString("0.0")+" / 10";
            if(selected==3) skill.Text+="  蓄力 "+pet.SpentProgress.ToString("0.0")+" / 15";
            stats.Text="体力 "+pet.Energy.ToString("0")+"   饱食 "+pet.Hunger.ToString("0")+"   亲密 "+pet.Affection.ToString("0")+"   ·   灭虫 "+pet.Kills+" / 清扫 "+pet.Cleaned;
            SetLabel(dispatch,pet.Deployed?"调整任务":"派出"); dispatch.IsEnabled=!world.Paused&&(pet.Deployed||count<2);
            recall.IsEnabled=pet.Deployed;
            feed.IsEnabled=!world.Paused&&pet.FeedCooldown<=0&&(pet.Energy<100||pet.Hunger<100);
            care.IsEnabled=!world.Paused&&pet.CareCooldown<=0&&(pet.Energy<100||pet.Affection<100);
            company.IsEnabled=!world.Paused&&pet.CompanyCooldown<=0&&(pet.Deployed||count<2);
            SetLabel(feed,pet.FeedCooldown>0?"喂食 · "+Math.Ceiling(pet.FeedCooldown)+"s":"喂食");
            SetLabel(care,pet.CareCooldown>0?"照顾 · "+Math.Ceiling(pet.CareCooldown)+"s":"照顾");
            SetLabel(company,pet.CompanyLeft>0?"陪伴中 · "+Math.Ceiling(pet.CompanyLeft)+"s":pet.CompanyCooldown>0?"陪伴 · "+Math.Ceiling(pet.CompanyCooldown)+"s":"陪伴");
            message.Text=world.LastEvent;
        }
    }
}

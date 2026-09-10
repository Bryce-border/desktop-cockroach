using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DesktopRoach
{
    public class Scene : FrameworkElement
    {
        public Simulation World;
        public Tool SelectedTool;
        public bool IsOverlay, Interactive = true, ShowObjects = true;
        private Point pointer, target;
        private bool inside, holding;
        private double scale = 1, ox, oy;
        private static readonly Dictionary<string, SolidColorBrush> brushes = new Dictionary<string, SolidColorBrush>();
        private static readonly Typeface typeface = new Typeface("Microsoft YaHei UI");
        public Scene(Simulation simulation)
        {
            World = simulation; ClipToBounds = true; Focusable = true;
            MouseMove += delegate(object sender, MouseEventArgs e) { var p = e.GetPosition(this); target = ToWorld(p); if (!inside) pointer = target; inside = true; };
            MouseLeave += delegate { inside = false; holding = false; Cursor = Cursors.Arrow; };
            MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!Interactive || SelectedTool == Tool.Observe) return;
                target = ToWorld(e.GetPosition(this)); if (!inside) pointer = target;
                inside = true; holding = true; World.Use(SelectedTool, pointer.X, pointer.Y); e.Handled = true;
            };
            MouseLeftButtonUp += delegate { holding = false; };
        }
        private Point ToWorld(Point p) { return new Point((p.X - ox) / scale, (p.Y - oy) / scale); }
        public void Frame(double dt)
        {
            bool slowed = World.Data.Roaches.Any(r => r.Baby && Simulation.Distance(pointer.X, pointer.Y, r.X, r.Y) < 42);
            double ease = 1 - Math.Exp(-dt * (slowed ? 3 : 35));
            pointer = new Point(pointer.X + (target.X - pointer.X) * ease, pointer.Y + (target.Y - pointer.Y) * ease);
            Cursor = inside && Interactive && SelectedTool != Tool.Observe ? Cursors.None : Cursors.Arrow;
            if (holding && Mouse.LeftButton == MouseButtonState.Pressed && (SelectedTool == Tool.Broom || SelectedTool == Tool.Mop || SelectedTool == Tool.Spray)) World.Use(SelectedTool, pointer.X, pointer.Y);
            InvalidateVisual();
        }
        internal static SolidColorBrush Brush(string color)
        {
            SolidColorBrush brush;
            if (!brushes.TryGetValue(color, out brush)) { brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); brushes[color] = brush; }
            return brush;
        }
        private static Pen Pen(string color, double width) { return new Pen(Brush(color), width); }
        private static void Line(DrawingContext dc, string color, double width, double x, double y, double xx, double yy) { dc.DrawLine(Pen(color,width),new Point(x,y),new Point(xx,yy)); }
        private static void Ellipse(DrawingContext dc, string color, double x, double y, double rx, double ry) { dc.DrawEllipse(Brush(color),null,new Point(x,y),rx,ry); }
        private static void Text(DrawingContext dc, string text, double size, string color, double x, double y)
        {
            dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, typeface, size, Brush(color), 1), new Point(x, y));
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            dc.DrawRectangle(IsOverlay ? (Interactive && SelectedTool != Tool.Observe ? Brush("#01000000") : Brushes.Transparent) : Brush("#EAF0ED"), null, new Rect(0,0,ActualWidth,ActualHeight));
            scale = Math.Max(.01, Math.Min(ActualWidth / Simulation.Width, ActualHeight / Simulation.Height));
            ox = (ActualWidth - Simulation.Width * scale)/2; oy = (ActualHeight - Simulation.Height * scale)/2;
            dc.PushTransform(new TranslateTransform(ox,oy)); dc.PushTransform(new ScaleTransform(scale,scale));
            if (!IsOverlay)
            {
                for (int x = 24; x < 1280; x += 32) for (int y = 24; y < 720; y += 32) Ellipse(dc,"#BBCBC2",x,y,.85,.85);
                Text(dc,"我的桌面",19,"#65796F",37,28);
                Text(dc, DateTime.Now.ToString("HH:mm"),20,"#65796F",1166,28);
                Text(dc,"DESKTOP / 01",12,"#94A69B",37,673);
                dc.DrawRoundedRectangle(Brush("#DAE4DE"),null,new Rect(540,657,200,43),12,12);
                for(int i=0;i<4;i++) { dc.DrawRoundedRectangle(Brush(new[]{"#3F7966","#D98C73","#7295AD","#88987B"}[i]),null,new Rect(560+i*43,666,27,26),5,5); Line(dc,"#EDF3EF",2,567+i*43,679,580+i*43,679); }
            }
            if (ShowObjects) foreach (DeskObject obj in World.Data.Objects) DrawObject(dc,obj);
            foreach (var item in World.Data.Items) DrawItem(dc,item);
            foreach (var r in World.Data.Roaches) DrawRoach(dc,r,World.Data.Elapsed);
            foreach (var pet in World.Data.Pets.Where(p=>p.Deployed)) Art.DrawPet(dc,pet,World.Data.Elapsed);
            double pollution = World.Data.Pollution;
            if (pollution > 1)
            {
                dc.PushOpacity(pollution / 100 * .23);
                dc.DrawRectangle(Brush("#778465"),null,new Rect(-ox/scale,-oy/scale,ActualWidth/scale,ActualHeight/scale));
                dc.Pop();
            }
            if(World.EffectLife > 0)
            {
                dc.PushOpacity(World.EffectLife / .45);
                double radius = Simulation.Radius(World.EffectTool);
                dc.DrawEllipse(Brush(World.EffectTool == Tool.Swatter ? "#60ED806A" : "#5062D5B0"),Pen("#F4FBF6",2),new Point(World.EffectX,World.EffectY),radius,radius);
                dc.Pop();
            }
            if(World.EffectLife>0) Art.DrawTool(dc,World.EffectTool,World.EffectX,World.EffectY,110,1-World.EffectLife/.45);
            if (inside && Interactive && SelectedTool != Tool.Observe)
            {
                bool slowed = World.Data.Roaches.Any(r => r.Baby && Simulation.Distance(pointer.X,pointer.Y,r.X,r.Y)<42);
                string color = slowed ? "#DA755C" : "#228267";
                double radius = Simulation.Radius(SelectedTool);
                dc.DrawEllipse(Brush("#1866BF9E"),Pen(color,1.6),pointer,radius,radius);
                Line(dc,color,2,pointer.X-7,pointer.Y,pointer.X+7,pointer.Y); Line(dc,color,2,pointer.X,pointer.Y-7,pointer.X,pointer.Y+7);
                if (World.Cooldown > .2) Text(dc,World.Cooldown.ToString("0.0")+"s",14,color,pointer.X+12,pointer.Y+9);
                if(slowed) Text(dc,"黏滞",15,color,pointer.X+12,pointer.Y-25);
                if(World.EffectLife<=0) Art.DrawTool(dc,SelectedTool,pointer.X,pointer.Y,105,0);
            }
            dc.Pop(); dc.Pop();
        }
        private static void DrawObject(DrawingContext dc, DeskObject obj)
        {
            dc.DrawRoundedRectangle(Brush("#160F3424"),null,new Rect(obj.X-23,obj.Y-17,54,46),6,6);
            dc.DrawRoundedRectangle(Brush("#81BCA0"),null,new Rect(obj.X-25,obj.Y-25,26,13),4,4);
            dc.DrawRoundedRectangle(Brush("#94CDAF"),Pen("#74B496",1),new Rect(obj.X-25,obj.Y-19,53,38),4,4);
            Line(dc,"#C3E2D0",2,obj.X-14,obj.Y-7,obj.X+14,obj.Y-7);
            Text(dc,obj.Name,15,"#496455",obj.X-31,obj.Y+28);
        }
        private static void DrawItem(DrawingContext dc, GroundItem item)
        {
            double x=item.X,y=item.Y;
            switch(item.Kind)
            {
                case ItemKind.Food:
                    Ellipse(dc,"#20795031",x+2,y+3,19,9);
                    for(int i=0;i<7;i++) { double a=i*2.399; double d=5+i*1.4; Ellipse(dc,i%2==0?"#CBA46C":"#A57F4B",x+Math.Cos(a)*d,y+Math.Sin(a)*d,3.2+i%3,2.3); }
                    break;
                case ItemKind.Stain:
                    Ellipse(dc,"#26859153",x,y,30,14); Ellipse(dc,"#2078874A",x+18,y+10,17,10); Ellipse(dc,"#27758443",x-15,y-3,9,12);
                    break;
                case ItemKind.Egg:
                    dc.PushTransform(new RotateTransform(22,x,y));
                    dc.DrawRoundedRectangle(Brush("#977444"),Pen("#674F32",1.5),new Rect(x-14,y-7,28,14),5,5);
                    for(int i=-9;i<12;i+=4) Line(dc,"#C3A371",1.3,x+i,y-5,x+i,y+5);
                    dc.Pop();
                    dc.DrawRoundedRectangle(Brush("#CAD7CC"),null,new Rect(x-15,y+13,30,3),1,1);
                    dc.DrawRoundedRectangle(Brush("#BF8456"),null,new Rect(x-15,y+13,30*Math.Min(1,item.Age/42),3),1,1);
                    break;
                case ItemKind.Corpse:
                    Ellipse(dc,"#27423E2C",x,y+2,20,9); Ellipse(dc,"#685C47",x,y,11,7);
                    for(int i=-1;i<=1;i++) { Line(dc,"#625A49",1.5,x+i*6,y-4,x+i*7+4,y-12); Line(dc,"#625A49",1.5,x+i*6,y+4,x+i*7-4,y+12); }
                    break;
                case ItemKind.Bait:
                    Art.DrawTool(dc,Tool.Bait,x,y-9,72,0);
                    Text(dc,Math.Max(0,item.Life-item.Age).ToString("0")+"s",12,"#39745D",x-10,y+25);
                    break;
            }
        }
        internal static void DrawRoach(DrawingContext dc, Roach r, double time)
        {
            dc.PushTransform(new TranslateTransform(r.X,r.Y)); dc.PushTransform(new RotateTransform(r.Angle*180/Math.PI));
            if(r.Baby) dc.PushTransform(new ScaleTransform(.52,.52));
            Ellipse(dc,"#24000000",-3,4,25,14);
            for(int side=-1;side<=1;side+=2)
                for(int leg=0;leg<3;leg++)
                {
                    double wave=Math.Sin(time*22+leg*2.2+side)*5;
                    double root=10-leg*13, knee=root-7+wave, end=root-14+wave;
                    Line(dc,"#372E25",2.5,root,side*7,knee,side*(17+leg*2));
                    Line(dc,"#504132",1.5,knee,side*(17+leg*2),end,side*(25+leg));
                    Line(dc,"#504132",1,end,side*(25+leg),end-4,side*(26+leg));
                }
            Ellipse(dc,"#362B23",-5,0,23,12);
            Ellipse(dc,"#79502F",-6,-1,20,10.5);
            for(int i=0;i<5;i++)
            {
                double xx=-20+i*6;
                Line(dc,"#4F3525",1,xx,-8+Math.Abs(i-2),xx+2,8-Math.Abs(i-2));
            }
            Ellipse(dc,"#A17343",-3,-4,16,4.3);
            Line(dc,"#452E20",1.3,-24,0,14,0);
            Ellipse(dc,"#443126",13,0,10,9); Ellipse(dc,"#795136",14,-2,7,6);
            Ellipse(dc,"#2A251F",23,0,6,5); Ellipse(dc,"#D5B97B",25,-3,1.1,1.1); Ellipse(dc,"#D5B97B",25,3,1.1,1.1);
            double sway=Math.Sin(time*7)*5;
            Line(dc,"#54412D",1.2,26,-2,40,-12+sway); Line(dc,"#54412D",.8,40,-12+sway,56,-17+sway);
            Line(dc,"#54412D",1.2,26,2,39,13+sway); Line(dc,"#54412D",.8,39,13+sway,53,22+sway);
            if(r.Poison>0) { Ellipse(dc,"#A286C668",-8,-4,3,2); Ellipse(dc,"#A286C668",2,4,2,2); }
            if(r.Baby) dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
}

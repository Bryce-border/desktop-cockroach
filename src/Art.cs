using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopRoach
{
    internal static class Art
    {
        private static readonly Dictionary<int,BitmapSource> pets=new Dictionary<int,BitmapSource>();
        private static readonly Dictionary<Tool,BitmapSource[]> tools=new Dictionary<Tool,BitmapSource[]>();
        public static BitmapSource Pet(int id)
        {
            BitmapSource result;
            if(!pets.TryGetValue(id,out result))
            {
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("pet-"+id+".png"))
                {
                    if(stream==null) throw new FileNotFoundException("Missing pet texture: "+id);
                    var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); result=bitmap;
                }
                pets.Add(id,result);
            }
            return result;
        }
        public static BitmapSource ToolFrame(Tool tool,int frame)
        {
            BitmapSource[] frames;
            if(!tools.TryGetValue(tool,out frames))
            {
                frames=new BitmapSource[8];
                for(int i=0;i<8;i++)
                {
                    var visual=new DrawingVisual();
                    using(var dc=visual.RenderOpen()) DrawToolModel(dc,tool,i/7.0);
                    var bitmap=new RenderTargetBitmap(160,160,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); frames[i]=bitmap;
                }
                tools.Add(tool,frames);
            }
            return frames[Math.Max(0,Math.Min(7,frame))];
        }
        private static Brush B(string color) { return Scene.Brush(color); }
        private static void Stroke(DrawingContext dc,string color,double width,double x,double y,double xx,double yy)
        {
            dc.DrawLine(new Pen(B(color),width) { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round },new Point(x,y),new Point(xx,yy));
        }
        private static void Box(DrawingContext dc,string color,double x,double y,double w,double h,double radius)
        { dc.DrawRoundedRectangle(B(color),null,new Rect(x,y,w,h),radius,radius); }
        private static void Oval(DrawingContext dc,string color,double x,double y,double rx,double ry) { dc.DrawEllipse(B(color),null,new Point(x,y),rx,ry); }
        private static void DrawToolModel(DrawingContext dc,Tool tool,double progress)
        {
            double swing=Math.Sin(progress*Math.PI*2);
            dc.PushTransform(new RotateTransform(tool==Tool.Swatter?-28*Math.Sin(progress*Math.PI):tool==Tool.Broom?swing*18:tool==Tool.Mop?swing*8:0,80,113));
            if(tool==Tool.Swatter)
            {
                Stroke(dc,"#234868",10,82,143,82,71); Stroke(dc,"#A1CADE",3,79,139,79,78);
                Box(dc,"#356887",48,20,65,67,12); Box(dc,"#A5D6E3",54,26,53,55,8);
                for(int i=0;i<6;i++) { Stroke(dc,"#4E8DA8",2,59+i*8,30,59+i*8,76); Stroke(dc,"#4E8DA8",2,58,33+i*8,103,33+i*8); }
                Oval(dc,"#17344B",82,140,2,3);
            }
            else if(tool==Tool.Broom)
            {
                Stroke(dc,"#754F36",9,91,16,78,105); Stroke(dc,"#D5B082",3,89,17,76,102);
                var shape=Geometry.Parse("M 58,98 L 98,98 117,141 Q 79,153 40,141 Z");
                dc.DrawGeometry(B("#E5BA66"),new Pen(B("#AE7940"),2),shape);
                for(int i=0;i<9;i++) Stroke(dc,"#BC8D48",1.6,60+i*4,107,47+i*8,139+(i%2)*4);
                Box(dc,"#D67C6D",55,97,48,12,3); Stroke(dc,"#F5BE92",2,60,101,98,101);
            }
            else if(tool==Tool.Mop)
            {
                Stroke(dc,"#427E78",9,81,15,80,111); Stroke(dc,"#A6D7CE",3,78,19,77,107);
                Box(dc,"#69B8B8",39,111,82,25,7);
                for(int i=0;i<10;i++) Stroke(dc,i%2==0?"#D9EFE7":"#ADD3D6",5,45+i*8,124,43+i*8+swing*3,145-Math.Abs(5-i));
                Box(dc,"#386762",41,108,78,12,4); Box(dc,"#7FAFAB",50,111,59,3,1);
            }
            else if(tool==Tool.Bait)
            {
                double lift=-Math.Sin(progress*Math.PI)*12;
                Oval(dc,"#24000000",80,132,47,12);
                Oval(dc,"#314B54",80,109+lift,47,22); Box(dc,"#314B54",33,88+lift,94,24,4);
                Oval(dc,"#8DB3BC",80,88+lift,47,24); Oval(dc,"#1F3B45",80,88+lift,33,16);
                Oval(dc,"#D6AD68",80,87+lift,17,10);
                for(int i=0;i<6;i++) { double a=i*Math.PI/3; Oval(dc,"#B98543",80+Math.Cos(a)*9,87+lift+Math.Sin(a)*4,3,2); }
                Box(dc,"#90CCC0",60,112+lift,39,9,3);
            }
            else if(tool==Tool.Spray)
            {
                Box(dc,"#73BCB1",60,62,47,82,9); Box(dc,"#E7F4E7",63,87,41,33,2);
                Box(dc,"#324F51",69,45,29,22,3); Box(dc,"#8CD4CE",80,37,31,13,3);
                Stroke(dc,"#466765",4,105,48,100,62);
                for(int i=0;i<6;i++) Oval(dc,"#707CCAD0",117+(i%3)*11,24+(i/3)*15,3+progress*3,3+progress*3);
            }
            dc.Pop();
        }
        public static void DrawTool(DrawingContext dc,Tool tool,double x,double y,double size,double progress)
        {
            dc.DrawImage(ToolFrame(tool,(int)(Math.Max(0,Math.Min(1,progress))*7)),new Rect(x-size/2,y-size/2,size,size));
        }
        public static void DrawPet(DrawingContext dc,PetState pet,double time)
        {
            double bounce=pet.Walking?Math.Abs(Math.Sin(pet.WalkPhase))*5:Math.Sin(time*2+pet.Id)*1.2;
            dc.DrawEllipse(B("#22000000"),null,new Point(pet.X,pet.Y+7),33,9);
            dc.PushTransform(new RotateTransform(pet.Walking?Math.Sin(pet.WalkPhase)*5:0,pet.X,pet.Y));
            dc.DrawImage(Pet(pet.Id),new Rect(pet.X-44,pet.Y-99-bounce,88,106)); dc.Pop();
            if(pet.Job!=PetJob.Rest && pet.CompanyLeft<=0 && pet.CareAnimation==null)
                DrawTool(dc,pet.AnimationLeft>0?pet.ActionTool:pet.Job==PetJob.Hunt?Tool.Swatter:Tool.Broom,pet.X+38,pet.Y-25,70,pet.AnimationLeft>0?1-pet.AnimationLeft/.65:0);
            Box(dc,"#AF253C30",pet.X-33,pet.Y+16,66,5,2);
            Box(dc,pet.Energy<20?"#EC9B78":"#72C59D",pet.X-33,pet.Y+16,66*pet.Energy/100,5,2);
            var label=pet.CompanyLeft>0?"陪伴中":pet.Status;
            var text=new FormattedText(label,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),12,B("#335844"),1);
            dc.DrawText(text,new Point(pet.X-text.Width/2,pet.Y+25));
            if(pet.CompanyLeft>0 || pet.AnimationLeft>1)
            {
                double rise=Math.Sin(time*3)*3;
                dc.DrawGeometry(B("#DB817C"),null,Geometry.Parse("M "+(pet.X+28).ToString(CultureInfo.InvariantCulture)+","+(pet.Y-110-rise).ToString(CultureInfo.InvariantCulture)+" l -7,-7 c -8,-9 -16,3 7,16 c 23,-13 15,-25 7,-16 z"));
            }
            if(pet.CareAnimation=="feed")
            {
                Oval(dc,"#D8A05C",pet.X+35,pet.Y-40,13,11);
                for(int i=0;i<5;i++) Oval(dc,"#856139",pet.X+29+i%3*5,pet.Y-45+i/3*8,1.5,1.5);
            }
            if(pet.Job==PetJob.Rest && pet.CompanyLeft<=0 && pet.CareAnimation==null)
            {
                var sleep=new FormattedText("z Z",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),15,B("#648A9A"),1);
                dc.DrawText(sleep,new Point(pet.X+25,pet.Y-99+Math.Sin(time*2)*3));
            }
        }
        public static void Export(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach(Tool tool in new[]{Tool.Swatter,Tool.Broom,Tool.Mop,Tool.Bait,Tool.Spray})
            {
                var visual=new DrawingVisual();
                using(var dc=visual.RenderOpen()) for(int i=0;i<8;i++) dc.DrawImage(ToolFrame(tool,i),new Rect(i*160,0,160,160));
                var sheet=new RenderTargetBitmap(1280,160,96,96,PixelFormats.Pbgra32); sheet.Render(visual);
                SaveBitmap(sheet,Path.Combine(directory,tool.ToString().ToLowerInvariant()+"-strip.png"));
                SaveBitmap(ToolFrame(tool,0),Path.Combine(directory,tool.ToString().ToLowerInvariant()+".png"));
            }
            var gallery=new DrawingVisual();
            using(var dc=gallery.RenderOpen())
            {
                dc.DrawRectangle(B("#E7EEEB"),null,new Rect(0,0,1000,250));
                int i=0;
                foreach(Tool tool in new[]{Tool.Swatter,Tool.Broom,Tool.Mop,Tool.Bait,Tool.Spray})
                {
                    dc.DrawImage(ToolFrame(tool,0),new Rect(i*200+20,20,160,160));
                    var text=new FormattedText(new[]{"蟑螂拍","扫把","拖把","蟑螂药","杀虫喷雾"}[i],CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),18,B("#355847"),1);
                    dc.DrawText(text,new Point(i*200+100-text.Width/2,205)); i++;
                }
            }
            var result=new RenderTargetBitmap(1000,250,96,96,PixelFormats.Pbgra32); result.Render(gallery); SaveBitmap(result,Path.Combine(directory,"tools-gallery.png"));
        }
        public static void SaveBitmap(BitmapSource bitmap,string path)
        { var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var file=File.Create(path)) encoder.Save(file); }
    }
}

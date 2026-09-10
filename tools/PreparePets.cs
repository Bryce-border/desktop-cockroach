using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// Extract the six supplied reference figures without removing enclosed white details.
internal static class PreparePets
{
    static void Main(string[] args)
    {
        Directory.CreateDirectory(args[1]);
        var boxes = new[] { new Rectangle(70,100,310,363), new Rectangle(433,100,301,366), new Rectangle(796,112,293,360), new Rectangle(55,620,318,352), new Rectangle(427,600,319,379), new Rectangle(782,628,323,345) };
        using(var source = new Bitmap(args[0]))
        using(var sheet = new Bitmap(960,720,PixelFormat.Format32bppArgb))
        using(var sg = Graphics.FromImage(sheet))
        {
            sg.Clear(Color.FromArgb(232,239,235));
            for(int id=0;id<boxes.Length;id++)
            using(var cut = source.Clone(boxes[id],PixelFormat.Format32bppArgb))
            {
                int w=cut.Width,h=cut.Height;
                var outside=new bool[w*h]; var queue=new Queue<int>();
                Action<int,int> visit=(x,y)=>
                {
                    if(x<0||y<0||x>=w||y>=h||outside[y*w+x]) return;
                    var c=cut.GetPixel(x,y);
                    if(Math.Min(c.R,Math.Min(c.G,c.B))<232 || Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))>6) return;
                    outside[y*w+x]=true; queue.Enqueue(y*w+x);
                };
                for(int x=0;x<w;x++) { visit(x,0); visit(x,h-1); }
                for(int y=0;y<h;y++) { visit(0,y); visit(w-1,y); }
                while(queue.Count>0) { int p=queue.Dequeue(),x=p%w,y=p/w; visit(x-1,y); visit(x+1,y); visit(x,y-1); visit(x,y+1); }
                for(int y=0;y<h;y++) for(int x=0;x<w;x++) if(outside[y*w+x]) cut.SetPixel(x,y,Color.Transparent);
                using(var sprite=new Bitmap(320,384,PixelFormat.Format32bppArgb))
                using(var g=Graphics.FromImage(sprite))
                {
                    g.InterpolationMode=InterpolationMode.HighQualityBicubic;
                    double scale=Math.Min(288.0/w,350.0/h); int dw=(int)(w*scale),dh=(int)(h*scale);
                    g.DrawImage(cut,new Rectangle((320-dw)/2,374-dh,dw,dh));
                    sprite.Save(Path.Combine(args[1],"pet-"+id+".png"),ImageFormat.Png);
                    sg.DrawImage(sprite,new Rectangle(id%3*320+40,id/3*360,240,288));
                    using(var font=new Font("Microsoft YaHei UI",16))
                    using(var brush=new SolidBrush(Color.FromArgb(45,70,57)))
                        sg.DrawString(new[]{"早八鹅","炸毛鹅","充鹅不闻","轻鹅易举","鹅累了","等放假鹅"}[id],font,brush,new RectangleF(id%3*320,id/3*360+306,320,36),new StringFormat { Alignment=StringAlignment.Center });
                }
            }
            sheet.Save(Path.Combine(args[1],"pet-roster.png"),ImageFormat.Png);
        }
    }
}

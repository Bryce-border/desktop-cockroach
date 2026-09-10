using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace DesktopRoach
{
    public enum Tool { Observe, Swatter, Spray, Bait, Broom, Mop }
    public enum ItemKind { Food, Egg, Corpse, Stain, Bait }
    public class Roach
    {
        public double X, Y, Angle, Age, Meal, Poison;
        public bool Baby;
    }
    public class GroundItem
    {
        public ItemKind Kind;
        public double X, Y, Age, Life;
    }
    public class DeskObject
    {
        public double X, Y;
        public string Name;
    }
    public class SaveData
    {
        public int Version = 1, Difficulty = 1, Kills, Cleaned;
        public double Pollution, Elapsed;
        public List<Roach> Roaches = new List<Roach>();
        public List<GroundItem> Items = new List<GroundItem>();
        public List<DeskObject> Objects = new List<DeskObject>();
    }
    public class Simulation
    {
        public const double Width = 1280, Height = 720;
        public const int MaxRoaches = 70, MaxItems = 100;
        public SaveData Data;
        public bool Paused;
        public double Cooldown, EffectLife, EffectX, EffectY;
        public Tool EffectTool;
        public string LastEvent = "桌面巡逻已开始";
        private readonly Random random;
        private double spawnIn = 12, dirtIn = 8;
        public Simulation(int seed) { random = new Random(seed); Reset(); }
        public void Reset()
        {
            int difficulty = Data == null ? 1 : Data.Difficulty;
            Data = new SaveData { Difficulty = difficulty };
            Data.Objects.Add(new DeskObject { X = 100, Y = 140, Name = "项目文件" });
            Data.Objects.Add(new DeskObject { X = 100, Y = 260, Name = "灵感收集" });
            Data.Objects.Add(new DeskObject { X = 100, Y = 380, Name = "今日待办" });
            for (int i = 0; i < 3; i++) Spawn(false, 420 + i * 220, 220 + i * 125);
            Add(ItemKind.Food, 730, 430); Add(ItemKind.Food, 320, 530);
            Cooldown = 0; EffectLife = 0; spawnIn = 12; dirtIn = 8;
            LastEvent = "新的桌面，新的巡逻";
        }
        private double Rand(double min, double max) { return min + random.NextDouble() * (max - min); }
        public void Spawn(bool baby, double x, double y)
        {
            if (Data.Roaches.Count >= MaxRoaches) return;
            Data.Roaches.Add(new Roach { X = x, Y = y, Baby = baby, Angle = Rand(0, Math.PI * 2) });
        }
        public void Add(ItemKind kind, double x, double y)
        {
            if (Data.Items.Count >= MaxItems) return;
            Data.Items.Add(new GroundItem { Kind = kind, X = x, Y = y, Life = kind == ItemKind.Bait ? 90 : 0 });
        }
        public void Tick(double dt)
        {
            if (Paused || dt <= 0) return;
            dt = Math.Min(dt, .1);
            Data.Elapsed += dt; Cooldown = Math.Max(0, Cooldown - dt); EffectLife = Math.Max(0, EffectLife - dt);
            double intensity = new[] { .55, 1, 1.7 }[Data.Difficulty];
            spawnIn -= dt * intensity; dirtIn -= dt * intensity;
            if (spawnIn <= 0) { Spawn(false, random.Next(2) == 0 ? 24 : Width - 24, Rand(70, Height - 60)); spawnIn = Rand(18, 35); LastEvent = "一只新蟑螂潜入了桌面"; }
            if (dirtIn <= 0) { Add(ItemKind.Food, Rand(160, 1160), Rand(90, 640)); dirtIn = Rand(12, 24); }
            foreach (GroundItem item in Data.Items.ToArray())
            {
                item.Age += dt;
                if (item.Kind == ItemKind.Egg && item.Age >= 42)
                {
                    for (int i = 0; i < 3; i++) Spawn(true, item.X + Rand(-15, 15), item.Y + Rand(-15, 15));
                    Data.Items.Remove(item); Add(ItemKind.Stain, item.X, item.Y); LastEvent = "卵鞘孵化了，幼虫正在扩散";
                }
                if (item.Kind == ItemKind.Bait && item.Age >= item.Life) Data.Items.Remove(item);
            }
            foreach (Roach roach in Data.Roaches.ToArray())
            {
                roach.Age += dt;
                if (roach.Baby && roach.Age > 60) { roach.Baby = false; roach.Age = 0; }
                if (roach.Poison > 0)
                {
                    roach.Poison -= dt;
                    if (roach.Poison <= 0) { Kill(roach); continue; }
                }
                GroundItem target = Data.Items.Where(i => i.Kind == ItemKind.Food || i.Kind == ItemKind.Bait)
                    .OrderBy(i => Distance(roach.X, roach.Y, i.X, i.Y)).FirstOrDefault();
                if (target != null)
                {
                    double wanted = Math.Atan2(target.Y - roach.Y, target.X - roach.X);
                    roach.Angle += Math.Atan2(Math.Sin(wanted - roach.Angle), Math.Cos(wanted - roach.Angle)) * Math.Min(1, dt * 3);
                    if (Distance(roach.X, roach.Y, target.X, target.Y) < 23)
                    {
                        if (target.Kind == ItemKind.Bait) { if (roach.Poison <= 0) roach.Poison = 3; }
                        else
                        {
                            roach.Meal += dt;
                            if (roach.Meal > 5)
                            {
                                Data.Items.Remove(target); Add(ItemKind.Stain, target.X, target.Y);
                                if (!roach.Baby) Add(ItemKind.Egg, target.X + 20, target.Y + 10);
                                roach.Meal = 0; LastEvent = "食物被啃食，留下了新的卵鞘";
                            }
                        }
                        continue;
                    }
                }
                else roach.Angle += Rand(-1, 1) * dt * 2;
                double speed = (roach.Baby ? 49 : 66) * (roach.Poison > 0 ? .45 : 1);
                roach.X += Math.Cos(roach.Angle) * speed * dt; roach.Y += Math.Sin(roach.Angle) * speed * dt;
                if (roach.X < 22 || roach.X > Width - 22) roach.Angle = Math.PI - roach.Angle;
                if (roach.Y < 52 || roach.Y > Height - 22) roach.Angle = -roach.Angle;
                roach.X = Clamp(roach.X, 22, Width - 22); roach.Y = Clamp(roach.Y, 52, Height - 22);
                if (!roach.Baby && roach.Age > 20)
                    foreach (DeskObject obj in Data.Objects)
                        if (Distance(roach.X, roach.Y, obj.X, obj.Y) < 65)
                        { obj.X = Clamp(obj.X + Math.Cos(roach.Angle) * dt * 22, 55, Width - 70); obj.Y = Clamp(obj.Y + Math.Sin(roach.Angle) * dt * 22, 80, Height - 70); }
            }
            Data.Pollution = Clamp(Data.Pollution + dt * (Data.Roaches.Count * .009 + Data.Items.Count(i => i.Kind == ItemKind.Stain) * .018), 0, 100);
        }
        private void Kill(Roach roach)
        {
            Data.Roaches.Remove(roach); Data.Kills++; Add(ItemKind.Corpse, roach.X, roach.Y);
        }
        public bool Use(Tool tool, double x, double y)
        {
            if (Paused || tool == Tool.Observe || Cooldown > 0) return false;
            EffectX = x; EffectY = y; EffectTool = tool; EffectLife = .45;
            double radius = Radius(tool);
            if (tool == Tool.Swatter || tool == Tool.Spray)
            {
                foreach (Roach r in Data.Roaches.Where(r => Distance(x, y, r.X, r.Y) <= radius).ToArray())
                { if (tool == Tool.Swatter) Kill(r); else if (r.Poison <= 0) r.Poison = 1.2; }
                Cooldown = tool == Tool.Spray ? 2.5 : .28;
            }
            if (tool == Tool.Bait)
            {
                if (Data.Items.Count(i => i.Kind == ItemKind.Bait) >= 5) { LastEvent = "最多同时放置 5 份诱饵"; return false; }
                Add(ItemKind.Bait, x, y); Cooldown = 1;
            }
            if (tool == Tool.Broom || tool == Tool.Mop)
            {
                var removed = Data.Items.Where(i => Distance(x, y, i.X, i.Y) <= radius &&
                    (tool == Tool.Mop ? i.Kind == ItemKind.Stain : i.Kind == ItemKind.Food || i.Kind == ItemKind.Egg || i.Kind == ItemKind.Corpse)).ToArray();
                foreach (var i in removed) Data.Items.Remove(i);
                Data.Cleaned += removed.Length;
                if (tool == Tool.Mop) Data.Pollution = Math.Max(0, Data.Pollution - 4 - removed.Length * 5);
                Cooldown = .18;
            }
            return true;
        }
        public static double Radius(Tool tool) { return tool == Tool.Spray ? 115 : tool == Tool.Mop ? 85 : tool == Tool.Broom ? 72 : tool == Tool.Bait ? 26 : 46; }
        public static double Distance(double x, double y, double a, double b) { return Math.Sqrt((x-a)*(x-a)+(y-b)*(y-b)); }
        public static double Clamp(double value, double min, double max) { return Math.Max(min, Math.Min(max, value)); }
        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            using (var stream = File.Create(temporary)) { new XmlSerializer(typeof(SaveData)).Serialize(stream, Data); stream.Flush(true); }
            if (!File.Exists(path)) { File.Move(temporary, path); return; }
            try { File.Replace(temporary, path, path + ".bak"); }
            catch (UnauthorizedAccessException) { CopyWithBackup(temporary, path); }
            catch (IOException) { CopyWithBackup(temporary, path); }
        }
        private static void CopyWithBackup(string temporary, string path)
        {
            // Some restricted filesystems reject ReplaceFile; keep a recoverable previous save.
            File.Copy(path, path + ".bak", true);
            File.Copy(temporary, path, true);
            File.Delete(temporary);
        }
        public bool Load(string path)
        {
            if (!File.Exists(path)) return false;
            try
            {
                SaveData data;
                using (var reader = System.Xml.XmlReader.Create(path, new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null }))
                    data = (SaveData)new XmlSerializer(typeof(SaveData)).Deserialize(reader);
                if (data.Version != 1 || data.Roaches == null || data.Items == null || data.Objects == null || data.Roaches.Count > MaxRoaches || data.Items.Count > MaxItems || data.Objects.Count > 10) return false;
                if (!Finite(data.Pollution) || !Finite(data.Elapsed) || data.Roaches.Any(r => r == null || !Finite(r.X) || !Finite(r.Y) || !Finite(r.Angle) || !Finite(r.Age) || !Finite(r.Meal) || !Finite(r.Poison)) || data.Items.Any(i => i == null || !Finite(i.X) || !Finite(i.Y) || !Finite(i.Age) || !Finite(i.Life)) || data.Objects.Any(o => o == null || !Finite(o.X) || !Finite(o.Y))) return false;
                data.Difficulty = Math.Max(0, Math.Min(2, data.Difficulty)); data.Pollution = Clamp(data.Pollution, 0, 100);
                Data = data; LastEvent = "已恢复上次的桌面生态"; return true;
            }
            catch (InvalidOperationException) { return false; }
            catch (IOException) { return false; }
            catch (System.Xml.XmlException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
        private static bool Finite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
    }
}

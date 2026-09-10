using System;
using System.IO;
using System.Linq;

namespace DesktopRoach
{
    internal static class SelfTests
    {
        private static int checks;
        private static void Assert(bool condition,string message) { checks++; if(!condition) throw new Exception(message); }
        private static Simulation Empty()
        {
            var s=new Simulation(123); s.Data.Roaches.Clear(); s.Data.Items.Clear(); return s;
        }
        private static void Advance(Simulation s,double seconds) { for(int i=0;i<(int)(seconds*10);i++) s.Tick(.1); }
        public static int Run()
        {
            string temp=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-data-"+Guid.NewGuid().ToString("N"),"save.xml");
            try
            {
                var s=Empty(); s.Spawn(false,500,350); s.Add(ItemKind.Food,500,350); Advance(s,6);
                Assert(s.Data.Items.Any(i=>i.Kind==ItemKind.Egg),"Eating must produce eggs");
                s=Empty(); s.Add(ItemKind.Egg,400,300); s.Data.Items[0].Age=41.9; Advance(s,.2);
                Assert(s.Data.Roaches.Count(r=>r.Baby)==3,"Egg must hatch three babies");
                Assert(!s.Data.Items.Any(i=>i.Kind==ItemKind.Egg),"Hatched egg must be consumed");
                s=Empty(); s.Spawn(false,100,100); s.Spawn(false,400,400); s.Use(Tool.Swatter,100,100);
                Assert(s.Data.Roaches.Count==1 && s.Data.Kills==1,"Swatter must be local");
                Assert(!s.Use(Tool.Swatter,400,400),"Cooldown must block repeated use");
                s=Empty(); s.Spawn(false,400,300); s.Use(Tool.Spray,400,300); Advance(s,2);
                Assert(s.Data.Roaches.Count==0,"Spray must kill poisoned roach");
                s=Empty(); s.Spawn(false,400,300); s.Use(Tool.Bait,400,300); Advance(s,4);
                Assert(s.Data.Roaches.Count==0,"Bait must poison and kill feeding roach");
                s=Empty(); s.Add(ItemKind.Food,400,300); s.Add(ItemKind.Stain,400,300); s.Add(ItemKind.Egg,400,300); s.Use(Tool.Broom,400,300);
                Assert(s.Data.Items.Count==1 && s.Data.Items[0].Kind==ItemKind.Stain,"Broom must leave oil stain");
                s.Data.Pollution=50; Advance(s,.3); s.Use(Tool.Mop,400,300);
                Assert(s.Data.Items.Count==0 && s.Data.Pollution<50,"Mop must remove oil and pollution");
                s=Empty(); for(int i=0;i<100;i++) s.Spawn(true,300,300);
                Assert(s.Data.Roaches.Count==Simulation.MaxRoaches,"Population must be bounded");
                for(int i=0;i<130;i++) s.Add(ItemKind.Food,400,400);
                Assert(s.Data.Items.Count==Simulation.MaxItems,"Items must be bounded");
                s.Paused=true; double age=s.Data.Elapsed; Advance(s,5);
                Assert(s.Data.Elapsed==age && !s.Use(Tool.Spray,300,300),"Pause must stop simulation and tools");
                s.Paused=false; s.Save(temp); var loaded=Empty();
                Assert(loaded.Load(temp) && loaded.Data.Roaches.Count==s.Data.Roaches.Count,"Save must round trip");
                s.Data.Pollution=24; s.Save(temp); Assert(loaded.Load(temp) && loaded.Data.Pollution==24,"Save must replace existing file");
                Assert(File.Exists(temp+".bak"),"Replacing a save must preserve backup");
                File.WriteAllText(temp,"broken"); Assert(!loaded.Load(temp),"Invalid save must be rejected");
                s=Empty(); s.Data.Pollution=double.NaN; s.Save(temp); Assert(!loaded.Load(temp),"Nonfinite save values must be rejected");
                s=Empty(); s.Data.Difficulty=2; Advance(s,3600);
                Assert(s.Data.Roaches.Count<=Simulation.MaxRoaches && s.Data.Items.Count<=Simulation.MaxItems && s.Data.Pollution<=100,"Long simulation must remain bounded");
                Assert(s.Data.Roaches.All(r=>r.X>=22 && r.X<=Simulation.Width-22 && r.Y>=52 && r.Y<=Simulation.Height-22),"Roaches must remain inside desktop");
                s=Empty(); s.Spawn(true,400,300); s.Data.Roaches[0].Age=59.95; Advance(s,.2);
                Assert(!s.Data.Roaches[0].Baby,"Babies must mature");
                s=Empty(); var obj=s.Data.Objects[0]; s.Spawn(false,obj.X,obj.Y); s.Data.Roaches[0].Age=25; double objectX=obj.X; Advance(s,.1);
                Assert(obj.X!=objectX,"Adult roach must drag nearby virtual objects");
                s=Empty(); for(int i=0;i<6;i++) { s.Cooldown=0; s.Use(Tool.Bait,400,300); }
                Assert(s.Data.Items.Count(i=>i.Kind==ItemKind.Bait)==5,"Bait placements must be capped");
                foreach(var bait in s.Data.Items) bait.Age=89.95; Advance(s,.2);
                Assert(s.Data.Items.All(i=>i.Kind!=ItemKind.Bait),"Bait must expire");
                string output="PASS: "+checks+" checks. Eating, hatching, combat, cleaning, pause, caps, persistence, one-hour simulation.";
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),output);
                return 0;
            }
            catch(Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-results.txt"),"FAIL after "+checks+" checks: "+ex); return 1;
            }
            finally
            {
                if(File.Exists(temp)) File.Delete(temp); if(File.Exists(temp+".bak")) File.Delete(temp+".bak");
                if(File.Exists(temp+".tmp")) File.Delete(temp+".tmp");
                if(Directory.Exists(Path.GetDirectoryName(temp))) Directory.Delete(Path.GetDirectoryName(temp));
            }
        }
    }
}

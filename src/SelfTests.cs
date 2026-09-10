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
                s=Empty();
                Assert(s.Data.Pets.Count==6 && s.DispatchPet(0,PetJob.Hunt) && s.DispatchPet(2,PetJob.Clean) && !s.DispatchPet(3,PetJob.Hunt),"Only two pets can be dispatched");
                s.RecallPet(2); Assert(s.DispatchPet(3,PetJob.Clean),"Recall must release a dispatch slot");
                s=Empty(); var pet=s.Data.Pets[1]; pet.X=400; pet.Y=300; s.Spawn(false,400,300); s.DispatchPet(1,PetJob.Hunt); Advance(s,.1);
                Assert(s.Data.Roaches.Count==0 && pet.Kills==1 && pet.Energy<100 && s.Cooldown==0,"Pet must hunt, spend energy and keep player cooldown independent");
                s=Empty(); pet=s.Data.Pets[2]; pet.X=400; pet.Y=300; s.Add(ItemKind.Egg,400,300); s.DispatchPet(2,PetJob.Clean); Advance(s,.1);
                Assert(!s.Data.Items.Any(i=>i.Kind==ItemKind.Egg) && pet.Cleaned==1 && pet.Energy==100,"Immune cleaner must remove eggs without spending energy");
                s.Add(ItemKind.Stain,400,300); s.Data.Pollution=30; Advance(s,2);
                Assert(!s.Data.Items.Any(i=>i.Kind==ItemKind.Stain) && s.Data.Pollution<30,"Cleaner must mop oil and lower pollution");
                s=Empty(); pet=s.Data.Pets[0]; pet.X=400; pet.Y=300; s.DispatchPet(0,PetJob.Hunt); pet.Energy=2; s.Spawn(false,400,300); Advance(s,.1);
                Assert(pet.Job==PetJob.Rest && pet.Kills==0 && !s.DispatchPet(0,PetJob.Hunt),"Exhausted pet must stop and reject work dispatch");
                Assert(s.FeedPet(0) && pet.Energy==28 && !s.FeedPet(0),"Food must recover energy and enforce cooldown");
                Assert(s.CarePet(0) && pet.Energy==40 && !s.CarePet(0),"Care must recover energy and enforce cooldown");
                Assert(s.AccompanyPet(0),"Companion action must start"); Advance(s,5);
                Assert(Math.Abs(pet.Energy-46)<.01 && pet.CompanyLeft>9.9 && pet.CompanyLeft<10.1,"Companionship must restore energy gradually");
                s.Paused=true; double energy=pet.Energy,remaining=pet.CompanyLeft,cooldown=pet.FeedCooldown; Advance(s,5);
                Assert(pet.Energy==energy && pet.CompanyLeft==remaining && pet.FeedCooldown==cooldown && !s.FeedPet(0) && !s.CarePet(0),"Pause must freeze pet actions and timers");
                s.Paused=false; Advance(s,10); Assert(pet.CompanyLeft<.01 && pet.Energy<=100,"Companionship must finish after 15 seconds");
                pet.Energy=95; pet.FeedCooldown=0; s.FeedPet(0); Assert(pet.Energy==100 && pet.Hunger<=100,"Pet recovery must cap values at 100");
                s.Save(temp); loaded=Empty(); Assert(loaded.Load(temp) && loaded.Data.Pets.Count==6 && loaded.Data.Pets[0].Energy==pet.Energy && loaded.Data.Pets[0].Deployed,"Pet state must persist without duplicated roster");
                var document=new System.Xml.XmlDocument(); document.Load(temp); var petsNode=document.DocumentElement.SelectSingleNode("Pets"); document.DocumentElement.RemoveChild(petsNode); document.Save(temp);
                Assert(loaded.Load(temp) && loaded.Data.Pets.Count==6 && !loaded.Data.Pets.Any(p=>p.Deployed),"Old saves must migrate to an inactive pet roster");
                s.Data.Pets[0].Energy=double.NaN; s.Save(temp); Assert(!loaded.Load(temp),"Invalid pet numeric data must be rejected");
                s=Empty(); s.DispatchPet(0,PetJob.Hunt); s.Data.Pets[0].X=60; s.Data.Pets[0].Y=100; s.Spawn(false,22,52); Advance(s,.1);
                Assert(s.Data.Pets[0].Kills==1,"Pet must reach pests at screen boundaries");
                s=Empty(); s.DispatchPet(1,PetJob.Hunt); s.DispatchPet(2,PetJob.Clean); Advance(s,3600);
                Assert(s.Data.Pets.All(p=>p.Energy>=0 && p.Energy<=100 && p.Hunger>=0 && p.Hunger<=100) && s.Data.Roaches.Count<=Simulation.MaxRoaches,"Pet ecosystem must remain bounded after one hour");
                for(int i=0;i<6;i++) Assert(Art.Pet(i).PixelWidth==320,"Pet PNG must be embedded");
                var first=Art.ToolFrame(Tool.Swatter,0); var second=Art.ToolFrame(Tool.Swatter,3); var a=new byte[160*160*4]; var b=new byte[a.Length]; first.CopyPixels(a,640,0); second.CopyPixels(b,640,0);
                Assert(!a.SequenceEqual(b) && a.Where((v,i)=>i%4==3).Any(v=>v==0) && a.Where((v,i)=>i%4==3).Any(v=>v>0),"Tool frames must move and preserve alpha");
                SkillChecks(temp);
                string output="PASS: "+checks+" checks. Ecology, pet care, six skills, animations, persistence, one-hour simulation.";
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
        private static Simulation Worker(int id,PetJob job)
        {
            var s=Empty(); s.DispatchPet(id,job); s.Data.Pets[id].X=400; s.Data.Pets[id].Y=300; return s;
        }
        private static void SkillChecks(string temp)
        {
            var s=Worker(0,PetJob.Hunt); var pet=s.Data.Pets[0]; pet.Energy=50;
            Assert(Simulation.IsEnraged(pet) && Simulation.MoveSpeed(pet)==PetCatalog.Speeds[0]*3,"Coffee threshold must apply 3x movement");
            pet.Energy=50.01; Assert(!Simulation.IsEnraged(pet),"Coffee must not trigger above half energy");
            pet.Energy=50; s.Spawn(false,400,300); Advance(s,.1); s.Spawn(false,400,300); s.Add(ItemKind.Food,400,300); Advance(s,.7);
            Assert(pet.Kills==2 && pet.CoffeeLeft>0,"Enraged pet must attack again at triple rate with coffee feedback");
            pet.Energy=51; pet.AnimationLeft=0; pet.ActionCooldown=0; s.Spawn(false,400,300); Advance(s,.1);
            Assert(Simulation.IsEnraged(pet) && pet.ActionDuration<.22,"Crossing threshold during an attack must accelerate its animation");
            s.FeedPet(0); Assert(!Simulation.IsEnraged(pet) && Simulation.MoveSpeed(pet)==PetCatalog.Speeds[0],"Feeding above 50 must end coffee buff");

            s=Worker(1,PetJob.Hunt); pet=s.Data.Pets[1]; s.Spawn(false,400,300); Advance(s,.1);
            Assert(s.Data.Bombs.Count==0 && pet.SpentProgress==5,"Keyboard charge must track actual work cost");
            s.Spawn(false,400,300); pet.ActionCooldown=0; pet.AnimationLeft=0; Advance(s,.1);
            Assert(s.Data.Bombs.Count==1 && pet.SpentProgress==0,"Every ten energy must throw a keyboard bomb");
            s.Add(ItemKind.Food,440,300); s.Spawn(false,440,300); pet.Job=PetJob.Rest;
            s.Paused=true; Advance(s,1); Assert(s.Data.Bombs[0].Age==0,"Pause must freeze bombs");
            s.Paused=false; Advance(s,.7); Assert(s.Data.Bombs[0].Exploded && s.Data.Roaches.Count==0 && pet.Kills==3,"Keyboard bomb must explode after flight and credit area kills");
            Advance(s,.5); Assert(s.Data.Bombs.Count==0,"Bomb effects must expire");
            pet.SpentProgress=9.99; pet.Job=PetJob.Hunt; pet.AnimationLeft=0; s.Spawn(false,900,300); Advance(s,.1);
            Assert(s.Data.Bombs.Count==1 && pet.SpentProgress>0 && pet.SpentProgress<.02,"Movement energy must also trigger keyboard bombs and preserve remainder");
            double remainder=pet.SpentProgress; s.FeedPet(1); Assert(pet.SpentProgress==remainder,"Recovery must not reset cumulative skill charge");

            s=Worker(2,PetJob.Hunt); pet=s.Data.Pets[2]; pet.Energy=0; s.Spawn(false,400,300); Advance(s,.1);
            Assert(pet.Kills==1 && pet.Energy==0 && pet.Job==PetJob.Hunt && s.DispatchPet(2,PetJob.Clean),"Zero-cost pet must work and accept tasks at zero energy");
            pet.ActionCooldown=0; pet.AnimationLeft=0; s.Data.Items.Clear(); s.Add(ItemKind.Food,900,300); double beforeX=pet.X; Advance(s,.1);
            Assert(pet.X>beforeX && pet.Energy==0,"Zero-cost movement must remain free");

            s=Worker(3,PetJob.Hunt); pet=s.Data.Pets[3];
            for(int i=0;i<3;i++) { pet.AnimationLeft=0; pet.ActionCooldown=0; s.Spawn(false,400,300); Advance(s,.1); }
            Assert(pet.Clones.Count==1 && Math.Abs(pet.SpentProgress-3)<.001,"Every fifteen energy must summon a clone with remainder");
            var clone=pet.Clones[0]; clone.X=450; clone.Y=300; s.Spawn(false,450,300); pet.AnimationLeft=1; int kills=pet.Kills; Advance(s,.1);
            Assert(pet.Kills==kills+1 && pet.Clones.Count==1,"Clone must work independently without recursive summons");
            clone.ActionCooldown=0; clone.AnimationLeft=0; pet.Job=PetJob.Clean; s.Add(ItemKind.Egg,clone.X,clone.Y); Advance(s,.1);
            Assert(!s.Data.Items.Any(i=>i.Kind==ItemKind.Egg) && pet.Cleaned==1,"Clone must follow master's cleaning task");
            pet.Energy=0; Advance(s,.1); Assert(pet.Clones.Count==0 && pet.Job==PetJob.Rest,"Clones must disappear when master runs out of energy");
            pet.Clones.Add(new PetActor()); s.RecallPet(3); Assert(pet.Clones.Count==0,"Recall must remove clones");

            s=Worker(4,PetJob.Hunt); pet=s.Data.Pets[4]; pet.Energy=0; Advance(s,1);
            Assert(pet.RecoveryBoost && Math.Abs(pet.Energy-.24)<.001,"Exhaustion recovery must run at 4x speed");
            s.CarePet(4); Assert(Math.Abs(pet.Energy-48.24)<.001,"Exhaustion buff must multiply care recovery");
            s.AccompanyPet(4); Advance(s,1); Assert(Math.Abs(pet.Energy-53.04)<.001,"Exhaustion buff must multiply continuous company recovery");
            s.FeedPet(4); Assert(pet.Energy==100 && !pet.RecoveryBoost,"Recovery buff must stop at full energy");
            pet.Energy=50; pet.CareCooldown=0; s.CarePet(4); Assert(pet.Energy==62,"Nonexhausted recovery must retain normal rate");

            s=Worker(5,PetJob.Hunt); pet=s.Data.Pets[5]; s.Spawn(false,700,300); Advance(s,.1);
            Assert(Simulation.WorkRange(pet)==360 && pet.Kills==1 && pet.X==400 && pet.TargetX>690,"Foresight must attack remotely at six times range");
            pet.Job=PetJob.Clean; pet.ActionCooldown=0; pet.AnimationLeft=0; s.Data.Items.Clear(); s.Add(ItemKind.Stain,710,300); s.Data.Pollution=25; Advance(s,.1);
            Assert(pet.Cleaned==1 && pet.X==400 && s.Data.Pollution<25 && pet.ActionTool==Tool.Mop,"Foresight must mop remotely");
            pet.ActionCooldown=0; pet.AnimationLeft=0; s.Add(ItemKind.Food,900,300); Advance(s,.1);
            Assert(pet.X>400 && s.Data.Items.Any(i=>i.Kind==ItemKind.Food),"Out-of-range targets must require movement");

            s=Worker(3,PetJob.Clean); pet=s.Data.Pets[3]; pet.SpentProgress=7; pet.Clones.Add(new PetActor { X=600,Y=400,ActionCooldown=.7 }); s.Data.Pets[4].RecoveryBoost=true; s.Data.Pets[4].Energy=12;
            s.Data.Bombs.Add(new KeyboardBomb { X=400,Y=300,TargetX=500,TargetY=350,Age=.3 }); s.Save(temp); var loaded=Empty();
            Assert(loaded.Load(temp) && loaded.Data.Pets[3].Clones.Count==1 && loaded.Data.Pets[3].SpentProgress==7 && loaded.Data.Pets[4].RecoveryBoost && loaded.Data.Bombs.Count==1,"Save must restore charge, clones, recovery and airborne bombs");
            var doc=new System.Xml.XmlDocument(); doc.Load(temp);
            foreach(System.Xml.XmlNode node in doc.SelectNodes("//SpentProgress|//RecoveryBoost|//Clones|//Bombs")) node.ParentNode.RemoveChild(node);
            doc.Save(temp); Assert(loaded.Load(temp) && loaded.Data.Pets[3].Clones.Count==0 && loaded.Data.Bombs.Count==0,"Pre-skill saves must load with empty skill state");
            s.Data.Pets[3].SpentProgress=double.NaN; s.Save(temp); Assert(!loaded.Load(temp),"Invalid skill data must be rejected");
        }
    }
}

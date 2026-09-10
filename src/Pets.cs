using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace DesktopRoach
{
    public enum PetJob { Rest, Hunt, Clean }
    public class PetActor
    {
        public double X=640,Y=550,ActionCooldown;
        [XmlIgnore] public double AnimationLeft,WalkPhase,ActionDuration=.65;
        [XmlIgnore] public Tool ActionTool;
        [XmlIgnore] public bool Walking;
        [XmlIgnore] public string Status="待命";
        [XmlIgnore] public double TargetX,TargetY;
    }
    public class PetState : PetActor
    {
        public int Id, Kills, Cleaned;
        public bool Deployed;
        public PetJob Job;
        public double Energy=100, Hunger=80, Affection=40;
        public double FeedCooldown, CareCooldown, CompanyLeft, CompanyCooldown, SpentProgress;
        public bool RecoveryBoost;
        public List<PetActor> Clones=new List<PetActor>();
        [XmlIgnore] public bool WasEnraged;
        [XmlIgnore] public double CoffeeLeft;
        [XmlIgnore] public string CareAnimation;
    }
    public static class PetCatalog
    {
        public static readonly string[] Names={"早八鹅","炸毛鹅","充鹅不闻","轻鹅易举","鹅累了","等放假鹅"};
        public static readonly string[] Talents={"咖啡狂暴","键盘轰炸","零耗体力","分身协作","深度补觉","远见遥控"};
        public static readonly string[] Skills={
            "体力不高于 50 时喝咖啡发怒，移速与攻速 +200%。",
            "累计消耗 10 点体力，投出一枚键盘炸弹，落点周围范围灭虫。",
            "移动与工作体力消耗降低 100%，无需消耗体力。",
            "累计消耗 15 点体力召唤一个分身，随主体工作；主体耗尽或召回时消失。",
            "体力耗尽后恢复速度 +300%，休息、喂食、照顾与陪伴均生效，回满结束。",
            "工作范围 +500%，在 360 范围内遥控灭虫与清扫。"};
        public static readonly double[] Speeds={148,122,105,115,78,120};
        public static readonly double[] HuntCosts={7,5,9,6,8,6};
        public static readonly double[] CleanCosts={5,7,3,3.5,5,4};
        public static List<PetState> Create()
        {
            return Enumerable.Range(0,6).Select(i=>new PetState { Id=i,X=420+i*80,Y=560 }).ToList();
        }
    }
    public partial class Simulation
    {
        public bool DispatchPet(int id,PetJob job)
        {
            if(id<0||id>=Data.Pets.Count||!Enum.IsDefined(typeof(PetJob),job)) return false;
            PetState pet=Data.Pets[id];
            if(!pet.Deployed && Data.Pets.Count(p=>p.Deployed)>=2) { LastEvent="最多同时派出两只桌宠"; return false; }
            if(job!=PetJob.Rest && pet.Id!=2 && pet.Energy<10) { LastEvent="体力不足，请先喂食、照顾或陪伴"; return false; }
            pet.Deployed=true; pet.Job=job; pet.Status=job==PetJob.Rest?"休息中":"寻找目标";
            LastEvent=PetCatalog.Names[id]+"已派出"; return true;
        }
        public void RecallPet(int id)
        {
            var pet=Data.Pets[id]; pet.Deployed=false; pet.Job=PetJob.Rest; pet.Status="待命"; pet.Walking=false; pet.CompanyLeft=0; pet.Clones.Clear();
        }
        public bool FeedPet(int id)
        {
            var pet=Data.Pets[id];
            if(Paused||pet.FeedCooldown>0 || pet.Energy>=100 && pet.Hunger>=100) return false;
            double gain=RecoverEnergy(pet,28); pet.Hunger=Clamp(pet.Hunger+35,0,100); pet.FeedCooldown=30;
            pet.AnimationLeft=2; pet.CareAnimation="feed"; pet.Status="吃点心"; LastEvent=PetCatalog.Names[id]+"吃饱了一些，体力 +"+gain.ToString("0"); return true;
        }
        public bool CarePet(int id)
        {
            var pet=Data.Pets[id];
            if(Paused||pet.CareCooldown>0 || pet.Energy>=100 && pet.Affection>=100) return false;
            double gain=RecoverEnergy(pet,12); pet.Affection=Clamp(pet.Affection+15,0,100); pet.CareCooldown=20;
            pet.AnimationLeft=2; pet.CareAnimation="care"; pet.Status="被照顾了"; LastEvent=PetCatalog.Names[id]+"很开心，体力 +"+gain.ToString("0"); return true;
        }
        public bool AccompanyPet(int id)
        {
            var pet=Data.Pets[id];
            if(Paused||pet.CompanyCooldown>0) return false;
            if(!pet.Deployed && !DispatchPet(id,PetJob.Rest)) return false;
            pet.CompanyLeft=15; pet.CompanyCooldown=35; pet.Status="陪伴中";
            LastEvent="正在陪伴"+PetCatalog.Names[id]; return true;
        }
        private void TickPets(double dt)
        {
            TickBombs(dt);
            foreach(var pet in Data.Pets)
            {
                RefreshSkillState(pet);
                pet.FeedCooldown=Math.Max(0,pet.FeedCooldown-dt); pet.CareCooldown=Math.Max(0,pet.CareCooldown-dt);
                pet.CompanyCooldown=Math.Max(0,pet.CompanyCooldown-dt); pet.CoffeeLeft=Math.Max(0,pet.CoffeeLeft-dt);
                TickActor(pet,dt,IsEnraged(pet)?3:1);
                if(pet.AnimationLeft<=0) pet.CareAnimation=null;
                foreach(var clone in pet.Clones) { TickActor(clone,dt); clone.Status="分身待命"; }
                if(pet.CompanyLeft>0 && pet.Deployed)
                {
                    double duration=Math.Min(dt,pet.CompanyLeft); pet.CompanyLeft-=duration;
                    RecoverEnergy(pet,duration*1.2); pet.Affection=Clamp(pet.Affection+duration*.6,0,100);
                    pet.Status="陪伴中"; continue;
                }
                if(!pet.Deployed) { pet.Status="待命"; continue; }
                if(pet.CareAnimation!=null) continue;
                pet.Hunger=Clamp(pet.Hunger-dt*.055,0,100);
                if(pet.Energy<=0 && pet.Id!=2) ExhaustPet(pet);
                if(pet.Job==PetJob.Rest)
                {
                    RecoverEnergy(pet,dt*.06); pet.Status=pet.RecoveryBoost?"深度补觉":pet.Energy<10?"累了，想吃点心":"休息中"; continue;
                }
                WorkPet(pet,pet,dt,false);
                if(pet.Energy>0 && pet.Job!=PetJob.Rest)
                    foreach(var clone in pet.Clones.ToArray()) WorkPet(clone,pet,dt,true);
            }
        }
        private static void TickActor(PetActor actor,double dt,double attackRate=1)
        {
            actor.ActionCooldown=Math.Max(0,actor.ActionCooldown-dt*attackRate);
            actor.AnimationLeft=Math.Max(0,actor.AnimationLeft-dt); actor.Walking=false;
        }
        private void WorkPet(PetActor actor,PetState pet,double dt,bool clone)
        {
                if(actor.AnimationLeft>0) return;
                Roach roach=null; GroundItem item=null;
                if(pet.Job==PetJob.Hunt) roach=Data.Roaches.OrderBy(r=>Distance(actor.X,actor.Y,r.X,r.Y)).FirstOrDefault();
                else item=Data.Items.Where(i=>i.Kind!=ItemKind.Bait).OrderBy(i=>Distance(actor.X,actor.Y,i.X,i.Y)).FirstOrDefault();
                bool polish=pet.Job==PetJob.Clean && item==null && Data.Pollution>=1;
                if(roach==null && item==null && !polish) { actor.Status=clone?"分身待命":"巡逻待命"; return; }
                double tx=roach!=null?roach.X:item!=null?item.X:actor.X;
                double ty=roach!=null?roach.Y:item!=null?item.Y:actor.Y;
                double distance=Distance(actor.X,actor.Y,tx,ty);
                if(distance>WorkRange(pet))
                {
                    double step=Math.Min(distance,dt*MoveSpeed(pet));
                    actor.X=Clamp(actor.X+(tx-actor.X)/distance*step,40,Width-40); actor.Y=Clamp(actor.Y+(ty-actor.Y)/distance*step,100,Height-50);
                    actor.WalkPhase+=dt*10; actor.Walking=true;
                    actor.Status=pet.Job==PetJob.Hunt?"追赶蟑螂":"前往清扫";
                    if(!clone) SpendEnergy(pet,dt*.18);
                    return;
                }
                if(actor.ActionCooldown>0) return;
                double cost=pet.Job==PetJob.Hunt?PetCatalog.HuntCosts[pet.Id]:PetCatalog.CleanCosts[pet.Id];
                if(!clone && pet.Id!=2 && pet.Energy<cost) { SpendEnergy(pet,pet.Energy); return; }
                double attackRate=IsEnraged(pet)?3:1;
                actor.ActionCooldown=pet.Id==1 && roach!=null?1.2:1.8;
                actor.ActionDuration=.65/attackRate; actor.AnimationLeft=actor.ActionDuration; actor.TargetX=tx; actor.TargetY=ty;
                if(roach!=null) { Kill(roach); pet.Kills++; actor.ActionTool=Tool.Swatter; actor.Status=pet.Id==5?"远程拍打":"拍打中"; }
                else
                {
                    actor.ActionTool=polish||item.Kind==ItemKind.Stain?Tool.Mop:Tool.Broom;
                    if(item!=null) { Data.Items.Remove(item); pet.Cleaned++; Data.Cleaned++; }
                    if(actor.ActionTool==Tool.Mop) Data.Pollution=Math.Max(0,Data.Pollution-5);
                    actor.Status=pet.Id==5?"远程清扫":"清扫中";
                }
                if(!clone) SpendEnergy(pet,cost);
        }
        private static bool ValidatePets(List<PetState> pets)
        {
            if(pets==null || pets.Count==0) return true;
            if(pets.Count!=6 || pets.Count(p=>p!=null&&p.Deployed)>2) return false;
            for(int i=0;i<6;i++)
            {
                var p=pets[i];
                if(p==null||p.Id!=i||!Enum.IsDefined(typeof(PetJob),p.Job)) return false;
                if(new[]{p.Energy,p.Hunger,p.Affection,p.X,p.Y,p.FeedCooldown,p.CareCooldown,p.CompanyLeft,p.CompanyCooldown,p.ActionCooldown,p.SpentProgress}.Any(v=>!Finite(v))) return false;
                if(p.SpentProgress<0 || p.SpentProgress>=(p.Id==1?10:15)) return false;
                if(p.Clones==null) p.Clones=new List<PetActor>();
                if(p.Clones.Any(c=>c==null || new[]{c.X,c.Y,c.ActionCooldown}.Any(v=>!Finite(v)))) return false;
                foreach(var c in p.Clones) { c.X=Clamp(c.X,40,Width-40); c.Y=Clamp(c.Y,100,Height-50); c.ActionCooldown=Clamp(c.ActionCooldown,0,2); }
                if(p.Id!=3 || !p.Deployed || p.Energy<=0) p.Clones.Clear();
                if(p.Id!=4 || p.Energy>=100) p.RecoveryBoost=false;
                p.WasEnraged=IsEnraged(p);
                p.Energy=Clamp(p.Energy,0,100); p.Hunger=Clamp(p.Hunger,0,100); p.Affection=Clamp(p.Affection,0,100);
                p.X=Clamp(p.X,40,Width-40); p.Y=Clamp(p.Y,100,Height-50);
                p.FeedCooldown=Clamp(p.FeedCooldown,0,30); p.CareCooldown=Clamp(p.CareCooldown,0,20);
                p.CompanyLeft=Clamp(p.CompanyLeft,0,15); p.CompanyCooldown=Clamp(p.CompanyCooldown,0,35); p.ActionCooldown=Clamp(p.ActionCooldown,0,2);
            }
            return true;
        }
    }
}

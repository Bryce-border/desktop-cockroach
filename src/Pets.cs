using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;

namespace DesktopRoach
{
    public enum PetJob { Rest, Hunt, Clean }
    public class PetState
    {
        public int Id, Kills, Cleaned;
        public bool Deployed;
        public PetJob Job;
        public double Energy=100, Hunger=80, Affection=40, X=640, Y=550;
        public double FeedCooldown, CareCooldown, CompanyLeft, CompanyCooldown, ActionCooldown;
        [XmlIgnore] public double AnimationLeft, WalkPhase;
        [XmlIgnore] public Tool ActionTool;
        [XmlIgnore] public bool Walking;
        [XmlIgnore] public string CareAnimation;
        [XmlIgnore] public string Status="待命";
    }
    public static class PetCatalog
    {
        public static readonly string[] Names={"早八鹅","炸毛鹅","充鹅不闻","轻鹅易举","鹅累了","等放假鹅"};
        public static readonly string[] Talents={"巡逻快手","灭虫能手","专注清扫","省力管家","慢慢来","全能搭档"};
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
            if(job!=PetJob.Rest && pet.Energy<10) { LastEvent="体力不足，请先喂食、照顾或陪伴"; return false; }
            pet.Deployed=true; pet.Job=job; pet.Status=job==PetJob.Rest?"休息中":"寻找目标";
            LastEvent=PetCatalog.Names[id]+"已派出"; return true;
        }
        public void RecallPet(int id)
        {
            var pet=Data.Pets[id]; pet.Deployed=false; pet.Job=PetJob.Rest; pet.Status="待命"; pet.Walking=false; pet.CompanyLeft=0;
        }
        public bool FeedPet(int id)
        {
            var pet=Data.Pets[id];
            if(Paused||pet.FeedCooldown>0 || pet.Energy>=100 && pet.Hunger>=100) return false;
            pet.Energy=Clamp(pet.Energy+28,0,100); pet.Hunger=Clamp(pet.Hunger+35,0,100); pet.FeedCooldown=30;
            pet.AnimationLeft=2; pet.CareAnimation="feed"; pet.Status="吃点心"; LastEvent=PetCatalog.Names[id]+"吃饱了一些，体力 +28"; return true;
        }
        public bool CarePet(int id)
        {
            var pet=Data.Pets[id];
            if(Paused||pet.CareCooldown>0 || pet.Energy>=100 && pet.Affection>=100) return false;
            pet.Energy=Clamp(pet.Energy+12,0,100); pet.Affection=Clamp(pet.Affection+15,0,100); pet.CareCooldown=20;
            pet.AnimationLeft=2; pet.CareAnimation="care"; pet.Status="被照顾了"; LastEvent=PetCatalog.Names[id]+"很开心，体力 +12"; return true;
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
            foreach(var pet in Data.Pets)
            {
                pet.FeedCooldown=Math.Max(0,pet.FeedCooldown-dt); pet.CareCooldown=Math.Max(0,pet.CareCooldown-dt);
                pet.CompanyCooldown=Math.Max(0,pet.CompanyCooldown-dt); pet.ActionCooldown=Math.Max(0,pet.ActionCooldown-dt);
                pet.AnimationLeft=Math.Max(0,pet.AnimationLeft-dt); pet.Walking=false;
                if(pet.AnimationLeft<=0) pet.CareAnimation=null;
                if(pet.CompanyLeft>0 && pet.Deployed)
                {
                    double duration=Math.Min(dt,pet.CompanyLeft); pet.CompanyLeft-=duration;
                    pet.Energy=Clamp(pet.Energy+duration*1.2,0,100); pet.Affection=Clamp(pet.Affection+duration*.6,0,100);
                    pet.Status="陪伴中"; continue;
                }
                if(!pet.Deployed) { pet.Status="待命"; continue; }
                if(pet.CareAnimation!=null) continue;
                pet.Hunger=Clamp(pet.Hunger-dt*.055,0,100);
                if(pet.Energy<1) { pet.Job=PetJob.Rest; pet.Status="累了，想吃点心"; }
                if(pet.Job==PetJob.Rest)
                {
                    pet.Energy=Clamp(pet.Energy+dt*.06,0,100); pet.Status=pet.Energy<10?"累了，想吃点心":"休息中"; continue;
                }
                if(pet.AnimationLeft>0) continue;
                Roach roach=null; GroundItem item=null;
                if(pet.Job==PetJob.Hunt) roach=Data.Roaches.OrderBy(r=>Distance(pet.X,pet.Y,r.X,r.Y)).FirstOrDefault();
                else item=Data.Items.Where(i=>i.Kind!=ItemKind.Bait).OrderBy(i=>Distance(pet.X,pet.Y,i.X,i.Y)).FirstOrDefault();
                bool polish=pet.Job==PetJob.Clean && item==null && Data.Pollution>=1;
                if(roach==null && item==null && !polish) { pet.Status="巡逻待命"; continue; }
                double tx=roach!=null?roach.X:item!=null?item.X:pet.X;
                double ty=roach!=null?roach.Y:item!=null?item.Y:pet.Y;
                double distance=Distance(pet.X,pet.Y,tx,ty);
                if(distance>60)
                {
                    double step=Math.Min(distance,dt*PetCatalog.Speeds[pet.Id]*(pet.Hunger<15?.7:1));
                    pet.X=Clamp(pet.X+(tx-pet.X)/distance*step,40,Width-40); pet.Y=Clamp(pet.Y+(ty-pet.Y)/distance*step,100,Height-50);
                    pet.WalkPhase+=dt*10; pet.Walking=true; pet.Energy=Math.Max(0,pet.Energy-dt*.18);
                    pet.Status=pet.Job==PetJob.Hunt?"追赶蟑螂":"前往清扫"; continue;
                }
                if(pet.ActionCooldown>0) continue;
                double cost=pet.Job==PetJob.Hunt?PetCatalog.HuntCosts[pet.Id]:PetCatalog.CleanCosts[pet.Id];
                if(pet.Energy<cost) { pet.Job=PetJob.Rest; pet.Status="体力不足，休息中"; continue; }
                pet.Energy-=cost; pet.ActionCooldown=pet.Id==1 && roach!=null?1.2:1.8; pet.AnimationLeft=.65;
                if(roach!=null) { Kill(roach); pet.Kills++; pet.ActionTool=Tool.Swatter; pet.Status="拍打中"; }
                else
                {
                    pet.ActionTool=polish||item.Kind==ItemKind.Stain?Tool.Mop:Tool.Broom;
                    if(item!=null) { Data.Items.Remove(item); pet.Cleaned++; Data.Cleaned++; }
                    if(pet.ActionTool==Tool.Mop) Data.Pollution=Math.Max(0,Data.Pollution-5);
                    pet.Status="清扫中";
                }
            }
        }
        private static bool ValidatePets(List<PetState> pets)
        {
            if(pets==null || pets.Count==0) return true;
            if(pets.Count!=6 || pets.Count(p=>p!=null&&p.Deployed)>2) return false;
            for(int i=0;i<6;i++)
            {
                var p=pets[i];
                if(p==null||p.Id!=i||!Enum.IsDefined(typeof(PetJob),p.Job)) return false;
                if(new[]{p.Energy,p.Hunger,p.Affection,p.X,p.Y,p.FeedCooldown,p.CareCooldown,p.CompanyLeft,p.CompanyCooldown,p.ActionCooldown}.Any(v=>!Finite(v))) return false;
                p.Energy=Clamp(p.Energy,0,100); p.Hunger=Clamp(p.Hunger,0,100); p.Affection=Clamp(p.Affection,0,100);
                p.X=Clamp(p.X,40,Width-40); p.Y=Clamp(p.Y,100,Height-50);
                p.FeedCooldown=Clamp(p.FeedCooldown,0,30); p.CareCooldown=Clamp(p.CareCooldown,0,20);
                p.CompanyLeft=Clamp(p.CompanyLeft,0,15); p.CompanyCooldown=Clamp(p.CompanyCooldown,0,35); p.ActionCooldown=Clamp(p.ActionCooldown,0,2);
            }
            return true;
        }
    }
}

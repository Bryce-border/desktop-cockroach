using System;
using System.Collections.Generic;
using System.Linq;

namespace DesktopRoach
{
    public class KeyboardBomb
    {
        public double X,Y,TargetX,TargetY,Age;
        public bool Exploded;
    }
    public partial class Simulation
    {
        public static bool IsEnraged(PetState pet) { return pet.Id==0 && pet.Energy>0 && pet.Energy<=50; }
        public static double MoveSpeed(PetState pet) { return PetCatalog.Speeds[pet.Id]*(pet.Hunger<15?.7:1)*(IsEnraged(pet)?3:1); }
        public static double WorkRange(PetState pet) { return pet.Id==5?360:60; }
        private void RefreshSkillState(PetState pet)
        {
            bool enraged=IsEnraged(pet);
            if(enraged && !pet.WasEnraged && pet.Deployed)
            {
                pet.CoffeeLeft=1.4;
                if(pet.CareAnimation==null) { pet.AnimationLeft/=3; pet.ActionDuration/=3; }
                LastEvent="早八鹅喝下咖啡，进入狂暴！";
            }
            pet.WasEnraged=enraged;
            if(pet.Id==4 && pet.Energy<=0) pet.RecoveryBoost=true;
        }
        private double RecoverEnergy(PetState pet,double amount)
        {
            if(pet.Id==4 && pet.Energy<=0) pet.RecoveryBoost=true;
            double before=pet.Energy;
            pet.Energy=Clamp(before+amount*(pet.Id==4 && pet.RecoveryBoost?4:1),0,100);
            if(pet.Energy>=100) pet.RecoveryBoost=false;
            RefreshSkillState(pet); return pet.Energy-before;
        }
        private void ExhaustPet(PetState pet)
        {
            pet.Energy=0; pet.Job=PetJob.Rest; pet.Clones.Clear(); pet.Status="体力耗尽，休息中";
            if(pet.Id==4) pet.RecoveryBoost=true;
            RefreshSkillState(pet);
        }
        private void SpendEnergy(PetState pet,double amount)
        {
            if(pet.Id==2) return;
            double actual=Math.Min(pet.Energy,Math.Max(0,amount)); pet.Energy-=actual;
            if(pet.Id==1 || pet.Id==3)
            {
                pet.SpentProgress+=actual;
                double threshold=pet.Id==1?10:15;
                while(pet.SpentProgress+1e-8>=threshold)
                {
                    pet.SpentProgress=Math.Max(0,pet.SpentProgress-threshold);
                    if(pet.Id==1)
                    {
                        var target=Data.Roaches.OrderBy(r=>Distance(pet.X,pet.Y,r.X,r.Y)).FirstOrDefault();
                        Data.Bombs.Add(new KeyboardBomb { X=pet.X,Y=pet.Y-40,TargetX=target==null?pet.X:target.X,TargetY=target==null?pet.Y:target.Y });
                        LastEvent="炸毛鹅投出了键盘炸弹！";
                    }
                    else if(pet.Energy>0)
                    {
                        double angle=pet.Clones.Count*2.399;
                        pet.Clones.Add(new PetActor { X=Clamp(pet.X+Math.Cos(angle)*52,40,Width-40),Y=Clamp(pet.Y+Math.Sin(angle)*40,100,Height-50) });
                        LastEvent="轻鹅易举召唤了一个分身！";
                    }
                }
            }
            if(pet.Energy<=1e-8) ExhaustPet(pet); else RefreshSkillState(pet);
        }
        private void TickBombs(double dt)
        {
            foreach(var bomb in Data.Bombs.ToArray())
            {
                bomb.Age+=dt;
                if(!bomb.Exploded && bomb.Age>=.65)
                {
                    bomb.Exploded=true;
                    foreach(var roach in Data.Roaches.Where(r=>Distance(bomb.TargetX,bomb.TargetY,r.X,r.Y)<=140).ToArray()) { Kill(roach); Data.Pets[1].Kills++; }
                }
                if(bomb.Age>=1.1) Data.Bombs.Remove(bomb);
            }
        }
        private static bool ValidateBombs(List<KeyboardBomb> bombs)
        {
            return bombs!=null && bombs.All(b=>b!=null && new[]{b.X,b.Y,b.TargetX,b.TargetY,b.Age}.All(v=>Finite(v)) && b.Age>=0 && b.Age<=1.1 && b.TargetX>=0 && b.TargetX<=Width && b.TargetY>=0 && b.TargetY<=Height);
        }
    }
}

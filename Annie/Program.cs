using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using SharpDX;

namespace Annie
{
    class Program
    {
        public const string ChampionName = "Annie";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        //ManaMenager
        public static GameObject Tibbers;

        public static float TibbersTimer = 0;
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;

        public static bool HaveStun = false;
        
        //AutoPotion
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);
        public static Items.Item Youmuu = new Items.Item(3142, 0);
        //Menu
        public static Menu Config;

        private static Obj_AI_Hero Player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;
            if (Player.BaseSkinName != ChampionName) return;
           
            //Create the spells
            Q = new Spell(SpellSlot.Q, 625f);
            W = new Spell(SpellSlot.W, 600f);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R, 625f);
            Q.SetTargetted(0.25f, 1400f);
            W.SetSkillshot(0.50f, 250f, float.MaxValue, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.20f, 250f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            //Create the menu
            Config = new Menu(ChampionName, ChampionName, true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();
            
            Config.SubMenu("Farm").AddItem(new MenuItem("farmQ", "Farm Q").SetValue(true));
            Config.SubMenu("Farm").AddItem(new MenuItem("farmW", "Lane clear W").SetValue(false));
            Config.SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana").SetValue(new Slider(60, 100, 30)));

            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("OrbDraw", "Draw AAcirlce OKTW© style").SetValue(false));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("1", "pls disable Orbwalking > Drawing > AAcirlce"));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("2", "My HP: 0-30 red, 30-60 orange,60-100 green"));
            Config.SubMenu("Draw").AddItem(new MenuItem("ComboInfo", "Combo Info").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("orb", "Orbwalker target OKTW© style").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("semi", "Semi-manual R target").SetValue(false));

            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));
            Config.AddItem(new MenuItem("autoE", "Auto E stack stun").SetValue(true));
            Config.AddItem(new MenuItem("sup", "Support mode").SetValue(true));
            Config.AddItem(new MenuItem("tibers", "TibbersAutoPilot").SetValue(true));
            Config.AddItem(new MenuItem("AACombo", "AA in combo").SetValue(false));

            Config.AddItem(new MenuItem("rCount", "Auto R stun x enemies").SetValue(new Slider(3, 0, 5)));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("R champions").AddItem(new MenuItem("ro" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            //Add the events we are going to use:
            Game.OnUpdate += Game_OnGameUpdate;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnCreate += Obj_AI_Base_OnCreate;
            Game.PrintChat("<font color=\"#ff00d8\">A</font>nie full automatic AI ver 1.4 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }

        static void Orbwalking_BeforeAttack(LeagueSharp.Common.Orbwalking.BeforeAttackEventArgs args)
        {
            if (Config.Item("sup").GetValue<bool>() && (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit))
            {
                if (((Obj_AI_Base)Orbwalker.GetTarget()).IsMinion) args.Process = false;
            }
        }

        private static void Obj_AI_Base_OnCreate(GameObject obj, EventArgs args)
        {
            if (obj.IsValid)
            {
                if (obj.Name == "Tibbers")
                    Tibbers = obj;
            }
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (ObjectManager.Player.HasBuff("Recall"))
                return;
       

            ManaMenager();
            PotionMenager();
            HaveStun = GetPassiveStacks();

            if (Combo && !Config.Item("AACombo").GetValue<bool>())
            {
                var t = TargetSelector.GetTarget(ObjectManager.Player.AttackRange + 150, TargetSelector.DamageType.Magical);
                if (t.IsValidTarget() && (ObjectManager.Player.GetAutoAttackDamage(t) * 2 > t.Health || ObjectManager.Player.Mana < RMANA))
                    Orbwalking.Attack = true;
                else
                    Orbwalking.Attack = false;
            }
            else
                Orbwalking.Attack = true;

            var target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Magical);
            if (target.IsValidTarget())
            {
                if (!HaveTibers && R.IsReady())
                {
                    if (Combo && HaveStun && target.CountEnemiesInRange(400) > 1)
                        R.Cast(target, true, true);
                    else if (Config.Item("rCount").GetValue<Slider>().Value > 0 && Config.Item("rCount").GetValue<Slider>().Value <= target.CountEnemiesInRange(300))
                        R.Cast(target, true, true);
                    else if (Combo && !W.IsReady() && !Q.IsReady()
                        && Q.GetDamage(target) < target.Health
                        && (target.CountEnemiesInRange(400) > 1 || R.GetDamage(target) + Q.GetDamage(target) > target.Health))
                        R.Cast(target, true, true);
                    else if (Combo && Q.GetDamage(target) < target.Health)
                        if (target.HasBuffOfType(BuffType.Stun) || target.HasBuffOfType(BuffType.Snare) ||
                                     target.HasBuffOfType(BuffType.Charm) || target.HasBuffOfType(BuffType.Fear) ||target.HasBuffOfType(BuffType.Taunt))
                        {
                            R.Cast(target, true, true);
                        }
                }
                if (W.IsReady() && (Farm || Combo))
                {
                    if (Combo && HaveStun && target.CountEnemiesInRange(250) > 1)
                        W.Cast(target, true, true);
                    else if (!Q.IsReady())
                        W.Cast(target, true, true);
                    else if (target.HasBuffOfType(BuffType.Stun) || target.HasBuffOfType(BuffType.Snare) || target.HasBuffOfType(BuffType.Charm) || 
                    target.HasBuffOfType(BuffType.Fear) ||target.HasBuffOfType(BuffType.Taunt))
                    {
                        W.Cast(target, true, true);
                    }
                }
                if (Q.IsReady() && (Farm || Combo))
                {
                    if (HaveStun && Combo && target.CountEnemiesInRange(400) > 1 && (W.IsReady() || R.IsReady()))
                    {
                        return;
                    }
                    else
                        Q.Cast(target, true);
                }
            }

            if (Config.Item("sup").GetValue<bool>())
            {
                if (Q.IsReady() && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && ObjectManager.Player.Mana > RMANA + QMANA)
                    farmQ();
            }
            else
            {
                if (Q.IsReady() && (!HaveStun || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) && (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear))
                    farmQ();
            }

            if (Config.Item("autoE").GetValue<bool>() && E.IsReady() && !HaveStun && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear)
                E.Cast();

            if (W.IsReady() && ObjectManager.Player.InFountain() && !HaveStun)
                W.Cast(ObjectManager.Player, true, true);
            if (Config.Item("tibers").GetValue<bool>() && HaveTibers)
            {
                if (Game.Time - TibbersTimer > 2)
                {
                    var BestEnemy = TargetSelector.GetTarget(2000, TargetSelector.DamageType.Magical);
                    foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(1500)))
                    {
                        if (enemy.IsValidTarget(2000) && BestEnemy.IsValidTarget(2000) && enemy.IsEnemy && BestEnemy.Position.Distance(Tibbers.Position) > enemy.Position.Distance(Tibbers.Position))
                            BestEnemy = enemy;
                    }
                    if (BestEnemy.IsValidTarget(2000))
                        R.Cast(BestEnemy.Position);
                }
            }
            else
            {
                Tibbers = null;
            }
        }

        public static void farmQ()
        {
            if (!Config.Item("farmQ").GetValue<bool>())
                return;
            var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All);
            if (Q.IsReady())
            {
                var mobs = MinionManager.GetMinions(Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    Q.Cast(mob, true);
                    if (Config.Item("farmW").GetValue<bool>() && ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value && W.IsReady())
                        W.Cast(mob, true);
                }
            }

            foreach (var minion in allMinionsQ)
            {
                if (minion.Health > ObjectManager.Player.GetAutoAttackDamage(minion) && minion.Health < Q.GetDamage(minion))
                {
                    Q.Cast(minion);
                    return;
                }
            }
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value && Config.Item("farmW").GetValue<bool>() && ObjectManager.Player.Mana > RMANA + QMANA + EMANA + WMANA * 2)
            {
                var Wfarm = W.GetCircularFarmLocation(allMinionsQ, W.Width);
                if (Wfarm.MinionsHit > 2 && W.IsReady())
                    W.Cast(Wfarm.Position);
            }
        }

        private static bool HaveTibers
        {
            get { return ObjectManager.Player.HasBuff("infernalguardiantimer"); }
        }

        private static bool Combo
        {
            get { return Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo; }
        }
        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
        }
        public static bool GetPassiveStacks()
        {
            var buffs = Player.Buffs.Where(buff => (buff.Name.ToLower() == "pyromania" || buff.Name.ToLower() == "pyromania_particle"));
            if (buffs.Any())
            {
                var buff = buffs.First();
                if (buff.Name.ToLower() == "pyromania_particle")
                    return true;
                else
                    return false;
            }
            return false;
        }
        public static void ManaMenager()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
            if (!R.IsReady())
                RMANA = QMANA - ObjectManager.Player.Level * 2;
            else
                RMANA = R.Instance.ManaCost;

            if (Farm)
                RMANA = RMANA + ObjectManager.Player.CountEnemiesInRange(2500) * 20;

            if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.2)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
            }
        }
        public static void PotionMenager()
        {
            if (Config.Item("pots").GetValue<bool>() && !ObjectManager.Player.InFountain() && !ObjectManager.Player.HasBuff("Recall"))
            {
                if (Potion.IsReady() && !ObjectManager.Player.HasBuff("RegenerationPotion", true))
                {
                    if (ObjectManager.Player.CountEnemiesInRange(700) > 0 && ObjectManager.Player.Health + 200 < ObjectManager.Player.MaxHealth)
                        Potion.Cast();
                    else if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.6)
                        Potion.Cast();
                }
                if (ManaPotion.IsReady() && !ObjectManager.Player.HasBuff("FlaskOfCrystalWater", true))
                {
                    if (ObjectManager.Player.CountEnemiesInRange(1200) > 0 && ObjectManager.Player.Mana < RMANA + WMANA + EMANA + RMANA)
                        ManaPotion.Cast();
                }
            }
        }

        public static void drawText(string msg, Obj_AI_Hero Hero, System.Drawing.Color color)
        {
            var wts = Drawing.WorldToScreen(Hero.Position);
            Drawing.DrawText(wts[0] - (msg.Length) * 5, wts[1], color, msg);
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("ComboInfo").GetValue<bool>())
            {
                var combo = "haras";
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget()))
                {
                    if (Q.GetDamage(enemy) > enemy.Health)
                        combo = "Q";
                    else if (Q.GetDamage(enemy) + W.GetDamage(enemy) > enemy.Health)
                        combo = "QW";
                    else if (Q.GetDamage(enemy) + R.GetDamage(enemy) + W.GetDamage(enemy) > enemy.Health)
                        combo = "QWR"; 
                    else if (Q.GetDamage(enemy) * 2 + R.GetDamage(enemy) + W.GetDamage(enemy) > enemy.Health)
                        combo = "QWRQ";
                    else
                        combo = "haras: " + (int)(enemy.Health - (Q.GetDamage(enemy) * 2 + R.GetDamage(enemy) + W.GetDamage(enemy)));
                    drawText(combo, enemy, System.Drawing.Color.GreenYellow);
                }
            }

            if (Config.Item("OrbDraw").GetValue<bool>())
            {
                if (ObjectManager.Player.HealthPercentage() > 60)
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.GreenYellow, 2, 1);
                else if (ObjectManager.Player.HealthPercentage() > 30)
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.Orange, 3, 1);
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.Red, 4, 1);
            }
            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (Q.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan, 1, 1);
            }
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Orange, 1, 1);
            }

            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range + R.Width/2, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, R.Range + R.Width/2, System.Drawing.Color.Gray, 1, 1);
            }

            if (Config.Item("orb").GetValue<bool>())
            {
                var orbT = Orbwalker.GetTarget();

                if (orbT.IsValidTarget())
                {
                    if (orbT.Health > orbT.MaxHealth * 0.6)
                        Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.GreenYellow, 5, 1);
                    else if (orbT.Health > orbT.MaxHealth * 0.3)
                        Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Orange, 10, 1);
                    else
                        Utility.DrawCircle(orbT.Position, orbT.BoundingRadius, System.Drawing.Color.Red, 10, 1);
                }

            }
            
        }
    }
}

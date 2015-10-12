using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
using System.Threading;

namespace Blitzcrank
{
    class Program
    {
        public const string ChampionName = "Blitzcrank";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell Q2;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static Spell R1;
        public static bool attackNow = true;
        //ManaMenager
        public static float QMANA;
        public static int pathe;

        public static float grab=0;
        public static float grabS=0;
        public static float grabW = 0;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;
        public static float qRange = 1000;
        public static bool Farm = false;
        public static bool Esmart = false;
        public static double WCastTime = 0;
        public static double OverKill = 0;
        public static double OverFarm = 0;
        public static Vector3 position1 = ObjectManager.Player.ServerPosition;
        public static Obj_AI_Hero positiont = ObjectManager.Player;
        //AutoPotion
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);
        public static Items.Item Youmuu = new Items.Item(3142, 0);
        public static int Muramana = 3042;
        public static int Tear = 3070;
        public static int Manamune = 3004;
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
            Q = new Spell(SpellSlot.Q, 1000);
            W = new Spell(SpellSlot.W, 200);
            E = new Spell(SpellSlot.E, 475);
            R = new Spell(SpellSlot.R, 600);

            Q.SetSkillshot(0.25f, 110f, 1800f, true, SkillshotType.SkillshotLine);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);
            //Create the menu
            Config = new Menu(ChampionName, ChampionName, true);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();
            Config.AddItem(new MenuItem("noti", "Show notification").SetValue(true));
            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));
            Config.AddItem(new MenuItem("autoW", "Auto W").SetValue(true));
            Config.AddItem(new MenuItem("autoE", "Auto E").SetValue(true));
            Config.SubMenu("Q option").AddItem(new MenuItem("qCC", "Auto Q cc & dash enemy").SetValue(true));
            Config.SubMenu("Q option").AddItem(new MenuItem("Hit", "Hit Chance Q").SetValue(new Slider(4, 4, 0)));
            Config.SubMenu("Q option").AddItem(new MenuItem("minGrab", "Min range grab").SetValue(new Slider(250, 125, (int)Q.Range)));
            Config.SubMenu("Q option").AddItem(new MenuItem("maxGrab", "Max range grab").SetValue(new Slider((int)Q.Range, 125, (int)Q.Range)));
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("Q option").SubMenu("Grab").AddItem(new MenuItem("grab" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            #region Combo
            Config.SubMenu("R option").AddItem(new MenuItem("rCount", "Auto R if enemies in range").SetValue(new Slider(3, 0, 5)));
            Config.SubMenu("R option").AddItem(new MenuItem("afterGrab", "Auto R after grab").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("rKs", "R ks").SetValue(false));
            Config.SubMenu("R option").AddItem(new MenuItem("inter", "OnPossibleToInterrupt")).SetValue(true);

            #endregion
            

            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));

            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += afterAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnInterruptableSpell;
            Game.PrintChat("<font color=\"#008aff\">B</font>litzcrank full automatic AI ver 1.0 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }
        private static void OnInterruptableSpell(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.Item("inter").GetValue<bool>() && R.IsReady() && unit.IsValidTarget(R.Range))
                R.Cast();
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            
            PotionMenager();
            if (Q.IsReady())
            {
                //Q.Cast(ObjectManager.Player);
                ManaMenager();
                foreach (var t in ObjectManager.Get<Obj_AI_Hero>())
                {

                    if (t.IsEnemy && !t.HasBuffOfType(BuffType.SpellImmunity) && !t.HasBuffOfType(BuffType.SpellShield) && t.IsValidTarget(Config.Item("maxGrab").GetValue<Slider>().Value) && Config.Item("grab" + t.BaseSkinName).GetValue<bool>() && ObjectManager.Player.Distance(t.ServerPosition) > Config.Item("minGrab").GetValue<Slider>().Value)
                    {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                            CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                        if (Orbwalker.ActiveMode.ToString() == "qCC" && Q.IsReady())
                        {
                            if (t.HasBuffOfType(BuffType.Stun) || t.HasBuffOfType(BuffType.Snare) ||
                                 t.HasBuffOfType(BuffType.Charm) || t.HasBuffOfType(BuffType.Fear) ||
                                 t.HasBuffOfType(BuffType.Taunt) || t.HasBuffOfType(BuffType.Slow) || t.HasBuff("Recall"))
                            {
                                Q.Cast(t, true);
                            }
                            else
                            {
                                Q.CastIfHitchanceEquals(t, HitChance.Dashing);
                                Q.CastIfHitchanceEquals(t, HitChance.Immobile);
                                
                            }
                        }
                    }
                }
            }
            if (!Q.IsReady() && Game.Time - grabW > 2)
            {
                foreach (var t in ObjectManager.Get<Obj_AI_Hero>())
                {
                    if (t.IsEnemy && t.HasBuff("rocketgrab2"))
                    {
                        grabS++;
                        grabW = Game.Time;
                    }
                }
            }

            if (R.IsReady() && Config.Item("rKs").GetValue<bool>())
                foreach (Obj_AI_Hero enem in ObjectManager.Get<Obj_AI_Hero>().Where(enem => enem.IsValid && enem.IsEnemy && enem.IsValidTarget(R.Range)))
                {
                    if (R.GetDamage(enem) > enem.Health)
                        R.Cast();
                }

            if (R.IsReady() && ObjectManager.Player.CountEnemiesInRange(600) >= Config.Item("rCount").GetValue<Slider>().Value && Config.Item("rCount").GetValue<Slider>().Value > 0)
                R.Cast();

            if (R.IsReady() && Config.Item("afterGrab").GetValue<bool>())
                foreach (Obj_AI_Hero enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValid && enemy.IsEnemy ))
                {
                    if (enemy.Buffs != null)
                    {
                        foreach (BuffInstance buff in enemy.Buffs)
                        {
                            if (buff.Name == "rocketgrab2" && buff.IsActive && enemy.IsValidTarget(400))
                            {
                                R.Cast();
                            }
                        }
                    }
                }
            
        }

        public static void debug(string msg)
        {
            if (Config.Item("debug").GetValue<bool>())
                Game.PrintChat(msg);
        }

        private static void afterAttack(AttackableUnit unit, AttackableUnit target)
        {

        }

        static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            if (args.Target.IsValid<Obj_AI_Hero>())
            {
                if (E.IsReady() && Config.Item("autoE").GetValue<bool>())
                {
                    E.Cast();
                }
                if (W.IsReady() && Config.Item("autoW").GetValue<bool>() )
                {
                    W.Cast();
                }
            }
        }
        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            var poutput = QWER.GetPrediction(target);
            var col = poutput.CollisionObjects.Count(ColObj => ColObj.IsEnemy && ColObj.IsMinion && !ColObj.IsDead);
            if (target.IsDead || col > 0 || target.Path.Count() > 1)
                return;
            if ((target.Path.Count() == 0 && target.Position == target.ServerPosition) || target.HasBuff("Recall") || poutput.Hitchance == HitChance.Immobile)
            {
                if (HitChanceNum < 3)
                    QWER.Cast(poutput.CastPosition);
                else if (Player.Distance(target.ServerPosition) < QWER.Range - (target.MoveSpeed * QWER.Delay))
                    QWER.Cast(poutput.CastPosition);
                return;
            }

            if (QWER.Delay < 0.30f && poutput.Hitchance == HitChance.Dashing)
            {
                QWER.Cast(poutput.CastPosition);
                return;
            }

            if (HitChanceNum == 0)
                QWER.Cast(target, true);
            else if (HitChanceNum == 1)
            {
                if ((int)poutput.Hitchance > 4)
                    QWER.Cast(poutput.CastPosition);
            }
            else if (HitChanceNum == 2)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                if (waypoints.Last<Vector2>().To3D().Distance(poutput.CastPosition) > QWER.Width && (int)poutput.Hitchance == 5)
                {
                    if (waypoints.Last<Vector2>().To3D().Distance(Player.Position) <= target.Distance(Player.Position) || (target.Path.Count() == 0 && target.Position == target.ServerPosition))
                    {
                        if (Player.Distance(target.ServerPosition) < QWER.Range - (poutput.CastPosition.Distance(target.ServerPosition) + target.BoundingRadius))
                        {
                            QWER.Cast(poutput.CastPosition);
                        }
                    }
                    else if ((int)poutput.Hitchance == 5)
                    {
                        QWER.Cast(poutput.CastPosition);
                    }
                }
            }
            else if (HitChanceNum == 3 && (int)poutput.Hitchance > 4)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || Player.Distance(target.Position) < SiteToSite)
                    QWER.Cast(poutput.CastPosition);
                else if ((target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) > BackToFront))
                {
                    if (waypoints.Last<Vector2>().To3D().Distance(Player.Position) <= target.Distance(Player.Position))
                    {
                        if (Player.Distance(target.ServerPosition) < QWER.Range - (poutput.CastPosition.Distance(target.ServerPosition)))
                        {
                            QWER.Cast(poutput.CastPosition);
                        }
                    }
                    else
                    {
                        QWER.Cast(poutput.CastPosition);
                    }
                }
            }
            else if (HitChanceNum == 4 && (int)poutput.Hitchance > 4)
            {
                List<Vector2> waypoints = target.GetWaypoints();

                if ((int)target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) == 0)
                    return;

                float SiteToSite = (((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 10 ) - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));

                if ((target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) > BackToFront)
                    || Player.Distance(target.Position) < SiteToSite + target.BoundingRadius * 2
                    || Player.Distance(waypoints.Last<Vector2>().To3D()) < BackToFront)
                {
                    //debug("STS " + (int)SiteToSite + " < " + (int)target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) + " BTF " + (int)Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) + " > " + (int)BackToFront);
                    if (waypoints.Last<Vector2>().To3D().Distance(Player.Position) <= target.Distance(Player.Position))
                    {
                        if (Player.Distance(target.ServerPosition) < QWER.Range - (poutput.CastPosition.Distance(target.ServerPosition)))
                        {
                            QWER.Cast(poutput.CastPosition);
                        }
                    }
                    else
                        QWER.Cast(poutput.CastPosition);

                }
                else
                {
                    //debug("STS " + (int)SiteToSite + " > " + (int)target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) + " BTF " + (int)Math.Abs(Player.Distance(waypoints.Last<Vector2>().To3D()) - Player.Distance(target.Position)) + " > " + (int)BackToFront + " ignore");
                }

            }
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            
        }

        public static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
           if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Q.IsReady()
                && unit.IsValidTarget(Config.Item("maxGrab").GetValue<Slider>().Value)
                && args.SData.IsAutoAttack()
                && ObjectManager.Player.Distance(unit.ServerPosition) > Config.Item("minGrab").GetValue<Slider>().Value
                && Config.Item("Hit").GetValue<Slider>().Value == 3 && unit.IsEnemy && unit.IsValid<Obj_AI_Hero>() && Config.Item("grab" + unit.BaseSkinName).GetValue<bool>())
                Q.CastIfHitchanceEquals(unit, HitChance.High, true);
           if (unit.IsMe && args.SData.Name == "RocketGrabMissile")
           {
               grab++;
           }
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
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("debug").GetValue<bool>())
            {

                Drawing.DrawText(Drawing.Width * 0f, Drawing.Height * 0.4f, System.Drawing.Color.Red, " grab: " + grab + " grab successful: " + grabS + " grab successful % : " + ((grabS / grab) * 100) + "%");
            }
            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && Q.IsReady())
                    if (Q.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
            }
            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && R.IsReady())
                    if (R.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Red);
            }

                var tw = TargetSelector.GetTarget(1500, TargetSelector.DamageType.Physical);
                if (tw.IsValidTarget())
                {
                   
                    if (Config.Item("debug").GetValue<bool>())
                    {
                        List<Vector2> waypoints = tw.GetWaypoints();

                        Render.Circle.DrawCircle(waypoints.Last<Vector2>().To3D(), 100, System.Drawing.Color.Red);
                        Render.Circle.DrawCircle(Q.GetPrediction(tw).CastPosition, 100, System.Drawing.Color.Azure);
                    }
           }
        }
    }
}
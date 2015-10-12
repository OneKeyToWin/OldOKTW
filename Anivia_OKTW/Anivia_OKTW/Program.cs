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
        public const string ChampionName = "Anivia";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        //ManaMenager
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;

        public static int FarmId;

        public static GameObject QMissile;
        public static GameObject RMissile;

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
            Q = new Spell(SpellSlot.Q, 1150);
            W = new Spell(SpellSlot.W, 950);
            E = new Spell(SpellSlot.E, 650);
            R = new Spell(SpellSlot.R, 650);

            Q.SetSkillshot(.25f, 110f, 850f, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(.6f, 1f, float.MaxValue, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(2f, 400f, float.MaxValue, false, SkillshotType.SkillshotCircle);

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

            Config.SubMenu("Farm").AddItem(new MenuItem("LCE", "Lane clear E").SetValue(false));
            Config.SubMenu("Farm").AddItem(new MenuItem("farmR", "Lane clear R").SetValue(false));
            Config.SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana").SetValue(new Slider(60, 100, 30)));
            
            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCQ", "Q").SetValue(false));
            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCW", "W").SetValue(false));

            Config.AddItem(new MenuItem("inter", "OnPossibleToInterrupt W")).SetValue(true);
            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("Haras Q").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));

            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));
            Config.AddItem(new MenuItem("AACombo", "AA in combo").SetValue(false));
            Config.AddItem(new MenuItem("Hit", "Hit Chance Skillshot").SetValue(new Slider(3, 4, 0)));
            
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));
            //Add the events we are going to use:
            Game.OnUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnDelete += Obj_AI_Base_OnDelete;
            Obj_AI_Base.OnCreate +=Obj_AI_Base_OnCreate;
            Orbwalking.BeforeAttack += Orbwalking_BeforeAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnInterruptableSpell;
            Drawing.OnDraw += Drawing_OnDraw;
            Game.PrintChat("<font color=\"#ff00d8\">A</font>nivia full automatic AI ver 1.0 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }

        private static void OnInterruptableSpell(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.Item("inter").GetValue<bool>() && W.IsReady() && unit.IsValidTarget(W.Range))
                W.Cast(unit);
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            var Target = (Obj_AI_Hero)gapcloser.Sender;
            if (Q.IsReady() && Config.Item("AGCQ").GetValue<bool>())
            {
                if (Target.IsValidTarget(Q.Range))
                {
                    Q.Cast(Target);
                    debug("AGC Q");
                }
            }
            else if (W.IsReady() && Config.Item("AGCW").GetValue<bool>())
            {
                if (Target.IsValidTarget(W.Range))
                {
                    W.Cast(Target);
                    debug("AGC W");
                }
            }
        }

        static void Orbwalking_BeforeAttack(LeagueSharp.Common.Orbwalking.BeforeAttackEventArgs args)
        {

            if (FarmId != args.Target.NetworkId)
                FarmId = args.Target.NetworkId;
        }

        private static void Obj_AI_Base_OnCreate(GameObject obj, EventArgs args)
        {
            if (obj.IsValid)
            {
                if (obj.Name == "cryo_FlashFrost_Player_mis.troy")
                    QMissile = obj;
                if (obj.Name.Contains("cryo_storm"))
                    RMissile = obj;
            }
        }

        private static void Obj_AI_Base_OnDelete(GameObject obj, EventArgs args)
        {
            if (obj.IsValid)
            {
                if (obj.Name == "cryo_FlashFrost_Player_mis.troy")
                    QMissile = null;
                if (obj.Name.Contains("cryo_storm"))
                    RMissile = null;
            }
        }
        
        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            PotionMenager();
            if ( Combo && !Config.Item("AACombo").GetValue<bool>())
            {
                if (!E.IsReady())
                    Orbwalking.Attack = true;

                else
                    Orbwalking.Attack = false;
            }
            else
                Orbwalking.Attack = true;

            if (R.IsReady())
            {
                var t = TargetSelector.GetTarget(R.Range + 400, TargetSelector.DamageType.Physical);
                if (RMissile == null && t.IsValidTarget() && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None)
                {
                    if (R.GetDamage(t) > t.Health)
                        R.Cast(t, true, true);
                    else if ( ObjectManager.Player.Mana > RMANA + EMANA && E.GetDamage(t) * 2 + R.GetDamage(t) > t.Health)
                        R.Cast(t, true, true);
                    if (ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA)
                        R.Cast(t, true, true);
                }

                var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, R.Range + 400, MinionTypes.All);
                var Rfarm = R.GetCircularFarmLocation(allMinionsQ, R.Width);

                if (RMissile == null 
                    && ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value 
                    && Config.Item("farmR").GetValue<bool>() && ObjectManager.Player.Mana > QMANA + EMANA
                    && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear
                    && Rfarm.MinionsHit > 2 )
                {
                        R.Cast(Rfarm.Position);
                }

                if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.LaneClear && RMissile != null && (RMissile.Position.CountEnemiesInRange(450) == 0 || ObjectManager.Player.Mana < EMANA + QMANA))
                {
                    R.Cast();
                    debug("combo");
                }
                else if (RMissile != null && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && (Rfarm.MinionsHit < 3 || ObjectManager.Player.Mana < QMANA + EMANA + WMANA || Rfarm.Position.Distance(RMissile.Position) > 400))
                {
                    R.Cast();
                    debug("farm");
                }
            }
            if (W.IsReady())
            {
                var ta = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ta.IsValidTarget(W.Range) && ObjectManager.Player.Mana > RMANA + EMANA + WMANA && ta.Path.Count() == 1 && W.GetPrediction(ta).CastPosition.Distance(ta.Position) > 150)
                {
                    if (ObjectManager.Player.Position.Distance(ta.ServerPosition) > ObjectManager.Player.Position.Distance(ta.Position))
                    {
                        if (ta.Position.Distance(ObjectManager.Player.ServerPosition) < ta.Position.Distance(ObjectManager.Player.Position) && ta.IsValidTarget(W.Range - 200))
                            CastSpell(W, ta, Config.Item("Hit").GetValue<Slider>().Value);
                    }
                    else
                    {
                        if (ta.Position.Distance(ObjectManager.Player.ServerPosition) > ta.Position.Distance(ObjectManager.Player.Position) && ta.IsValidTarget(E.Range) && ta.HasBuffOfType(BuffType.Slow))
                            CastSpell(W, ta, Config.Item("Hit").GetValue<Slider>().Value);
                    }
                }
            }
            if (Q.IsReady() && QMissile == null)
            {
                ManaMenager();
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var qDmg = Q.GetDamage(t);
                    
                    if (qDmg > t.Health)
                        Q.Cast(t, true);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + QMANA)
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Farm &&  ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA) && !ObjectManager.Player.UnderTurret(true))
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range) && Config.Item("haras" + enemy.BaseSkinName).GetValue<bool>()))
                        {
                            CastSpell(Q, enemy, Config.Item("Hit").GetValue<Slider>().Value);
                        }
                    }

                    else if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Farm) && ObjectManager.Player.Mana > RMANA + QMANA + EMANA)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                            {
                                Q.Cast(enemy, true);
                            }
                        }
                    }
                }
            }
            
            if (E.IsReady() )
            {
                ManaMenager();
                var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    
                    var qCd = Q.Instance.CooldownExpires - Game.Time;
                    var rCd = R.Instance.CooldownExpires - Game.Time;
                    if (ObjectManager.Player.Level < 7)
                        rCd = 10;
                    //debug("Q " + qCd + "R " + rCd + "E now " + E.Instance.Cooldown);
                    var eDmg = E.GetDamage(t);
                    if (t.HasBuff("chilled"))
                    {
                        eDmg = 2 * eDmg;
                    }
                    if (eDmg > t.Health)
                        E.Cast(t, true);
                    else if ((t.HasBuff("chilled") || (qCd > E.Instance.Cooldown - 1 && rCd > E.Instance.Cooldown-1)) && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + EMANA && QMissile == null)
                    {
                        if (RMissile == null && R.IsReady())
                            R.Cast(t, true, true);
                        E.Cast(t, true);
                    }
                    else if (t.HasBuff("chilled") && (Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA) && !ObjectManager.Player.UnderTurret(true) && QMissile == null)
                    {
                        if (RMissile == null && R.IsReady())
                            R.Cast(t, true, true);
                        E.Cast(t, true);
                    }
                    else if (t.HasBuff("chilled")  && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                    {
                        E.Cast(t, true);
                    }
                }
                farmE();
            }
            if (Q.IsReady() && QMissile != null)
            {
                if (QMissile.Position.CountEnemiesInRange(220) > 0)
                    Q.Cast();
            }
        }

        public static void farmE()
        {
            if (Config.Item("LCE").GetValue<bool>() && ObjectManager.Player.Mana > QMANA + EMANA + WMANA && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && !Orbwalking.CanAttack() && ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value)
            {

                var mobs = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    E.Cast(mob, true);
                    return;
                }
                
                var minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, E.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth);
                foreach (var minion in minions.Where(minion =>  minion.Health > ObjectManager.Player.GetAutoAttackDamage(minion) && FarmId != minion.NetworkId ))
                {
                    var eDmg = E.GetDamage(minion);
                    if (minion.HasBuff("chilled"))
                        eDmg = 2 * eDmg;
                    
                    if(minion.Health < eDmg * 0.9 ) 
                        E.Cast(minion);
                }
            }
        }

        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            //HitChance 0 - 2
            // example CastSpell(Q, ts, 2);
            var poutput = QWER.GetPrediction(target);
            var col = poutput.CollisionObjects.Count(ColObj => ColObj.IsEnemy && ColObj.IsMinion && !ColObj.IsDead);
            if (target.IsDead || col > 0 || target.Path.Count() > 1)
                return;

            if ((target.Path.Count() == 0 && target.Position == target.ServerPosition) || target.HasBuff("Recall"))
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
            else if (HitChanceNum == 3)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed) - QWER.Width) * 6;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
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
                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed) - QWER.Width) * 6;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));

                if (Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
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
        }

        private static bool Combo
        {
            get { return Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo; }
        }
        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
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

       public static void debug(string msg)
        {
            if (Config.Item("debug").GetValue<bool>())
                Game.PrintChat(msg);
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (RMissile != null)
            Render.Circle.DrawCircle(RMissile.Position, 200, System.Drawing.Color.Cyan);

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
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && E.IsReady())
                    if (E.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Blue);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Blue);
            }
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && W.IsReady())
                    if (W.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Yellow);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, W.Range, System.Drawing.Color.Yellow);
            }
        }
    }
}

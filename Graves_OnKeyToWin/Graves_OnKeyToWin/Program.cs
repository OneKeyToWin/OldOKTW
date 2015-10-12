using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
namespace Graves_OnKeyToWin
{
    class Program
    {
        public const string ChampionName = "Graves";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell Q1;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static Spell R1;
        public static bool attackNow = true;
        //ManaMenager
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;
        public static float qRange = 900;
        public static bool Esmart = false;
        public static double secoundDmgR = 0.65;
        public static double OverKill = 0;
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
            Q = new Spell(SpellSlot.Q, 900);
            W = new Spell(SpellSlot.W, 950f);
            E = new Spell(SpellSlot.E, 450f);
            R = new Spell(SpellSlot.R, 1000f);
            R1 = new Spell(SpellSlot.R, 1500f);

            Q.SetSkillshot(0.26f, 50f, 1950f, false, SkillshotType.SkillshotLine);
            W.SetSkillshot(0.35f, 250f, 1650f, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.25f, 120f, 2100f, false, SkillshotType.SkillshotLine);
            R1.SetSkillshot(0.26f, 120f, 2100f, false, SkillshotType.SkillshotLine);

            SpellList.Add(Q);
            SpellList.Add(Q1);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);
            SpellList.Add(R1);
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

            Config.SubMenu("Draw").AddItem(new MenuItem("noti", "Show notification").SetValue(false));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("OrbDraw", "Draw AAcirlce OKTW© style").SetValue(false));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("", "pls disable Orbwalking > Drawing > AAcirlce"));
            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("orb", "Orbwalker target").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("qTarget", "Q Target").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("semi", "Semi-manual R target").SetValue(false));

            foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.Team != Player.Team))
                Config.SubMenu("Haras").AddItem(new MenuItem("haras" + enemy.BaseSkinName, enemy.BaseSkinName).SetValue(true));

            Config.SubMenu("R config").AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.SubMenu("R config").AddItem(new MenuItem("fastR", "Fast R ks Combo").SetValue(false));
            Config.SubMenu("R config").AddItem(new MenuItem("useR", "Semi-manual cast R key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space

            Config.SubMenu("E config").AddItem(new MenuItem("autoE", "Auto E").SetValue(true));
            Config.SubMenu("E config").AddItem(new MenuItem("smartE", "SmartCast E key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space

            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCE", "AntiGapcloserE").SetValue(true));
            Config.SubMenu("AntiGapcloser").AddItem(new MenuItem("AGCW", "AntiGapcloserW").SetValue(true));

            Config.SubMenu("Farm").AddItem(new MenuItem("farmQ", "Lane clear Q").SetValue(true));
            Config.SubMenu("Farm").AddItem(new MenuItem("Mana", "LaneClear Mana").SetValue(new Slider(80, 100, 30)));

            Config.AddItem(new MenuItem("pots", "Use pots").SetValue(true));
            Config.AddItem(new MenuItem("Hit", "Hit Chance skills").SetValue(new Slider(3, 3, 0)));
            
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));
            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += afterAttack;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Game.PrintChat("<font color=\"#9c3232\">G</font>raves full automatic AI ver 1.7 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if ( ObjectManager.Player.Mana > RMANA + EMANA && ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range).CountEnemiesInRange(400) < 3)
            {
                var Target = (Obj_AI_Hero)gapcloser.Sender;
                if (Target.IsValidTarget(E.Range) && E.IsReady() )
                {
                    if (Config.Item("AGCE").GetValue<bool>() && E.IsReady())
                    {
                        E.Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range), true);
                        debug("E AGC");
                    }
                    if (Config.Item("AGCW").GetValue<bool>() && W.IsReady())
                    {
                        W.Cast(ObjectManager.Player.Position, true);
                        debug("W AGC");
                    }
                }
            }
            return;
        }
        public static void debug(string msg)
        {
            if (Config.Item("debug").GetValue<bool>())
                Game.PrintChat(msg);
        }

        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();


            

            if (E.IsReady())
            {
                if (Config.Item("smartE").GetValue<KeyBind>().Active)
                    Esmart = true;
                if (Esmart && ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range).CountEnemiesInRange(500) < 4)
                    E.Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range), true);
            }
            else
                Esmart = false;

            if (W.IsReady() && attackNow)
            {
                var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Magical);
                if (t.IsValidTarget())
                {
                    if (W.GetDamage(t) > t.Health && !Orbwalking.InAutoAttackRange(t))
                    {
                        W.Cast(t, true, true);
                        debug("W ks");
                        OverKill = Game.Time;
                        return;
                    }
                    else if (W.GetDamage(t) + Q.GetDamage(t) > t.Health && ObjectManager.Player.Mana >  QMANA + EMANA + RMANA )
                        W.Cast(t, true, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + QMANA + EMANA + WMANA)
                        W.Cast(t, true, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + WMANA + QMANA + 5
                        && !Orbwalking.InAutoAttackRange(t))
                        W.Cast(t, true, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + QMANA + WMANA
                       && ObjectManager.Player.CountEnemiesInRange(300) > 0)
                        W.Cast(t, true, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + WMANA + EMANA
                        && ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.4)
                        W.Cast(t, true, true);
                    else if ((Orbwalker.ActiveMode.ToString() == "Combo" || Farm) && ObjectManager.Player.Mana > RMANA + QMANA + WMANA)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                                W.Cast(enemy, true, true);
                        }
                    }
                }
            }
            if (E.IsReady() && Config.Item("autoE").GetValue<bool>() && attackNow)
            {
                var t = TargetSelector.GetTarget(E.Range + Q.Range, TargetSelector.DamageType.Physical);
                var t2 = TargetSelector.GetTarget(900f, TargetSelector.DamageType.Physical);
                if ( ObjectManager.Player.Mana > RMANA + EMANA
                    && ObjectManager.Player.CountEnemiesInRange(240) > 0
                    && ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range).CountEnemiesInRange(500) < 3
                    && t.Position.Distance(Game.CursorPos) > t.Position.Distance(ObjectManager.Player.Position))
                    E.Cast(ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range), true);
                else if (E.IsReady() && (Game.Time - OverKill > 0.4) && ObjectManager.Player.Position.Extend(Game.CursorPos, E.Range).CountEnemiesInRange(700) < 3 && Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Health > ObjectManager.Player.MaxHealth * 0.4 && !ObjectManager.Player.UnderTurret(true))
                {
                    if (t.IsValidTarget()
                    && Q.IsReady()
                    && ObjectManager.Player.Mana > RMANA + EMANA
                    && Q.GetDamage(t) + ObjectManager.Player.GetAutoAttackDamage(t2) * 3 > t.Health
                    && t.Position.Distance(Game.CursorPos) + 200 < t.Position.Distance(ObjectManager.Player.Position) 
                    && !Orbwalking.InAutoAttackRange(t)
                    )
                    {
                        E.Cast(Game.CursorPos, true);
                        debug("E + aa + Q");
                    }
                    else if (t2.IsValidTarget()
                     && ObjectManager.Player.Mana > QMANA + RMANA
                     && ObjectManager.Player.GetAutoAttackDamage(t2) * 2 > t2.Health
                     && !Orbwalking.InAutoAttackRange(t2)
                     && t2.Position.Distance(Game.CursorPos) + 200 < t2.Position.Distance(ObjectManager.Player.Position) 
                     )
                    {
                        E.Cast(Game.CursorPos, true);
                        debug("E + aa");
                    }
                }
            }

            if (Q.IsReady() && attackNow)
            {
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    if (Q.GetDamage(t) + ObjectManager.Player.GetAutoAttackDamage(t) > t.Health)
                    {
                        Q.Cast(t, true);
                        OverKill = Game.Time;
                        debug("Q ks");
                    }
                    else if (Q.GetDamage(t) + R.GetDamage(t) > t.Health && R.IsReady())
                    {
                        Q.Cast(t, true);
                        debug("Q + R ks");
                    }
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + QMANA && attackNow)
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) && ObjectManager.Player.Mana > RMANA + EMANA + WMANA + QMANA + QMANA) && t.IsValidTarget(Q.Range - 100) && attackNow && Config.Item("haras" + t.BaseSkinName).GetValue<bool>())
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Orbwalker.ActiveMode.ToString() == "Combo" || Farm) && ObjectManager.Player.Mana > RMANA + QMANA + EMANA && attackNow)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                                Q.Cast(enemy, true);
                        }
                    }
                }
                else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && ObjectManager.Player.ManaPercentage() > Config.Item("Mana").GetValue<Slider>().Value && Config.Item("farmQ").GetValue<bool>() && ObjectManager.Player.Mana > RMANA + QMANA + EMANA + WMANA )
                {
                    var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All);
                    var Qfarm = Q.GetCircularFarmLocation(allMinionsQ, 200);
                    if (Qfarm.MinionsHit > 3 && Q.IsReady())
                        Q.Cast(Qfarm.Position);
                }
            }

            var tar = TargetSelector.GetTarget(1800, TargetSelector.DamageType.Physical);
            if (Config.Item("useR").GetValue<KeyBind>().Active && tar.IsValidTarget() && R.IsReady())
            {
                    R1.Cast(tar, true);
            }
            if (R.IsReady() && Config.Item("autoR").GetValue<bool>())
            {
                bool cast = false;
                foreach (var target in ObjectManager.Get<Obj_AI_Hero>().Where(target => target.IsValidTarget(R1.Range)))
                {
                    if (ValidUlt(target))
                    {
                        float predictedHealth = target.Health + target.HPRegenRate * 2;
                        double Rdmg = R.GetDamage(target) + (R.GetDamage(target) * target.CountAlliesInRange(400) * 0.2);
                        var collisionTarget = target;
                        cast = true;
                        PredictionOutput output = R.GetPrediction(target);
                        Vector2 direction = output.CastPosition.To2D() - Player.Position.To2D();
                        direction.Normalize();
                        List<Obj_AI_Hero> enemies = ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget()).ToList();
                        foreach (var enemy in enemies)
                        {
                            if (enemy.SkinName == target.SkinName || !cast)
                                continue;
                            PredictionOutput prediction = R.GetPrediction(enemy);
                            Vector3 predictedPosition = prediction.CastPosition;
                            Vector3 v = output.CastPosition - Player.ServerPosition;
                            Vector3 w = predictedPosition - Player.ServerPosition;
                            double c1 = Vector3.Dot(w, v);
                            double c2 = Vector3.Dot(v, v);
                            double b = c1 / c2;
                            Vector3 pb = Player.ServerPosition + ((float)b * v);
                            float length = Vector3.Distance(predictedPosition, pb);
                            if (length < (120 + enemy.BoundingRadius) && Player.Distance(predictedPosition) < Player.Distance(target.ServerPosition))
                            {
                                cast = false;
                                collisionTarget = enemy;
                            }
                        }
                        if (cast
                            && Rdmg > predictedHealth
                            && target.IsValidTarget(R.Range)
                            && (!Orbwalking.InAutoAttackRange(target) || ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.6))
                        {
                            CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("Rdmg");
                        }
                        else if (cast
                            && Rdmg * secoundDmgR > predictedHealth
                            && target.IsValidTarget(R1.Range)
                            && target.CountAlliesInRange(300) == 0 && (!Orbwalking.InAutoAttackRange(target) || ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.6))
                        {
                            CastSpell(R1, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("Rdmg 0.7");
                        }
                        else if (!cast && Rdmg * secoundDmgR > predictedHealth
                            && target.IsValidTarget(GetRealDistance(collisionTarget) + 700))
                        {
                            CastSpell(R1, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("Rdmg 0.7 collision");
                        }
                        else if (cast 
                            && Config.Item("fastR").GetValue<bool>() 
                            && Rdmg  > predictedHealth 
                            && Orbwalking.InAutoAttackRange(target) 
                            && Orbwalker.ActiveMode.ToString() == "Combo")
                        {
                            CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("R fast");
                        }
                    }
                }
            }
            PotionMenager();
            if (Orbwalker.GetTarget() == null)
                attackNow = true;
        }

        private static bool ValidUlt(Obj_AI_Hero target)
        {
            if (target.HasBuffOfType(BuffType.PhysicalImmunity)
            || target.HasBuffOfType(BuffType.SpellImmunity)
            || target.IsZombie
            || target.HasBuffOfType(BuffType.Invulnerability)
            || target.HasBuffOfType(BuffType.SpellShield)
            )
                return false;
            else
                return true;
        }

        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            //HitChance 0 - 2
            // example CastSpell(Q, ts, 2);
            var poutput = QWER.GetPrediction(target);
            var col = poutput.CollisionObjects.Count(ColObj => ColObj.IsEnemy && ColObj.IsMinion && !ColObj.IsDead);
            if (QWER.Collision && col > 0)
                return;
            if (HitChanceNum == 0)
                QWER.Cast(target, true);
            else if (HitChanceNum == 1)
            {
                if ((int)poutput.Hitchance > 4)
                    QWER.Cast(poutput.CastPosition);
            }
            else if (HitChanceNum == 2)
            {
                if ((target.IsFacing(ObjectManager.Player) && (int)poutput.Hitchance == 5) || (target.Path.Count() == 0 && target.Position == target.ServerPosition))
                {
                    if (ObjectManager.Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed) + (target.BoundingRadius * 2)))
                    {
                        QWER.Cast(poutput.CastPosition);
                    }
                }
                else if ((int)poutput.Hitchance == 5)
                {
                    QWER.Cast(poutput.CastPosition);
                }
            }
            else if (HitChanceNum == 3)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || ObjectManager.Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                else if (target.Path.Count() < 2
                    && (target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) - ObjectManager.Player.Distance(target.Position)) > BackToFront
                    || target.HasBuffOfType(BuffType.Slow) || target.HasBuff("Recall")
                    || (target.Path.Count() == 0 && target.Position == target.ServerPosition)
                    ))
                {
                    if (target.IsFacing(ObjectManager.Player) || target.Path.Count() == 0)
                    {
                        if (ObjectManager.Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed) + (target.BoundingRadius * 2)))
                            QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                    else
                    {
                        QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }
            }
            else if (HitChanceNum == 4 && (int)poutput.Hitchance > 4)
            {
                List<Vector2> waypoints = target.GetWaypoints();
                float SiteToSite = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed)) * 6 - QWER.Width;
                float BackToFront = ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.ServerPosition) / QWER.Speed));
                if (ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) < SiteToSite || ObjectManager.Player.Distance(target.Position) < SiteToSite)
                    QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                else if (target.Path.Count() < 2
                    && (target.ServerPosition.Distance(waypoints.Last<Vector2>().To3D()) > SiteToSite
                    || Math.Abs(ObjectManager.Player.Distance(waypoints.Last<Vector2>().To3D()) - ObjectManager.Player.Distance(target.Position)) > BackToFront
                    || target.HasBuffOfType(BuffType.Slow) || target.HasBuff("Recall")
                    || (target.Path.Count() == 0 && target.Position == target.ServerPosition)
                    ))
                {
                    if (target.IsFacing(ObjectManager.Player) || target.Path.Count() == 0)
                    {
                        if (ObjectManager.Player.Distance(target.Position) < QWER.Range - ((target.MoveSpeed * QWER.Delay) + (Player.Distance(target.Position) / QWER.Speed) + (target.BoundingRadius * 2)))
                            QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                    else
                    {
                        QWER.CastIfHitchanceEquals(target, HitChance.High, true);
                    }
                }
            }
        }

        private static void afterAttack(AttackableUnit unit, AttackableUnit target)
        {
            if (!unit.IsMe)
                return;
            attackNow = true;
        }
        static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            attackNow = false;
        }

        private static float GetRealDistance(GameObject target)
        {
            return ObjectManager.Player.ServerPosition.Distance(target.Position) + ObjectManager.Player.BoundingRadius +
                   target.BoundingRadius;
        }

        public static void ManaMenager()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
            if (!R.IsReady())
                RMANA = QMANA - ObjectManager.Player.Level * 3;
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
                    if (ObjectManager.Player.CountEnemiesInRange(1200) > 0 && ObjectManager.Player.Mana < RMANA + WMANA + EMANA)
                        ManaPotion.Cast();
                }
            }
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (Config.Item("OrbDraw").GetValue<bool>())
            {
                if (ObjectManager.Player.HealthPercentage() > 60)
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.GreenYellow, 1, 1);
                else if (ObjectManager.Player.HealthPercentage() > 30)
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.Orange, 2, 1);
                else 
                    Utility.DrawCircle(ObjectManager.Player.Position, ObjectManager.Player.AttackRange + ObjectManager.Player.BoundingRadius * 2, System.Drawing.Color.Red, 3, 1);
            }
            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && Q.IsReady())
                    if (Q.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range , System.Drawing.Color.Gray,1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Gray, 1, 1);
            }
            if (Config.Item("wRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && W.IsReady())
                    if (W.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && E.IsReady())
                    if (E.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Gray, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Gray, 1, 1);
            }
            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && R.IsReady())
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                    else
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
            }
            if (Config.Item("noti").GetValue<bool>())
            {
                var t = TargetSelector.GetTarget(1800, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget() && R.IsReady())
                {
                    var rDamage = R.GetDamage(t) * secoundDmgR;
                    if (rDamage > t.Health)
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.5f, System.Drawing.Color.Red, "Ult can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                    if (Config.Item("useR").GetValue<KeyBind>().Active)
                    {
                        R1.Cast(t, true);
                    }
                }
                var tw = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (tw.IsValidTarget())
                {
                    if (Q.GetDamage(tw) > tw.Health)
                    {
                        Utility.DrawCircle(ObjectManager.Player.ServerPosition, Q.Range, System.Drawing.Color.Red);
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.4f, System.Drawing.Color.Red, "Q can kill: " + t.ChampionName + " have: " + t.Health + "hp");
                    }
                }
            }
        }
    }
}
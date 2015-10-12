using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using System.Diagnostics;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
using System.Threading;

namespace KogMaw
{
    class Program
    {
        public const string ChampionName = "KogMaw";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;

        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W; 
        public static Spell E;
        public static Spell R;
        public static bool attackNow = true;
        //ManaMenager
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;
        public static double OverKill = 0;
        //AutoPotion
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);

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
            if (Player.ChampionName != ChampionName) return;

            //Create the spells
            Q = new Spell(SpellSlot.Q, 980);
            W = new Spell(SpellSlot.W, 1000);
            E = new Spell(SpellSlot.E, 1200);
            R = new Spell(SpellSlot.R, 1800);

            Q.SetSkillshot(0.25f, 50f, 2000f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.25f, 120f, 1400f, false, SkillshotType.SkillshotLine);
            R.SetSkillshot(1.5f, 200f, float.MaxValue, false, SkillshotType.SkillshotCircle);

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

            Config.SubMenu("Iteams").AddItem(new MenuItem("mura", "Auto Muramana").SetValue(true));
            Config.SubMenu("Iteams").AddItem(new MenuItem("pots", "Use pots").SetValue(true));

            Config.SubMenu("E config").AddItem(new MenuItem("AGC", "AntiGapcloserE").SetValue(true));

            Config.SubMenu("W config").AddItem(new MenuItem("autoW", "Auto W").SetValue(true));
            Config.SubMenu("W config").AddItem(new MenuItem("harasW", "Haras W on max range").SetValue(true));

            Config.SubMenu("R option").AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("comboStack", "Max combo stack R").SetValue(new Slider(2, 10, 0)));
            Config.SubMenu("R option").AddItem(new MenuItem("harasStack", "Max haras stack R").SetValue(new Slider(1, 10, 0)));
            Config.SubMenu("R option").AddItem(new MenuItem("Rcc", "R cc").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("Rslow", "R slow").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("Raoe", "R aoe").SetValue(true));

            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("OrbDraw", "Draw AAcirlce OKTW© style").SetValue(false));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("1", "pls disable Orbwalking > Drawing > AAcirlce"));
            Config.SubMenu("Draw").SubMenu("Draw AAcirlce OKTW© style").AddItem(new MenuItem("2", "My HP: 0-30 red, 30-60 orange,60-100 green"));
            Config.SubMenu("Draw").AddItem(new MenuItem("ComboInfo", "R killable info").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("noti", "Show notification").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("wRange", "W range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw only ready spells").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("orb", "Orbwalker target OKTW© style").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("semi", "Semi-manual R target").SetValue(false));

            Config.AddItem(new MenuItem("sheen", "Sheen logic").SetValue(true));
            Config.AddItem(new MenuItem("AApriority", "AA priority over spell").SetValue(true));
            
            Config.AddItem(new MenuItem("Hit", "Hit Chance Skillshot").SetValue(new Slider(3, 4, 0)));
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));
            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;

            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += afterAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Game.PrintChat("<font color=\"#008aff\">K</font>og Maw full automatic AI ver 1.3 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }


        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();

            if (Player.IsZombie && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                var t = TargetSelector.GetTarget(800, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                Player.IssueOrder(GameObjectOrder.MoveTo, t.ServerPosition);
            }

            if (E.IsReady() && Sheen())
            {
                //W.Cast(ObjectManager.Player);
                var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var qDmg = Q.GetDamage(t);
                    var eDmg = E.GetDamage(t);
                    if (eDmg > t.Health)
                        CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (eDmg + qDmg > t.Health && Q.IsReady())
                        CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + WMANA + EMANA + QMANA)
                        CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Farm) && ObjectManager.Player.Mana > RMANA + WMANA + EMANA)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(E.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow) || enemy.HasBuff("Recall"))
                            {
                                E.Cast(enemy, true);
                            }
                        }
                    }
                }
            }
            if (Q.IsReady() && Sheen())
            {
                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget() )
                {
                    var qDmg = Q.GetDamage(t);
                    var eDmg = E.GetDamage(t);
                    if (t.IsValidTarget(W.Range) && qDmg + eDmg > t.Health)
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + QMANA * 2 + EMANA  )
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA*2 + WMANA) && !ObjectManager.Player.UnderTurret(true) )
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
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
            PotionMenager();

            if (Config.Item("autoW").GetValue<bool>() && W.IsReady() )
            {
                W.Range = 650 + 110 + 20 * ObjectManager.Player.Spellbook.GetSpell(SpellSlot.W).Level;
                if (ObjectManager.Player.CountEnemiesInRange(W.Range) > 0 && Sheen())
                {
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                    W.Cast();
                else if (Farm && Config.Item("harasW").GetValue<bool>())
                    W.Cast();
                else if (Farm && ObjectManager.Player.CountEnemiesInRange(ObjectManager.Player.AttackRange) > 0)
                    W.Cast();
                }
            }

            if (R.IsReady() && Config.Item("autoR").GetValue<bool>() && Sheen())
            {
                R.Range = 800 + 300 * ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Level;
                var target = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Magical);
                if (target.IsValidTarget(R.Range) && (Game.Time - OverKill > 0.6) && ValidUlt(target))
                {
                    double Rdmg = R.GetDamage(target) + (R.GetDamage(target) * target.CountAlliesInRange(500));
                    // Overkill protection
                    if (target.Health < R.GetDamage(target) * target.CountAlliesInRange(500) * 0.2)
                        Rdmg = 0;

                    var harasStack = Config.Item("harasStack").GetValue<Slider>().Value;
                    var comboStack = Config.Item("comboStack").GetValue<Slider>().Value;

                    if ( R.GetDamage(target) > target.Health)
                        CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && Rdmg * 2 > target.Health && ObjectManager.Player.Mana > RMANA * 3)
                        CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ( GetRStacks() < comboStack + 2 && ObjectManager.Player.Mana > RMANA * 3)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(R.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                                    enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                                    enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Suppression) ||
                                    enemy.IsStunned || enemy.HasBuff("Recall"))
                                R.Cast(enemy, true);
                            else
                                R.CastIfHitchanceEquals(enemy, HitChance.Immobile, true);
                        }
                    }

                    if (target.HasBuffOfType(BuffType.Slow) && Config.Item("Rslow").GetValue<bool>() && GetRStacks() < comboStack + 1 && ObjectManager.Player.Mana > RMANA + WMANA + EMANA + QMANA)
                        CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && GetRStacks() < comboStack && ObjectManager.Player.Mana > RMANA + WMANA + EMANA + QMANA)
                        CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ( Farm && GetRStacks() < harasStack  && ObjectManager.Player.Mana > RMANA + WMANA + EMANA + QMANA)
                        CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                }
            }
        }

        private static bool Sheen()
        {
            var target = Orbwalker.GetTarget();
            if (!(target is Obj_AI_Hero))
                attackNow = true;
            if (target.IsValidTarget() && Player.HasBuff("sheen") && Config.Item("sheen").GetValue<bool>() && target is Obj_AI_Hero)
            {
                debug("shen true");
                return false;
            }
            else if (target.IsValidTarget() && Config.Item("AApriority").GetValue<bool>() && target is Obj_AI_Hero && !attackNow )
            {
                debug("spellDisable");
                return false;
            }
            else
            {
                return true;
            }
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

        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
        }
        private static int GetRStacks()
        {
            foreach (var buff in ObjectManager.Player.Buffs)
            {
                if (buff.Name == "kogmawlivingartillerycost")
                    return buff.Count;
            }
            return 0;
        }

        public static void debug(string msg)
        {
            if (Config.Item("debug").GetValue<bool>())
                Game.PrintChat(msg);
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

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Config.Item("AGC").GetValue<bool>() && E.IsReady() && ObjectManager.Player.Mana > RMANA + EMANA)
            {
                var Target = (Obj_AI_Hero)gapcloser.Sender;
                if (Target.IsValidTarget(E.Range))
                {
                    E.Cast(Target, true);
                    debug("E AGC");
                }
            }
            return;
        }

        public static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
            if (args.Target == null)
                return;
            foreach (var target in ObjectManager.Get<Obj_AI_Hero>())
            {
                if (args.Target.NetworkId == target.NetworkId && args.Target.IsEnemy)
                {
                    var dmg = unit.GetSpellDamage(target, args.SData.Name);
                    double HpLeft = target.Health - dmg;
                    if (HpLeft < 0 && target.IsValidTarget() && target.IsValidTarget(R.Range))
                    {
                        OverKill = Game.Time;
                        debug("OverKill detection " + target.ChampionName);
                    }
                    if (target.IsValidTarget(Q.Range) && Q.IsReady())
                    {
                        var qDmg = Q.GetDamage(target);
                        if (qDmg > HpLeft && HpLeft > 0)
                        {
                            CastSpell(Q, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("Q ks OPS");
                        }
                    }
                    if (target.IsValidTarget(W.Range) && W.IsReady())
                    {
                        var wDmg = W.GetDamage(target);
                        if (wDmg > HpLeft && HpLeft > 0)
                        {
                            CastSpell(W, target, Config.Item("Hit").GetValue<Slider>().Value);
                            debug("W ks OPS");
                        }
                    }
                    if (Config.Item("autoR").GetValue<bool>() && target.IsValidTarget(R.Range) && R.IsReady() )
                    {
                        double rDmg = R.GetDamage(target) + (R.GetDamage(target) * target.CountAlliesInRange(500));
                        if (rDmg > HpLeft && HpLeft > 0)
                        {
                            debug("R OPS");
                            CastSpell(R, target, Config.Item("Hit").GetValue<Slider>().Value);
                        }
                    }
                }
            }
        }

        public static void ManaMenager()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
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
                    if (R.GetDamage(enemy) > enemy.Health)
                    {
                        combo = "KILL R";
                        drawText(combo, enemy, System.Drawing.Color.GreenYellow);
                    }
                    else
                    {
                        combo = (int)(enemy.Health / R.GetDamage(enemy)) + " R";
                        drawText(combo, enemy, System.Drawing.Color.Red);
                    }
                    
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
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (E.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow, 1, 1);
            }
            if (Config.Item("rRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>())
                {
                    if (R.IsReady())
                        Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
                }
                else
                    Utility.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Gray, 1, 1);
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
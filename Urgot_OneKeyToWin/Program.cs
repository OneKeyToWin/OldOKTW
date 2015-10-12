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

namespace Urgot_OneKeyToWin
{
    class Program
    {
        public const string ChampionName = "Urgot";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;

        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell Q2;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        public static bool attackNow = true;
        //ManaMenager
        public static int FarmId;
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;
        public static bool Farm = false;
        public static bool Esmart = false;
        public static double WCastTime = 0;
        public static double OverKill = 0;
        public static double OverFarm = 0;
        public static double lag = 0;
        public static List<Obj_AI_Base> minions;
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
            Q = new Spell(SpellSlot.Q, 980);
            Q2 = new Spell(SpellSlot.Q, 1200);
            W = new Spell(SpellSlot.W);
            E = new Spell(SpellSlot.E, 1100);
            R = new Spell(SpellSlot.R, 850);

            Q.SetSkillshot(0.25f, 60f, 1600f, true, SkillshotType.SkillshotLine);
            Q2.SetSkillshot(0.25f, 60f, 1600f, false, SkillshotType.SkillshotLine);
            E.SetSkillshot(0.25f, 300f, 1750f, false, SkillshotType.SkillshotCircle);
    
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
            Config.SubMenu("Iteams").AddItem(new MenuItem("stack", "Stack Tear if full mana").SetValue(false));
            Config.SubMenu("Iteams").AddItem(new MenuItem("pots", "Use pots").SetValue(true));

            Config.SubMenu("E config").AddItem(new MenuItem("autoE", "Auto E haras").SetValue(true));

            Config.SubMenu("R option").AddItem(new MenuItem("autoR", "Auto R under turrent").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("inter", "OnPossibleToInterrupt R").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("Rhp", "dont R if under % hp").SetValue(new Slider(50, 100, 0)));
            Config.SubMenu("R option").AddItem(new MenuItem("useR", "Semi-manual cast R key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space

            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("orb", "Orbwalker target").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("qTarget", "Q Target").SetValue(true));
            Config.SubMenu("Draw").AddItem(new MenuItem("semi", "Semi-manual R target").SetValue(false));

            #region Shield
            Config.SubMenu("W Shield Config").AddItem(new MenuItem("autoW", "Auto W").SetValue(true));
            Config.SubMenu("W Shield Config").AddItem(new MenuItem("AGC", "AntiGapcloserW").SetValue(true));
            Config.SubMenu("W Shield Config").AddItem(new MenuItem("Wdmg", "W dmg % hp").SetValue(new Slider(10, 100, 0)));
            #endregion

            Config.SubMenu("Farm").AddItem(new MenuItem("farmQ", "Farm Q").SetValue(true));
            Config.SubMenu("Farm").AddItem(new MenuItem("LC", "LaneClear").SetValue(true));
            Config.AddItem(new MenuItem("Hit", "Hit Chance Skillshot").SetValue(new Slider(4, 4, 0)));
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));
            Config.AddItem(new MenuItem("urf", "Urf mode").SetValue(false));
            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Orbwalking.BeforeAttack += BeforeAttack;
            Orbwalking.AfterAttack += afterAttack;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            AntiGapcloser.OnEnemyGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnPossibleToInterrupt += OnInterruptableSpell;
            Game.PrintChat("<font color=\"#008aff\">U</font>rgot full automatic AI ver 1.0 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }
        #region Farm

        private static void OnInterruptableSpell(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.Item("inter").GetValue<bool>() && R.IsReady() && unit.IsValidTarget(R.Range) && ObjectManager.Player.HealthPercentage() >= Config.Item("Rhp").GetValue<Slider>().Value)
                R.Cast(unit);
        }

        public static void farmQ()
        {
            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                var mobs = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, 800, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth);
                if (mobs.Count > 0)
                {
                    var mob = mobs[0];
                    Q.Cast(mob, true);
                }
            }

            minions = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range, MinionTypes.All, MinionTeam.Enemy, MinionOrderTypes.MaxHealth);
            foreach (var minion in minions.Where(minion => FarmId != minion.NetworkId))
            {
                if (!Orbwalker.InAutoAttackRange(minion))
                {
                    if (minion.Health < Q.GetDamage(minion))
                    {
                        Q.Cast(minion);
                        FarmId = minion.NetworkId;
                        return;
                    }
                }
                else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear && Config.Item("LC").GetValue<bool>())
                {
                    if (minion.Health < Q.GetDamage(minion) * 0.9)
                    {
                        Q.Cast(minion);
                        FarmId = minion.NetworkId;
                        return;
                    }   
                }
            }
        }

        #endregion

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear || Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit)
                Farm = true;
            else
                Farm = false;

            if (Orbwalker.GetTarget() == null)
                attackNow = true;

            if (E.IsReady())
            {
                var qCd = Q.Instance.CooldownExpires - Game.Time;
                //W.Cast(ObjectManager.Player);
                ManaMenager();
                var t = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var qDmg = Q.GetDamage(t);
                    var eDmg = E.GetDamage(t);
                    if (eDmg > t.Health)
                        E.Cast(t);
                    else if (eDmg + qDmg > t.Health && ObjectManager.Player.Mana > EMANA + QMANA)
                        CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > EMANA + QMANA * 2 && qCd < 0.5f)
                        CastSpell(E, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if (Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA * 5 && Config.Item("autoE").GetValue<bool>())
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
            if (W.IsReady() && Config.Item("urf").GetValue<bool>() && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.None)
            {
                W.Cast();
            }
            if (Q.IsReady())
            {
                ManaMenager();
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q2.Range)))
                {
                    if (enemy.HasBuff("urgotcorrosivedebuff") && (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo || Farm))
                    {
                        Q2.Cast(enemy.ServerPosition);
                        if ((ObjectManager.Player.Mana > EMANA + QMANA * 4 || Q.GetDamage(enemy) * 3 > enemy.Health) && !Orbwalking.InAutoAttackRange(enemy) && W.IsReady())
                            W.Cast();
                    }
                }

                var t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                if (ObjectManager.Player.CountEnemiesInRange(Q.Range - 200) == 0)
                    t = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                else
                    t = TargetSelector.GetTarget(Q.Range - 200, TargetSelector.DamageType.Physical);

                if (t.IsValidTarget())
                {

                    var qDmg = Q.GetDamage(t);
                    var eDmg = E.GetDamage(t);
                    if (t.IsValidTarget(W.Range) && qDmg + eDmg > t.Health)
                        Q.Cast(t, true);
                    else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo && ObjectManager.Player.Mana > RMANA + QMANA )
                        CastSpell(Q, t, Config.Item("Hit").GetValue<Slider>().Value);
                    else if ((Farm && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA) && !ObjectManager.Player.UnderTurret(true))
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(Q.Range)))
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
                if ((Game.Time - lag > 0.1) && Farm && attackNow && Config.Item("farmQ").GetValue<bool>() && ObjectManager.Player.Mana > RMANA + EMANA + WMANA + QMANA * 3)
                {
                    farmQ();
                    lag = Game.Time;
                }
                else if ((Game.Time - OverFarm > 4.1) && !Farm && Config.Item("stack").GetValue<bool>() && !ObjectManager.Player.HasBuff("Recall") && ObjectManager.Player.Mana > ObjectManager.Player.MaxMana * 0.95 && Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Combo && !t.IsValidTarget() && (Items.HasItem(Tear) || Items.HasItem(Manamune)))
                {
                    Q.Cast(ObjectManager.Player);
                    OverFarm = Game.Time;
                }

            }
            
            PotionMenager();
            var tr = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
            if (Config.Item("useR").GetValue<KeyBind>().Active && tr.IsValidTarget())
            {
                R.Cast(tr);
            }
            if (R.IsReady() && ObjectManager.Player.HealthPercentage() >= Config.Item("Rhp").GetValue<Slider>().Value )
            {
                R.Range = 400 + 150 * R.Level;

                foreach (var target in ObjectManager.Get<Obj_AI_Hero>().Where(target => target.IsValidTarget(R.Range)))
                {
                    if (!target.HasBuffOfType(BuffType.PhysicalImmunity) &&
                        !target.HasBuffOfType(BuffType.SpellImmunity) &&
                        !target.HasBuffOfType(BuffType.SpellShield))
                    {
                        if (ObjectManager.Player.UnderTurret(false) && !ObjectManager.Player.UnderTurret(true) && Config.Item("autoR").GetValue<bool>() && target.CountEnemiesInRange(700) < 2 + ObjectManager.Player.CountAlliesInRange(700))
                        {
                            R.Cast(target);
                        }
                    }
                }
            }
        }

        private static void CastSpell(Spell QWER, Obj_AI_Hero target, int HitChanceNum)
        {
            //HitChance 0 - 2
            // example CastSpell(Q, ts, 2);

            if (HitChanceNum == 0)
                QWER.Cast(target, true);
            else if (HitChanceNum == 1)
                QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
            else if (HitChanceNum == 2)
            {
                if (target.Path.Count() < 2 && (int)QWER.GetPrediction(target).Hitchance > 4)
                    QWER.CastIfHitchanceEquals(target, HitChance.VeryHigh, true);
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
            else if (HitChanceNum == 4)
            {
                var poutput = QWER.GetPrediction(target);
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
            if (FarmId != args.Target.NetworkId)
                FarmId = args.Target.NetworkId;
            if (Config.Item("mura").GetValue<bool>())
            {
                int Mur = Items.HasItem(Muramana) ? 3042 : 3043;
                if (args.Target.IsEnemy && args.Target.IsValid<Obj_AI_Hero>() && Items.HasItem(Mur) && Items.CanUseItem(Mur) && ObjectManager.Player.Mana > RMANA + EMANA + QMANA + WMANA)
                {
                    if (!ObjectManager.Player.HasBuff("Muramana"))
                        Items.UseItem(Mur);
                }
                else if (ObjectManager.Player.HasBuff("Muramana") && Items.HasItem(Mur) && Items.CanUseItem(Mur))
                    Items.UseItem(Mur);
            }
        }

        private static void AntiGapcloser_OnEnemyGapcloser(ActiveGapcloser gapcloser)
        {
            if (Config.Item("AGC").GetValue<bool>() && W.IsReady() && ObjectManager.Player.Mana > RMANA + WMANA )
            {
                var Target = (Obj_AI_Hero)gapcloser.Sender;
                if (Target.IsValidTarget(E.Range))
                {
                    W.Cast();
                    debug("E AGC");
                }
            }
            return;
        }


        public static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
            var dmg = unit.GetSpellDamage(ObjectManager.Player, args.SData.Name);
            double HpLeft = ObjectManager.Player.Health - dmg;
            double HpPercentage = (dmg * 100) / ObjectManager.Player.Health;
            double shieldValue = 20 + W.Level * 40 + 0.08 * ObjectManager.Player.MaxMana + 0.8 * ObjectManager.Player.FlatMagicDamageMod;
            if (unit.IsEnemy && args.Target.IsMe && Config.Item("autoW").GetValue<bool>() && W.IsReady() && (ObjectManager.Player.Mana > RMANA + EMANA || ObjectManager.Player.Health - dmg < dmg))
            {
                if (HpPercentage >= Config.Item("Wdmg").GetValue<Slider>().Value)
                    W.Cast();
                else if (dmg > shieldValue)
                    W.Cast();
                //Game.PrintChat("" + HpPercentage);
            }
           
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
        private static void Drawing_OnDraw(EventArgs args)
        {

            if (Config.Item("qRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && Q.IsReady())
                    if (Q.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, Q.Range, System.Drawing.Color.Cyan);
            }
            if (Config.Item("eRange").GetValue<bool>())
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && E.IsReady())
                    if (Q.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow);
            }
            if (Config.Item("rRange").GetValue<bool>() && ObjectManager.Player.UnderTurret(false))
            {
                if (Config.Item("onlyRdy").GetValue<bool>() && E.IsReady())
                    if (R.IsReady())
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Green);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, R.Range, System.Drawing.Color.Green);
            }
            if (Config.Item("orb").GetValue<bool>())
            {
                var orbT = Orbwalker.GetTarget();
                if (orbT.IsValidTarget())
                    Render.Circle.DrawCircle(orbT.Position, 100, System.Drawing.Color.Pink);
            }
            if (Config.Item("semi").GetValue<bool>() && R.IsReady())
            {
                var t = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                    Render.Circle.DrawCircle(t.Position, 100, System.Drawing.Color.Red);
            }
        }
    }
}
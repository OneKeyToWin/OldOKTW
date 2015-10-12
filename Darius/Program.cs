#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;

#endregion

namespace Darius
{
    internal class Program
    {
        private const string ChampionName = "Darius";
        private static Orbwalking.Orbwalker Orbwalker;
        private static readonly List<Spell> SpellList = new List<Spell>();
        private static Spell Q, W, E, R;
        public static Menu Config;
        public static SpellSlot IgniteSlot;
        public static Items.Item Hydra;
        public static Items.Item Tiamat;
        public static Items.Item Randuin;
        public static float QMANA;
        public static float WMANA;
        public static float EMANA;
        public static float RMANA;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }

        private static void Game_OnGameLoad(EventArgs args)
        {
            if (ObjectManager.Player.ChampionName != ChampionName) return;

            Q = new Spell(SpellSlot.Q, 400);
            W = new Spell(SpellSlot.W, 145);
            E = new Spell(SpellSlot.E, 540);
            R = new Spell(SpellSlot.R, 460);
            
            E.SetSkillshot(0.1f, 50f * (float)Math.PI / 180, float.MaxValue, false, SkillshotType.SkillshotCone);
            
            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            Config = new Menu(ChampionName, ChampionName, true);
            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();

            Config.SubMenu("R option").AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.SubMenu("R option").AddItem(new MenuItem("useR", "Semi-manual cast R key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space

            Config.SubMenu("Draw").AddItem(new MenuItem("noti", "Show notification").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("qRange", "Q range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("eRange", "E range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("rRange", "R range").SetValue(false));
            Config.SubMenu("Draw").AddItem(new MenuItem("onlyRdy", "Draw when skill rdy").SetValue(true));
            
            Config.AddItem(new MenuItem("inter", "OnPossibleToInterrupt E")).SetValue(true);
            Config.AddItem(new MenuItem("farmQ", "Farm Q").SetValue(true));
            Config.AddItem(new MenuItem("haras", "Haras Q").SetValue(true));
            Config.AddItem(new MenuItem("debug", "Debug").SetValue(false));

            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnUpdate += Game_OnGameUpdate;
            Interrupter.OnPossibleToInterrupt += OnInterruptableSpell;
            Orbwalking.BeforeAttack += BeforeAttack;
            Game.PrintChat("<font color=\"#008aff\">D</font>arius full automatic AI ver 1.0 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }
        private static void OnInterruptableSpell(Obj_AI_Base unit, InterruptableSpell spell)
        {
            if (Config.Item("inter").GetValue<bool>() && E.IsReady() && unit.IsValidTarget(E.Range))
                E.Cast(unit);
        }
        static void BeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
             if (args.Target.IsValid<Obj_AI_Hero>() && W.IsReady()  && ObjectManager.Player.Mana > RMANA + WMANA)
                    W.Cast();
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
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow);
                    else
                        Render.Circle.DrawCircle(ObjectManager.Player.Position, E.Range, System.Drawing.Color.Yellow);
            }
            if (Config.Item("noti").GetValue<bool>())
            {

                var tw = TargetSelector.GetTarget(1000, TargetSelector.DamageType.Physical);
                if (tw.IsValidTarget())
                {

                    if (Q.GetDamage(tw) > tw.Health)
                    {
                        Render.Circle.DrawCircle(tw.ServerPosition, 200, System.Drawing.Color.Red);
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.4f, System.Drawing.Color.Red, "Q kill: " + tw.ChampionName + " have: " + tw.Health + "hp");
                    }
                    else if (Q.GetDamage(tw) + W.GetDamage(tw) > tw.Health)
                    {
                        Render.Circle.DrawCircle(tw.ServerPosition, 200, System.Drawing.Color.Red);
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.4f, System.Drawing.Color.Red, "Q + W kill: " + tw.ChampionName + " have: " + tw.Health + "hp");
                    }
                    else if (Q.GetDamage(tw) + W.GetDamage(tw) + R.GetDamage(tw) > tw.Health)
                    {
                        Render.Circle.DrawCircle(tw.ServerPosition, 200, System.Drawing.Color.Red);
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.4f, System.Drawing.Color.Red, "Q + W + R kill: " + tw.ChampionName + " have: " + tw.Health + "hp");
                    }
                }
            }
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            if (R.IsReady() && Config.Item("autoR").GetValue<bool>())
            {
                CastR();
            }
            if (R.IsReady() && Config.Item("useR").GetValue<KeyBind>().Active)
            {
                var targetR = TargetSelector.GetTarget(R.Range, TargetSelector.DamageType.Physical);
                if (targetR.IsValidTarget())
                    R.Cast(targetR, true);
            }

            
            ManaMenager();
            if (Q.IsReady() && ObjectManager.Player.CountEnemiesInRange(Q.Range) > 0)
            {
                if (ObjectManager.Player.Mana > RMANA + QMANA && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                    Q.Cast();
                else if (ObjectManager.Player.Mana > RMANA + QMANA + EMANA + WMANA && Farm && Config.Item("haras").GetValue<bool>())
                    Q.Cast();
                if (!R.IsReady())
                {
                    var target = TargetSelector.GetTarget(Q.Range, TargetSelector.DamageType.Physical);
                    if (target.IsValidTarget() && ObjectManager.Player.Distance(target.Position) < Q.Range && Q.GetDamage(target) > target.Health)
                        Q.Cast();
                }
            }
            if (E.IsReady() && ObjectManager.Player.Mana > RMANA + EMANA && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
            {
                var target = TargetSelector.GetTarget(E.Range, TargetSelector.DamageType.Physical);
                if (target.IsValidTarget())
                {
                    if ((target.Path.Count() > 0 || (ObjectManager.Player.Distance(target.ServerPosition) > 460 && target.Path.Count() == 0)) && ObjectManager.Player.Distance(target.ServerPosition) >= ObjectManager.Player.Distance(target.Position) && ObjectManager.Player.Distance(target.ServerPosition) > 260)
                        E.Cast(target, true, true);
                }
            }
            if (Config.Item("farmQ").GetValue<bool>() && Q.IsReady() && ObjectManager.Player.Mana > RMANA + QMANA + EMANA + WMANA && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, Q.Range,
                   MinionTypes.All);
                foreach (var minion in allMinionsQ)
                    if (ObjectManager.Player.Distance(minion.ServerPosition) > 300 && minion.Health <  ObjectManager.Player.GetSpellDamage(minion, SpellSlot.Q) * 0.6)
                        Q.Cast();
            }
           
        }
        private static bool Farm
        {
            get { return (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) || (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit); }
        }
        private static void CastR()
        {
            foreach (var target in ObjectManager.Get<Obj_AI_Hero>().Where(target => ValidUlt(target) && target.IsValidTarget(R.Range) && !target.IsZombie && !target.HasBuffOfType(BuffType.SpellImmunity) && !target.HasBuffOfType(BuffType.SpellShield) && !target.HasBuffOfType(BuffType.PhysicalImmunity)))
            {
                if (R.GetDamage(target) - target.Level > target.Health)
                {
                    R.Cast(target, true);
                    
                }
                else
                {
                    foreach (var buff in target.Buffs)
                    {
                        if (buff.Name == "dariushemo")
                        {
                            if (R.GetDamage(target) * (1 + (float)buff.Count / 5) - 1 > target.Health)
                            {
                                R.CastOnUnit(target, true);
                            }
                            else if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.4 && ObjectManager.Player.GetSpellDamage(target, SpellSlot.R, 1) * 1.2 * ((1 + buff.Count / 5) - 1) > target.Health)
                            {

                                R.CastOnUnit(target, true);
                            }
                            
                        }
                    }
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
        public static void ManaMenager()
        {
            QMANA = Q.Instance.ManaCost;
            WMANA = W.Instance.ManaCost;
            EMANA = E.Instance.ManaCost;
            if (!R.IsReady())
                RMANA = QMANA - 10;
            else
                RMANA = R.Instance.ManaCost + (R.Instance.ManaCost * ObjectManager.Player.CountEnemiesInRange(R.Range));

            if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.2)
            {
                QMANA = 0;
                WMANA = 0;
                EMANA = 0;
                RMANA = 0;
            }
        }
       
    }
}
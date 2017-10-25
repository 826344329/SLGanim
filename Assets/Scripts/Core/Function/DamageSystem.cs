﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class DamageSystem {
    static int baseCritRate = 5;
    static int basePounceRate = 5;
    //突袭(Pounce)：无视防御力
    //背击(BackStab)：无视一半防御力
    //暴击(Crit)：伤害结果增加50%
    //返回true继续执行剩余Hit，返回false停止执行剩余Hit。
    public static bool Apply(Transform attacker, Transform defender, int damageFactor, int skillRate, int extraCrit, int extraPounce, bool backStabBonus)
    {
        var def = defender.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "def").value;
        var currentHp = defender.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "hp").value;
        var atk = attacker.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "atk").value;

        if (Random.Range(0f, 100f) > HitRateSystem(attacker, defender, skillRate))
        {
            DebugLogPanel.GetInstance().Log("Miss");
            return true;
        }

        var dodgeBuff = defender.GetComponent<Unit>().Buffs.Find(b => b.GetType() == typeof(DodgeBuff));
        if (dodgeBuff != null)
        {
            dodgeBuff.Apply(defender);
            return false;
        }

        if (defender.GetComponent<CharacterStatus>().characterIdentity == CharacterStatus.CharacterIdentity.clone)
        {
            defender.GetComponent<Unit>().OnDestroyed();
            return false;
        }

        int damage = atk + damageFactor - def;
        if (PounceSystem(extraPounce))
        {
            DebugLogPanel.GetInstance().Log("突袭！");
            damage = atk + damageFactor;
        }
        else if (backStabBonus)
        {
            if (BackStab(attacker, defender))
            {
                DebugLogPanel.GetInstance().Log("背击！");
                damage = atk + damageFactor - def / 2;
            }
        }
        if (CritSystem(extraCrit))
        {
            DebugLogPanel.GetInstance().Log("暴击！");
            damage = (int)(damage * 1.5f);
        }

        damage = damage >= 0 ? damage : 0;
        DebugLogPanel.GetInstance().Log(damage.ToString() + "（" + attacker.GetComponent<CharacterStatus>().roleCName + " -> " + defender.GetComponent<CharacterStatus>().roleCName + "）");
        var hp = currentHp - damage;
        ChangeData.ChangeValue(defender, "hp", hp);

        if (hp <= 0)
        {
            defender.GetComponent<Unit>().OnDestroyed();
            return false;
        }

        return true;
    }

    public static int HitRateSystem(Transform attacker, Transform defender, int skillRate)
    {
        var attackerDex = attacker.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "dex").value;
        var defenderDex = defender.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "dex").value;
        int finalRate = skillRate + (attackerDex - defenderDex) / 2;
        return finalRate;
    }

    private static bool CritSystem(int extraRate)
    {
        var r = Random.Range(0f, 1f);
        bool crit = r < (((float)(baseCritRate + extraRate)) /100);
        
        return crit;
    }

    private static bool PounceSystem(int extraRate)
    {
        var r = Random.Range(0f, 1f);
        bool pounce = r < (((float)(basePounceRate + extraRate)) / 100);
        
        return pounce;
    }

    private static bool BackStab(Transform attacker, Transform defender)
    {
        if ((defender.transform.position - attacker.transform.position).normalized == defender.forward)
            return true;
        return false;
    }

    public static List<Transform> ComboDetect(Transform attacker, Transform defender)
    {
        var list = Detect.DetectObjects(1, defender.position);

        List<Transform> comboUnits = new List<Transform>();

        if((attacker.position - defender.position).magnitude == 1)
        {
            foreach (var l in list)
            {
                foreach (var u in l)
                {
                    if (u.position != attacker.position)
                    {
                        if (u.GetComponent<CharacterStatus>())
                        {
                            if (!attacker.GetComponent<CharacterStatus>().IsEnemy(u.GetComponent<CharacterStatus>()))
                            {
                                if (u.GetComponent<CharacterStatus>().skills.ContainsKey("NinjaCombo"))
                                {
                                    if (u.GetComponent<Animator>().runtimeAnimatorController == attacker.GetComponent<Animator>().runtimeAnimatorController)
                                    {
                                        comboUnits.Add(u);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        return comboUnits;
    }
    
    public static int ExpectDamage(Transform attacker, Transform defender, int damageFactor, int hit, bool backStabBonus)
    {
        var def = defender.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "def").value;
        var atk = attacker.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "atk").value;

        int damage = atk + damageFactor - def;

        if (backStabBonus)
        {
            if (BackStab(attacker, defender))
            {
                damage = atk + damageFactor - def / 2;
            }
        }
        damage = damage * hit;

        var comboUnits = ComboDetect(attacker, defender);
        
        if(comboUnits.Count > 0)
        {
            foreach(var u in comboUnits)
            {
                var ninjaCombo = new NinjaCombo();
                ninjaCombo.SetLevel(u.GetComponent<CharacterStatus>().skills["NinjaCombo"]);
                if (BackStab(u, defender))
                {
                    damage += (u.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "atk").value + ninjaCombo.damageFactor - def / 2);
                }
                else
                {
                    damage += (u.GetComponent<CharacterStatus>().attributes.Find(d => d.eName == "atk").value + ninjaCombo.damageFactor - def);
                }
            }
        }

        if(damage < 0)
        {
            damage = 0;
        }

        return damage;
    }
}
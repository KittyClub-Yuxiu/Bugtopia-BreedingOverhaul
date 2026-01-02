using BepInEx;
using BepInEx.Configuration; // 引入配置系统
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using Peecub; // 引用游戏命名空间

namespace BreedingOverhaul
{
    [BepInPlugin("BreedingLogicOverhaul", "Breeding Logic Overhaul", "1.0.0")]
    public class BreedingOverhaulPlugin : BaseUnityPlugin
    {
        // 定义静态配置项，方便补丁类直接访问
        public static ConfigEntry<float> SameLevelUpgradeChance;
        public static ConfigEntry<float> DiffLevelBreakthroughChance;

        void Awake()
        {
            // --- 绑定配置文件 ---
            // 参数说明: Bind(分组, 键名, 默认值, 描述)
            
            SameLevelUpgradeChance = Config.Bind("Probabilities", 
                "SameLevelUpgradeChance", 
                0.5f, 
                "当虫虫稀有等级相同时（两个白色、两个绿色、两个紫色），生出更高等级虫虫的概率（0.0至1.0）。默认值为0.5（50%）。（两个金色固定出金）");

            DiffLevelBreakthroughChance = Config.Bind("Probabilities", 
                "DiffLevelBreakthroughChance", 
                0.2f, 
                "当虫虫稀有等级不同时（白色和紫色、绿色和紫色等），生出更高等级虫虫的概率（0.0至1.0）。默认值为0.2（20%）。（剩余概率均分给父母间的所有等级）");

            // 自动应用所有补丁
            Harmony.CreateAndPatchAll(typeof(BreedingPatch));
            
            Logger.LogInfo($"繁育重构Mod已加载。配置: 同级进化率={SameLevelUpgradeChance.Value}, 异级突破率={DiffLevelBreakthroughChance.Value}");
        }
    }

    [HarmonyPatch(typeof(RM), "GetBabyColor")]
    public class BreedingPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(ColorType firstColor, ColorType secondColor, ref ColorType __result)
        {
            // 1. 获取父母的等级
            int level1 = GetLevel(firstColor);
            int level2 = GetLevel(secondColor);
            ColorType resultColor;

            // 2. 判断是否同级
            if (level1 == level2)
            {
                // ==== 同级繁殖逻辑 ====
                // 读取配置文件的概率
                float chance = BreedingOverhaulPlugin.SameLevelUpgradeChance.Value;

                // 判定是否进化
                bool upgrade = UnityEngine.Random.value < chance; 
                
                int targetLevel = upgrade ? (level1 + 1) : level1;

                // 修正：如果已经是最高级(4级)，无法进化，保持4级
                if (targetLevel > 4) targetLevel = 4;

                // 从目标等级中随机抽取一个颜色
                List<ColorType> potentialColors = GetColorsInLevel(targetLevel);
                resultColor = GetRandomColorFromList(potentialColors);

                // (可选) 调试日志，如果觉得刷屏可以注释掉
                // Debug.Log($"[Mod] 同级繁殖: L{level1} + L{level2} -> {(upgrade ? "进化" : "保级")} -> {resultColor}");
            }
            else
            {
                // ==== 异级繁殖逻辑 ====
                int minLv = Mathf.Min(level1, level2);
                int maxLv = Mathf.Max(level1, level2);

                // 计算“突破级”：比当前最高级父母再高一级
                int upperLevel = maxLv + 1;
                if (upperLevel > 4) upperLevel = 4; // 封顶

                // 读取配置文件的概率
                float chance = BreedingOverhaulPlugin.DiffLevelBreakthroughChance.Value;

                if (UnityEngine.Random.value < chance)
                {
                    // 触发突破概率 (默认20%)
                    List<ColorType> upperColors = GetColorsInLevel(upperLevel);
                    resultColor = GetRandomColorFromList(upperColors);
                    // Debug.Log($"[Mod] 异级繁殖(突破): L{minLv} + L{maxLv} -> L{upperLevel} -> {resultColor}");
                }
                else
                {
                    // 未触发突破 (默认80%)：均分区间内的所有可能
                    List<ColorType> pool = new List<ColorType>();

                    // 收集从 minLv 到 maxLv 之间所有的颜色
                    for (int i = minLv; i <= maxLv; i++)
                    {
                        pool.AddRange(GetColorsInLevel(i));
                    }

                    // 从池子里均等随机抽取
                    resultColor = GetRandomColorFromList(pool);
                    // Debug.Log($"[Mod] 异级繁殖(常规): L{minLv} + L{maxLv} -> 池大小{pool.Count} -> {resultColor}");
                }
            }

            // 将计算结果赋值给返回值
            __result = resultColor;

            // 返回 false 拦截原函数
            return false;
        }

        // ---- 辅助函数：定义等级规则 (保持不变) ----
        
        static int GetLevel(ColorType color)
        {
            switch (color)
            {
                case ColorType.A: // 0
                case ColorType.B: // 1
                    return 1;
                
                case ColorType.C: // 2
                case ColorType.D: // 3
                    return 2;
                
                case ColorType.E: // 4
                    return 3;
                
                case ColorType.F: // 5
                    return 4;
                
                default:
                    return 1;
            }
        }

        static List<ColorType> GetColorsInLevel(int level)
        {
            switch (level)
            {
                case 1:
                    return new List<ColorType> { ColorType.A, ColorType.B };
                case 2:
                    return new List<ColorType> { ColorType.C, ColorType.D };
                case 3:
                    return new List<ColorType> { ColorType.E };
                case 4:
                    return new List<ColorType> { ColorType.F };
                default:
                    return new List<ColorType> { ColorType.F };
            }
        }

        static ColorType GetRandomColorFromList(List<ColorType> list)
        {
            if (list == null || list.Count == 0) return ColorType.A;
            int index = UnityEngine.Random.Range(0, list.Count);
            return list[index];
        }
    }
}

using System.IO;
using UnityEngine;

namespace Shenyan
{
    /// <summary>存档内容：金币/等级/经验/异能点、异能等级、商店装备与道具。</summary>
    [System.Serializable]
    public class SaveData
    {
        public int gold;
        public int level = 1;
        public int exp;
        public int skillPoints;

        public int eyeLevel;
        public int turtleLevel;
        public int whaleLevel = -1;
        public int intimidateLevel;

        public int fuelTank;
        public int motor;
        public int load;
        public int hook;
        public int torpedoes;
        public int baits;

        public bool introPlayed;
    }

    /// <summary>
    /// 自动存档：写 Application.persistentDataPath/save.json。
    /// 买卖、加点、完成一局都会调用 Save()；进入游戏时自动读一次。
    /// </summary>
    public static class SaveSystem
    {
        const string FileName = "save.json";

        /// <summary>开场漫画是否已经看过（只有第一次开新游戏才播）。</summary>
        public static bool IntroPlayed;

        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public static void Save()
        {
            var d = new SaveData
            {
                gold = PlayerProgress.Gold,
                level = PlayerProgress.Level,
                exp = PlayerProgress.Exp,
                skillPoints = PlayerProgress.SkillPoints,

                eyeLevel = AbilityData.EyeLevel,
                turtleLevel = AbilityData.TurtleLevel,
                whaleLevel = AbilityData.WhaleLevel,
                intimidateLevel = AbilityData.IntimidateLevel,

                fuelTank = ShopData.FuelTankLevel,
                motor = ShopData.MotorLevel,
                load = ShopData.LoadLevel,
                hook = ShopData.HookLevel,
                torpedoes = ShopData.Torpedoes,
                baits = ShopData.Baits,

                introPlayed = IntroPlayed,
            };

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(d, true));
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[存档] 保存失败: " + e.Message);
            }
        }

        public static void Load()
        {
            if (!File.Exists(SavePath)) return;

            try
            {
                var d = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (d == null) return;

                PlayerProgress.Gold = d.gold;
                PlayerProgress.Level = Mathf.Max(1, d.level);
                PlayerProgress.Exp = Mathf.Max(0, d.exp);
                PlayerProgress.SkillPoints = Mathf.Max(0, d.skillPoints);

                AbilityData.EyeLevel = Mathf.Clamp(d.eyeLevel, 0, AbilityData.EyeMax);
                AbilityData.TurtleLevel = Mathf.Clamp(d.turtleLevel, 0, AbilityData.TurtleMax);
                AbilityData.WhaleLevel = Mathf.Clamp(d.whaleLevel, -1, AbilityData.WhaleMax);
                AbilityData.IntimidateLevel = Mathf.Clamp(d.intimidateLevel, 0, AbilityData.IntimidateMax);

                ShopData.FuelTankLevel = Mathf.Clamp(d.fuelTank, 0, ShopData.MaxLevel);
                ShopData.MotorLevel = Mathf.Clamp(d.motor, 0, ShopData.MaxLevel);
                ShopData.LoadLevel = Mathf.Clamp(d.load, 0, ShopData.MaxLevel);
                ShopData.HookLevel = Mathf.Clamp(d.hook, 0, ShopData.MaxLevel);
                ShopData.Torpedoes = Mathf.Max(0, d.torpedoes);
                ShopData.Baits = Mathf.Max(0, d.baits);

                IntroPlayed = d.introPlayed;

                Debug.Log("[存档] 已读取 " + SavePath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[存档] 读取失败: " + e.Message);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadOnBoot()
        {
            Load();
        }
    }
}

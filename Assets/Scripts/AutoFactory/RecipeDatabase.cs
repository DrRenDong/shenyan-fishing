using System.Collections.Generic;
using UnityEngine;

namespace Shenyan
{
    /// <summary>工厂类型：跟素材一一对应。</summary>
    public enum FactoryType
    {
        Preprocess,     // 预处理厂
        Forming,        // 成型厂
        Canning,        // 灌装封口厂
        Sealing,        // 塑封分装厂
        Drying,         // 干燥厂
        Fermenting,     // 发酵熟成厂
        Extraction,     // 萃取精炼厂
        Freezing,       // 冷冻冷藏厂
        Smoking,        // 烟熏房
        Frying,         // 油炸房
    }

    /// <summary>
    /// 一条加工配方 = 这栋厂输入什么、输出什么：
    /// 主料（蓝）→ 辅料/包材（黄）→ 成品（绿）→ 副产物（橙，可 0~2 路）。
    /// 端口只是给人看的信息，不限制数量——物理上所有物料共用同一个进出口。
    /// 公用工程（电/蒸汽/水/冷媒/气）不占端口，统一折进运营费。
    ///
    /// 鱼这一路是「动态」的：主料不写死物品，而是按工序匹配（例如预处理厂输入任何生鱼），
    /// 产物名由「工艺词 + 鱼名」自动拼出来，价格按倍率累积——这样不用给每种鱼×每道工序建物品。
    /// </summary>
    public class Recipe
    {
        public string Id;
        public string ProductId;
        public string DisplayName;

        public string[] PrimaryItems;
        public float[] PrimaryPerUnit;

        public string[] AuxItems;
        public float[] AuxPerUnit;

        public string[] ByproductIds;
        public float[] ByproductPerUnit;

        public float OutputPerUnit = 1f;

        /// <summary>批次型：0 = 连续生产；&gt;0 = 一次吞下「速率×回合数」的料，然后闷头做这么多回合再一次性出货。</summary>
        public int BatchTurns;

        /// <summary>动态主料：主料最后一道工序必须落在这个集合里；空 = 生鱼（还没加工过的鱼）。</summary>
        public FishStage[] RequiresStages;

        /// <summary>动态主料：这栋厂给鱼施加的工序（产物的名字与价格由此决定）。</summary>
        public FishStage? AppliesStage;

        public Recipe(string id, string productId, string displayName,
                      string[] primary, float[] primaryPerUnit,
                      string[] aux, float[] auxPerUnit,
                      string[] byproducts, float[] byproductPerUnit,
                      float outputPerUnit, int batchTurns = 0,
                      FishStage[] requiresStages = null, FishStage? appliesStage = null)
        {
            Id = id;
            ProductId = productId;
            DisplayName = displayName;
            PrimaryItems = primary ?? new string[0];
            PrimaryPerUnit = primaryPerUnit ?? new float[0];
            AuxItems = aux ?? new string[0];
            AuxPerUnit = auxPerUnit ?? new float[0];
            ByproductIds = byproducts ?? new string[0];
            ByproductPerUnit = byproductPerUnit ?? new float[0];
            OutputPerUnit = outputPerUnit;
            BatchTurns = batchTurns;
            RequiresStages = requiresStages;
            AppliesStage = appliesStage;
        }

        public bool IsBatch => BatchTurns > 0;
        public int ByproductCount => ByproductIds.Length;

        /// <summary>主料是不是「按工序匹配的鱼」。</summary>
        public bool DynamicPrimary => AppliesStage.HasValue;

        /// <summary>动态主料每份输入多少（KG）。</summary>
        public float DynamicPrimaryPerUnit =>
            PrimaryPerUnit != null && PrimaryPerUnit.Length > 0 ? PrimaryPerUnit[0] : 1f;
    }

    /// <summary>配方表：每种工厂对应它自己的那套端口。</summary>
    public static class RecipeDatabase
    {
        static readonly Dictionary<FactoryType, Recipe[]> ByType = new Dictionary<FactoryType, Recipe[]>();

        // ---- 鱼这一路：输入「上一道工序」的鱼，输出「下一道工序」的鱼 ----
        public static readonly Recipe PreprocessRecipe = new Recipe(
            "剖好的", "", "剖好的鱼",
            null, new[] { 1f },
            null, null,
            new[] { ItemDatabase.Offcut, ItemDatabase.WasteWater }, new[] { 0.3f, 0.1f },
            1f,
            appliesStage: FishStage.Fillet);

        public static readonly Recipe FormingRecipe = new Recipe(
            "裹粉", "", "裹粉鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.Starch }, new[] { 0.2f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Breading);

        public static readonly Recipe FryingRecipe = new Recipe(
            "油炸", "", "油炸鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.CookingOil }, new[] { 0.2f },
            new[] { ItemDatabase.WasteOil }, new[] { 0.15f },
            1f,
            requiresStages: new[] { FishStage.Breading },
            appliesStage: FishStage.Fried);

        public static readonly Recipe CanningRecipe = new Recipe(
            "罐头", "", "鱼罐头",
            null, new[] { 1f },
            new[] { ItemDatabase.EmptyCan }, new[] { 1f },
            new[] { ItemDatabase.ResidualLiquid }, new[] { 0.01f },
            1f, batchTurns: 4,
            requiresStages: new[] { FishStage.Fried },
            appliesStage: FishStage.Canned);

        public static readonly Recipe DryingRecipe = new Recipe(
            "鱼干", "", "鱼干",
            null, new[] { 1f },
            null, null,
            new[] { ItemDatabase.WasteHeat }, new[] { 0.3f },
            1f,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Dried);

        public static readonly Recipe FreezingRecipe = new Recipe(
            "冷冻", "", "冷冻鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.Water }, new[] { 0.2f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Frozen);

        public static readonly Recipe SmokingRecipe = new Recipe(
            "烟熏", "", "烟熏鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.Sawdust }, new[] { 0.2f },
            new[] { ItemDatabase.SmokeGas }, new[] { 0.1f },
            1f,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Smoked);

        public static readonly Recipe SealingRecipe = new Recipe(
            "袋装", "", "袋装鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.Film }, new[] { 1f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Dried },
            appliesStage: FishStage.Bagged);

        // ---- 副产物这一路：边角料 → 发酵液 / 鱼油 / 鱼粉（固定物品，不掺品种） ----
        public static readonly Recipe FermentingRecipe = new Recipe(
            "发酵液", ItemDatabase.Slurry, "发酵液",
            new[] { ItemDatabase.Offcut }, new[] { 1f },
            new[] { ItemDatabase.Salt }, new[] { 0.2f },
            new[] { ItemDatabase.Brine }, new[] { 0.1f },
            0.9f, batchTurns: 3);

        public static readonly Recipe ExtractionRecipe = new Recipe(
            "鱼油", ItemDatabase.FishOil, "鱼油",
            new[] { ItemDatabase.Offcut }, new[] { 1f },
            new[] { ItemDatabase.Enzyme }, new[] { 0.1f },
            new[] { ItemDatabase.Residue }, new[] { 0.6f },
            0.3f, batchTurns: 3);

        public static readonly Recipe FishMealRecipe = new Recipe(
            "鱼粉", ItemDatabase.FishMeal, "鱼粉",
            new[] { ItemDatabase.Offcut }, new[] { 1f },
            null, null,
            new[] { ItemDatabase.Residue }, new[] { 0.2f },
            1f);

        // ---- 成型类的其他成品：鱼糜 / 鱼丸 / 鱼糕 / 鱼肉肠 ----
        public static readonly Recipe MinceRecipe = new Recipe(
            "鱼糜", "", "鱼糜",
            null, new[] { 1f },
            null, null,
            new[] { ItemDatabase.Offcut }, new[] { 0.1f },
            1f,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Minced);

        public static readonly Recipe BallRecipe = new Recipe(
            "鱼丸", "", "鱼丸",
            null, new[] { 1f },
            new[] { ItemDatabase.Starch }, new[] { 0.3f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Minced },
            appliesStage: FishStage.Ball);

        public static readonly Recipe CakeRecipe = new Recipe(
            "鱼糕", "", "鱼糕",
            null, new[] { 1f },
            new[] { ItemDatabase.Starch }, new[] { 0.3f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Minced },
            appliesStage: FishStage.Cake);

        public static readonly Recipe SausageRecipe = new Recipe(
            "鱼肉肠", "", "鱼肉肠",
            null, new[] { 1f },
            new[] { ItemDatabase.Starch }, new[] { 0.2f },
            null, null,
            1f,
            requiresStages: new[] { FishStage.Minced },
            appliesStage: FishStage.Sausage);

        // ---- 发酵类：腌制 → 鱼露 ----
        public static readonly Recipe SaltingRecipe = new Recipe(
            "腌制", "", "腌制鱼",
            null, new[] { 1f },
            new[] { ItemDatabase.Salt }, new[] { 0.2f },
            new[] { ItemDatabase.Brine }, new[] { 0.05f },
            1f, batchTurns: 3,
            requiresStages: new[] { FishStage.Fillet },
            appliesStage: FishStage.Salted);

        public static readonly Recipe FishSauceRecipe = new Recipe(
            "鱼露", "", "鱼露",
            null, new[] { 1f },
            new[] { ItemDatabase.Salt }, new[] { 0.1f },
            new[] { ItemDatabase.Brine }, new[] { 0.1f },
            1f, batchTurns: 3,
            requiresStages: new[] { FishStage.Salted },
            appliesStage: FishStage.Sauce);

        // ---- 调味罐头：油炸鱼 + 番茄盐 → 茄汁罐头 ----
        public static readonly Recipe TomatoCanRecipe = new Recipe(
            "茄汁罐头", "", "茄汁鱼罐头",
            null, new[] { 1f },
            new[] { ItemDatabase.EmptyCan, ItemDatabase.Salt }, new[] { 1f, 0.05f },
            new[] { ItemDatabase.ResidualLiquid }, new[] { 0.01f },
            1f, batchTurns: 4,
            requiresStages: new[] { FishStage.Fried },
            appliesStage: FishStage.TomatoCan);

        public static readonly Recipe[] All =
        {
            PreprocessRecipe, FormingRecipe, CanningRecipe, SealingRecipe, DryingRecipe,
            FermentingRecipe, ExtractionRecipe, FreezingRecipe, SmokingRecipe, FryingRecipe,
            MinceRecipe, BallRecipe, CakeRecipe, SausageRecipe,
            SaltingRecipe, FishSauceRecipe, TomatoCanRecipe, FishMealRecipe,
        };

        static RecipeDatabase()
        {
            ByType[FactoryType.Preprocess] = new[] { PreprocessRecipe };
            ByType[FactoryType.Forming] = new[] { FormingRecipe, MinceRecipe, BallRecipe, CakeRecipe, SausageRecipe };
            ByType[FactoryType.Canning] = new[] { CanningRecipe, TomatoCanRecipe };
            ByType[FactoryType.Sealing] = new[] { SealingRecipe };
            ByType[FactoryType.Drying] = new[] { DryingRecipe };
            ByType[FactoryType.Fermenting] = new[] { SaltingRecipe, FishSauceRecipe, FermentingRecipe };
            ByType[FactoryType.Extraction] = new[] { ExtractionRecipe, FishMealRecipe };
            ByType[FactoryType.Freezing] = new[] { FreezingRecipe };
            ByType[FactoryType.Smoking] = new[] { SmokingRecipe };
            ByType[FactoryType.Frying] = new[] { FryingRecipe };
        }

        /// <summary>这个物品能不能当这条配方的主料（动态配方按「最后一道工序」匹配）。</summary>
        public static bool MatchesPrimary(Recipe recipe, string itemId)
        {
            if (!recipe.DynamicPrimary) return false;
            if (!ItemDatabase.IsFish(itemId)) return false;

            bool hasStage = ItemDatabase.TryGetLastStage(itemId, out FishStage stage);
            if (recipe.RequiresStages == null || recipe.RequiresStages.Length == 0)
                return !hasStage;      // 生鱼：还没加工过

            if (!hasStage) return false;
            for (int k = 0; k < recipe.RequiresStages.Length; k++)
                if (recipe.RequiresStages[k] == stage) return true;
            return false;
        }

        /// <summary>这条配方输入某个主料时的产物 id（动态配方按工序生成）。</summary>
        public static string ProductFor(Recipe recipe, string primaryId)
        {
            if (!recipe.DynamicPrimary) return recipe.ProductId;
            return ItemDatabase.Process(primaryId, recipe.AppliesStage.Value);
        }

        /// <summary>施加某道工序的配方（找不到返回 null）。</summary>
        public static Recipe FindByStage(FishStage stage)
        {
            for (int k = 0; k < All.Length; k++)
                if (All[k].AppliesStage.HasValue && All[k].AppliesStage.Value == stage) return All[k];
            return null;
        }

        /// <summary>这条配方归哪座厂（找不到返回 null）。</summary>
        public static FactoryType? TypeOfRecipe(Recipe recipe)
        {
            foreach (var kv in ByType)
                for (int k = 0; k < kv.Value.Length; k++)
                    if (kv.Value[k] == recipe) return kv.Key;
            return null;
        }

        /// <summary>从生鱼到目标工序的完整工序链（含目标）。给「配方速查」面板用。</summary>
        public static List<FishStage> ChainOf(FishStage stage)
        {
            var list = new List<FishStage>();
            BuildChain(stage, list, 0);
            return list;
        }

        static void BuildChain(FishStage stage, List<FishStage> list, int depth)
        {
            if (depth > 8) return;
            Recipe recipe = FindByStage(stage);
            if (recipe != null && recipe.RequiresStages != null && recipe.RequiresStages.Length > 0)
                BuildChain(recipe.RequiresStages[0], list, depth + 1);
            list.Add(stage);
        }

        public static Recipe[] ForType(FactoryType type)
        {
            return ByType.TryGetValue(type, out var list) ? list : new Recipe[0];
        }

        public static Recipe DefaultFor(FactoryType type)
        {
            var list = ForType(type);
            return list.Length > 0 ? list[0] : All[0];
        }

        public static string TypeName(FactoryType type)
        {
            switch (type)
            {
                case FactoryType.Preprocess: return "预处理厂";
                case FactoryType.Forming: return "成型厂";
                case FactoryType.Canning: return "灌装封口厂";
                case FactoryType.Sealing: return "塑封分装厂";
                case FactoryType.Drying: return "干燥厂";
                case FactoryType.Fermenting: return "发酵熟成厂";
                case FactoryType.Extraction: return "萃取精炼厂";
                case FactoryType.Freezing: return "冷冻冷藏厂";
                case FactoryType.Smoking: return "烟熏房";
                case FactoryType.Frying: return "油炸房";
                default: return type.ToString();
            }
        }
    }
}

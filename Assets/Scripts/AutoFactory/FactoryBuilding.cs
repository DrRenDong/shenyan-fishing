using UnityEngine;

namespace Shenyan
{
    /// <summary>加工厂：按速率输入原料、输出产物；输入输出共用一个「出入口」（同一套道路连接关系）。</summary>
    public class FactoryBuilding : PlacedTile
    {
        public override bool CanDemolish => true;

        [Header("运行")]
        public bool powered = true;
        [Tooltip("玩家手动开关：停工时不生产、也不扣日运营费")]
        public bool running = true;
        public float dailyCost = 0f;              // 0 时详情面板显示「？」
        public float ratePerTurn = 1f;            // 每回合生产多少「份」

        [Header("配方")]
        public FactoryType factoryType = FactoryType.Canning;
        public string recipeId = "";

        /// <summary>当前配方（没指定就用该厂型的默认配方）。</summary>
        public Recipe CurrentRecipe
        {
            get
            {
                if (!string.IsNullOrEmpty(recipeId))
                {
                    var list = RecipeDatabase.ForType(factoryType);
                    for (int k = 0; k < list.Length; k++)
                        if (list[k].Id == recipeId) return list[k];
                }
                return RecipeDatabase.DefaultFor(factoryType);
            }
        }

        public string ProductId => CurrentRecipe.ProductId;

        /// <summary>
        /// 这栋厂要不要电。一级烟熏房烧柴、一级预处理厂靠手工，都不用接电；
        /// 升到二级以后改用机器，就得通电了。
        /// </summary>
        public bool NeedsPower
        {
            get
            {
                if (level <= 1 && factoryType == FactoryType.Smoking) return false;
                if (level <= 1 && factoryType == FactoryType.Preprocess) return false;
                return true;
            }
        }

        /// <summary>当前实际有没有电（不需要电的厂永远算有电）。</summary>
        public bool HasPower => !NeedsPower || PowerGridView.GridPowered(Cell);

        public string ProductName
        {
            get
            {
                Recipe recipe = CurrentRecipe;
                return recipe.DynamicPrimary
                    ? recipe.DisplayName                    // 动态配方：成品名随鱼种变，这里显示泛指
                    : ItemDatabase.Get(recipe.ProductId).DisplayName;
            }
        }

        public void SetRecipe(string id)
        {
            recipeId = id;
        }

        // ---- 手动选料 ----
        [Header("选料")]
        [Tooltip("只加工这一种原料（空 = 自动挑库存最多的可加工原料）")]
        public string selectedPrimary = "";

        public bool HasSelectedPrimary => !string.IsNullOrEmpty(selectedPrimary);

        public void SetSelectedPrimary(string id)
        {
            selectedPrimary = string.IsNullOrEmpty(id) ? "" : id;
        }
        /// <summary>没接仓库时的成品暂存。</summary>
        public Inventory outputBuffer = new Inventory();
        public float outputBufferCapacity = 50f;

        public float TotalProduced { get; private set; }
        public float TotalFishConsumed { get; private set; }
        public float TotalContainerConsumed { get; private set; }
        public float LastQuality { get; private set; }
        public bool LastTickStalled { get; private set; }

        // ---- 批次型状态（P2 灌装 / P5 发酵 / P6 萃取）----
        /// <summary>这一批还要做几回合；0 = 空闲等料。</summary>
        public int BatchTurnsLeft { get; private set; }
        /// <summary>这一批一共几份。</summary>
        public float BatchCopies { get; private set; }
        public float BatchQuality { get; private set; }
        /// <summary>这一批用的主料（动态配方记下来，出货时才知道是什么鱼）。</summary>
        public string BatchPrimaryId { get; private set; }

        public bool IsBatchRunning => BatchTurnsLeft > 0;
        public int BatchTotalTurns => CurrentRecipe.BatchTurns;

        public void BeginBatch(float copies, float quality, int turns, string primaryId = null)
        {
            BatchCopies = copies;
            BatchQuality = quality;
            BatchTurnsLeft = turns;
            BatchPrimaryId = primaryId;
        }

        public void AdvanceBatchTurn()
        {
            if (BatchTurnsLeft > 0) BatchTurnsLeft--;
        }

        public void EndBatch()
        {
            BatchCopies = 0f;
            BatchTurnsLeft = 0;
            BatchPrimaryId = null;
        }

        public void ReportProduced(float amount, float quality)
        {
            TotalProduced += amount;
            LastQuality = quality;
            if (amount > 1e-5f) ProducedLastTurn = true;
        }

        /// <summary>上一回合有没有产出（有产出就派小人把货搬去仓库）。</summary>
        public bool ProducedLastTurn { get; private set; }
        public void MarkProduced(bool produced) => ProducedLastTurn = produced;

        public void ReportConsumed(float fish, float containers)
        {
            TotalFishConsumed += fish;
            TotalContainerConsumed += containers;
        }

        public void MarkStalled(bool stalled) => LastTickStalled = stalled;

        /// <summary>开工 / 停工（停工不生产、也不扣日费）。</summary>
        public void SetRunning(bool on) => running = on;

        /// <summary>上一回合是不是真的从仓库拿到了料（只有这种厂才派小人搬货）。</summary>
        public bool SuppliedLastTurn { get; private set; }
        public void MarkSupplied(bool supplied) => SuppliedLastTurn = supplied;
    }
}

using UnityEngine;

namespace Shenyan
{
    /// <summary>路面标记：只登记坐标（石砖路的外形由地面图层画），用于连通性判断。</summary>
    public class RoadTile : PlacedTile
    {
        public override bool CanDemolish => true;

        protected override void Awake()
        {
            base.Awake();
            // 注册放到 Start：运行时 new 出来的 RoadTile 在 Awake 时 gridX/gridY 还没赋值，
            // 会把路面错误注册到 (0,0)。
        }

        protected override void Start()
        {
            base.Start();
            if (FactoryWorld.Instance != null) FactoryWorld.Instance.RegisterRoad(Cell);
        }
    }
}

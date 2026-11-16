using UnityEngine;

namespace Shenyan
{
    /// <summary>
    /// 棱镜塔：电流束穿过它时，会从塔身向左右两侧（垂直于原束的方向）各再射出一条电流束。
    /// 分出来的两条束和普通束一样会被接收塔截断，也照样能穿过别的棱镜塔继续分叉。
    /// </summary>
    public class PrismTower : PlacedTile
    {
        public override bool CanDemolish => true;
    }
}

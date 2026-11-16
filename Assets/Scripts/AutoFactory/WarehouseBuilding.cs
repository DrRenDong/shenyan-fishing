using UnityEngine;

namespace Shenyan
{
    /// <summary>仓库：无限容量，存溢出的物资。同种不同品质按质量加权合并。</summary>
    public class WarehouseBuilding : PlacedTile
    {
        public override bool CanDemolish => true;

        public Inventory storage = new Inventory();

        public void Seed(string itemId, float amount, float quality)
        {
            storage.Add(itemId, amount, quality);
        }
    }
}

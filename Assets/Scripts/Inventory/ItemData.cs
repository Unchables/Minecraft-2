using UnityEngine;

namespace Inventory
{
    [CreateAssetMenu(menuName = "Inventory/ItemData", fileName = "New ItemData")]
    public class ItemData : ScriptableObject
    {
        public int sizeWidth = 1;
        public int sizeHeight = 1;

        public Sprite itemIcon;
    }
}
using UnityEngine;
using UnityEngine.UI;

namespace Inventory
{
    public class InventoryItem : MonoBehaviour
    {
        public ItemData itemData;

        public int onGridPositionX;
        public int onGridPositionY;
        public void Set(ItemData newItemData)
        {
            itemData = newItemData;

            GetComponent<Image>().sprite = itemData.itemIcon;
            
            Vector2 size = Vector2.zero;
            size.x = itemData.sizeWidth * ItemGrid.TileSizeWidth;
            size.y = itemData.sizeHeight * ItemGrid.TileSizeHeight;
            GetComponent<RectTransform>().sizeDelta = size;
        }
    }
}

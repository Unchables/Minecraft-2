using UnityEngine;
using UnityEngine.UI;

namespace Inventory
{
    public class InventoryItem : MonoBehaviour
    {
        public ItemData itemData;

        public int height => rotated ? itemData.sizeWidth : itemData.sizeHeight;
        public int width => rotated ? itemData.sizeHeight : itemData.sizeWidth;
        

        public int onGridPositionX;
        public int onGridPositionY;
        
        public bool rotated = false;
        
        public void Set(ItemData newItemData)
        {
            itemData = newItemData;

            GetComponent<Image>().sprite = itemData.itemIcon;
            
            Vector2 size = Vector2.zero;
            size.x = width * ItemGrid.TileSizeWidth;
            size.y = height * ItemGrid.TileSizeHeight;
            GetComponent<RectTransform>().sizeDelta = size;
        }

        public void Rotate()
        {
            rotated = !rotated;

            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.rotation = Quaternion.Euler(0, 0, rotated ? 90 : 0);
        }
    }
}

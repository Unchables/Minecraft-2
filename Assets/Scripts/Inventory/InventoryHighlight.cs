using UnityEngine;

namespace Inventory
{
    public class InventoryHighlight : MonoBehaviour
    {
        [SerializeField] private RectTransform highlighter;

        public void Show(bool enable)
        {
            highlighter.gameObject.SetActive(enable);
        }
        public void SetSize(InventoryItem targetItem)
        {
            Vector2 size = new Vector2();
            size.x = targetItem.width * ItemGrid.TileSizeWidth;
            size.y = targetItem.height * ItemGrid.TileSizeHeight;
            highlighter.sizeDelta = size;
        }
        
        public void SetPosition(ItemGrid targetGrid, InventoryItem targetItem)
        {
            var position = targetGrid.GridToWorldPosition(targetItem, targetItem.onGridPositionX, targetItem.onGridPositionY);
            
            highlighter.localPosition = position;
        }

        public void SetParent(ItemGrid targetGrid)
        {
            if (!targetGrid) return;
            highlighter.SetParent(targetGrid.GetComponent<RectTransform>());
        }

        public void SetPosition(ItemGrid targetGrid, InventoryItem targetItem, int posX, int posY)
        {
            var pos = targetGrid.GridToWorldPosition(targetItem, posX, posY);
            
            highlighter.localPosition = pos;
        }
    }
}
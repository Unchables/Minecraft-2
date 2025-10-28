using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventory
{
    public class GridInteract : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private InventoryController _inventoryController;
        private ItemGrid itemGrid;

        public void Awake()
        {
            _inventoryController = FindObjectOfType(typeof(InventoryController)) as InventoryController;
            itemGrid = GetComponent<ItemGrid>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _inventoryController.selectedItemGrid = itemGrid;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _inventoryController.selectedItemGrid = null;
        }
    }
}
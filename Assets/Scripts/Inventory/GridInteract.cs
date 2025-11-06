using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Inventory
{
    public class GridInteract : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private InventoryController _inventoryController;
        private ItemGrid _itemGrid;

        public void Awake()
        {
            _itemGrid = GetComponent<ItemGrid>();
        }

        private void Start()
        {
            _inventoryController = InventoryController.Instance;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _inventoryController.selectedItemGrid = _itemGrid;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _inventoryController.selectedItemGrid = null;
        }
    }
}
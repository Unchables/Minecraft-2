using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Inventory
{
    public class InventoryController : MonoBehaviour
    {
        [HideInInspector] public ItemGrid selectedItemGrid;
        public InventoryItem itemPrefab;
        public Transform canvasTransform;
        
        [SerializeField] private List<ItemData> items;

        private InventoryItem _selectedItem;
        private InventoryItem _overlapItem;
        
        private RectTransform _selectedItemTransform;
        
        private void Update()
        {
            UpdateItemDrag();

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                CreateRandomItem();
            }
            
            if (!selectedItemGrid) return;

            LeftMouseButtonPress();
        }

        private void CreateRandomItem()
        {
            InventoryItem inventoryItem = Instantiate(itemPrefab);
            _selectedItem = inventoryItem;
            
            _selectedItemTransform = inventoryItem.GetComponent<RectTransform>();
            _selectedItemTransform.SetParent(canvasTransform);
            
            int selectedItemID = Random.Range(0, items.Count);
            _selectedItem.Set(items[selectedItemID]);
        }


        private void UpdateItemDrag()
        {
            if (_selectedItem)
                _selectedItemTransform.position = Mouse.current.position.ReadValue();
        }

        private void LeftMouseButtonPress()
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
                
            Vector2Int tileGridPosition = selectedItemGrid.GetTileGridPosition(Mouse.current.position.ReadValue());

            if (!_selectedItem)
            {
                PickupItem(tileGridPosition);
            }
            else
            {
                PlaceItem(tileGridPosition);
            }
        }

        private void PickupItem(Vector2Int tileGridPosition)
        {
            _selectedItem = selectedItemGrid.PickUpItem(tileGridPosition.x, tileGridPosition.y);
            if (_selectedItem)
            {
                _selectedItemTransform = _selectedItem.GetComponent<RectTransform>();
            }
        }

        private void PlaceItem(Vector2Int tileGridPosition)
        {
            if (selectedItemGrid.PlaceItem(_selectedItem, _overlapItem, tileGridPosition.x, tileGridPosition.y))
            {
                _selectedItem = null;
                if (_overlapItem)
                {
                    _selectedItem = _overlapItem;
                    _overlapItem = null;
                    _selectedItemTransform = _selectedItem.GetComponent<RectTransform>();
                }
            }
        }
    }
}
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Inventory
{
    public class InventoryController : MonoBehaviour
    {
        private ItemGrid _selectedItemGrid;

        public ItemGrid selectedItemGrid
        {
            get => _selectedItemGrid;
            set
            {
                _selectedItemGrid = value;
                inventoryHighlight.SetParent(value);
            }
        }

        public InventoryItem itemPrefab;
        public Transform canvasTransform;
        
        [SerializeField] private List<ItemData> items;

        private InventoryItem _selectedItem;
        private InventoryItem _overlapItem;
        
        private RectTransform _selectedItemTransform;
        
        [SerializeField] InventoryHighlight inventoryHighlight;

        public static InventoryController Instance;
        
        private void Awake()
        {
            Instance = this;
            inventoryHighlight = GetComponent<InventoryHighlight>();
        }

        private void Update()
        {
            UpdateItemDrag();

            if (Keyboard.current.qKey.wasPressedThisFrame)
            {
                CreateRandomItem();
            }
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RotateItem();
            }

            if (!_selectedItemGrid)
            {
                inventoryHighlight.Show(false);
                return;
            }

            HandleHighlight();

            LeftMouseButtonPress();
        }

        private void RotateItem()
        {
            if (!_selectedItem) return;

            _selectedItem.Rotate();
            _oldPosition = Vector2Int.one * -999; // force itemHighight update
        }

        private Vector2Int _oldPosition;
        private InventoryItem _itemTohighlight;
        private void HandleHighlight()
        {
            var positionOnGrid = GetTileGridPosition();

            if (_oldPosition == positionOnGrid) return;
            
            _oldPosition = positionOnGrid;
            if (!_selectedItem)
            {
                _itemTohighlight = _selectedItemGrid.GetItem(positionOnGrid.x, positionOnGrid.y);
                
                if (_itemTohighlight != null)
                {
                    inventoryHighlight.Show(true);
                    inventoryHighlight.SetSize(_itemTohighlight);
                    inventoryHighlight.SetPosition(_selectedItemGrid, _itemTohighlight);
                }
                else
                {
                    inventoryHighlight.Show(false);
                }
            }
            else
            {
                inventoryHighlight.Show(_selectedItemGrid.BoundaryCheck(positionOnGrid.x, positionOnGrid.y,
                    _selectedItem.width, _selectedItem.height));
                inventoryHighlight.SetSize(_selectedItem);
                inventoryHighlight.SetPosition(_selectedItemGrid, _selectedItem, positionOnGrid.x, positionOnGrid.y);
            }
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

            var tileGridPosition = GetTileGridPosition();

            if (!_selectedItem)
            {
                PickupItem(tileGridPosition);
            }
            else
            {
                PlaceItem(tileGridPosition);
            }
        }

        private Vector2Int GetTileGridPosition()
        {
            Vector2 mousePosition = Mouse.current.position.ReadValue();

            mousePosition.x -= ItemGrid.TileSizeWidth * 0.5f;
            mousePosition.y += ItemGrid.TileSizeHeight * 0.5f;
            if (_selectedItem)
            {
                mousePosition.x -= (_selectedItem.width - 1) * ItemGrid.TileSizeWidth / 2;
                mousePosition.y += (_selectedItem.height - 1) * ItemGrid.TileSizeHeight / 2;
            }
            else
            {
            }
                
            Vector2Int tileGridPosition = _selectedItemGrid.WorldToGridPosition(mousePosition);
            return tileGridPosition;
        }

        private void PickupItem(Vector2Int tileGridPosition)
        {
            _selectedItem = _selectedItemGrid.PickUpItem(tileGridPosition.x, tileGridPosition.y);
            if (_selectedItem)
            {
                _selectedItemTransform = _selectedItem.GetComponent<RectTransform>();
                _selectedItemTransform.SetAsFirstSibling();
                _selectedItemTransform.SetParent(canvasTransform);
            }
        }

        private void PlaceItem(Vector2Int tileGridPosition)
        {
            _overlapItem = null;
            
            if (_selectedItemGrid.PlaceItem(_selectedItem, ref _overlapItem, tileGridPosition.x, tileGridPosition.y))
            {
                _selectedItem = null;
                if (_overlapItem != null)
                {
                    _selectedItem = _overlapItem;
                    _overlapItem = null;
                    _selectedItemTransform = _selectedItem.GetComponent<RectTransform>();
                    _selectedItemTransform.SetParent(canvasTransform);
                }
            }
        }
    }
}
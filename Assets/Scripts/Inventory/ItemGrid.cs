using System;
using UnityEngine;

namespace Inventory
{
    public class ItemGrid : MonoBehaviour
    {
        [SerializeField] private GameObject inventoryItemPrefab;
        
        [SerializeField] private int gridSizeWidth = 20;
        [SerializeField] private int gridSizeHeight = 10;
        
        public const float TileSizeWidth = 32;
        public const float TileSizeHeight = 32;

        private InventoryItem[,] _inventoryItemSlots;

        private RectTransform _rectTransform;
        
        void Start()
        {
            _rectTransform = GetComponent<RectTransform>();
            Init(gridSizeWidth, gridSizeHeight);
        }

        private void Init(int width, int height)
        {
            _inventoryItemSlots = new InventoryItem[width, height];
            Vector2 size = new Vector2(width * TileSizeWidth, height * TileSizeHeight);
            _rectTransform.sizeDelta = size;
        }

        public bool PlaceItem(InventoryItem inventoryItem, ref InventoryItem overlapItem, int posX, int posY)
        {
            if (!BoundaryCheck(posX, posY, inventoryItem.width, inventoryItem.height))
                return false;

            if (!OverlapCheck(posX, posY, inventoryItem.width, inventoryItem.height,
                    ref overlapItem))
            {
                overlapItem = null;
                return false;
            }

            if (overlapItem != null)
            {
                CleanGridReference(overlapItem);
            }
            
            RectTransform itemRectTransform = inventoryItem.GetComponent<RectTransform>();
            itemRectTransform.SetParent(_rectTransform);

            for (int x = 0; x < inventoryItem.width; x++)
            {
                for (int y = 0; y < inventoryItem.height; y++)
                {
                    _inventoryItemSlots[posX + x, posY + y] = inventoryItem;
                }
            }
            
            inventoryItem.onGridPositionX = posX;
            inventoryItem.onGridPositionY = posY;

            Vector2 position = GridToWorldPosition(inventoryItem, posX, posY);

            itemRectTransform.localPosition = position;
            
            return true;
        }

        public Vector2 GridToWorldPosition(InventoryItem item, int gridPosX, int gridPosY)
        {
            Vector2 position;
            position.x = (gridPosX + item.width * 0.5f) * TileSizeWidth;
            position.y = (-gridPosY - item.height * 0.5f) * TileSizeHeight;
            return position;
        }

        public Vector2Int WorldToGridPosition(Vector2 mousePosition)
        {
            var positionOnTheGrid = new Vector2
            {
                x = mousePosition.x - _rectTransform.position.x,
                y = _rectTransform.position.y - mousePosition.y
            };

            var tileGridPosition = new Vector2Int
            {
                x = Mathf.RoundToInt(positionOnTheGrid.x / TileSizeWidth),
                y = Mathf.RoundToInt(positionOnTheGrid.y / TileSizeHeight)
            };
            
            return tileGridPosition;
        }

        private bool OverlapCheck(int posX, int posY, int width, int height, ref InventoryItem overlapItem)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (_inventoryItemSlots[posX + x, posY + y])
                    {
                        if (!overlapItem)
                        {
                            overlapItem = _inventoryItemSlots[posX + x, posY + y];
                        }
                        else
                        {
                            // FIX: The grid was indexed incorrectly.
                            // It should check against the item at the current loop coordinates [posX + x, posY + y].
                            if (overlapItem != _inventoryItemSlots[posX + x, posY + y])
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            
            return true;
        }

        public InventoryItem PickUpItem(int x, int y)
        {
            InventoryItem toReturn = _inventoryItemSlots[x, y];
            
            if(!toReturn) return null;

            CleanGridReference(toReturn);
            return toReturn;
        }

        private void CleanGridReference(InventoryItem item)
        {
            for (int ix = 0; ix < item.width; ix++)
            {
                for (int iy = 0; iy < item.height; iy++)
                {
                    _inventoryItemSlots[item.onGridPositionX + ix, item.onGridPositionY + iy] = null;
                }
            }
        }

        bool PositionCheck(int posX, int posY)
        {
            if(posX < 0 || posY < 0)
            {
                return false;
            }
            
            if(posX >= gridSizeWidth || posY >= gridSizeHeight)
            {
                return false;
            }

            return true;
        }

        public bool BoundaryCheck(int posX, int posY, int width, int height)
        {
            if (!PositionCheck(posX, posY)) return false;

            posX += width - 1;
            posY += height - 1;

            if (!PositionCheck(posX, posY)) return false;

            return true;
        }

        public InventoryItem GetItem(int x, int y)
        {
            return _inventoryItemSlots[x, y];
        }
    }
}
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

        private Vector2 positionOnTheGrid = new Vector2();
        private Vector2Int tileGridPosition = new Vector2Int();
        public Vector2Int GetTileGridPosition(Vector2 mousePosition)
        {
            positionOnTheGrid.x = mousePosition.x - _rectTransform.position.x;
            positionOnTheGrid.y = _rectTransform.position.y - mousePosition.y;

            tileGridPosition.x = (int)(positionOnTheGrid.x / TileSizeWidth);
            tileGridPosition.y = (int)(positionOnTheGrid.y / TileSizeHeight);
            
            return tileGridPosition;
        }

        public bool PlaceItem(InventoryItem inventoryItem, InventoryItem overlapItem, int posX, int posY)
        {
            if (!BoundryCheck(posX, posY, inventoryItem.itemData.sizeWidth, inventoryItem.itemData.sizeHeight))
                return false;

            if (!OverlapCheck(posX, posY, inventoryItem.itemData.sizeWidth, inventoryItem.itemData.sizeHeight,
                    ref overlapItem))
            {
                overlapItem = null;
                return false;
            }

            if (overlapItem)
            {
                CleanGridReference(overlapItem);
            }
            
            RectTransform itemRectTransform = inventoryItem.GetComponent<RectTransform>();
            itemRectTransform.SetParent(_rectTransform);

            for (int x = 0; x < inventoryItem.itemData.sizeWidth; x++)
            {
                for (int y = 0; y < inventoryItem.itemData.sizeHeight; y++)
                {
                    _inventoryItemSlots[posX + x, posY + y] = inventoryItem;
                }
            }
            
            inventoryItem.onGridPositionX = posX;
            inventoryItem.onGridPositionY = posY;

            Vector2 position = Vector2.zero;
            position.x = posX * TileSizeWidth;
            position.y = -posY * TileSizeWidth;
            
            itemRectTransform.localPosition = position;
            
            return true;
        }

        private bool OverlapCheck(int posX, int posY, int width, int height, ref InventoryItem overlapItem)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (_inventoryItemSlots[posX + x, posY + y] != null)
                    {
                        if (overlapItem == null)
                        {
                            overlapItem = _inventoryItemSlots[posX + x, posY + y];
                        }
                        else
                        {
                            if (overlapItem != _inventoryItemSlots[posX, posY + y])
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
            for (int ix = 0; ix < item.itemData.sizeWidth; ix++)
            {
                for (int iy = 0; iy < item.itemData.sizeHeight; iy++)
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

        bool BoundryCheck(int posX, int posY, int width, int height)
        {
            if (!PositionCheck(posX, posY)) return false;

            posX += width - 1;
            posY += height - 1;

            if (!PositionCheck(posX, posY)) return false;

            return true;
        }
    }
}

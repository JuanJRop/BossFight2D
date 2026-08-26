using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Scripts.Progression
{
    [Serializable]
    public sealed class InventoryStack
    {
        public string itemId;
        public int quantity;

        public InventoryStack(string id, int amount)
        {
            itemId = id;
            quantity = amount;
        }
    }

    public static class PlayerInventory
    {
        private const string InventoryKey = "progression.inventory";

        [Serializable]
        private sealed class InventorySaveData
        {
            public List<InventoryStack> stacks = new();
        }

        private static InventorySaveData data;
        public static event Action OnInventoryChanged;

        public static IReadOnlyList<InventoryStack> Stacks
        {
            get
            {
                EnsureLoaded();
                return data.stacks;
            }
        }

        public static int GetQuantity(string itemId)
        {
            InventoryStack stack = Find(itemId);
            return stack != null ? stack.quantity : 0;
        }

        public static bool Has(string itemId, int quantity = 1)
        {
            return quantity > 0 && GetQuantity(itemId) >= quantity;
        }

        public static bool Add(string itemId, int quantity = 1)
        {
            if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0) return false;
            EnsureLoaded();
            InventoryStack stack = Find(itemId);
            if (stack == null)
            {
                stack = new InventoryStack(itemId.Trim(), 0);
                data.stacks.Add(stack);
            }
            stack.quantity += quantity;
            Save();
            return true;
        }

        public static bool Remove(string itemId, int quantity = 1)
        {
            if (quantity <= 0) return false;
            InventoryStack stack = Find(itemId);
            if (stack == null || stack.quantity < quantity) return false;
            stack.quantity -= quantity;
            if (stack.quantity <= 0) data.stacks.Remove(stack);
            Save();
            return true;
        }

        private static InventoryStack Find(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return null;
            EnsureLoaded();
            return data.stacks.Find(stack => string.Equals(stack.itemId, itemId.Trim(),
                StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureLoaded()
        {
            if (data != null) return;
            string json = PlayerPrefs.GetString(InventoryKey, string.Empty);
            data = string.IsNullOrWhiteSpace(json) ? new InventorySaveData() : JsonUtility.FromJson<InventorySaveData>(json);
            if (data == null) data = new InventorySaveData();
            if (data.stacks == null) data.stacks = new List<InventoryStack>();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(InventoryKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            OnInventoryChanged?.Invoke();
        }
    }
}

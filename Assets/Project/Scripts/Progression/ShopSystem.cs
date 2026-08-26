using System;
using UnityEngine;

namespace Project.Scripts.Progression
{
    public enum ShopPurchaseResult
    {
        Success,
        InvalidOffer,
        NotEnoughGold
    }

    [Serializable]
    public sealed class ShopOffer
    {
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, Min(0)] private int price;
        [SerializeField, Min(1)] private int quantity = 1;

        public string ItemId => itemId;
        public string DisplayName => displayName;
        public int Price => Mathf.Max(0, price);
        public int Quantity => Mathf.Max(1, quantity);
    }

    public static class ShopSystem
    {
        public static event Action<ShopOffer> OnPurchased;

        public static ShopPurchaseResult TryPurchase(ShopOffer offer)
        {
            if (offer == null || string.IsNullOrWhiteSpace(offer.ItemId))
                return ShopPurchaseResult.InvalidOffer;
            if (!PlayerEconomy.TrySpendGold(offer.Price))
                return ShopPurchaseResult.NotEnoughGold;

            if (!PlayerInventory.Add(offer.ItemId, offer.Quantity))
            {
                PlayerEconomy.AddGold(offer.Price);
                return ShopPurchaseResult.InvalidOffer;
            }

            OnPurchased?.Invoke(offer);
            return ShopPurchaseResult.Success;
        }
    }
}

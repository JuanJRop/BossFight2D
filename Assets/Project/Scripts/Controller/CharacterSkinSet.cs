using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Scripts.Controller
{
    public sealed class CharacterSkinSet : MonoBehaviour
    {
        [SerializeField] private string displayName;
        [SerializeField] private Sprite[] sprites;

        private Dictionary<string, Sprite> spriteLookup;

        public string DisplayName => displayName;
        public Sprite PreviewSprite => Resolve("Idle_0");

        public Sprite Resolve(string sourceName)
        {
            if (string.IsNullOrWhiteSpace(sourceName)) return null;
            BuildLookup();
            spriteLookup.TryGetValue(CanonicalName(sourceName), out Sprite result);
            return result;
        }

        private void BuildLookup()
        {
            if (spriteLookup != null) return;
            spriteLookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
            if (sprites == null) return;

            foreach (Sprite sprite in sprites)
            {
                if (sprite == null) continue;
                spriteLookup[CanonicalName(sprite.name)] = sprite;
            }
        }

        private static string CanonicalName(string value)
        {
            string normalized = value.Trim().ToLowerInvariant();
            normalized = normalized.Replace("death", "dead").Replace("die", "dead");
            return normalized;
        }
    }
}

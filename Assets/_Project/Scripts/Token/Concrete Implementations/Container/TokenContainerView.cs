using UnityEngine;
using Services.AddressablesSystem;
using System.Collections.Generic;
using System.Threading.Tasks;
using DG.Tweening;
using System;

namespace Core.Tokens
{
    /// <summary>
    /// View layer for the token container.
    /// Responsible for reflecting the container's token state visually.
    ///
    /// LAYOUT CONTRACT:
    /// - Tokens are arranged left-to-right in rows of <see cref="columns"/>.
    /// - The grid grows upward (+Y) when a new row is needed.
    /// - AddTokenToView appends to the current visual list and positions the new token.
    /// - RemoveToken destroys the matching visual and calls ArrangeTokens to close the gap.
    /// </summary>
    public class TokenContainerView : MonoBehaviour
    {
        private AddressablesService addressablesService = new AddressablesService();

        [SerializeField] private int columns = 5;
        [SerializeField] private float spacing = 1.5f;
        [SerializeField] private List<GameObject> spawnedTokens = new();

        /// <summary>
        /// Spawns a visual for the token and appends it at the next grid slot.
        /// </summary>
        public async Task AddTokenToView(TokenController token)
        {
            int index = spawnedTokens.Count;

            int row = index / columns;
            int col = index % columns;

            Vector3 offset = new Vector3(
                col * spacing,
                row * spacing,    // rows grow upward (+Y)
                0f
            );

            Vector3 spawnPosition = transform.position + offset;

            GameObject instance = await addressablesService.SpawnTokenReturningObject(
                token.GetType().Name.ToString(),
                token.data.tokenModelAddress,
                token.data.tokenLogoAddress,
                spawnPosition,
                Quaternion.identity,
                this.transform,
                token.data.backgroundColor,
                token.data.logoColor
            );

            if (instance == null)
            {
                Debug.LogError("Spawn returned null instance.");
                return;
            }

            spawnedTokens.Add(instance);
        }

        /// <summary>
        /// Repositions all spawned tokens to fill gaps left by removed tokens.
        /// </summary>
        private void ArrangeTokens()
        {
            for (int i = 0; i < spawnedTokens.Count; i++)
            {
                var obj = spawnedTokens[i];

                if (obj == null)
                    continue;

                int row = i / columns;
                int col = i % columns;

                Vector3 offset = new Vector3(
                    col * spacing,
                    row * spacing,    // rows grow upward (+Y)
                    0f
                );

                obj.transform.position = transform.position + offset;
            }
        }

        /// <summary>
        /// Destroys the visual for the given token and rearranges remaining tokens.
        /// </summary>
        public void RemoveToken(TokenController tokenController)
        {
            var obj = spawnedTokens.FindLast(o => o.name == tokenController.GetType().Name);

            if (obj != null)
            {
                spawnedTokens.Remove(obj);
                addressablesService.Destroy(obj);
                ArrangeTokens();
            }
        }
    }
}
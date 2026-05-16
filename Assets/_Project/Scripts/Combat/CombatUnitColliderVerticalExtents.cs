using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Extensos verticais dos colliders de combate (não-trigger) num <see cref="Transform"/> de referência,
    /// para ancorar HUD diegética e marcadores em cima/baixo do volume físico da unidade.
    /// </summary>
    public static class CombatUnitColliderVerticalExtents
    {
        private static readonly Vector3[] BoundsCornerSigns =
        {
            new(-1f, -1f, -1f),
            new(-1f, -1f, 1f),
            new(-1f, 1f, -1f),
            new(-1f, 1f, 1f),
            new(1f, -1f, -1f),
            new(1f, -1f, 1f),
            new(1f, 1f, -1f),
            new(1f, 1f, 1f),
        };

        public static bool TryGetVerticalExtentsInLocalSpace(
            Transform referenceTransform,
            out float localMinY,
            out float localMaxY)
        {
            localMinY = float.PositiveInfinity;
            localMaxY = float.NegativeInfinity;

            if (referenceTransform == null)
            {
                return false;
            }

            var foundCollider = false;
            var colliders = referenceTransform.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                AccumulateWorldBoundsCornersInLocalSpace(referenceTransform, collider.bounds, ref localMinY, ref localMaxY);
                foundCollider = true;
            }

            if (foundCollider)
            {
                return true;
            }

            return TryGetRendererVerticalExtentsInLocalSpace(referenceTransform, out localMinY, out localMaxY);
        }

        public static bool TryGetTopWorldY(Transform unitRoot, out float topWorldY)
        {
            topWorldY = float.NegativeInfinity;

            if (unitRoot == null)
            {
                return false;
            }

            var foundCollider = false;
            var colliders = unitRoot.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                topWorldY = Mathf.Max(topWorldY, collider.bounds.max.y);
                foundCollider = true;
            }

            if (foundCollider)
            {
                return true;
            }

            var unitRenderer = unitRoot.GetComponentInChildren<Renderer>();
            if (unitRenderer == null)
            {
                topWorldY = unitRoot.position.y;
                return false;
            }

            topWorldY = unitRenderer.bounds.max.y;
            return true;
        }

        public static bool TryGetBottomWorldY(Transform unitRoot, out float bottomWorldY)
        {
            bottomWorldY = float.PositiveInfinity;

            if (unitRoot == null)
            {
                return false;
            }

            var foundCollider = false;
            var colliders = unitRoot.GetComponentsInChildren<Collider>(includeInactive: false);
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled || collider.isTrigger)
                {
                    continue;
                }

                bottomWorldY = Mathf.Min(bottomWorldY, collider.bounds.min.y);
                foundCollider = true;
            }

            if (foundCollider)
            {
                return true;
            }

            var unitRenderer = unitRoot.GetComponentInChildren<Renderer>();
            if (unitRenderer == null)
            {
                bottomWorldY = unitRoot.position.y;
                return false;
            }

            bottomWorldY = unitRenderer.bounds.min.y;
            return true;
        }

        /// <summary>
        /// Offset local = topo do collider (eixo Y local da referência) + <paramref name="additionalLocalOffset"/>.
        /// </summary>
        public static Vector3 ComposeLocalOffsetAnchoredToColliderTop(
            Transform followTarget,
            Vector3 additionalLocalOffset,
            float fallbackLocalTopY = 1.8f)
        {
            if (TryGetVerticalExtentsInLocalSpace(followTarget, out _, out var localMaxY))
            {
                return new Vector3(
                    additionalLocalOffset.x,
                    localMaxY + additionalLocalOffset.y,
                    additionalLocalOffset.z);
            }

            return new Vector3(
                additionalLocalOffset.x,
                fallbackLocalTopY + additionalLocalOffset.y,
                additionalLocalOffset.z);
        }

        /// <summary>
        /// Offset local = base do collider (eixo Y local da referência) + <paramref name="additionalLocalOffset"/>.
        /// </summary>
        public static Vector3 ComposeLocalOffsetAnchoredToColliderBottom(
            Transform followTarget,
            Vector3 additionalLocalOffset,
            float fallbackLocalBottomY = 0f)
        {
            if (TryGetVerticalExtentsInLocalSpace(followTarget, out var localMinY, out _))
            {
                return new Vector3(
                    additionalLocalOffset.x,
                    localMinY + additionalLocalOffset.y,
                    additionalLocalOffset.z);
            }

            return new Vector3(
                additionalLocalOffset.x,
                fallbackLocalBottomY + additionalLocalOffset.y,
                additionalLocalOffset.z);
        }

        private static void AccumulateWorldBoundsCornersInLocalSpace(
            Transform referenceTransform,
            Bounds worldBounds,
            ref float localMinY,
            ref float localMaxY)
        {
            var worldCenter = worldBounds.center;
            var worldExtents = worldBounds.extents;

            for (var cornerIndex = 0; cornerIndex < BoundsCornerSigns.Length; cornerIndex++)
            {
                var cornerSign = BoundsCornerSigns[cornerIndex];
                var worldCorner = worldCenter + Vector3.Scale(worldExtents, cornerSign);
                var localCorner = referenceTransform.InverseTransformPoint(worldCorner);
                localMinY = Mathf.Min(localMinY, localCorner.y);
                localMaxY = Mathf.Max(localMaxY, localCorner.y);
            }
        }

        private static bool TryGetRendererVerticalExtentsInLocalSpace(
            Transform referenceTransform,
            out float localMinY,
            out float localMaxY)
        {
            localMinY = float.PositiveInfinity;
            localMaxY = float.NegativeInfinity;

            var renderers = referenceTransform.GetComponentsInChildren<Renderer>(includeInactive: false);
            var foundRenderer = false;
            foreach (var renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                AccumulateWorldBoundsCornersInLocalSpace(referenceTransform, renderer.bounds, ref localMinY, ref localMaxY);
                foundRenderer = true;
            }

            return foundRenderer;
        }

    }
}

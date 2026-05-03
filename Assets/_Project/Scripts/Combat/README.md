**Inimigos visuais (manual no Unity)**

1. **Prefab de batalha** (um por arquétipo): root com **Collider**, **`EnemyAnimationController`**, modelo + **Animator**. O **`CombatCapsuleTag`** fica só no **root do prefab** (o bind remove tags duplicados nos filhos e remove o tag do slot `Unit_enemy_*` ao instanciar, para o raycast resolver um único `combatantId`). O ataque/morte são conduzidos **só nesta instância** pelo `CombatPrototypeController` (duração do clip de Attack/Death + margens no inspector do controller de combate); não uses o hub para animar todos os inimigos ao mesmo tempo.

2. **Asset** *Create → Erumperem → Combat → Enemy Visual Definition*: preenche tier, elemento, `spawnWeight`, e arrasta o **prefab** para `battlePrefab`.

3. **Asset** *Create → Erumperem → Combat → Enemy Visual Spawn Catalog*: lista as definições (vários inimigos / pesos).

4. Na cena, no **`CombatPrototypeController`**: atribui o catálogo a `enemyVisualSpawnCatalog` e liga **`spawnEnemyModelsFromCatalog`**. Os slots em `enemyVisualRoots` definem o **pai**; o instance mantém **localPosition, localRotation e localScale** do root do prefab (o código só limpa mesh/collider no slot antes de instanciar).

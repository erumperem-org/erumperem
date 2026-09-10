# AreaZones

Pacote genérico e independente de zonas circulares no mundo: limite externo
de mapa e áreas seguras. Não depende de nenhum outro sistema do projeto
(movimentação, IA, etc.) — pode ser copiado inteiro para qualquer outro
projeto Unity e funciona sozinho.

---

## 1. Arquivos

| Arquivo | O que é |
|---|---|
| `Runtime/CircularZone.cs` | Classe base abstrata: raio, centro (posição do transform), `Contains(ponto)` e gizmo. Toda zona circular herda dela. |
| `Runtime/MapLimits.cs` | Limite externo circular do mapa. Sem comportamento além do herdado. |
| `Runtime/SafeArea.cs` | Escopo genérico de área segura. Sem comportamento reativo — só delimita. |
| `Runtime/Hub.cs` | `SafeArea` com rastreamento ativo de um alvo (`Transform`) e eventos de entrada/saída. |
| `Editor/CircularZoneEditor.cs` | Handle de raio arrastável no Scene view, para qualquer `CircularZone` (e subclasses). |
| `Editor/HubEditor.cs` | Estende o editor acima: readout de estado + botões de simulação de entrada/saída em Play Mode. |

---

## 2. Por que existe essa hierarquia

`MapLimits` e `SafeArea` são conceitualmente a mesma coisa por baixo — um
círculo no mundo que algo testa "estou dentro ou fora dele". Em vez de
duplicar `Contains`/gizmo em cada um, os dois herdam de `CircularZone`.

`Hub` é uma `SafeArea` com **comportamento específico**: ela mesma faz o
polling de um alvo e dispara eventos. Isso fica isolado numa subclasse
porque nem toda área segura precisa desse tracking ativo — pode haver uma
`SafeArea` puramente decorativa/estrutural, consultada sob demanda por outro
sistema via `Contains()`, sem gerar eventos.

Se no futuro surgir uma forma de área diferente (retangular, poligonal...),
ela implementa a mesma interface de zona sem herdar de `CircularZone` — quem
consome só precisa do contrato `Contains(Vector3)` exposto por cada tipo,
não necessariamente de uma base comum.

---

## 3. Como usar

### 3.1 Limite do mapa

1. Crie um GameObject vazio no centro do mapa (ex: `MapLimits`).
2. Adicione o componente `MapLimits`.
3. Selecione o objeto na Scene view e arraste o handle esférico que aparece
   sobre ele para ajustar o raio visualmente (ou digite o valor no
   Inspector). O gizmo amarelo mostra o limite atual.

### 3.2 Área segura simples (sem eventos)

1. Adicione o componente `SafeArea` a um GameObject posicionado no centro da
   área.
2. Ajuste o raio do mesmo jeito (handle no Scene view). Gizmo verde.
3. Qualquer script pode consultar `minhaArea.Contains(posicao)` diretamente.

### 3.3 Hub (área segura com eventos)

1. Adicione o componente `Hub` (em vez de `SafeArea`) a um GameObject no
   centro do HUB.
2. Arraste o `Transform` do alvo a ser rastreado (ex: o jogador) no campo
   `Target`.
3. Ajuste `Check Interval` (padrão 0.2s) — não precisa ser todo frame.
4. Assine os eventos por código:

```csharp
public class InimigosOrchestrator : MonoBehaviour
{
    [SerializeField] private Hub hub;

    private void OnEnable()
    {
        hub.OnPlayerEnteredSafeArea += HandlePlayerSafe;
        hub.OnPlayerExitedSafeArea += HandlePlayerAvailable;
    }

    private void OnDisable()
    {
        hub.OnPlayerEnteredSafeArea -= HandlePlayerSafe;
        hub.OnPlayerExitedSafeArea -= HandlePlayerAvailable;
    }

    private void HandlePlayerSafe()
    {
        // avisar todos os inimigos para entrarem em Resting
    }

    private void HandlePlayerAvailable()
    {
        // avisar todos os inimigos que o alvo voltou a ser caçável
    }
}
```

5. Em Play Mode, o Inspector do `Hub` mostra se o alvo está dentro dele
   agora e oferece botões **"Simular Entrada do Player"** /
   **"Simular Saída do Player"** para testar o código acima sem precisar
   mover o alvo até lá de verdade.

---

## 4. Notas

- `Contains` ignora a altura (Y) — testa só a distância no plano X/Z. Ideal
  para terrenos sem variação de altura relevante; para mapas com múltiplos
  níveis (andares, plataformas), essa checagem precisaria ser adaptada.
- `Hub` usa uma coroutine própria como loop de polling; ela é iniciada em
  `OnEnable` e parada em `OnDisable`, então desativar o GameObject do Hub já
  para completamente o rastreamento sem custo extra.
- Os métodos `Editor_SimulateEnter`/`Editor_SimulateExit` do `Hub` são
  compilados apenas em Editor (`#if UNITY_EDITOR`) — não existem em builds
  de player.

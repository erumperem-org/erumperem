# CharacterPersistence

Serviço genérico de leitura/escrita do save de personagens em JSON. Não
conhece `PlayableCharacter` nem qualquer outro sistema de personagens -
trabalha só com DTOs simples (id + estado + posição + rotação). Pode ser
reaproveitado por qualquer sistema que precise persistir esse tipo de dado
por GameObject, sem trazer nenhuma dependência do domínio de personagens
jogáveis junto.

---

## 1. Arquivos

| Arquivo | O que é |
|---|---|
| `Runtime/CharacterSaveRecord.cs` | Um registro: id, estado (texto livre), posição, rotação. |
| `Runtime/CharacterSaveData.cs` | Raiz do arquivo: papéis atuais + lista de registros. |
| `Runtime/CharacterPersistenceService.cs` | `LoadAsync()`/`SaveAsync(data)` - leitura/escrita em JSON no disco. |
| `Editor/CharacterPersistenceTestbed.cs` | Menu de teste (`Tools > Character Persistence`) que salva/lê dados fictícios sem depender de nenhum personagem real. |

---

## 2. Formato do arquivo

```json
{
  "InGameCharacterId": "hero-01",
  "CompanionCharacterId": "hero-02",
  "Records": [
    { "Id": "hero-01", "State": "InGame",    "Position": {...}, "Rotation": {...} },
    { "Id": "hero-02", "State": "Companion", "Position": {...}, "Rotation": {...} },
    { "Id": "hero-03", "State": "Resting",   "Position": {...}, "Rotation": {...} }
  ]
}
```

`InGameCharacterId`/`CompanionCharacterId` são a fonte da verdade de quem
está em cada papel. O campo `State` de cada registro é salvo por
completude/legibilidade, mas não deve ser usado para decidir papel no load.

---

## 3. Local do arquivo

`Application.persistentDataPath/characters_save.json` — caminho fixo, sem
parâmetro de configuração por enquanto.

---

## 4. Testbed

Menu `Tools > Character Persistence`:
- **Save Dados Fictícios** — grava um `CharacterSaveData` de teste no disco.
- **Load e Logar** — lê o arquivo e loga o conteúdo no Console.

Use isso para validar que a serialização funciona antes de testar o fluxo
completo com personagens reais.

---

## 5. Notas

- `LoadAsync()` retorna `null` se o arquivo não existir — não lança
  exceção. Quem chama decide o fallback.
- Falhas de leitura/escrita (permissão, disco cheio, JSON corrompido) são
  logadas via `Debug.LogError` e engolidas — o método retorna `null`
  (load) ou simplesmente não escreve (save), sem propagar a exceção.
- Este pacote não tem gatilho automático de save/load — é puramente a API.
  Quem decide quando chamar é o consumidor (ver `PlayableCharacters`).

using Game.Core.Data;
using Game.Core.Items;

var catalogDirectory = CombatDataLoader.ResolveDefaultCatalogDirectory();
var itemsPath = Path.Combine(catalogDirectory, "items.json");
var catalog = CombatItemCatalogFactory.CreateDefaultCatalog();
CombatCatalogWriter.WriteItems(itemsPath, catalog);
Console.WriteLine($"Wrote {catalog.Count} items to {itemsPath}");

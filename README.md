### Consignes .NET: 
* Ignorez les migrations BDD
* Ne pas modifier les classes qui ont un commentaire: `// WARN: Should not be changed during the exercise
`
* Pour lancer les tests: `dotnet test`

## Démarche
1. Tests de caractérisation sur `POST /orders/{id}/processOrder`, écrits et vérifiés sur le code d'origine avant toute modification.
2. Refactoring par petites étapes, tests verts à chaque commit.
3. Tests unitaires par règle métier, avec une date figée (`IClock`).

## Organisation
* `Controllers` : HTTP uniquement.
* `Services/Orders` : traitement d'une commande, une seule sauvegarde.
* `Services/ProductHandlers` : une règle par type de produit (NORMAL, SEASONAL, EXPIRABLE).
* `Entities/Product` : règles de stock et de dates.
* `Repositories` : accès à la base via EF Core.

Ajouter un type de produit revient à ajouter un `IProductHandler` et à l'enregistrer dans `Program.cs`.

## Changements de comportement
* Une seule sauvegarde par commande au lieu d'une par produit : en cas d'erreur, rien n'est enregistré.
* Commande inconnue : `OrderNotFoundException` au lieu d'une `NullReferenceException` (la réponse reste une 500).

## Points à valider avec le métier
Comportements actuels conservés (tests `_CurrentQuirk`) :
* SEASONAL non vendu le premier et le dernier jour de la saison (le dernier jour, le stock passe à 0).
* SEASONAL en rupture sans délai : délai de 0 jour annoncé (un NORMAL ne notifie rien).
* EXPIRABLE non expiré mais en rupture : notification d'expiration au lieu d'un délai.
* EXPIRABLE sans date d'expiration : erreur 500.

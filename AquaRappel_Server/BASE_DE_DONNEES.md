# Base de données AquaRappel (SQLite, fichier data/aquarappel.db)

## Modèle (une table par type de donnée, tout est rattaché à `users`)

```
users (email PK, name, salt, hash, failed, locked_until)
   │1
   ├──< tokens   (hash PK, email FK, expires)                      jetons de session (SHA-256 uniquement)
   ├──1 settings (email PK/FK, goal_ml, water_*, break_*, bed_*, updated_at)   réglages et rappels
   ├──< water    (email FK, time, ml)            PK (email, time, ml)   prises d'eau
   ├──< sleep    (email FK, date, hours, quality) PK (email, date)       nuits (qualité : Mal, Bof, Top)
   └──< breaks   (email FK, time)                PK (email, time)       pauses écran
```

- Clés étrangères avec `ON DELETE CASCADE` : supprimer un compte supprime toutes ses données (droit à l'effacement).
- Contraintes `CHECK` : 1 ≤ ml ≤ 5000, 0 ≤ heures ≤ 24, qualité ∈ {Mal, Bof, Top}.
- Requêtes toutes paramétrées (aucune concaténation de texte) : pas d'injection SQL.
- Mots de passe : jamais en clair, PBKDF2 (100 000 itérations) + sel ; jetons : seul le SHA-256 est stocké.
- Index `idx_water_email_time` pour lire rapidement les prises d'eau d'un utilisateur.
- Mode WAL : lectures et écritures simultanées sans blocage.
